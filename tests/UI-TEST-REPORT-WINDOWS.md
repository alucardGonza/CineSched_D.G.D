# CineSched Windows x64 visual test report

## Environment

- Date: 2026-08-16
- OS: Windows 11 Pro 10.0.26200, x64
- Display: 3200x2000, high-DPI
- CPU/GPU: AMD Ryzen AI 9 HX 370 / Radeon 890M
- SDK: .NET 10.0.400
- Branch: `feature/uno-dotnet10-cross-platform`
- Evidence run: `artifacts/ui-validation/windows-x64/20260816-222102/`
- Tested sizes: maximized, 1280x800 target and 1024x640 logical equivalent

## Final result

The Windows x64 acceptance run has no open P0, P1 or P2 defect in the exercised scope. The application builds with warnings as errors and all 75 tests pass.

Per the user's instruction, the run did not invoke Open, Save, Save As, Import or Export controls after a native Windows picker interrupted automation. Their underlying workflows were instead executed without Explorer by the existing integration tests: four Swift compatibility fixtures, the complete import/schedule/lock/undo/persist workflow, equivalent FDX/Fountain/Highland imports and all six readable PDFs.

## Defects found and repaired

| Defect | Severity | Repair | Final evidence |
|---|---|---|---|
| Calendar, Production, Reports and Settings rendered simultaneously. | P1 | Replaced the outer `Pivot` with a single-active-workspace host. | `02-calendar.png` through `06-settings.png` |
| Navigation selection did not isolate Reports. | P1 | Centralized workspace visibility in `ShowWorkspace`. | `05-reports.png` |
| File/Edit and indexer-bound labels were blank. | P2 | Added explicit localized view-model properties. | `01-calendar-maximized.png` |
| Boneyard heading/actions and scene-card actions clipped or overlapped. | P1 | Split headings/actions into rows and stacked card actions below content. | `22-final-boneyard-layout.png` |
| Recent projects footer was clipped into the compact rail. | P2 | Show footer only while the pane is open. | `32-sidebar-open-small-final.png` |
| Shoot-day dates included time/offset and clipped in Spanish. | P2 | Added a current-culture short-date converter. | `23-spanish-current.png` |
| Calendar range controls overflowed at 1024x640. | P2 | Changed the range header to two responsive rows. | `28-calendar-spanish-1024x640-fixed-with-days.png` |
| Production/Calendar actions and enum values remained in English. | P2 | Added explicit translations and a localized enum converter. | `24-production-spanish.png`, `30-settings-localized-enums-final.png` |
| Major controls lacked stable automation identities. | P2 | Added IDs for navigation, project commands, settings, schedule, reports, rosters, call sheets and lock actions. | UIA inspection in this run |

## Case results

| ID | Result | Evidence/notes |
|---|---|---|
| WIN-SHELL-001/002/003 | Pass | Exclusive workspaces, complete menus and correct navigation in `01`-`06`. |
| WIN-PROJ-001 | Pass | Dirty New cancel/confirm semantics verified in the baseline run. |
| WIN-PROJ-002 | Pass by integration | Save/reload/compatibility verified without invoking Explorer. |
| WIN-LIFE-001/002 | Pass | Autosave recovery prompt and recovery path exercised with isolated app data. |
| WIN-SCENE-001 | Pass | Created `12A. INT. STUDIO - DAY`; `2 3/8` became 19 eighths; editor scroll and populated Boneyard inspected. |
| WIN-SCENE-002/003 | Pass by unit tests | Validation, duplicate/delete, search and all sort queries execute without persistence reorder. |
| WIN-SCHED-001/002 | Pass | Moved scene to selected day, then Undo and Redo; toggled blackout and restored it. Evidence `14`-`17`. |
| WIN-STRIP-001/002 | Pass | Populated stripboard inspected; Banner, Meal and Event editors opened and cancelled. Evidence `18`-`19`. |
| WIN-PROD-001 | Pass | Production setup, cast, crew and location were entered and rendered. Evidence `20`, `24`. |
| WIN-AVAIL-001 | Pass | Added unavailable range for Alex Rivera/Alice on 2026-08-16. |
| WIN-CONFLICT-001 | Pass | Availability conflict appeared with actor, character and date. Evidence `21`. |
| WIN-LOCK-001 | Pass by unit/integration | Baseline/add/remove/undo/persist behavior covered without native dialogs. |
| WIN-CALL-001 | Pass by unit/integration | Initialization, stable roster IDs and synchronized data are covered; controls were visually inspected. |
| WIN-IMPORT-001 | Pass by integration | Equivalent FDX, Fountain and Highland samples normalize identically. |
| WIN-PDF-001 | Pass by integration | All six PDFs generate, reopen and satisfy orientation/content assertions. Native save picker not invoked. |
| WIN-UX-001 | Pass | English/Spanish navigation, fields, dates, actions and enum options verified. Evidence `23`, `24`, `30`, `31`. |
| WIN-UX-002 | Pass | Green/Light and Yellow/Dark inspected at 1024x640. Evidence `29`-`31`. |
| WIN-UX-003 | Pass with limitation | UIA names/IDs and focus outlines inspected; exhaustive screen-reader narration was not performed. |
| WIN-UX-004 | Pass | Hover, selection, dialog and pane transitions did not shift final layout. |
| WIN-UX-005 | Pass | Maximized and 1024x640 layouts inspected; responsive range fix revalidated in `28`. |
| WIN-EXIT-001 | Pass | Every test instance closed through normal `WM_CLOSE`; no process was force-killed. |

## Automated verification

```text
dotnet build CineSched.slnx -c Release --no-restore -warnaserror
Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test CineSched.slnx -c Release --no-build
Passed: 75, Failed: 0, Skipped: 0
```

Windows ARM64, Linux and macOS are explicitly outside this Windows x64 report.
