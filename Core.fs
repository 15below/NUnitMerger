module FifteenBelow.NUnitMerger.Core

open System
open System.IO
open System.Xml
open System.Xml.Linq

let inline (!!) arg =
  ( ^a : (static member op_Implicit : ^b -> ^a) arg)

type culture =
    {
        currentculture : System.Globalization.CultureInfo
        currentuiculture : string
    }

let GetCulture (xDoc : XDocument) =
    let culture = xDoc.Element(!!"test-results").Element(!!"culture-info")
    {
        currentculture = System.Globalization.CultureInfo(culture.Attribute(!!"current-culture").Value)
        currentuiculture = culture.Attribute(!!"current-uiculture").Value
    }

let CreateCulture culture =
    XElement.Parse (sprintf "<culture-info current-culture=\"%s\" current-uiculture=\"%s\" />" culture.currentculture.Name culture.currentuiculture)

let getInt culture intStr =
    Convert.ToInt32(intStr, culture.currentculture.NumberFormat)

let getDouble culture doubleStr =
    Convert.ToDouble(doubleStr, culture.currentculture.NumberFormat)

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

let GetTestSummary culture (xDoc : XDocument) =
    let tr = xDoc.Element(!!"test-results")
    {
        total = tr.Attribute(!!"total").Value |> getInt culture
        errors = tr.Attribute(!!"errors").Value |> getInt culture
        failures = tr.Attribute(!!"failures").Value |> getInt culture
        notrun = tr.Attribute(!!"not-run").Value |> getInt culture 
        inconclusive = tr.Attribute(!!"inconclusive").Value |> getInt culture
        ignored = tr.Attribute(!!"ignored").Value |> getInt culture
        skipped = tr.Attribute(!!"skipped").Value |> getInt culture
        invalid = tr.Attribute(!!"invalid").Value |> getInt culture
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

let FoldAssemblyToProjectTuple culture agg (assembly : XElement) =
    let result, time, asserts = agg
    let outResult =
        if assembly.Attribute(!!"result").Value = "Failure" then "Failure" 
        elif assembly.Attribute(!!"result").Value = "Inconclusive" && result = "Success" then "Inconclusive"
        else result
    (outResult, time + getDouble culture (assembly.Attribute(!!"time").Value), asserts + getInt culture (assembly.Attribute(!!"asserts").Value))
    

let TestProjectSummary culture assemblies =
    assemblies
    |> Seq.fold (FoldAssemblyToProjectTuple culture) ("Success", 0.0, 0)

let CreateTestProjectNode culture assemblies =
    let result, time, asserts = TestProjectSummary culture assemblies
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
    let thisCulture = GetCulture xDoc
    if environment <> (GetEnvironment xDoc) || culture <> thisCulture then printf "Unmatched environment and/or cultures detected: some of theses results files are not from the same test run."
    (MergeTestSummary (GetTestSummary thisCulture xDoc) summary, environment, culture, Seq.append assemblies (GetTestAssemblies xDoc))

let FoldDocs docs =
    let first = Seq.head docs
    let culture = GetCulture first
    let state = (GetTestSummary culture first, GetEnvironment first, culture, Seq.empty)
    Seq.fold Folder state docs

let CreateMerged state =
    let summary, environment, culture, assemblies = state
    let results = (CreateTestSummaryElement summary)
    results.Add [CreateEnvironment environment;CreateCulture culture;CreateTestProjectNode culture assemblies]
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
