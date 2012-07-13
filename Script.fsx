// This file is a script that can be executed with the F# Interactive.  
// It can be used to explore and test the library project.
// Note that script files will not be part of the project build.

#r "System.Xml.Linq"
#load "Core.fs"
open FifteenBelow.NUnitMerger.Core
open System.IO
open System.Xml.Linq

let resultsDoc =
    Directory.GetFiles (@"d:\MJN\Source\build\scripts\TestResults", "*.xml")
    |> Seq.map File.ReadAllText
    |> Seq.map (fun x -> XDocument.Parse x)
    |> FoldDocs
    |> CreateMerged
