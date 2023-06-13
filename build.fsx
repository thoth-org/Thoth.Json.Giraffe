#r "nuget: Fun.Build, 0.3.7"
#r "nuget: Fake.IO.FileSystem, 6.0.0"
#r "nuget: BlackFox.CommandLine, 1.0.0"
#r "nuget: FsToolkit.ErrorHandling, 4.3.0"
#r "nuget: Fake.Tools.Git, 6.0.0"
#r "nuget: Fake.Api.GitHub, 6.0.0"

open Fun.Build
open System.IO
open System.Text.RegularExpressions
open Fake.IO
open FsToolkit.ErrorHandling
open BlackFox.CommandLine
open Fake.Tools
open Fake.Api

module Util =

    let visitFile (visitor: string->string) (fileName: string) =
        File.ReadAllLines(fileName)
        |> Array.map (visitor)
        |> fun lines -> File.WriteAllLines(fileName, lines)

    let replaceLines (replacer: string->Match->string option) (reg: Regex) (fileName: string) =
        fileName |> visitFile (fun line ->
            let m = reg.Match(line)
            if not m.Success
            then line
            else
                match replacer line m with
                | None -> line

                | Some newLine -> newLine)
    let needsPublishing (versionRegex: Regex) (searchedVersion: string) projFile =
        File.ReadLines(projFile)
        |> Seq.tryPick (fun line ->
            let m = versionRegex.Match(line)
            if m.Success then Some m else None)
        |> function
            | None -> failwith "Couldn't find version in project file"
            | Some m ->
                let sameVersion = m.Groups.[1].Value = searchedVersion
                if sameVersion then
                    printfn "Already version %s, no need to publish." searchedVersion
                not sameVersion

module Changelog =

    let versionRegex = Regex("^## ?\\[?v?([\\w\\d.-]+\\.[\\w\\d.-]+[a-zA-Z0-9])\\]?", RegexOptions.IgnoreCase)

    let getLastVersion (changelodPath : string) =
        File.ReadLines changelodPath
            |> Seq.tryPick (fun line ->
                let m = versionRegex.Match(line)
                if m.Success then Some m else None)
            |> function
                | None -> failwith "Couldn't find version in changelog file"
                | Some m ->
                    m.Groups.[1].Value

    let isPreRelease (version : string) =
        let regex = Regex(".*(alpha|beta|rc).*", RegexOptions.IgnoreCase)
        regex.IsMatch(version)

    let getNotesForVersion (version : string) =
        File.ReadLines("CHANGELOG.md")
        |> Seq.skipWhile(fun line ->
            let m = versionRegex.Match(line)

            if m.Success then
                (m.Groups.[1].Value <> version)
            else
                true
        )
        // Remove the version line
        |> Seq.skip 1
        // Take all until the next version line
        |> Seq.takeWhile (fun line ->
            let m = versionRegex.Match(line)
            not m.Success
        )

let root = __SOURCE_DIRECTORY__
let gitOwner = "thoth-org"
let repoName = "Thoth.Json.Giraffe"

module Stages =

    let clean =
        stage "Clean" {
            run (fun _ ->
                [
                    "src/bin"
                    "src/obj"
                    "tests/bin"
                    "tests/obj"
                ]
                |> Shell.cleanDirs
            )
        }

    let test =
        stage "Tests" {
            workingDir "tests"
            run "dotnet test"
        }

pipeline "Test" {

    Stages.clean
    noPrefixForStep
    Stages.test

    runIfOnlySpecified
}

pipeline "Publish" {
    whenEnvVar "NUGET_KEY"

    Stages.clean
    Stages.test


    stage "Publish packages to NuGet" {
        workingDir "src"
        
        run (fun ctx -> 
            let nugetKey = ctx.GetEnvVar "NUGET_KEY"

            asyncResult {
                let version = Changelog.getLastVersion "CHANGELOG.md"
                let versionRegex = Regex("<Version>(.*?)</Version>", RegexOptions.IgnoreCase)
                let projectFile = "src/Thoth.Json.Giraffe.fsproj"

                if Util.needsPublishing versionRegex version projectFile then
                    (versionRegex, projectFile)
                    ||> Util.replaceLines (fun line _ ->
                        versionRegex.Replace(line, "<Version>" + version + "</Version>")
                        |> Some
                    )

                    let! dotnetPackOutput =
                        CmdLine.empty
                        |> CmdLine.appendRaw "dotnet"
                        |> CmdLine.appendRaw "pack"
                        |> CmdLine.appendPrefix "-c" "Release"
                        // |> CmdLine.appendRaw $"""/p:PackageReleaseNotes="{toPackageReleaseNotes releaseNotes.Notes}" """
                        |> CmdLine.toString
                        |> ctx.RunCommandCaptureOutput

                    let m = Regex.Match(dotnetPackOutput, ".*'(?<nupkg_path>.*\.(?<version>.*\..*\..*)\.nupkg)'")

                    if not m.Success then
                        failwithf "Couldn't find NuGet package in output: %s" dotnetPackOutput

                    let nupkgPath = m.Groups.["nupkg_path"].Value

                    do! CmdLine.empty
                        |> CmdLine.appendRaw "dotnet"
                        |> CmdLine.appendRaw "nuget"
                        |> CmdLine.appendRaw "push"
                        |> CmdLine.appendRaw nupkgPath
                        |> CmdLine.appendPrefix "--api-key" nugetKey
                        |> CmdLine.appendPrefix "--source" "nuget.org"
                        |> CmdLine.toString
                        |> ctx.RunCommand
                else
                    return! Error "Already published version"
            }
        )
    }

    stage "Release on Github" {
        run (fun ctx ->
            let githubToken = ctx.GetEnvVar "GITHUB_TOKEN"

            let version = Changelog.getLastVersion "CHANGELOG.md"
            let isPreRelease = Changelog.isPreRelease version
            let notes = Changelog.getNotesForVersion version

            Git.Staging.stageAll root
            let commitMsg = $"Release version {version}"
            Git.Commit.exec root commitMsg
            Git.Branches.push root

            GitHub.createClientWithToken githubToken
            |> GitHub.draftNewRelease gitOwner repoName version isPreRelease notes
            |> GitHub.publishDraft
        )
    }

    runIfOnlySpecified
}

tryPrintPipelineCommandHelp ()
