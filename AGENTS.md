# N.StLine — Project Rules

## Project Goal

N.StLine is a Windows desktop application for optimizing 1D linear material cutting.

Priority:

1. Correct calculation
2. Deterministic results
3. Clear, honest result status (Optimal vs BestFound)
4. Usable UI that matches the N.Tools ecosystem

---

## Technology

- Language: C# 12
- Platform: .NET 8
- UI: WPF
- Theme: MaterialDesignThemes.Wpf 4.9.0
- Serialization: System.Text.Json
- Reports: DocX 5.0.0
- Target OS: Windows 10/11

---

## Architecture

Use this structure:

```
/src/NStLine.Core       — domain models, contracts, constants
/src/NStLine.Cutting    — calculation engine (exact + heuristic)
/src/NStLine.App        — WPF UI, ViewModels, app services
/src/*.Tests            — unit tests
```

Business logic must never depend on WPF. The calculation engine is independent and testable.

---

## Calculation Rules

- Results must be deterministic.
- Identical input always produces identical output.
- Quantity is never altered by the algorithm.
- A bar cannot contain a part longer than the bar length.
- Cut model: N placed parts → N cuts → cut loss = N × CutWidth.
- Optimality order: minimize bar count; then minimize total remainder.

---

## UI Rules

- Never show “optimal” unless the exact solver proved it.
- Use MaterialDesign themes; colors and sizes come from XAML resources, not literals.
- All dimensions are in millimeters.
- Localization: EN and UA only.

---

## Before Writing Code

- Reuse patterns from N.StCut only where they fit the 1D domain.
- Prefer modifying existing project files over creating new ones.
- Check the `.specs/NStLine.MVP.Spec.md` for current scope.

---

## Completion Checklist

- No hardcoded values in logic or UI
- No duplicated logic
- Project builds without warnings in Release
- Existing behavior preserved
- Tests pass

---

## Build & Test Commands

```powershell
# Build
 dotnet build src/NStLine.slnx

# Release build + tests
 dotnet build src/NStLine.slnx -c Release
 dotnet test src/NStLine.slnx -c Release

# Run
 dotnet run --project src/NStLine.App/NStLine.App.csproj
```
