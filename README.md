# NUnit test result merging

This mini-project was born out of a desire at 15below to get Continuous Integration on TeamCity
working as painlessly as possible.

## The short version

Pull and build the solution in Visual Studio 2010. Reference the resulting dll from msbuild or
as a reference from any .net project and feed it a collection of NUnit test result files.

Watch one combined file emerge as a result.

*Caveats*

* This code is only currently tested on NUnit 2.5
* It assumes that all of the test results files are generated during a single 'test run', and will issue warnings if the environment and culture tags of the files do not match

## Using in MSBuild

Load the task:

```xml
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"
				 ToolsVersion="4.0"
				 DefaultTargets="Build">
  <UsingTask AssemblyFile="$(MSBuildProjectDirectory)\..\Tools\MSBuild\15below.NUnitMerger.dll" TaskName="FifteenBelow.NUnitMerger.MSBuild.NUnitMergeTask" />
  ...
```

Feed it an array of files with in a target:

```xml
  <Target Name="UnitTest" DependsOnTargets="OtherThings">
  	... Generate the individual files here in $(TestResultsDir) ...

    <ItemGroup>
      <ResultsFiles Include="$(TestResultsDir)\*.xml" />
    </ItemGroup> 

    <NUnitMergeTask FilesToBeMerged="@(ResultsFiles)" OutputPath="$(MSBuildProjectDirectory)\TestResult.xml" />
  </Target>
 ```

 Find the resulting combined results at OutputPath.

Using in F#
-----------

Create an F# console app and add 15below.NUnitMerger.dll, System.Xml and System.Xml.Linq as references.

```fsharp
open FifteenBelow.NUnitMerger.Core
open System.IO
open System.Xml.Linq

// All my files are in one directory
WriteMergedNunitResults (@"..\testdir", "*.xml", "myMergedResults.xml")

// I want files from all over the place
let myFiles = ... some filenames as a Seq

myFiles
|> Seq.map (fun fileName -> XDocument.Parse(File.ReadAllText(fileName)))
|> FoldDocs
|> CreateMerged
|> fun x -> File.WriteAllText("myOtherMergedResults.xml", x.ToString())

```

## Why?

We run a lot of parallelisable tests as part of our build. Some recursive MSBuild trickery later,
we had all our test dlls running in parallel, exporting seperate test results for each assembly:

```xml
  <Target Name="UnitTestDll">
    <Message Text="Testing $(NUnitFile)" />
    <ItemGroup>
      <ThisDll Include="$(NUnitFile)"/>
    </ItemGroup>
    <NUnit ToolPath="$(NUnitFolder)" Assemblies="@(ThisDll)" OutputXmlFile="$(TestResultsDir)\%(ThisDll.FileName)-test-results.xml" ExcludeCategory="Integration,IntegrationTest,IntegrationsTest,IntegrationTests,IntegrationsTests,Integration Test,Integration Tests,Integrations Tests,Approval Tests" ContinueOnError="true" />
  </Target>
  
  <Target Name="UnitTest" DependsOnTargets="Clean;CompileAndPackage">
      <Message Text="Run all tests in Solution $(SolutionFileName)" />
	  <CreateItem Include="$(SolutionFolder)**\bin\$(configuration)\**\*.Tests.dll" Exclude="$(SolutionFolder)\NuGet**;$(SolutionFolder)**\obj\**\*.Tests.dll;$(SolutionFolder)**\pnunit.tests.dll">
		<Output TaskParameter="Include" ItemName="NUnitFiles" />
	  </CreateItem>
    <ItemGroup>
      <TempProjects Include="$(MSBuildProjectFile)">
        <Properties>NUnitFile=%(NUnitFiles.Identity)</Properties>
      </TempProjects>
    </ItemGroup>
    <RemoveDir Directories="$(TestResultsDir)" Condition = "Exists('$(TestResultsDir)')"/>
    <MakeDir Directories="$(TestResultsDir)"/>

    <MSBuild Projects="@(TempProjects)" BuildInParallel="true" Targets="UnitTestDll" />
  </Target>
```

So far, so good. Parallel test running that worked both in CI and on the developers machine using the same build script.

Unfortunately, most NUnit reporting tools do not cope well with merging multiple files, and while TeamCity happily aggregated
the results checking test results on the local machine became a pain.

Now with NUnitMergeTask we just amend the build file as below, and all your normal NUnit tools carry on working as before.

```xml
  <Target Name="UnitTestDll">
  	...snip
  </Target>
  
  <Target Name="UnitTest" DependsOnTargets="Clean;CompileAndPackage">
  	...snip

    <MSBuild Projects="@(TempProjects)" BuildInParallel="true" Targets="UnitTestDll" />

    <ItemGroup>
      <ResultsFiles Include="$(TestResultsDir)\*.xml" />
    </ItemGroup> 

    <NUnitMergeTask FilesToBeMerged="@(ResultsFiles)" OutputPath="$(MSBuildProjectDirectory)\TestResult.xml" />
  </Target>
```