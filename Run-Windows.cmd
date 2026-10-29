@echo off
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
 echo Install the .NET 8 SDK or open the solution in Visual Studio with .NET desktop development.
 pause
 exit /b 1
)
dotnet run --project "Decision_support_loans_bank\Decision_support_loans_bank.csproj" --configuration Release
if errorlevel 1 pause
