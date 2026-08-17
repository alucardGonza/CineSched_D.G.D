# Contributing to CineSched

Thanks for helping improve CineSched. Bug fixes, features, documentation, accessibility improvements, packaging work, and platform validation are welcome.

## Requirements

- The .NET SDK selected by `global.json`
- Git
- Platform tooling required by the target you intend to build or package

## Setup

1. Fork and clone the repository.
2. Create a focused branch from an up-to-date `main`.
3. Restore, build, test, and run the desktop application:

```powershell
dotnet restore CineSched.slnx --locked-mode
dotnet build CineSched.slnx -c Release --no-restore -warnaserror
dotnet test CineSched.slnx -c Release --no-build
dotnet run --project src/CineSched.App/CineSched.App.csproj -f net10.0-desktop
```

## Architecture

- `src/CineSched.Core/Features/<Feature>/` contains one `Models.cs` and one cohesive `<Feature>Service.cs` per vertical slice.
- `src/CineSched.App/` contains the Uno UI, view model, lifecycle, native dialogs, clipboard, preferences, and recent-file integrations.
- `tests/CineSched.Tests/` is the only test project and contains unit tests plus a small number of valuable integration pipelines.
- Core must not reference Uno, XAML, ports, adapters, or handler classes.

Read `ARCHITECTURE.md`, `SPEC.md`, and `MIGRATION-PLAN.md` before making structural or compatibility changes.

## Pull requests

1. Keep each pull request focused on one feature or fix.
2. Use descriptive commits and explain behavior, impact, and validation in the PR.
3. Add or update tests for business behavior and regressions.
4. Run the Release build and complete test suite before pushing.
5. Perform manual UI validation on every platform affected by a presentation change.
6. Update documentation and `CHANGELOG.md` when user-visible behavior changes.
7. Open a draft PR while work or platform validation remains incomplete.

## Project compatibility

CineSched saves `.cinesched` files as portable JSON. Changes to the wire model must:

- Continue opening the compatibility fixtures in `tests/CineSched.Tests/TestAssets/Projects/`.
- Use optional fields and safe defaults for newly introduced data.
- Preserve stable IDs, dates, enum wire values, and fields unknown to the UI where applicable.
- Document migrations and compatibility impact in the pull request.

The `legacy-*` fixtures are intentionally retained: they are test inputs, not active application code.

## Code style

- Follow the repository's .NET analyzers and nullable-reference settings.
- Keep UI-independent behavior in the appropriate Core feature service.
- Keep the UI layer focused on presentation and native desktop integration.
- Avoid introducing additional projects, architectural layers, or per-operation handlers without an accepted architecture change.
- Treat compiler warnings as errors in verification builds.

## Reporting bugs

Include the operating system, .NET SDK version, build/runtime target, steps to reproduce, expected and actual behavior, relevant logs, and screenshots for visual defects. Never attach production data or secrets.

## License

By contributing, you agree that your contributions are licensed under the GNU General Public License v3.
