# CineSched desktop acceptance checklist

This checklist is the required manual evidence for `@ui @manual` scenarios in `SPEC.md`. A release candidate is not accepted until every cell is checked on a clean installation without a preinstalled .NET runtime.

| Area | Windows x64/ARM64 | Linux x64/ARM64 | macOS x64/ARM64 |
|---|---|---|---|
| App starts, restores window and remains responsive | ☐ | ☐ | ☐ |
| CommandBar New/Open/Save/Save As and cancellation | ☐ | ☐ | ☐ |
| Unsaved New/Open confirmation preserves document when cancelled | ☐ | ☐ | ☐ |
| Recent file opens; stale entry reports error and is removed | ☐ | ☐ | ☐ |
| Autosave waits two seconds, debounces, and recovery opens safely | ☐ | ☐ | ☐ |
| Calendar/Stripboard navigation preserves selected day | ☐ | ☐ | ☐ |
| Boneyard search, scroll and Ctrl/Cmd/Shift selection | ☐ | ☐ | ☐ |
| Single and grouped scene drag/drop preserves relative order | ☐ | ☐ | ☐ |
| Whole-day drag/drop moves scenes and call sheet together | ☐ | ☐ | ☐ |
| Scene create/edit/duplicate/delete dialogs and validation | ☐ | ☐ | ☐ |
| Banner, meal and calendar-event editors | ☐ | ☐ | ☐ |
| Production cast/crew/location/unavailability editors | ☐ | ☐ | ☐ |
| Call sheet editor and synchronized roster projections | ☐ | ☐ | ☐ |
| Conflict viewer and schedule-lock difference report | ☐ | ☐ | ☐ |
| Breakdown browser in script order | ☐ | ☐ | ☐ |
| Import FDX/Fountain/Highland, confirmation and error dialogs | ☐ | ☐ | ☐ |
| All six PDF pickers, cancellation and readable output | ☐ | ☐ | ☐ |
| English/Spanish changes UI, dates and reports | ☐ | ☐ | ☐ |
| System/blue/green/yellow and system/light/dark restore after restart | ☐ | ☐ | ☐ |
| Native clipboard operation works | ☐ | ☐ | ☐ |
| Hover/selection/drag transitions use opacity/transform without layout drift | ☐ | ☐ | ☐ |
| Large representative project remains scrollable and responsive | ☐ | ☐ | ☐ |

For each checked cell, attach the packaged artifact name, OS version, architecture, test date and screenshots or a short screen recording to the release evidence. Failures must become a Core/integration regression test when the issue is not purely visual.
