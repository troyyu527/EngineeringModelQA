# Engineering Model QA

A Windows desktop workbench for checking local IFC4 models against JSON rule profiles, exporting CSV/HTML reports, and comparing revisions. Runs fully offline; no Autodesk software required.

> Work in progress. Full documentation follows with the first release.

## Build

Requires Windows, the .NET Framework 4.8 runtime (part of Windows 10/11) and the .NET SDK 8 or later
(the app targets .NET Framework 4.8; the SDK is only the build tool).

```
dotnet build EngineeringModelQA.slnx
dotnet test EngineeringModelQA.slnx
src\EngineeringModelQA\bin\Debug\net48\EngineeringModelQA.exe
```

Sample models are in `samples/fixtures`, sample rule profiles in `samples/profiles`.
