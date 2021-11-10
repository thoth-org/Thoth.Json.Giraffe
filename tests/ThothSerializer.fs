module Tests.ThothSerializer

open System
open System.Net.Http

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection

open Giraffe
open Thoth.Json.Giraffe

open Expecto

[<CLIMutable>]
type AlarmListViewModel = {
    Id: string
    ClientId: System.Guid
    Timestamp: System.DateTimeOffset
    Name: string
    ClientName: string
    ClientFQDN: string
    Labels: string array
}

module Routes =
    let root = "/"
    let single = "/alarmListViewModel"
    let nonSeq= "/nonseq"
    let seq = "/seq"
    let arraySeq = "/array"
    let listSeq = "/list"

module SampleSeq =
    open Thoth.Json.Net

    let data = [|
        {
            Id= "2151c25d-6ab0-483d-b3ba-65090511156a"
            ClientId= System.Guid "8de76830-430e-4f01-a34c-09169f4f135a"
            Timestamp= System.DateTimeOffset()
            Name=""
            ClientName="fake"
            ClientFQDN="http://localhost:8008"
            Labels=Array.empty
        }
    |]
    let data2 = data |> List.ofArray
    let expectedSingle = """{"id":"2151c25d-6ab0-483d-b3ba-65090511156a","clientId":"8de76830-430e-4f01-a34c-09169f4f135a","timestamp":"0001-01-01T00:00:00.0000000+00:00","name":"","clientName":"fake","clientFQDN":"http://localhost:8008","labels":[]}"""
    let expected = """[{"id":"2151c25d-6ab0-483d-b3ba-65090511156a","clientId":"8de76830-430e-4f01-a34c-09169f4f135a","timestamp":"0001-01-01T00:00:00.0000000+00:00","name":"","clientName":"fake","clientFQDN":"http://localhost:8008","labels":[]}]"""
    let cs = CaseStrategy.CamelCase
    let respondJsonAutoArray (x: 't []) =
        Thoth.Json.Giraffe.ThothSerializer.RespondJsonSeq x <| Encode.Auto.generateEncoder(cs)
    let respondJsonAutoList (x: 't list) =
        Thoth.Json.Giraffe.ThothSerializer.RespondJsonSeq x <| Encode.Auto.generateEncoder(cs)
// ----------------------------------------------------------------------------
// Test server/client setup
// ----------------------------------------------------------------------------
module Encode =
    open Thoth.Json.Net

    let person (id, name) =
      Encode.object
        [ "id",   Encode.int id
          "name", Encode.string name
        ]

    let persons ps = List.map person ps |> Encode.list

module Decode =
    open Thoth.Json.Net

    let person =
        Decode.map2 (fun id name -> id, name)
            (Decode.field "id" Decode.int)
            (Decode.field "name" Decode.string)

    let persons =
        Decode.list person

let webApp =
    let postAndSendBack next ctx =
        task {
            match! ThothSerializer.ReadBody ctx Decode.persons with
            | Ok body -> return! ThothSerializer.RespondJson body Encode.persons next ctx
            | Error e -> return! text (sprintf "Error while deserializing: %s" e) next ctx
        }

    choose
        [
            route Routes.root >=>
                choose
                    [
                        GET >=>
                            ThothSerializer.RespondJson
                                [
                                    (1, "Maxime")
                                    (2, "Thoth")
                                ]
                                Encode.persons
                        POST >=> postAndSendBack
                    ]
            route Routes.single >=> GET >=> ThothSerializer.RespondJson (SampleSeq.data.[0]) (Thoth.Json.Net.Encode.Auto.generateEncoder(SampleSeq.cs))
            route Routes.nonSeq >=> GET >=> ThothSerializer.RespondJson (SampleSeq.data) (Thoth.Json.Net.Encode.Auto.generateEncoder(SampleSeq.cs))
            route Routes.seq >=> GET >=> Thoth.Json.Giraffe.ThothSerializer.RespondJsonSeq (SampleSeq.data :> seq<_>) (Thoth.Json.Net.Encode.Auto.generateEncoder(SampleSeq.cs))
            route Routes.arraySeq >=> GET >=> SampleSeq.respondJsonAutoArray SampleSeq.data
            route Routes.listSeq >=> GET >=> SampleSeq.respondJsonAutoList SampleSeq.data2
      ]

let createHost () =
    HostBuilder()
        .ConfigureServices(Action<IServiceCollection>
            (fun (services : IServiceCollection) -> services.AddGiraffe() |> ignore))
        .ConfigureWebHostDefaults(fun webHost ->
                webHost.Configure(Action<IApplicationBuilder> (fun app -> app.UseGiraffe(webApp) |> ignore))
                 |> ignore)
        .Build()

[<Tests>]
let tests =
    let json = "[{\"id\":1,\"name\":\"Maxime\"},{\"id\":2,\"name\":\"Thoth\"}]"
    let createSeqTest name route =
        testTask name {
            let host = createHost ()
            use _ = host.StartAsync ()
            let client = new HttpClient()
            let! (response : HttpResponseMessage) = client.GetAsync(sprintf "http://localhost:5000%s" route)
            let! content = response.EnsureSuccessStatusCode().Content.ReadAsStringAsync()
            Expect.equal content SampleSeq.expected <| sprintf "Serialization Seq - %s" name
        }

    testList "ThothSerializer" [
        testTask "Serialization" {
            let host = createHost ()
            use _ = host.StartAsync ()
            let client = new HttpClient()
            let! (response : HttpResponseMessage) = client.GetAsync("http://localhost:5000/")
            let! content = response.EnsureSuccessStatusCode().Content.ReadAsStringAsync()

            Expect.equal content json "Serialization failure"
        }

        testTask "SerializationAlarmListViewModel" {
            let host = createHost ()
            use _ = host.StartAsync ()
            let client = new HttpClient()
            let! (response : HttpResponseMessage) = client.GetAsync(sprintf "http://localhost:5000%s" Routes.single)
            let! content = response.EnsureSuccessStatusCode().Content.ReadAsStringAsync()

            Expect.equal content SampleSeq.expectedSingle "Serialization failure"
        }

        createSeqTest "SerializationNonSeq" Routes.nonSeq
        createSeqTest "SerializationSeq" Routes.seq
        createSeqTest "SerializationArray" Routes.arraySeq
        createSeqTest "SerializationList" Routes.listSeq

        testTask "Deserialization" {
            let host = createHost ()
            use _ = host.StartAsync ()
            let client = new HttpClient()
            let! (response : HttpResponseMessage) = client.PostAsync("http://localhost:5000/", new StringContent(json))
            let! content = response.EnsureSuccessStatusCode().Content.ReadAsStringAsync()

            Expect.equal content json "Deserialization failure"
        }

        testTask "DeserializationWhenBodyIsEmpty" {
            let host = createHost ()
            use _ = host.StartAsync ()
            let client = new HttpClient()
            let! (response : HttpResponseMessage) = client.PostAsync("http://localhost:5000/", null)
            let! content = response.Content.ReadAsStringAsync()

            Expect.stringStarts content "Error while deserializing" "Deserialization exception handling failure"
        }
  ]
