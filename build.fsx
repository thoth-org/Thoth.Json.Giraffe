#r "nuget: Fun.Build, 0.3.7"
#r "nuget: Fake.IO.FileSystem, 6.0.0"
#r "nuget: BlackFox.CommandLine, 1.0.0"
#r "nuget: Fake.Core.ReleaseNotes, 6.0.0"
#r "nuget: FsToolkit.ErrorHandling, 4.3.0"

open Fun.Build
open System
open System.IO
open System.Text.RegularExpressions
open Fake.Core
open Fake.IO
open Fake.IO.FileSystemOperators
open BlackFox.CommandLine
open FsToolkit.ErrorHandling

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

// Build a NuGet package
let needsPublishing (versionRegex: Regex) (releaseNotes: ReleaseNotes.ReleaseNotes) projFile =
    printfn "Project: %s" projFile
    if releaseNotes.NugetVersion.ToUpper().EndsWith("NEXT")
    then
        printfn "Version in Release Notes ends with NEXT, don't publish yet."
        false
    else
        File.ReadLines(projFile)
        |> Seq.tryPick (fun line ->
            let m = versionRegex.Match(line)
            if m.Success then Some m else None)
        |> function
            | None -> failwith "Couldn't find version in project file"
            | Some m ->
                let sameVersion = m.Groups.[1].Value = releaseNotes.NugetVersion
                if sameVersion then
                    printfn "Already version %s, no need to publish." releaseNotes.NugetVersion
                not sameVersion

let toPackageReleaseNotes (notes: string list) =
    String.Join("\n * ", notes)
    |> (fun txt -> txt.Replace("\"", "\\\""))

let createPublishNugetStageForProject (projectFile : string) =
    let projectDir = IO.Path.GetDirectoryName(projectFile)

    stage $"Publish NuGet for {projectFile}" {
        workingDir projectDir

        run (fun ctx -> asyncResult {
            let nugetKey = ctx.GetEnvVar "NUGET_KEY"
            let releaseNotes = projectDir </> "RELEASE_NOTES.md" |> ReleaseNotes.load
            let versionRegex = Regex("<Version>(.*?)</Version>", RegexOptions.IgnoreCase)

            do! ctx.RunCommand "pwd"

            if needsPublishing versionRegex releaseNotes projectFile then
                (versionRegex, projectFile)
                ||> Util.replaceLines (fun line _ ->
                    versionRegex.Replace(line, "<Version>"+releaseNotes.NugetVersion+"</Version>")
                    |> Some
                )

                let! dotnetPackOutput =
                    CmdLine.empty
                    |> CmdLine.appendRaw "dotnet"
                    |> CmdLine.appendRaw "pack"
                    |> CmdLine.appendPrefix "-c" "Release"
                    |> CmdLine.appendRaw $"""/p:PackageReleaseNotes="{toPackageReleaseNotes releaseNotes.Notes}" """
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
        }
        )
    }


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

    createPublishNugetStageForProject "./src/Thoth.Json.Giraffe.fsproj"

    runIfOnlySpecified
}

tryPrintPipelineCommandHelp ()
