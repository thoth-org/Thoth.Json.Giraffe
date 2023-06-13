namespace Thoth.Json.Giraffe

open System.IO
open System.Text
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Microsoft.AspNetCore.Http
open Giraffe
open Thoth.Json.Net

type ThothSerializer (?caseStrategy : CaseStrategy, ?extra : ExtraCoders, ?skipNullField : bool) =
    static let Utf8EncodingWithoutBom = new UTF8Encoding(false)
    static let DefaultBufferSize = 1024

    /// Responds a JSON
    static member RespondRawJson (body: JToken) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                ctx.SetContentType "application/json; charset=utf-8"
                let stream = new System.IO.StreamWriter(ctx.Response.Body, Utf8EncodingWithoutBom, DefaultBufferSize, true)
                let jsonWriter = new JsonTextWriter(stream)
                do! body.WriteToAsync(jsonWriter)
                do! jsonWriter.FlushAsync()
                return Some ctx
            }

    /// Responds a JSON
    static member RespondJson (body: 'T) (encoder: Encoder<'T>) =
        encoder body |> ThothSerializer.RespondRawJson

    /// Responds a JSON array by writing items
    /// into response stream one by one
    static member RespondRawJsonSeq (items: JToken seq) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                ctx.SetStatusCode 200
                ctx.SetContentType "application/json; charset=utf-8"
                let stream =
                    new System.IO.StreamWriter(ctx.Response.Body, Utf8EncodingWithoutBom, DefaultBufferSize, true)
                let jsonWriter = new JsonTextWriter(stream)
                jsonWriter.WriteStartArray()
                for item in items do
                    do! item.WriteToAsync(jsonWriter)
                jsonWriter.WriteEndArray()
                do! jsonWriter.FlushAsync()
                return Some ctx
            }

    /// Responds a JSON array by serializing items
    /// into response stream one by one
    static member RespondJsonSeq (items: 'T seq) (encoder: Encoder<'T>) =
        items |> Seq.map encoder |> ThothSerializer.RespondRawJsonSeq

    static member ReadBodyRaw (ctx: HttpContext) =
        task {
            try
                use stream = new System.IO.StreamReader(ctx.Request.Body, Utf8EncodingWithoutBom, true, DefaultBufferSize, true)
                use jsonReader = new JsonTextReader(stream)
                let! json = JValue.ReadFromAsync jsonReader
                return Ok json
            with
                | :? Newtonsoft.Json.JsonReaderException as ex ->
                    return Error("Given an invalid JSON: " + ex.Message)
        }

    static member ReadBody (ctx: HttpContext) (decoder: Decoder<'T>) =
        task {
            match! ThothSerializer.ReadBodyRaw ctx with
            | Ok json -> return Decode.fromValue "$" decoder json
            | Error e -> return Error e
        }

    static member ReadBodyUnsafe (ctx: HttpContext) (decoder: Decoder<'T>) =
        task {
            let! json = ThothSerializer.ReadBody ctx decoder
            return match json with
                   | Ok value -> value
                   | Error er -> failwith er
        }

    interface Json.ISerializer with
        member __.SerializeToString (o : 'T) =
            let t = if isNull <| box o then typeof<'T> else o.GetType()
            let encoder = Encode.Auto.LowLevel.generateEncoderCached(t, ?caseStrategy=caseStrategy, ?extra=extra, ?skipNullField=skipNullField)
            encoder o |> Encode.toString 0

        member __.Deserialize<'T> (json : string) =
            let decoder = Decode.Auto.generateDecoderCached<'T>(?caseStrategy=caseStrategy, ?extra=extra)
            match Decode.fromString decoder json with
            | Ok x -> x
            | Error er -> failwith er

        member __.Deserialize<'T> (bytes : byte[]) =
            let decoder = Decode.Auto.generateDecoderCached<'T>(?caseStrategy=caseStrategy, ?extra=extra)
            use stream = new MemoryStream(bytes)
            use streamReader = new StreamReader(stream)
            use jsonReader = new JsonTextReader(streamReader)
            jsonReader.DateParseHandling <- DateParseHandling.None
            let json = JValue.ReadFrom jsonReader
            match Decode.fromValue "$" decoder json with
            | Ok value -> value
            | Error er -> failwith er

        member __.DeserializeAsync<'T> (stream : Stream) = task {
            let decoder = Decode.Auto.generateDecoderCached<'T>(?caseStrategy=caseStrategy, ?extra=extra)
            use streamReader = new StreamReader(stream)
            use jsonReader = new JsonTextReader(streamReader)
            jsonReader.DateParseHandling <- DateParseHandling.None
            let! json = JValue.ReadFromAsync jsonReader
            return
              match Decode.fromValue "$" decoder json with
              | Ok value -> value
              | Error er -> failwith er
          }

        member __.SerializeToBytes<'T>(o : 'T) : byte array =
            let t = if isNull <| box o then typeof<'T> else o.GetType()
            let encoder = Encode.Auto.LowLevel.generateEncoderCached(t, ?caseStrategy=caseStrategy, ?extra=extra, ?skipNullField=skipNullField)
            // TODO: Would it help to create a pool of buffers for the memory stream?
            use stream = new MemoryStream()
            use writer = new StreamWriter(stream, Utf8EncodingWithoutBom, DefaultBufferSize)
            use jsonWriter = new JsonTextWriter(writer)
            (encoder o).WriteTo(jsonWriter)
            jsonWriter.Flush()
            stream.ToArray()

        // TODO: Giraffe only calls this when writing chunked JSON (and setting Response header "Transfer-Encoding" to "chunked")
        // https://github.com/giraffe-fsharp/Giraffe/blob/f623527e1c537e77a07a5e594ced80f4f74016df/src/Giraffe/ResponseWriters.fs#L162
        // But it doesn't work. We need to prefix the chunk with the byte lenght, and finish it with \r\n
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Transfer-Encoding#Directives
        member __.SerializeToStreamAsync (o : 'T) (stream : Stream) =
            upcast task {
                let streamWriter = new System.IO.StreamWriter(stream, Utf8EncodingWithoutBom, DefaultBufferSize, true)
                let jsonWriter = new JsonTextWriter(streamWriter)
                let encoder = Encode.Auto.generateEncoderCached<'T>(?caseStrategy=caseStrategy, ?extra=extra, ?skipNullField=skipNullField)
                do! (encoder o).WriteToAsync(jsonWriter)
                do! jsonWriter.FlushAsync()
            }
