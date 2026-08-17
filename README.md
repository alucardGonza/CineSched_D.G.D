# CineSched (D.G.D Fork) — cross-platform production scheduling

A cross-platform film and television scheduler built with .NET 10 and Uno Platform for Windows, Linux, and macOS. It includes visual calendar scheduling, stripboard timeline cascading, PDF reports, bilingual resources, call sheets, actor availability/conflict tracking, and script import. Compatibility with v4.5 projects is protected by representative fixtures and automated tests rather than by retaining the superseded Swift application.

![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![Uno Platform](https://img.shields.io/badge/Uno%20Platform-6.6-teal)
![Version](https://img.shields.io/badge/version-5.0.0-purple)
![License](https://img.shields.io/badge/license-GPL--v3-lightgrey)

## Build and run

The repository uses the .NET SDK selected by `global.json`.

```powershell
dotnet restore CineSched.slnx --locked-mode
dotnet build CineSched.slnx -c Release --no-restore -warnaserror
dotnet test tests/CineSched.Tests/CineSched.Tests.csproj -c Release --no-build
dotnet run --project src/CineSched.App/CineSched.App.csproj -f net10.0-desktop
```

Core is organized by vertical slice. Every feature directory contains one `Models.cs` and one concrete `<Feature>Service.cs`; there are no ports, adapters, handlers, or entity-only layers. Unit and the four valuable end-to-end integration suites live in the single `CineSched.Tests` project. See [MIGRATION-PLAN.md](MIGRATION-PLAN.md), [SPEC.md](SPEC.md), and [ARCHITECTURE.md](ARCHITECTURE.md) for the migration contract.

> **CineSched D.G.D Fork**: Enhanced with dynamic timeline cascade, 7-column production calendar, vector shooting schedule PDF generation, pure bilingual localization (Español / English), and independent calendar events.

## ✨ Key Features & New Additions

### 🌐 100% Multilingual Support (Español / English)
- Instant language switching between **Español** and **English** with reactive UI updates across all views, menus, and sheets.
- **Locale-aware PDF generation**: Dates, month names, day numbers, banners, and headers format cleanly according to the active language (`Martes, 11 de Agosto de 2026` vs `Tuesday, August 11, 2026`).
- Native translation of all milestone banners, call sheet fields, and DOOD industry codes.

### ⏱️ Dynamic Timeline Cascade & Stripboard Scheduling
- **Automatic Time Cascading**: Calculates exact start and end times for every scene and banner from call time to wrap time.
- **Quick Time Editor (Double-Click Time Badge)**: Double-click any time badge on the stripboard to open the dedicated time editor:
  - 🔘 *Automatic Cascade*: Computes timing naturally according to day order.
  - 🔘 *Fixed Time Anchor*: Locks specific scenes or meals to fixed clock times (e.g., `11:00 AM` or `01:30 PM`).
  - *Duration Controls*: Independent Steppers for hours and minutes with real-time preview of the start $\rightarrow$ end range (`11:30 AM ➔ 01:00 PM (1h 30m)`).

### 📑 Vector Plan de Rodaje (Shooting Schedule / One-Line PDF)
- **High-Resolution Vector PDF Exporter** modeled after international production standards.
- Clean, uncluttered layout with 4 bounded columns:
  1. **Horario / Time Badge**: Spacious, high-contrast time badge (`07:30 AM – 07:45 AM`).
  2. **Escena & Locación Completa / Scene Title**: Full, unobstructed description width without truncated text.
  3. **Página de Guión / Script Page**: Dedicated column aligned across all rows (`Pág. 1` / `Pg. 1`).
  4. **Octavos / Eighths**: Right-aligned duration (`6/8 pág` / `6/8 pgs`).
- **Dynamic Milestones in Header**: Displays Crew Call (`🚌`), Set Call (`🎬`), and Lunch (`🍽️`) synced live with the day's timeline.
- **End-of-Day Wrap Bar**: Clear summary with exact wrap time, cumulative page counts, and total estimated duration.

### 📅 Fixed 7-Column Production Calendar & Calendar Events
- **Standard 7-Day Grid**: Fixed Monday-to-Sunday layout respecting standard weekday sequences regardless of window width.
- **Independent Calendar Events**: Add travel days, rehearsals, scouting, or rest days directly on the calendar. Calendar events are completely isolated from scene counters and never pollute the Boneyard.

### 🚩 Notice Banners & Milestone Strips
- Add custom company moves, meal breaks, and notice strips into any shoot day.
- Banner strips feature custom tinting and icons, with dynamic synchronization to call sheets and timelines.

### 📅 Visual Calendar Scheduling
- Drag-and-drop scene strips onto calendar days
- Color-coded scene types: orange for day, blue for night, green for custom (company moves, etc.) — red is reserved for flagged strips (see Conflicts & Blackout Days below)
- Multi-select scenes on the calendar (Ctrl/Cmd-click across any day, Shift-click for a range within a day) and drag or right-click the whole group at once
- **Send to Day…** — right-click a scene (or selection) and jump it to any day via a small graphical calendar picker, without dragging across a long schedule
- **Remove from Day** — send a scene (or selection) back to the Boneyard from the calendar
- Drag entire days (scenes + call sheet) to reschedule — swaps content if the target day is occupied
- Fast custom hover tooltips (0.5s) show a scene's cast and summary without waiting on the system's default delay
- Search the whole schedule (title, cast, or summary) from the toolbar and jump straight to a match, scheduled or not
- Dynamic week rows, automatic per-day totals for page count and estimated time

### 🎬 Scene Management
- Create scenes with custom titles, durations, and time estimates
- Day, Night, or Custom type for each scene — Custom strips require only a title, page count and time are optional
- "Boneyard" sidebar for unscheduled scenes with sort options (Location, INT/EXT, Cast, Day/Night, Default), remembered between launches
- Multi-select in the Boneyard (Ctrl/Cmd-click / Shift-click) and drag the whole group onto a calendar day at once
- Double-click to edit any scene; hover for a quick cast/summary tooltip
- Flexible duration input (pages in eighths: "1 7/8", "15", etc.) and time input (hours or minutes: "4", "2:30", "15")

### 🏷️ Scene Breakdown Tagging
- Tag any scene with Extras/Background, Props, Wardrobe, Vehicles, Special Equipment, Stunts, SFX, and VFX (kept separate from SFX), plus free-text breakdown notes — collapsible in the scene editor so a quick duration tweak stays fast
- **Breakdown Browser** — a dedicated view that steps through every scene in script order (parsed from each scene's own number, regardless of scheduling status), so you can tag your way through the whole script start to finish
- **Export Scene Breakdowns** — one bordered breakdown-sheet PDF per scene, script order, in the classic AD grid layout (Scene #, INT/EXT, Setting, Description, Cast, and every tagged category)

### 👥 Actor Availability & Conflict Scanning
- Mark date ranges an actor is unavailable directly on their entry in Production Setup
- Conflicts are scanned automatically in the background — no manual action needed — any time the schedule or availability changes
- A scene scheduled against an actor's unavailable dates turns red on the calendar, with a warning icon, and the date header gets a flag too
- **Scan for Conflicts** report lists every conflict at once, with one click to jump to that day

### 🚫 Blackout Days
- Right-click any date header to mark it unavailable (a holiday, a scheduled day off) — or mark every matching weekday at once ("Mark All Saturdays as Unavailable") for recurring non-shoot days
- Scenes can still be scheduled on a blackout day (handy as working space while rearranging the board) — they just get flagged red as a heads-up, not blocked
- Blackout days shade clearly on the calendar and on the Days Out of Days report header

### 🔒 Schedule Lock
- **Lock Schedule** snapshots exactly which days each actor is currently working — the schedule stays fully editable afterward, nothing is restricted
- Any later change that shifts an actor's working days gets flagged: a badge on the affected date headers, plus a full **Schedule Lock Report** listing exactly what was added or removed per actor, each entry clickable to jump to that day
- **Unlock Schedule** clears the baseline whenever you want to start fresh after a legitimate re-lock

### 📊 Days Out of Days (DOOD)
- One PDF report, one row per cast member, one column per shoot day, using standard industry codes: **SW** (start work), **W** (work), **H** (hold), **WF** (work finish), **SWF** (single-day role), **X** (marked unavailable)
- TOT / WRK / HLD summary columns per actor for quick reference (or contract terms)
- **Include Hold Days in DOoD Report** toggle (Production menu) — turn it off if you only pay actors for days actually on set, and Hold days print blank with the HLD column dropped entirely rather than showing zeros
- Blackout days shade in the header; actor-specific unavailable dates show as **X** even outside their normal working span
- Paginates cleanly across both rows and columns for long schedules or large casts, with a legend on every page

### 📄 Final Draft Script Import
- Import `.fdx` files directly from Final Draft — works whether or not Final Draft itself is installed
- Automatic scene number extraction, location parsing (INT./EXT.), and day/night detection (DAY/MORNING/AFTERNOON vs NIGHT/EVENING/DUSK/DAWN)
- All scene headings auto-capitalized; scenes land in the Boneyard with default values (1/8 page, 15 min) ready to refine

### 📋 Call Sheets
- Click any date header to open that day's call sheet editor
- Per-day fields: general call time, locations, cast, and free-form notes
- Cast auto-pulled from scheduled scenes, fully editable; actor → character lookup resolves live from Production Setup, so a rename there updates every call sheet automatically — including ones already saved
- Per-day crew selection from your roster, referenced by stable ID (not frozen text), so a crew rename or role change ripples through every call sheet that already has them checked
- **Location roster** — pick a saved location instead of retyping the same address every time you shoot there
- Export professional PDF call sheets; a blue dot on date headers shows which days already have call sheet data

### 🎥 Production Setup
- Company name, director, and contact number
- Editable cast list (actor + character, in place — no delete-and-recreate) with per-actor unavailable-date ranges
- Editable crew roster (name, role, Daily-default checkbox), also editable in place
- Reusable location roster

### 🗂️ Desktop Menus & Undo
- Full **File** menu: New, Open, Open Recent, Import Script, Save, Save As, Export Schedule PDF, Export Days Out of Days, Export Scene Breakdowns
- **Save** writes silently to the last-used file; **Save As** always prompts — file panels default to wherever your project already lives instead of always forcing Documents
- **Edit** menu: Undo/Redo for structural schedule changes — moving, removing, sending to a day, duplicating, or rearranging whole days
- **View** menu: Dark Mode, alongside the native Toggle Sidebar
- **Production** menu: Production Setup, Scan for Conflicts, Breakdown Browser, Hold-days toggle, Lock/Unlock Schedule, Schedule Lock Report

### 💾 Auto-Save & File Handling
- Automatic project saving after any change
- Manual Save/Save As for sharing `.cinesched` projects, with Open Recent tracking your last 10 projects
- "New" fully resets the project — clears all scenes, call sheets, title, and production info

### 🎨 Customization
- Dark/Light mode toggle, remembered between launches
- Collapsible sidebar sections (Select Date Range, New Scene) to give the Boneyard more room while actively scheduling
- Adjustable date ranges, with an option to shift already-scheduled scenes when the start date changes

## Installation

### Requirements
- Windows, Linux, or macOS
- The .NET SDK selected by `global.json`

### Setup
1. Clone or download this repository.
2. Run the restore, build, test, and launch commands from [Build and run](#build-and-run).
3. Use the scripts under `packaging/` when producing a distributable for a supported desktop RID.

### Unsigned development packages
Locally produced packages are not code-signed. Operating-system security prompts are therefore expected until the release pipeline is configured with the appropriate Windows, macOS, and Linux signing credentials.

## Usage

### Creating a New Schedule

1. **Set Your Movie Title** — enter your project name at the top of the sidebar
2. **Set Date Range** — choose start and end dates, toggle Shift Schedule if you want existing scenes to move with a date change, and click Update Calendar
3. **Set Up Production Info** *(recommended)* — open Production Setup from the Production menu: company, director, contact, cast (with unavailable dates if applicable), crew, and locations
4. **Add Scenes** — manually via the sidebar's New Scene form, or import a Final Draft script
5. **Sort and Select in the Boneyard** — group by Location, INT/EXT, Cast, or Day/Night; Ctrl/Cmd-click or Shift-click to select multiple scenes at once
6. **Schedule Scenes** — drag from the Boneyard onto calendar days, individually or as a group
7. **Tag Breakdowns** *(optional)* — use the Breakdown Browser to step through the script in order and tag Props, Wardrobe, VFX, and the rest
8. **Lock the Schedule** *(recommended before contracts go out)* — Production → Lock Schedule, then keep working; any change to an actor's working days from there gets flagged automatically

### Building Call Sheets

1. Click a date header to open that day's call sheet editor
2. Set call time, add locations (typed fresh or picked from your roster), and notes
3. Cast auto-pulls from that day's scenes; crew shows your roster as checkboxes, with Daily defaults pre-checked
4. Export PDF to generate the printable call sheet

### Exporting Reports

- **Export Schedule to PDF** — the full calendar as a landscape PDF
- **Export Days Out of Days** — one row per actor, standard DOOD codes, with the Hold-days toggle applied
- **Export Scene Breakdowns** — one bordered breakdown sheet per scene, script order

## Duration & Time Input Examples

### Page Duration (in eighths)
- `15` = 15 eighths (1 7/8 pages)
- `8` = 8 eighths (1 page)
- `1 7/8` = 1 and 7/8 pages
- `7/8` = 7/8 of a page
- `2.5` = 2.5 pages (converts to eighths)

### Estimated Time
- `4` = 4 hours (numbers ≤10 default to hours)
- `15` = 15 minutes (numbers >10 default to minutes)
- `2:30` = 2 hours 30 minutes

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| Ctrl/Cmd+N | New Project |
| Ctrl/Cmd+O | Open… |
| Ctrl/Cmd+S | Save |
| Ctrl/Cmd+Shift+S | Save As… |
| Ctrl/Cmd+E | Export Schedule to PDF |
| Ctrl/Cmd+Shift+E | Export Days Out of Days |
| Ctrl/Cmd+Z | Undo |
| Ctrl/Cmd+Shift+Z | Redo |
| Ctrl/Cmd+Shift+P | Production Setup |
| Ctrl/Cmd+Shift+K | Scan for Conflicts |
| Ctrl/Cmd+Shift+B | Breakdown Browser |
| Ctrl/Cmd+Shift+D | Toggle Dark Mode |

## Project Structure

```text
CineSched/
├── src/
│   ├── CineSched.App/               # Uno shell, view model and desktop integrations
│   └── CineSched.Core/              # UI-independent vertical feature slices
├── tests/
│   └── CineSched.Tests/             # Unit and valuable integration tests
├── packaging/                       # Windows, Linux and macOS packaging
├── .github/workflows/               # Build, test and publish automation
├── CineSched.slnx                   # .NET solution
├── ARCHITECTURE.md                  # Dependency and source-layout contract
├── MIGRATION-PLAN.md                # Migration phases and acceptance criteria
└── SPEC.md                          # Gherkin feature specification
```

## File Formats

### Project Files (.cinesched)
Save and share schedules as `.cinesched` JSON documents containing all scenes, calendar days, call sheets, production info (cast, crew, locations, availability), and any active schedule lock.

### Final Draft Scripts (.fdx)
Import scripts directly from Final Draft — extracts scene headings automatically, preserving scene numbers from the script.

## Tips & Tricks

1. **Efficient Workflow**
   - Import your script first, then set up Production Setup before building call sheets
   - Sort the Boneyard by Location, then ⇧-click a range to select everything at one location and drag the whole group onto a day at once
   - Use the Breakdown Browser to tag the entire script in one pass rather than scene-by-scene as you schedule
   - Lock the schedule once it's stable enough to send out contracts — you'll get flagged automatically if a later change shifts anyone's days
   - Use Custom strips for company moves so they stand out and still print clean (white) on PDF exports

2. **Keyboard Shortcuts**
   - See the full table above — most common actions have one

3. **PDF Export Tips**
   - Schedule PDF: landscape, automatic page breaks for long schedules
   - Days Out of Days: paginates across both rows and columns as needed, with the legend repeated on every page
   - Scene Breakdowns: one page per scene in script order, fixed-size cells so the grid always stays aligned

## Known Limitations

- Schedule PDF exports are landscape US Letter only; call sheet and breakdown sheet exports are portrait US Letter only
- Very long scene titles truncate (with an ellipsis) in the calendar and in PDF exports
- ⇧-click range selection on the calendar only works within a single day (a range spanning multiple days isn't well-defined)
- A conflict or blackout flag only applies to characters matched to a named cast member in Production Setup — unlisted/background character names are skipped

## Windows / Cross-Platform

CineSched is currently macOS-only, but the `.json` project format is simple and portable by design. A Windows developer who wants to build a compatible version — using Electron, Flutter, or Avalonia — would be able to read and write the same save files. See [CONTRIBUTING.md](CONTRIBUTING.md) for more detail.

## Contributing

Contributions are welcome — bug fixes, new features, documentation, packaging, and platform validation. See [CONTRIBUTING.md](CONTRIBUTING.md) to get started.

## Credits

Originally created for macOS and migrated to .NET 10 with Uno Platform for cross-platform desktop use.

Special thanks to:
- **Final Draft** for the `.fdx` format
- **Claude (Anthropic)** for development assistance

## License

GNU General Public License v3 — free to use, modify, and distribute, but any modified versions must also be released as open source under the same license. See [LICENSE](LICENSE) for details.

## Support

For issues or questions, please open a GitHub issue.

---

**Version**: 5.0
**Compatible With**: Windows, Linux, macOS, and Final Draft `.fdx` files
