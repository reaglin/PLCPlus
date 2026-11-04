# PLC+ — development plan

Numbered tasks per phase, each with what "done" means. Requirement IDs (FR-, NFR-) and section numbers refer to Ron's
specification, `docs/SPECIFICATION.pdf`. Decisions and the reasons for them are in `docs/PLAN.md`.

The spec's three delivery phases map onto this plan like this:
- **Spec Phase 1** (core and foundations, Units 1–5) is plan phases 0–11. Its gate: the engine test suite passes, the engine
  matches a lab PLC on the reference programs, and a pilot runs with the faculty training cohort.
- **Spec Phase 2** (intermediate and submission) is plan phase 12.
- **Spec Phase 3** (advanced and dialects) is plan phase 13.

Started **2026-11-04** from the specification.

---

## How an item is marked

| Mark | Means |
|---|---|
| *(no icon)* | to do |
| ⚠️ | **action needed**: built and tested, waiting for Ron to look; or blocked on an answer from Ron. The row says what |
| ✅ | done: built, tested **and verified by Ron** |

Claude never marks an item ✅. Passing tests and a clean UX review earn ⚠️.

---

## Standing items (every release)

| # | Task | Done when |
|---|---|---|
| R.1 | AI Manager: **not used in Phase 1** (Ron, question 9). If the instructors' review shows an AI part is needed, the question comes back to Ron; then every release moves to the latest AI Manager first | — |
| R.2 | A hand-test exe in `manual-test\` for every round Ron tests (`docs/MANUAL-TESTING.md` has the command) | Rebuilt before each hand-test |

---

## Phase 0 — the repository and a first look ⚠️ 2026-11-04

| # | Task | Done when |
|---|---|---|
| 0.1 | Repo `PLCPlus`, `Directory.Build.props` (warnings as errors), `.gitignore`, `PLCPlus.slnx` with spec 11's project layout | ⚠️ Built 2026-11-04 |
| 0.2 | `docs/SPECIFICATION.pdf` (Ron's spec), `docs/PLAN.md` (decisions), this plan, `docs/MANUAL-TESTING.md`, `CLAUDE.md` | ⚠️ 2026-11-04 |
| 0.3 | Architecture test: `Learning.Core` references no `Plc.*` assembly (spec 12, NFR-5) | ⚠️ `Learning.Core.Tests`, 3 tests |
| 0.4 | **First-look window** (`PLCPlus.exe`): the motor station live (hold Start, press Stop), the program's rungs with power flow shown in green, thick and TRUE, the simulated clock with Pause / Step one scan / Reset, and **Check this program**, which runs the challenge's tests and shows each result, with hints after a failure. Help panel (F1), Help ▸ About with "Created by Dr. Ron Eaglin" | ⚠️ **Ron: hand-test 0** in `docs/MANUAL-TESTING.md`. Photographed through PrintWindow: all 3 tests pass with the seal-in program |
| 0.5 | GitHub repo `reaglin/PLCPlus`, pushed | ⚠️ Ron created it (public) 2026-11-04: https://github.com/reaglin/PLCPlus; pushed the same day |

---

## Phase 1 — the PLC engine (FR-2, NFR-3; spec 4) ⚠️ built 2026-11-04 (Ron started it the same day)

| # | Task | Done when |
|---|---|---|
| 1.1 | The neutral program model: project, tags, program units, rungs as series/parallel trees of contacts, coils and boxes; saved as JSON | ⚠️ Built 2026-11-04; round-trip test |
| 1.2 | Elementary types BOOL, INT (wraps at 16 bits), DINT, REAL, TIME; literals `TRUE`, `10`, `2.5`, `T#1m30s` | ⚠️ Built; 9 tests |
| 1.3 | The scan cycle on a simulated clock (inputs → programs → outputs → clock), configurable scan time | ⚠️ Built; tests |
| 1.4 | Contacts (NO, NC, rising edge, falling edge) and coils (normal, negated, set, reset) | ⚠️ Built; 9 tests |
| 1.5 | TON / TOF / TP, CTU / CTD / CTUD, EQ NE GT GE LT LE, ADD SUB MUL DIV MOD, MOVE, AND OR XOR NOT; each with a one-sentence summary and help on every operand | ⚠️ Built; 30 tests |
| 1.6 | Forcing inputs and outputs; live power flow per element | ⚠️ Built; tests |
| 1.7 | Faults: divide by zero and an index out of range stop the PLC at the rung, outputs off, Reset starts again | ⚠️ Built; tests. The watchdog comes with jumps and loops (not in the Phase 1 instruction set) |
| 1.8 | Validator: messages for a beginner that point to the rung and the element | ⚠️ Built; 8 tests |
| 1.9 | Run to a breakpoint (a rung), and a trace recorder (tag values per scan) that the grader and the failure replay use | ⚠️ Built 2026-11-04: `SetBreakpoint` / `RunToBreakpoint` stop at the end of the first scan in which the rung passes power; `TraceRecorder` records any tags and writes a change log; the grader's `Replay` runs one scenario with a trace. The first-look window now shows, after a failed check, what happened in the first failed test. 5 tests |
| 1.10 | Arrays and structures (spec 4.1); index-out-of-range fault | ⚠️ Built 2026-11-04: `ARRAY[lo..hi] OF t`, structures declared in the project (`types`), arrays of structures; indexes by literal or by an INT/DINT tag; the validator checks bounds, members and index types in words for a beginner. 12 tests |
| 1.11 | Golden-file tests: a set of reference programs with their expected traces, checked into `tests/golden/` | ⚠️ Built 2026-11-04: **R01–R11** (seal-in, TON, TOF, TP, CTU, CTD, CTUD, latch and edges, compare and math, array and fault, timer cascade), written by `tools/make-golden-cases.py`; each trace checked by hand. Ron: read one or two `.trace.txt` files and say if the format is clear |
| 1.12 | **Lab comparison:** the reference programs run on the lab's **Allen-Bradley** PLC and the traces compared (spec 4.5, the Phase 1 gate) | ⚠️ **Ready for the lab:** `docs/LAB-COMPARISON.md` has every program as Logix rung text, the instruction map with the differences (CTD's done bit, no TP, divide by zero, the fault codes to confirm), how to run it (a 10 ms periodic task, internal tags first, a Trend), and a results table. Ron / the lab: who runs it |
| 1.13 | Instruction presentation: each instruction's symbol and operand layout for the editor, and the lesson it links to | With phase 3 |

---

## Phase 2 — simulation scenes (FR-3; spec 5.3)

| # | Task | Done when |
|---|---|---|
| 2.1 | The scene format (JSON): components, layout, physics parameters, I/O map to tags; physics on the engine's clock | ⚠️ Built 2026-11-04 for push buttons, pilot lights and motors |
| 2.2 | Scene 1: **pilot light and push-button station** | |
| 2.3 | Scene 2: **motor start/stop with seal-in** | ⚠️ `motor-station.json` (the first-look scene) |
| 2.4 | Scene 3: **traffic light** | |
| 2.5 | Scene 4: **garage door** (limit switches, travel time, reversing motor) | |
| 2.6 | Scene 5: **tank fill and drain** (level, float switches, valves, pump) | |
| 2.7 | Scene 6: **conveyor sorter** (parts spawner, photo-eye, diverter) | |
| 2.8 | The rest of the component library: selector switch, e-stop, thumbwheel, limit switch, proximity, float switch, analog sensors, solenoid valve, cylinder, heater, pump, stack light, numeric display, horn, conveyor, tank, part spawner, diverter, elevator car | |
| 2.9 | The scene view: draws any scene from its file and animates it, with buttons operable by keyboard and screen reader | The first look's machine panel is hand-made; this replaces it |
| 2.10 | Fault injection: a stuck sensor, a failed motor, a wiring error | ⚠️ Stuck push button and failed motor built; wiring error to come |

---

## Phase 3 — the ladder editor (FR-2; spec 5.1)

| # | Task | Done when |
|---|---|---|
| 3.1 | Place contacts, coils and boxes on rungs by dragging **or by keyboard**, with parallel branches | |
| 3.2 | Tags: autocomplete, declare on first use, type checking while typing | |
| 3.3 | Live mode: energized paths highlighted, live values beside each instruction, forced values clearly marked | The first look's read-only `LadderView` grows into this |
| 3.4 | Rung comments, undo/redo, copy/paste of rungs | |
| 3.5 | Validation messages shown on the rung and element they are about | The validator exists (1.8) |
| 3.6 | Printing a rung (for the report's program evidence, spec 9.2) with the same drawing code | |

---

## Phase 4 — the Learning Core: content and the learning path (FR-1; spec 6)

| # | Task | Done when |
|---|---|---|
| 4.1 | Content package: a folder or zip with `manifest.json`, lessons in Markdown with front-matter, scenes, challenges, media | `content/plcplus-foundations/manifest.json` is a placeholder |
| 4.2 | The hierarchy: learning path → unit → lesson → activity (reading, guided simulation, quiz, graded challenge); stable IDs for objectives and activities | |
| 4.3 | **Every module (unit) has Learn, Assess and Reference** (D7). Its first page shows its prerequisites, learning outcomes and topics | Ron's first shape; PLC instructors review the flow (4.9) |
| 4.4 | Prerequisites are **recommended, not enforced** (Ron, question 6): a module whose prerequisites are not mastered says what to finish first but opens anyway, and the report shows the instructor it was opened early | |
| 4.5 | The lesson viewer **in the program's own window** (Ron, question 7): Markdown (Markdig) into a WPF FlowDocument, with live widgets (a mini simulation, a truth table, a diagram); no web view | |
| 4.6 | Submission points declared in the path: which exercises they cover and what counts as complete | |
| 4.7 | `Tools.ContentValidator`: checks links, IDs and challenge definitions from the command line | |
| 4.8 | Package signing: **Ron signs the content when it is published** (question 8), with a key kept out of git; a signing step in the publish script; the program rejects unsigned or untrusted packages | |
| 4.9 | **Review of the flow by PLC course instructors**: their comments become plan items | ⚠️ Ron arranges the reviewers |

---

## Phase 5 — assessment (FR-4; spec 7)

| # | Task | Done when |
|---|---|---|
| 5.1 | The challenge format and grader: scenarios, assertions at times, invariants, hidden scenarios, constraints, points, pass threshold, tiered hints | ⚠️ Built 2026-11-04; 7 tests |
| 5.2 | Assertions still to add: timing windows ("on between 2.0 and 2.2 s") and ordering ("A before B") | |
| 5.3 | Replay a failed test visually from its trace | Needs 1.9 |
| 5.4 | Each attempt recorded: score, time, hints used, program hash, machine | Needs phase 6 |
| 5.5 | Other assessments: multiple choice, numeric with tolerance, truth-table fill-in, match the symbol, find the fault in a faulted scene | |
| 5.6 | Mastery rules per objective (e.g. "pass two linked challenges at 80% or more"); states: not started, in progress, mastered, needs review | |

---

## Phase 6 — learners and progress files (FR-5, FR-9, FR-10; spec 8) — the shared engine (D5)

| # | Task | Done when |
|---|---|---|
| 6.1 | Start screen: every learner on this computer, by name and when they last used it | |
| 6.2 | New learner: display name, optional student ID, a 4–6 digit PIN, and a one-time recovery code to write down or print | |
| 6.3 | The progress file (`.plcprogress`): a small readable header (name, profile ID, last saved, app version) and **one encrypted block of JSON** (Ron, question 4); AES-GCM with a PBKDF2 key from the PIN, a second copy of the key wrapped with the recovery code | |
| 6.4 | Wrong PIN: after 5 tries a wait that grows; auto-lock after 15 minutes idle and on Switch learner | |
| 6.5 | Remove from this computer (needs the PIN; offers to export first) | |
| 6.6 | Export and import ("Add learner from file"), showing which computer saved it last and when | |
| 6.7 | Merging two copies: attempts are append-only with unique IDs, so both copies' attempts are kept and mastery recalculated | |
| 6.8 | The shared folder `%ProgramData%\PLCPlus\Learners`, so lab PCs work with one shared login or separate ones; changeable in settings. Lab PCs are not reimaged (Ron, question 10) | |
| 6.10 | **Backups are required** (Ron, question 10): the program asks the learner to back up (export) their file to a drive or cloud folder they choose, shows when the last backup was made, and reminds them when it is out of date (at each submission point and when they sign out) | |
| 6.9 | The event log (xAPI-style learning events) inside the progress file | |

---

## Phase 7 — the program's window and the learner dashboard (FR-6; spec 8.5)

| # | Task | Done when |
|---|---|---|
| 7.1 | The shell in MVVM (CommunityToolkit.Mvvm), **in the family's layout** (Ron, question 11): menu bar, rail of actions, the learning path as a tree (units → lessons), Learn / Assess / Reference as tabs on the right, a status line saying what to do next | |
| 7.2 | Path map: each unit and lesson locked, available, in progress or mastered (with a mark as well as a colour) | |
| 7.3 | Submission points: not reached, ready to generate, generated (with the date) | |
| 7.4 | Objectives view with evidence, and a "review suggested" list | |
| 7.5 | Time on task and progress totals always visible | |
| 7.6 | Help menu as in the usability checklist: Help for this screen (F1), Help on the web, Check for a new version, About (Store link once there is a Store ID) | About built in phase 0 |
| 7.7 | Accessibility: keyboard throughout, screen-reader names, scalable text, colour-safe marks (NFR-6) | |

---

## Phase 8 — results, reports and submission (FR-7, FR-8, FR-11; spec 9, 10) — the shared engine (D5, D6)

| # | Task | Done when |
|---|---|---|
| 8.1 | The progress snapshot: one format-neutral record of everything a report shows (spec 9.5) | |
| 8.2 | The **final screen** of a submission point, as in the Gamify+ games: grade, progress, the learner's name, and the ways to hand it in | |
| 8.3 | The **PDF report**: cover, summary, exercise detail, program evidence (optional), verification (report ID, versions, code, QR); PDF/A and tagged; default name `PLCPlus_<LastName>_<SubmissionPoint>_<yyyy-MM-dd>.pdf` | QuestPDF (its Community licence covers an organisation under US $1M revenue) and QRCoder |
| 8.4 | The **signed result line** for any LMS, discussion or e-mail, in Gamify+'s shape. **Signed both ways** (Ron, question 3): always with the computer's own key (proves it was not edited), and also with the instructor's **class code** when the learner has entered one (proves it came from that class) | |
| 8.4a | The report cover's fields: **name required**; student ID, course and section, and e-mail optional (Ron, question 14) | |
| 8.5 | **Send to my instructor**: opens the learner's e-mail with the result line and says where the saved PDF is, to attach | |
| 8.6 | Incomplete reports allowed, marked Incomplete with what is missing; regenerate earlier reports; an on-demand full report | |
| 8.7 | Submission history: every report with its ID, date and hash | |
| 8.8 | The report verifier (FR-11, spec Phase 2): checks a PDF or a result line, and makes an instructor's class code | |

---

## Phase 9 — the content for Units 1–5 (spec 6.2)

| # | Task | Done when |
|---|---|---|
| 9.1 | Unit 1 Electrical foundations: voltage, current, circuits, AC/DC, safety | |
| 9.2 | Unit 2 Logic and number systems: binary, hex, BCD, Boolean logic, truth tables | |
| 9.3 | Unit 3 Relay control: relays, contactors, relay ladder diagrams, seal-in circuits | |
| 9.4 | Unit 4 PLC fundamentals: hardware, I/O modules, sensors, scan cycle, tags and addressing | |
| 9.5 | Unit 5 Basic ladder programming: contacts, coils, latching, edge detection | The motor start/stop challenge is its first |
| 9.6 | Objectives with stable IDs. **The grant's outcomes are written as we go** (Ron, question 12): the grant asks for faculty training on PLCs and student educational resources, so each unit's outcomes are drafted with the unit, kept in one list (`docs/OUTCOMES.md`, once there is a unit), and Ron reviews them | ⚠️ Ron reviews each unit's outcomes |

---

## Phase 10 — the Phase 1 gate

| # | Task | Done when |
|---|---|---|
| 10.1 | UX review of every screen (`ux-reviewer`: cognitive walkthrough and heuristic evaluation) against `..\USABILITY-CHECKLIST.md`; fixes made | |
| 10.2 | Round manual test in `docs/MANUAL-TESTING.md`; Ron's hand-test | |
| 10.3 | The engine matches a lab PLC on the reference programs (1.12) | |
| 10.4 | **Pilot with the faculty training cohort** | ⚠️ Ron |

---

## Phase 11 — the Microsoft Store and softwareplus.ai

| # | Task | Done when |
|---|---|---|
| 11.1 | **PLC+** (Ron, question 1) reserved in Partner Center; the icon with the "+" theme. **Free** on the Store (question 2) | ⚠️ **PLC+ reserved by Ron 2026-11-04.** The name in the program changed to PLC+ the same day; the Store ID goes in About's Store link once Partner Center shows it |
| 11.2 | MSIX packaging (from Gamify+ / TrafficLab+), WACK, release documents in `docs/release/` | |
| 11.3 | Lab install: provisioned for every account on a lab PC; tested on a shared login and on separate logins | |
| 11.4 | Privacy policy at `softwareplus.ai/plcplus/privacy/`; the program's page and help at `softwareplus.ai/plcplus/`; a row in `status.html` | |
| 11.5 | Help ▸ About's three links (page, Store, all programs) and Check for a new version | |

---

## Phase 12 — spec Phase 2: intermediate and submission

Units 6–8 and 11 (timers and counters, data and math, sequencing and state machines, troubleshooting), more scenes, fault
injection, the report verifier tool, and report refinements from the faculty pilot. Gate: a student pilot in one course
section, with PDF reports handed in through the LMS and reviewed end to end. Broken into tasks when phase 10 is done.

## Phase 13 — spec Phase 3: advanced and dialects

Units 9, 10 and 12 (analog and PID, Structured Text and function blocks, industrial systems); the Structured Text editor
(AvalonEdit) and the SFC editor; the first vendor dialect, **Allen-Bradley (Rockwell)**, as in the lab (question 5); optional report formats (CSV, an LMS
connection). No research analytics: the grant does not require research on outcomes (question 13). Gate: advanced modules validated in the lab on the vendor's hardware.

---

## Open questions for Ron — all 14 answered (asked and answered 2026-11-04)

1. ~~**The name.**~~ **PLC+** in front of people; `PLCPlus` in code, folders and the exe. Changed in the program 2026-11-04;
   Ron reserved it in Partner Center the same day (11.1).
2. ~~**Free on the Store?**~~ **Free on the Microsoft Store.**
3. ~~**How a report is verified.**~~ **Both, as recommended**: always signed with the computer's key, and also with the
   instructor's class code when there is one (8.4, 8.8).
4. ~~**What is inside the progress file.**~~ **JSON** in the encrypted block. The files are not likely to get large (6.3).
5. ~~**The lab PLC.**~~ **Allen-Bradley.** The lab comparison runs on it (1.12), and Rockwell is the first vendor dialect
   (phase 13).
6. ~~**Prerequisites.**~~ **Recommended, not enforced** (4.4).
7. ~~**Lesson pages.**~~ **The program's own window** (4.5).
8. ~~**Who signs the content.**~~ **Ron signs the content when it is published** (4.8).
9. ~~**AI.**~~ **No AI in Phase 1.** If the review shows an AI part is needed, the question comes back (R.1).
10. ~~**Lab PCs.**~~ **Not reimaged**, but students are still required to back up their files (6.10).
11. ~~**The main window's layout.**~~ **The family's layout** (7.1).
12. ~~**Grant outcomes.**~~ **Written as we go.** The grant asks only for faculty training on PLCs and student educational
    resources (9.6).
13. ~~**Research use of learning events.**~~ **Not required by the grant.** No research analytics; the event log stays in
    the progress file only as the record behind time on task and the report.
14. ~~**Report cover fields.**~~ **Only the name is required**; student ID, course and section, and e-mail are optional (8.4a).

---

## References

- `docs/SPECIFICATION.pdf` — Ron's technical specification, the source of every requirement
- `docs/PLAN.md` — decisions D1–D19 and how the engine and grader work
- `docs/MANUAL-TESTING.md` — Ron's hand-tests and the hand-test build command
- `docs/LAB-COMPARISON.md` — running the reference programs on the lab's Allen-Bradley PLC (1.12)
- `tests/golden/` — the reference programs R01–R11 and their expected traces (1.11)
- `docs/images/architecture.png` — the architecture diagram from the spec
- `content/plcplus-foundations/` — the first scene, challenge and example programs
- `..\USABILITY-CHECKLIST.md` — the usability rules every Store program meets
- `..\GamifyPlus\CLAUDE.md` — the signed result line and the Verify page that D6 and question 3 follow
