# CineSched Windows x64 visual test plan

## Run contract

- Target: Windows 11 x64, .NET 10 desktop build.
- Data: an isolated app-data directory plus disposable copies of `TestAssets`.
- Window states: 1024x640, 1280x800, and maximized.
- Evidence: screenshots under `artifacts/ui-validation/windows-x64/<run>/` and results in `UI-TEST-REPORT-WINDOWS.md`.
- Pass rule: no open P0-P2 defect. A blocked case is not a pass.

Severity is P0 for crashes/data loss, P1 for blocked or incorrect primary flows, P2 for material layout, accessibility, keyboard, contrast, or usability defects, and P3 for minor cosmetics.

## Cases

| ID | Area | Steps | Expected |
|---|---|---|---|
| WIN-SHELL-001 | Startup | Start with isolated app data at each window size; expand/collapse sidebar. | One readable workspace, no overlap or clipping, responsive close. |
| WIN-SHELL-002 | Menus | Open File, Edit, and Production; invoke and cancel safe commands. | Every menu is visible, named, enabled correctly, and dismisses normally. |
| WIN-SHELL-003 | Navigation | Visit Calendar, Stripboard, Production, Reports, Settings and return. | Exactly one section is visible and selected day/project state is preserved. |
| WIN-PROJ-001 | New/dirty | Edit title; invoke New; cancel; invoke again and confirm. | Cancel preserves data; confirm creates `Untitled Movie`. |
| WIN-PROJ-002 | Open/save | Open a fixture copy; cancel Save As; Save As to QA folder; edit and Save. | Association changes only after successful save and re-open preserves IDs/data. |
| WIN-PROJ-003 | Recent | Open 11 disposable files, reopen one, then remove/move one and invoke it. | Ten unique MRU entries; stale entry reports safely and is removed. |
| WIN-PROJ-004 | Autosave | Make rapid changes, wait two seconds, close abnormally, relaunch and recover. | One debounced snapshot is offered and restored without associating the autosave file. |
| WIN-SCENE-001 | Scene CRUD | Create full breakdown scene; submit invalid duration; edit; duplicate; cancel delete; confirm delete. | Validation is non-mutating; identity/order and independent categories are correct. |
| WIN-SCENE-002 | Boneyard | Search title/cast/summary; exercise all sorts, scrolling and Ctrl/Shift selection. | Correct results/order; multi-selection remains in its context. |
| WIN-SCENE-003 | Clipboard | Select a scene and copy via button/shortcut. | Native clipboard contains display title and summary. |
| WIN-SCHED-001 | Range | Apply shorter/longer date ranges with and without shift. | Calendar remains Monday-aligned; scenes shift or return to Boneyard per mode. |
| WIN-SCHED-002 | Scene movement | Drag one and several scenes, reorder, remove, then undo/redo. | IDs are neither duplicated nor lost; relative order is stable. |
| WIN-SCHED-003 | Whole day | Populate scenes and call sheet, then drag day onto empty and occupied days. | Complete day content moves/swaps while container dates stay fixed. |
| WIN-SCHED-004 | Blackout | Toggle one date and all equivalent weekdays; schedule a scene there. | Styling/conflict appears without blocking scheduling. |
| WIN-STRIP-001 | Timeline | Add anchored scene, automatic scene, banner, meal and event; edit durations. | Cascade and wrap update without negative durations or layout drift. |
| WIN-PROD-001 | Setup | Edit general data; CRUD cast, crew and locations; test location duplicate. | Stable IDs and deduplicated logical location are reflected immediately. |
| WIN-PROD-002 | Availability | Add valid/invalid date ranges and remove one. | Invalid end-before-start is rejected; conflicts recalculate. |
| WIN-LOCK-001 | Conflict/lock | Lock, move an actor scene, inspect added/removed days, jump to date, unlock. | Reports match the schedule and navigation reaches the correct day. |
| WIN-CALL-001 | Call sheet | Initialize, select crew, edit every section, save, rename roster data, reload. | Cast/location are unique and roster projections follow stable IDs. |
| WIN-IMPORT-001 | Imports | Import reference FDX, Fountain, Highland; test confirmation and invalid inputs. | Normalized scenes match; cancellation/error never mutates project. |
| WIN-PDF-001 | Reports | Cancel picker once; generate all six PDFs and open representative pages. | Files are readable, oriented/paginated correctly, localized, with key text. |
| WIN-UX-001 | Localization | Switch English/Spanish while every workspace is visited. | Navigation, key fields, dates and reports change without fallback artifacts. |
| WIN-UX-002 | Theme | Exercise System/Blue/Green/Yellow with System/Light/Dark and restart. | Contrast remains readable and preferences restore. |
| WIN-UX-003 | Keyboard/a11y | Traverse with keyboard; test Ctrl shortcuts, tooltips and UIA tree. | Logical focus order, visible focus, named actionable controls, correct shortcuts. |
| WIN-UX-004 | Motion | Observe hover, selection and drag states. | Short opacity/transform transitions do not move final layout. |
| WIN-PERF-001 | Large project | Open representative large data; navigate and scroll every list. | Startup <10s, navigation <1s, no UI hang >1s, no unbounded visual growth. |
| WIN-EXIT-001 | Restart | Close normally and relaunch twice. | Exit is clean; only expected settings/recovery state returns. |

## Defect workflow

Record actual result, evidence, severity, root cause, fix commit, and retest for every failure. Domain/persistence failures receive a regression in the existing `CineSched.Tests` project. Purely visual failures receive before/after evidence and full shell regression.
