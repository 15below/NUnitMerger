module FifteenBelow.NUnitMerger.Core

open System
open System.IO
open System.Xml
open System.Xml.Linq

let inline (!!) arg =
  ( ^a : (static member op_Implicit : ^b -> ^a) arg)

type ResultSummary =
    {
        total : int
        errors : int
        failures : int
        notrun : int
        inconclusive : int
        ignored : int
        skipped : int
        invalid : int
        datetime : DateTime
    }

let GetTestSummary (xDoc : XDocument) =
    let tr = xDoc.Element(!!"test-results")
    {
        total = tr.Attribute(!!"total").Value |> Convert.ToInt32
        errors = tr.Attribute(!!"errors").Value |> Convert.ToInt32
        failures = tr.Attribute(!!"failures").Value |> Convert.ToInt32
        notrun = tr.Attribute(!!"not-run").Value |> Convert.ToInt32 
        inconclusive = tr.Attribute(!!"inconclusive").Value |> Convert.ToInt32
        ignored = tr.Attribute(!!"ignored").Value |> Convert.ToInt32
        skipped = tr.Attribute(!!"skipped").Value |> Convert.ToInt32
        invalid = tr.Attribute(!!"invalid").Value |> Convert.ToInt32
        datetime = String.concat " " [tr.Attribute(!!"date").Value;tr.Attribute(!!"time").Value] |> DateTime.Parse
    }

let CreateTestSummaryElement summary =
    XElement.Parse (sprintf "<test-results name=\"Merged results\" total=\"%d\" errors=\"%d\" failures=\"%d\" not-run=\"%d\" inconclusive=\"%d\" skipped=\"%d\" invalid=\"%d\" date=\"%s\" time=\"%s\" />" summary.total summary.errors summary.failures summary.notrun summary.inconclusive summary.skipped summary.invalid (summary.datetime.ToString("yyyy-MM-dd")) (summary.datetime.ToString("HH:mm:ss")))

type environment =
    {
        nunitversion : string
        clrversion : string
        osversion : string
        platform : string
        cwd : string
        machinename : string
        user : string
        userdomain : string
    }

let GetEnvironment (xDoc : XDocument) =
    let env = xDoc.Element(!!"test-results").Element(!!"environment")
    {
        nunitversion = env.Attribute(!!"nunit-version").Value
        clrversion = env.Attribute(!!"clr-version").Value
        osversion = env.Attribute(!!"os-version").Value
        platform = env.Attribute(!!"platform").Value
        cwd = env.Attribute(!!"cwd").Value
        machinename = env.Attribute(!!"machine-name").Value
        user = env.Attribute(!!"user").Value
        userdomain = env.Attribute(!!"user-domain").Value
    }

let CreateEnvironment environment =
    XElement.Parse (sprintf "<environment nunit-version=\"%s\" clr-version=\"%s\" os-version=\"%s\" platform=\"%s\" cwd=\"%s\" machine-name=\"%s\" user=\"%s\" user-domain=\"%s\" />" environment.nunitversion environment.clrversion environment.osversion environment.platform environment.cwd environment.machinename environment.user environment.userdomain)

type culture =
    {
        currentculture : string
        currentuiculture : string
    }

let GetCulture (xDoc : XDocument) =
    let culture = xDoc.Element(!!"test-results").Element(!!"culture-info")
    {
        currentculture = culture.Attribute(!!"current-culture").Value
        currentuiculture = culture.Attribute(!!"current-uiculture").Value
    }

let CreateCulture culture =
    XElement.Parse (sprintf "<culture-info current-culture=\"%s\" current-uiculture=\"%s\" />" culture.currentculture culture.currentuiculture)

let FoldAssemblyToProjectTuple agg (assembly : XElement) =
    let result, time, asserts = agg
    let outResult =
        if assembly.Attribute(!!"result").Value = "Failure" then "Failure" 
        elif assembly.Attribute(!!"result").Value = "Inconclusive" && result = "Success" then "Inconclusive"
        else result
    (outResult, time + Convert.ToDouble (assembly.Attribute(!!"time").Value), asserts + Convert.ToInt32 (assembly.Attribute(!!"asserts").Value))
    

let TestProjectSummary assemblies =
    assemblies
    |> Seq.fold FoldAssemblyToProjectTuple ("Success", 0.0, 0)

let CreateTestProjectNode assemblies =
    let result, time, asserts = TestProjectSummary assemblies
    let projectEl = XElement.Parse (sprintf "<test-suite type=\"Test Project\" name=\"\" executed=\"True\" result=\"%s\" time=\"%f\" asserts=\"%d\" />" result time asserts)
    let results = XElement.Parse ("<results/>")
    results.Add (assemblies |> Seq.toArray)
    projectEl.Add results
    projectEl

let MergeTestSummary agg summary =
    { agg with 
        total = agg.total + summary.total
        errors = agg.errors + summary.errors
        failures = agg.failures + summary.failures
        notrun = agg.notrun + summary.notrun
        inconclusive = agg.inconclusive + summary.inconclusive
        ignored = agg.ignored + summary.ignored
        skipped = agg.skipped + summary.skipped
        invalid = agg.invalid + summary.invalid
        datetime = Seq.min [agg.datetime;summary.datetime]
    }

let GetTestAssemblies (xDoc : XDocument) =
    xDoc.Descendants()
    |> Seq.filter (fun el -> el.Name = (!!"test-suite") && el.Attribute(!!"type").Value = "Assembly")

let GetXDocs directory filter =
    Directory.GetFiles(directory, filter, SearchOption.AllDirectories)
    |> Seq.map (fun fileName -> XDocument.Parse(File.ReadAllText(fileName)))

let Folder state xDoc =
    let summary, environment, culture, assemblies = state
    // Sanity check!
    if environment <> (GetEnvironment xDoc) || culture <> (GetCulture xDoc) then printf "Unmatched environment and/or cultures detected: some of theses results files are not from the same test run."
    (MergeTestSummary (GetTestSummary xDoc) summary, environment, culture, Seq.append assemblies (GetTestAssemblies xDoc))

let FoldDocs docs =
    let state = (Seq.head docs |> GetTestSummary, Seq.head docs |> GetEnvironment, Seq.head docs |> GetCulture, Seq.head docs |> GetTestAssemblies)
    Seq.fold Folder state docs

let CreateMerged state =
    let summary, environment, culture, assemblies = state
    let results = (CreateTestSummaryElement summary)
    results.Add [CreateEnvironment environment;CreateCulture culture;CreateTestProjectNode assemblies]
    results

let WriteMergedNunitResults (directory, filter, outfile) =
    GetXDocs directory filter
    |> FoldDocs
    |> CreateMerged
    |> fun x -> File.WriteAllText(outfile, x.ToString())

let AllSucceeded xDocs =
    xDocs
    |> Seq.map GetTestAssemblies
    |> Seq.concat
    |> Seq.map (fun assembly -> assembly.Attribute(!!"result").Value)
    |> Seq.map (fun x -> x <> "Failure")
    |> Seq.reduce (&&)
