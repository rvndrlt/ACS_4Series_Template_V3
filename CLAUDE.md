# ACS_4Series_Template_V3

Crestron SIMPL# Pro control-system program for 4-Series processors.
This program runs ON the Crestron processor. It pairs with a separate HTML UI
project (see Related projects) which provides the user-facing interface.

## Build & target
- C# / .NET Framework 4.8
- Crestron SimplSharpPro SDK 2.21.226
- Output: .cpz archive (deployed to processor)
- Pre-build: `update_version.bat` generates `GitVersionInfo.cs`
- Solution: ACS_4Series_Template_V3.sln

## Related project (UI)
- HTML UI repo: `C:\Users\robertvanderluit\source\HTML8\html`
  - Talks to this processor program over Crestron's contract / CH5 API
  - Runs in: web browser, Crestron One app (iPad), or HTML-capable Crestron panels (e.g. TST-1080, TSW-1070)
- Native panels (TSR-310, TSW-770) use .sgd files in this repo directly — they do NOT use the HTML UI

## Touchpanels supported
- TSR-310 (handheld remote) — TSR-310.sgd / TSR-310.cs
- TSW-770 / TSW-770-DARK (in-wall) — .sgd files
- HTML panels — ACSconfig-HTML-V3.json + HTML/ bindings (UI lives in the HTML8 repo)

## Code structure
- `ControlSystem.*.cs` — partial classes split by subsystem (Navigation, Video, HomePageMusic, SigHandlers, Subsystems, ConsoleCommands)
- `Contract/*.g.cs` — SmartGraphics-generated contract bindings (DO NOT hand-edit; regenerated)
- `UI/TouchpanelUI.*.cs` — touchpanel partial classes (ButtonFeedback, Core, SigChange, PageFlips, SmartObjects, Subscriptions, MusicSharing)
- `Music/`, `Video/`, `Climate/`, `DM/`, `QuickActions/` — subsystem logic
- `*Config.cs` / `*ScenariosConfig.cs` — config data classes deserialized from `ACSconfig.json`

## Branch
- Working branch: `main`

## Gotchas
- `Contract/*.g.cs` is auto-generated — hand edits get overwritten
- Pre-build runs `update_version.bat`; if it fails, build fails before compile
- For UI work, first confirm whether the issue is in native .sgd panels (this repo) or HTML panels (HTML8/html repo)

## Rules

### Contract signals are for dynamic lists ONLY
Contract signals (e.g. `MusicRoomControl[idx].*`, `HomeMusicZone[idx].*`) exist to
serve dynamically built lists whose item count changes at runtime. They must NEVER
be hijacked to control fixed/static UI components like the AUDIO_SUB1 volume slider
or individual page buttons. Static components must use raw joins (boolean, analog,
serial) handled directly in the TouchpanelUI SigChange handlers.
