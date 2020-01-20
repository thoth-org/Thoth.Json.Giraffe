module Tests.ThothSerializer

open System
open System.Net
open System.Net.Http

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection

open FSharp.Control.Tasks.V2.ContextInsensitive

open Giraffe
open Thoth.Json.Giraffe

open Expecto

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
      [ route "/" >=> 
          choose
            [ GET >=>
                ThothSerializer.RespondJson
                  [ (1, "Maxime")
                    (2, "Thoth")
                  ]
                  Encode.persons
              POST >=> postAndSendBack
            ]
      ]

let createHost () =
    HostBuilder()
        .ConfigureServices(Action<IServiceCollection> 
            (fun (services : IServiceCollection) -> services.AddGiraffe() |> ignore))
        .ConfigureWebHost(fun webHost ->
                webHost.UseTestServer() |> ignore
                webHost.Configure(Action<IApplicationBuilder> (fun app -> app.UseGiraffe(webApp) |> ignore))
                 |> ignore)

[<Tests>]
let tests =
  let json = "[{\"id\":1,\"name\":\"Maxime\"},{\"id\":2,\"name\":\"Thoth\"}]"
  testList "ThothSerializer" [
      testTask "Serialization" {
          let hostBuilder = createHost ()
          let! (host : IHost) = hostBuilder.StartAsync()
          let client = host.GetTestClient()
          let! (response : HttpResponseMessage) = client.GetAsync("/")
          let! content = response.EnsureSuccessStatusCode().Content.ReadAsStringAsync()
          
          Expect.equal content json "Serialization failure"
      }
      
      testTask "Deserialization" {
          let hostBuilder = createHost ()
          let! (host : IHost) = hostBuilder.StartAsync()
          let client = host.GetTestClient()
          let! (response : HttpResponseMessage) = client.PostAsync("/", new StringContent(json))
          let! content = response.EnsureSuccessStatusCode().Content.ReadAsStringAsync()
          
          Expect.equal content json "Deserialization failure"
      }
  ]
