﻿
namespace Strangelights.FsViewModels
open System
open System.IO
open System.Globalization
open System.Collections
open System.Reflection
open Microsoft.FSharp.Reflection

type CsvReader<'a>(s: String, ?skipRows: int, ?dateFormat: string) =
    do if not (FSharpType.IsTuple(typeof<'a>)) then
         failwith "Type parameter must be a tuple"
    let elements = FSharpType.GetTupleElements(typeof<'a>)
    let getParseFunc t =
        match t with
        | _ when t = typeof<string> ->
            fun x -> x :> obj
        | _ when t = typeof<DateTime> ->
            let parser =
                match dateFormat with
                | Some format -> 
                    fun s -> 
                        try 
                            DateTime.ParseExact(s, format, null)
                        with _ ->
                            printfn "Error parsing: %s" s 
                            reraise()
                | None -> DateTime.Parse
            fun x -> parser x :> obj
        | _ when t = typeof<float> ->
                    fun s -> 
                        try 
                            Double.Parse(s, CultureInfo.InvariantCulture) :> obj
                        with _ ->
                            printfn "Error parsing: %s" s 
                            reraise()
        | _  ->
            let parse = t.GetMethod("Parse", BindingFlags.Static ||| BindingFlags.Public, null, [| typeof<string> |], null)
            fun (s: string) ->
                parse.Invoke(null, [| box s |]) 
    let funcs = Seq.map getParseFunc elements
    //do printfn "%s" s
    let lines = s.Split([|'\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
    let lines = 
        match skipRows with
        | Some x -> Seq.skip x lines
        | None -> lines |> Seq.ofArray
    let parseRow row = 
        let items =
            Seq.zip (List.ofArray row) funcs
            |> Seq.map (fun (ele, parser) -> parser ele)
        FSharpValue.MakeTuple(Array.ofSeq items, typeof<'a>)
    let items = 
        lines 
        |> Seq.map (fun x -> (parseRow (x.Split([|','|]))) :?> 'a)
        |> Seq.toList 
    interface seq<'a> with
        member x.GetEnumerator() = 
           let seq = Seq.ofList items
           seq.GetEnumerator()
    interface IEnumerable with
        member x.GetEnumerator() = 
           let seq = Seq.ofList items
           seq.GetEnumerator() :> IEnumerator
