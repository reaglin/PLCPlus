# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.
The portfolio's shared guidance is in `..\CLAUDE.md` (loaded automatically).

## What this is

**PLC+** (repo `PLCPlus`, public at https://github.com/reaglin/PLCPlus; free on the Microsoft Store, name reserved by Ron 2026-11-04) teaches PLC programming at home, from electrical and logic basics to advanced ladder logic,
with **simulated machines in place of real hardware**. It is the self-paced half of a PLC course; the lab is the other half.
It is for students, faculty in a grant training cohort, and the instructors who receive learners' progress reports.
WPF on .NET 10. The source is Ron's specification, `docs/SPECIFICATION.pdf` (2026-11-04).

**Status: phase 0 built 2026-11-04** (⚠️ Ron's hand-test 0). Built so far:
- the engine: the program model, the scan cycle on a simulated clock, the IEC instruction set, forcing, faults and the
  validator;
- the grader;
- the first scene and challenge (motor start/stop);
- a first-look window.

**Phase 1 (the engine) built 2026-11-04:** arrays and structures, breakpoints, traces, failure replay, and the reference
programs R01–R11 with their traces (`tests/golden/`) and the Allen-Bradley lab guide (`docs/LAB-COMPARISON.md`). 101 tests
pass (130 since phase 2). Ron answered all 14 plan questions on 2026-11-04 (`docs/PLAN.md` D10–D19). **Phase 2 (scenes) built 2026-11-04:** six machines (pilot lights, motor, traffic light,
garage door, tank, conveyor sorter), each with a challenge, a working program and a common mistake; the window is now a
preview for the PLC instructors' review (`docs/INSTRUCTOR-REVIEW.md`). The Allen-Bradley comparison waits for the lab
instructor. **Phase 3 (the ladder editor) built 2026-11-04:** `src/Plc.Editors` holds the editing (tested
without a window); "My program (edit it)" in the preview is editable, runs live, and is kept per challenge. **Phase 4 (the
learning path) built 2026-11-04:** the window is now the family's tree-and-tabs layout: 12 modules, each a list of items, each
item with Learn / Example / Exercise / Reference tabs (Ron, D20); content as Markdown with front-matter
(`docs/CONTENT-FORMAT.md`), the validator `tools/ContentValidator`, and progress for one learner. **Phase 5 (assessment) built 2026-11-04:**
timing windows and ordering in tests, Replay the failed test, attempts with hints and computer, questions of five kinds on the module
checks (find the fault on a faulted machine), mastery per outcome. The instructors review on https://softwareplus.ai/plcplus/review/.
**Phase 6 (learners and progress files) built 2026-11-05:** the start screen "Who is learning?", PIN-locked
`.plcprogress` files in `%ProgramData%\PLCPlus\Learners` (`Learning.Core.Learners`), recovery code, wrong-PIN waits, auto-lock,
import and merge, required backups with reminders, the event log; My program and answers per learner.
**Phase 7 (the dashboard) built 2026-11-05:** My progress at the top of the tree (path map ★ ✓ ↺ ◐ ○, module checks as submission
points, review suggested, outcomes), totals and time on task above the tree, View ▸ text size per learner, Help on the web and Check
for a new version; MVVM step 1 (CommunityToolkit.Mvvm; `src/Learning.Shell/ViewModels/ShellViewModel.cs` holds the window's state).
**Phase 8 (reports) built 2026-11-05:** Hand in a report (the second row of the path): a signed, tagged PDF/A progress report per module
check or of everything (`Learning.Core.Reports` makes the snapshot, the seal, the result line `PLC1|…` and the check;
`src/Learning.Reports.Pdf` draws it with PDFsharp and QRCoder), My details for reports, Send to my instructor, the reports made, and
File ▸ Check a report (for instructors) with class codes (D23). `PLCPLUS_SAVE_TO=<folder>` saves reports there without the Save
dialog (captures only).
**Phase 9 (the content for Units 1–5) first drafts 2026-11-05:** modules 1, 2 and 4 written (lessons, practice questions on every
item, a check each), modules 3 and 5 revised; a typed-answer question kind (`text`: binary, hex, BCD); five example machines so every
Example tab runs a machine **like** its exercise's (D21; `tools/make-example-machines.py`; the motor can now run in reverse); the
outcomes in `docs/OUTCOMES.md` (`tools/make-outcomes.py`) for Ron to review. 277 tests.
**Phase 10 (modules 6, 7, 8 and 11) first drafts 2026-11-05:** timers and counters (TONR added), data and math (thumbwheel and
numeric display components), sequencing (cylinder component, step logic, step numbers, state machines, SFC introduced), troubleshooting
(`Tag:failed` device faults, find-and-fix exercises with a challenge's `startProgram`); 50 items, 40 machines. **Pop out** (Ron): the
Example or Exercise in its own resizable modal window. The validator enforces D21 (no Example or picture shows an exercise's answer).
374 tests.
**Phase 11 (the release gate) built 2026-11-05:** a full UX sweep of every screen fixed; "preview" gone (title PLC+, version 1.0.0 in
`Directory.Build.props`); Help ▸ Testing guide and feedback, and the start screen's part for instructors; the round manual test at the
top of `docs/MANUAL-TESTING.md`. 375 tests.
**Phase 12 (the Store) built 2026-11-05:** the icon (a ladder rung, the amber "+" as its coil), the MSIX scripts from Author+
(`packaging/`), the release documents (`docs/release/STORE-SUBMISSION.md`, `LAB-INSTALL.md`), screenshots (`store/`), About's Store
line (shown once `StoreUpdates.StorePage` has the Store ID), and on softwareplus.ai the privacy policy, the instructors' testing guide
(`/plcplus/testing/`) and the review site without the zip. ⚠️ Waits for Ron: the Package/Identity/Name and Store ID from Partner
Center, WACK, then the submission (hidden, direct link).

**The goal now (Ron, 2026-11-05, D24): the instructors' release.** PLC+ 1.0 on the Microsoft Store, hidden (direct link only), for
the PLC instructors who give feedback; modules 1–8 and 11 written; a testing guide at softwareplus.ai/plcplus/testing/. Plan phases
10 (content for 6, 7, 8, 11), 11 (the release gate) and 12 (Store, testing guide, site).

### Read these first

| File | What it is |
|---|---|
| `docs/DEVELOPMENT-PLAN.md` | **The work breakdown**: phases, tasks, marks, open questions. Start here |
| `docs/PLAN.md` | Decisions with Ron and why, the architecture, how the engine and grader work |
| `docs/SPECIFICATION.pdf` | Ron's specification: requirement IDs FR-1…13, NFR-1…9 |
| `docs/MANUAL-TESTING.md` | Ron's hand-tests and the hand-test build command |
| `docs/CONTENT-FORMAT.md` | How modules, items, Reference pages and widgets are written |
| `docs/LAB-COMPARISON.md` | The reference programs on the lab's Allen-Bradley PLC |
| `docs/INSTRUCTOR-REVIEW.md` | What the instructors review in the preview, and how to send notes |

## Layout

```
PLCPlus.slnx
src/Learning.Core/       the shared learning engine; subject-neutral, NEVER references Plc.* (architecture test)
src/Learning.Shell/      the WPF program, PLCPlus.exe (ViewModels/: the window's state, MVVM; ReportScreens.cs: hand in and check reports)
src/Learning.Reports.Pdf/ the progress report as a tagged PDF/A (PDFsharp, QRCoder); subject-neutral like Learning.Core
src/Plc.Engine/          program model (JSON), PlcRuntime (scan cycle), validator, instruction contract
src/Plc.Dialects.Iec/    Generic IEC 61131-3 instructions
src/Plc.Scenes/          simulated machines (JSON scenes, components)
src/Plc.Grading/         challenges and the grader (implements Learning.Core.IChallengeRunner)
src/Plc.Editors/         the ladder editor's model: every edit, undo/redo, declare on first use (no UI)
content/plcplus-foundations/   modules/ (module.md + one .md per item), reference/, scenes, challenges, programs (copied beside the exe)
tools/ContentValidator/  checks a content package: dotnet run --project tools/ContentValidator
tests/Plc.Engine.Tests/  engine, instructions, scenes, grading
tests/golden/            reference programs R01–R11 (+ .trace.txt), written by tools/make-golden-cases.py
tools/make-phase2-content.py  wrote the six scenes, challenges and example programs (the JSON is the source now)
tests/Learning.Core.Tests/     architecture rule
tests/Plc.Editors.Tests/       the editor, including building a program and passing a challenge
tools/capture-window.ps1 photographs the window (PrintWindow) and presses buttons by UI Automation
packaging/               AppxManifest.xml, pack.ps1, make-msixupload.ps1, run-wack.ps1, make-store-assets.ps1 (from Author+)
store/                   images/ (listing art, drawn by make-store-assets.ps1), screenshots/ (seven, 1424 × 836)
docs/release/            STORE-SUBMISSION.md (Partner Center field by field), LAB-INSTALL.md
```

**Build and test:** `dotnet build PLCPlus.slnx`, then `dotnet test` each of `tests/Plc.Engine.Tests`, `tests/Plc.Editors.Tests`, `tests/Learning.Core.Tests`.
**The Store package:** `.\packaging\make-msixupload.ps1` (add `-IdentityName … -Publisher … -VerifyFamilyName` for the real one), then
`.\packaging\run-wack.ps1` (administrator); see `docs/release/STORE-SUBMISSION.md`.
**The old review build** (no longer offered; 1.0 comes from the Store) (self-contained, gitignored `review\`): `dotnet publish src/Learning.Shell/Learning.Shell.csproj -c Release
-r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
-p:EnableCompressionInSingleFile=true -p:DebugType=none -o review/PLCPlus-preview`, copy `docs/INSTRUCTOR-REVIEW.md` in, and zip it.

## Rules for this repository

- **Stay with what we know works for our software** (Ron, 2026-11-04):
  - MSIX on the Microsoft Store, not Velopack;
  - WPF drawing, not SkiaSharp;
  - .NET's own cryptography, not SQLCipher.

  Ask before adding a library the other programs do not already use.
- **The Learning Core is the shared engine** for this and Ron's other learning software (Ron, 2026-11-04). It covers
  reporting results, submission that works on every platform or straight to the instructor, and many learners on one
  computer. Keep it subject-neutral: nothing PLC-specific goes in `Learning.Core`, and the architecture test enforces it.
- **Deterministic:** the engine and scenes run only on the simulated clock (`PlcRuntime.NowMs`), never the wall clock. The
  same program and scenario always grade the same way (NFR-3).
- **Every instruction's behaviour is pinned by tests** in `Plc.Engine.Tests` and by the golden traces in `tests/golden/`.
  Changing one changes how work is graded, so it is a deliberate act: rewrite the traces with `PLCPLUS_UPDATE_GOLDEN=1`, read
  the diff, and note it in `docs/LAB-COMPARISON.md` if it touches what the lab compares.
- **Offline, and nothing sent:** no network calls with learner data (NFR-2). Progress leaves only as files the learner saves.
- **Content is data:** lessons, scenes and challenges are files in `content/`, not code (NFR-4).
- **Words for a beginner:** validation messages, failed tests and hints say what happened and what to do. Never show
  colour alone: power is green **and** thick **and** TRUE.
- **Modules → items** (Ron, 2026-11-04, D20): every item has the tabs Learn, Example, Exercise and Reference; a module's first
  page shows prerequisites (recommended, not enforced), outcomes, topics and items; its last item is the module check. PLC
  instructors will review the flow, so expect it to change. Run the content validator after editing content.
- **The instructors' review site** (https://softwareplus.ai/plcplus/review/) is generated from this repo: after changing content or
  `docs/INSTRUCTOR-REVIEW.md`, run `python tools/make-review-site.py`, then in `..\SoftwarePlus` `.\deploy.ps1 -Status` and
  `.\deploy.ps1 -Only plcplus -Reason "PLC+: …"`.
- `PLCPLUS_DATA=<folder>` points a run at its own learners' folder and settings (for captures and tests), so it never changes a
  tester's learners in `%ProgramData%\PLCPlus`, and never takes the preview's progress.
- Name: **PLC+** in front of a person (Ron, 2026-11-04); `PLCPlus` in code, folders and the exe.
- **No AI in Phase 1** (Ron). The lab PLC is **Allen-Bradley**: the engine is compared against it, and Rockwell is the
  first vendor dialect. Ron signs content packages when they are published; the key never goes in git.

## Portfolio conventions that apply

- Plan in `docs/DEVELOPMENT-PLAN.md`: no icon = to do, ⚠️ = waiting for Ron, ✅ = Ron verified. Never mark ✅.
  Update the plan in the same commit as the work.
- Development cycle: works with tests → UX review (`ux-reviewer`) → Ron's hand-test → back to code.
- `..\USABILITY-CHECKLIST.md` for every screen; a hand-test exe in `manual-test\` (gitignored).
- About: "Created by Dr. Ron Eaglin" with his picture, and links to `softwareplus.ai/plcplus` and all programs (the Store
  link once there is a Store ID).
- See the window with PrintWindow and UI Automation only (`tools/capture-window.ps1`), never a screen grab or keystrokes.
