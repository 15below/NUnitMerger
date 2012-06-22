namespace FifteenBelow.NUnitMerger.MSBuild
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open FifteenBelow.NUnitMerger.Core
open System.IO
open System.Xml.Linq

type NUnitMergeTask () =
    inherit Task ()
    
    let mutable (fileArray : ITaskItem[]) = Array.empty 
    let mutable outputFile = ""

    [<Required>]
    member x.FilesToBeMerged
        with get() = fileArray
        and set(value) = fileArray <- value

    [<Required>]
    member x.OutputPath
        with get() = outputFile
        and set(value) = outputFile <- value

    override x.Execute() =
        x.Log.LogMessage (MessageImportance.Normal, sprintf "Merging nunit results into file: %s" x.OutputPath)
        let xDocs = 
            x.FilesToBeMerged
            |> Seq.map (fun file -> XDocument.Parse(File.ReadAllText(file.ItemSpec)))
        let mergedResults =
            xDocs
            |> FoldDocs
            |> CreateMerged
        mergedResults
        |> fun out -> File.WriteAllText(x.OutputPath, out.ToString())
        if AllSucceeded xDocs then
            true
        else
            x.Log.LogError("Some tests failed.")
            false
