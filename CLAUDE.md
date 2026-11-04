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

74 tests pass. Ron answered all 14 plan questions on 2026-11-04 (`docs/PLAN.md` D10–D19). Next: phases 1–3 (engine,
scenes, ladder editor).

### Read these first

| File | What it is |
|---|---|
| `docs/DEVELOPMENT-PLAN.md` | **The work breakdown**: phases, tasks, marks, open questions. Start here |
| `docs/PLAN.md` | Decisions with Ron and why, the architecture, how the engine and grader work |
| `docs/SPECIFICATION.pdf` | Ron's specification: requirement IDs FR-1…13, NFR-1…9 |
| `docs/MANUAL-TESTING.md` | Ron's hand-tests and the hand-test build command |

## Layout

```
PLCPlus.slnx
src/Learning.Core/       the shared learning engine; subject-neutral, NEVER references Plc.* (architecture test)
src/Learning.Shell/      the WPF program, PLCPlus.exe
src/Plc.Engine/          program model (JSON), PlcRuntime (scan cycle), validator, instruction contract
src/Plc.Dialects.Iec/    Generic IEC 61131-3 instructions
src/Plc.Scenes/          simulated machines (JSON scenes, components)
src/Plc.Grading/         challenges and the grader (implements Learning.Core.IChallengeRunner)
content/plcplus-foundations/   scenes, challenges, example programs (copied beside the exe)
tests/Plc.Engine.Tests/  engine, instructions, scenes, grading
tests/Learning.Core.Tests/     architecture rule
tools/capture-window.ps1 photographs the window (PrintWindow) and presses buttons by UI Automation
```

**Build and test:** `dotnet build PLCPlus.slnx`, `dotnet test tests/Plc.Engine.Tests`, `dotnet test tests/Learning.Core.Tests`.

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
- **Every instruction's behaviour is pinned by tests** in `Plc.Engine.Tests`. Changing one changes how work is graded, so
  it is a deliberate act with its tests updated.
- **Offline, and nothing sent:** no network calls with learner data (NFR-2). Progress leaves only as files the learner saves.
- **Content is data:** lessons, scenes and challenges are files in `content/`, not code (NFR-4).
- **Words for a beginner:** validation messages, failed tests and hints say what happened and what to do. Never show
  colour alone: power is green **and** thick **and** TRUE.
- **Modules:** each has Learn, Assess and Reference, and its first page shows prerequisites, learning outcomes and topics
  (Ron, 2026-11-04). PLC instructors will review the flow, so expect it to change.
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
