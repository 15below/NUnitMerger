// This file is a script that can be executed with the F# Interactive.  
// It can be used to explore and test the library project.
// Note that script files will not be part of the project build.

#r "System.Xml.Linq"
#load "Core.fs"
open FifteenBelow.NUnitMerger.Core
open System.IO
open System.Xml.Linq

Directory.GetFiles (@"d:\MJN\Source\build\scripts\TestResults", "*.xml")
|> Seq.take 3
|> Seq.map File.ReadAllText
|> Seq.map XDocument.Parse
|> AllSucceeded
|> printfn "%A"
