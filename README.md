# N.StLine

N.StLine is a Windows desktop application for deterministic one-dimensional cutting optimization of long stock materials, including pipes, profiles, and structural sections.

## Features

- Material library with stock length and kerf settings
- Multiple materials in one cutting project
- Manual and clipboard/Excel-style part entry
- Deterministic exact and heuristic cutting solvers
- Honest `Optimal` and `BestFound` result statuses
- Sequential cutting layouts with bar remainder and utilization metrics
- Project save/load and DOCX reports
- English and Ukrainian interface
- Light and dark themes

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
.\installer\build-installer.ps1 -Version 1.0.0
```

Output:

```text
artifacts\installer\N.StLine-Setup-1.0.0-win-x64.exe
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
git tag v1.0.0
git push origin v1.0.0
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
