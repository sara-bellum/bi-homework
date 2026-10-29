# Verification

Verified against the exact database supplied in `bi-homework-main.zip`.

## Completed

- Compiled all Windows Forms source files with the .NET 8 Roslyn C# compiler, .NET 8 reference assemblies, Windows Desktop reference assemblies and the pinned Microsoft.Data.Sqlite assembly. No compiler errors or warnings in the final compilation.
- Executed the shared model and analytics code in a .NET 8 console harness against the supplied SQLite database; all 33 checks passed.
- Checks cover OLS extrapolation, flooring negative forecasts, a single-month helper, rate-cut sensitivity, zero elasticity, rejecting zero/negative rates, early-payment classification, interest estimation, funding/operating costs, candidate rules, exclusion of missing eligibility fields, zero-filled monthly history, full database loading, share reconciliation, reconciliation to the source early-payment view, and finite margins across all records. Revision checks additionally cover English source mappings, recommendation and action labels, recovery shortfall, 50-row paging and page bounds, whole-result sorting, empty tables and the exact history/forecast boundary.
- Preserved the source database unchanged. No writes or schema migrations are required.

## Reconciled figures

| Measure | Result |
| --- | ---: |
| All source loans | 90,427 |
| Individuals-only loans (excludes small_business) | 89,417 |
| Original granted amount, individuals only | USD 1,342,086,300.00 |
| Flagged individuals-only loans (includes grace period) | 11,727 |
| Early payoffs, all source loans | 31,914 |
| Early payoffs, individuals only | 31,615 |
| Closed-loan net margin, individuals only, 3% funding and 1% lifetime operating assumptions | USD −27,738,443.85 |

The net-margin figure is the model estimate described in README, not an audited profit figure.

## Re-run checks

On a machine with the .NET 8 SDK:

```powershell
dotnet run --project Validation/AnalyticsChecks.csproj
```

This console project links the application's actual `Loan.cs`, `Analytics.cs` and `Db.cs`; it does not duplicate their implementation. It copies the source database beside the validation executable automatically.

## Interface revision

The latest edition changes the presentation only: standard tabs and File/Help menus replace the branded sidebar, system colours replace the dark theme, headings and summary text are smaller, and detailed notes move into Help. The pie chart is now a conventional filled pie. Filters, sorting, paging, charts and calculations retain the previous behaviour.

## Revised interface verification

The revised chart and form source files were included in the final Windows-targeted C# compilation. The pagination implementation is shared by the actual form and the executed console checks. Original database counts, margin calculations and early-payoff reconciliation remain unchanged.

## Limits of verification

The environment used to prepare this deliverable runs Linux. The native Windows GUI could not be launched or visually inspected here. The standard `dotnet build` / MSBuild command was blocked by process-information access in this environment, so compilation was checked by calling Roslyn directly. Visual Studio / MSBuild restore and F5 startup still require confirmation on Windows. No prebuilt executable is included.

Forecast predictive accuracy and demand elasticity were not validated against out-of-sample application data. Their limits are stated in the application and README. Database status outcomes are historical snapshot values, not an as-of reconstruction.
