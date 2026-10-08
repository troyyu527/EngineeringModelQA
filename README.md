# Engineering Model QA

A Windows desktop workbench for checking local IFC4 models against JSON rule profiles, exporting CSV/HTML reports, and comparing revisions. Runs fully offline; no Autodesk software required.

> Work in progress. Full documentation follows with the first release.

## Build

Requires the .NET 10 SDK on Windows.

```
dotnet build EngineeringQa.slnx
dotnet test EngineeringQa.slnx
dotnet run --project src/EngineeringQa.Desktop
```
