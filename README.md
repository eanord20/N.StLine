# N.StLine

N.StLine is a Windows desktop application for deterministic one-dimensional cutting optimization of long stock materials, including pipes, profiles, and structural sections.

## Features

- Material library with stock length and kerf settings, JSON import/export
- Multiple materials in one cutting project
- Manual and clipboard/Excel-style part entry
- Deterministic exact and heuristic cutting solvers
- Honest `Optimal` and `BestFound` result statuses
- Sequential cutting layouts with bar remainder and utilization metrics
- Project save/load and DOCX reports
- English and Ukrainian interface
- Light and dark themes

## Version 1.2.0

### Added and improved

- Material-library JSON import/export with duplicate detection and preservation of existing entries.
- Outlined **+ Add** action and reorganized calculation/report controls.
- Calculation and report settings under **Edit**, with report folder and optional file name on the main toolbar.
- Configurable report file names, date/time prefixes or suffixes, titles, summaries, separators, material/stock labels and optional part names; regular-weight bar multiplicities.
- Report cutting-pattern formatting applies only to DOCX; screen and clipboard layouts remain unchanged.

### Fixed

- Decimal boundary rejection, omitted valid cutting patterns and false `Optimal` results. Validation, heuristic packing and exact search now share exact scaled-length calculations.
- Silent quantity coercion and discarded clipboard rows. Invalid entries retain their source text in diagnostics and block calculation and project saving until corrected or removed.
- Lost invalid-paste diagnostics after Undo.
- Results becoming incorrectly current after input changes during calculation; stale results cannot be copied or reported.
- Unhandled errors reading damaged settings, material libraries and projects. Damaged startup files are protected from automatic overwrite; failed project loads preserve the current task and result.
- Quantity increment overflow, null part entries and pattern-count conversion overflow for very small parts.
- Delayed cancellation during heuristic packing; copies are processed without first allocating the entire expanded demand list.
- Non-finite result totals: unsupported aggregate ranges are rejected rather than returned as `Infinity` or `NaN`, including multi-material totals.
- Disabled **Calculate** now explains why: hovering shows a localized hint when table rows contain validation errors.

### Clarified

- Search profiles are **Fast**, **Balanced** and **Custom**; extended limits remain available through Custom.
- Utilization is useful part length divided by total stock length. Kerf is reported separately and is not counted as useful output.

## Materials and report settings

In **Materials**, import or export the library as JSON. Import adds materials without replacing existing entries; duplicates (name, stock length and kerf) are skipped. Click **OK** to keep imported changes, or **Cancel** to discard them. Export writes the current library shown in the dialog.

The outlined **+ Add** button selects a library material for the project. The main toolbar also provides the report folder and an optional file name. Leave the name empty to keep automatic timestamped naming; the `.docx` extension is added automatically. Existing reports require confirmation before replacement.

Under **Edit**, open **Calculation settings** for the search profile and limits, or **Report settings** for file-name date/time prefixes or suffixes and DOCX content. The report can use a custom title, date/time, an optional summary, a part separator (`+`, `|`, `-`, `*`, space, comma or period), material headings in cutting schemes, and optional part names. Bar multiplicities use regular weight. The preview uses the current cutting result; screen layouts and clipboard formatting are unaffected. Settings are saved between sessions.

## Download

Installers and portable builds are published on the [Releases](https://github.com/eanord20/N.StLine/releases) page.

The installer is self-contained and does not require a separate .NET installation. Supported operating systems: Windows 10 and Windows 11 (x64).

## Build from source

Requirements:

- .NET SDK 9.0.200 or newer (the application targets .NET 8; the newer SDK is required to read the `.slnx` solution)
- Windows 10/11

```powershell
git clone https://github.com/eanord20/N.StLine.git
cd N.StLine
dotnet restore src/NStLine.slnx
dotnet build src/NStLine.slnx -c Release
dotnet test src/NStLine.slnx -c Release
dotnet run --project src/NStLine.App/NStLine.App.csproj
```

## Build the installer

Install [Inno Setup 6](https://jrsoftware.org/isdl.php), then run:

```powershell
.\installer\build-installer.ps1 -Version 1.2.0
```

Output:

```text
artifacts\installer\N.StLine-Setup-1.2.0-win-x64.exe
```

The installer:

- installs for the current user without administrator privileges;
- installs to `%LocalAppData%\Programs\N.StLine`;
- supports English and Ukrainian;
- creates a Start menu shortcut;
- optionally creates a desktop shortcut;
- includes the self-contained .NET 8 runtime.

## Create a GitHub release

Push a semantic version tag:

```powershell
git tag v1.2.0
git push origin v1.2.0
```

GitHub Actions builds and tests the solution, publishes the self-contained `win-x64` application, compiles the Inno Setup installer, creates a portable ZIP, generates SHA-256 checksums, and publishes all artifacts in a GitHub Release.

## Project structure

```text
src/NStLine.Core          Domain models and contracts
src/NStLine.Cutting       Exact and heuristic cutting solvers
src/NStLine.App           WPF desktop application
src/*.Tests               Automated tests
installer                 Inno Setup build files
.github/workflows         CI and release automation
```

## License

Licensed under the [GNU General Public License v3 or later](LICENSE).

## Author

Nikolay Sukhomlin  
Contact: nik.sukhomlin20@gmail.com
