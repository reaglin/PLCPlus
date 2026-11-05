# PLC+ — development plan

Numbered tasks per phase, each with what "done" means. Requirement IDs (FR-, NFR-) and section numbers refer to Ron's
specification, `docs/SPECIFICATION.pdf`. Decisions and the reasons for them are in `docs/PLAN.md`.

Started **2026-11-04** from the specification.

## The goal now: the instructors' release (Ron, 2026-11-05)

Ron: *"The plan is to complete most of the modules and create a release for the Windows App Store. This will be the easiest way
to get it to the instructors who will be providing feedback. At this point we will only be releasing it to PLC instructors."*

- **PLC+ 1.0 for the PLC instructors**, on the Microsoft Store, free, **hidden**: not found by searching the Store, installed only
  from a direct link (Ron chose this over a private audience list, 2026-11-05). It can be made discoverable later.
- **Modules 1–8 and 11 written** (Ron, 2026-11-05): the spec's Phase 1 and Phase 2 units. Modules 9, 10 and 12 show their outcomes
  as "coming later".
- **A testing guide for the instructors** at `softwareplus.ai/plcplus/testing/`: install from the link, what to try, how to send
  feedback. It is the page the release is advertised with.

Phases 10 to 12 below lead to it. What the instructors send back becomes phase 13.

The spec's three delivery phases map onto this plan like this:
- **Spec Phase 1** (core and foundations, Units 1–5) is plan phases 0–9, and its gate is in phase 11. Its gate: the engine test
  suite passes, the engine matches a lab PLC on the reference programs, and a pilot runs with the faculty training cohort. The
  instructors' release is that pilot.
- **Spec Phase 2** (intermediate and submission): its units are phase 10; the rest is phase 13, after the instructors' feedback.
- **Spec Phase 3** (advanced and dialects) is plan phase 14.

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
| 0.4 | **First-look window** (`PLCPlus.exe`; became the review preview in phase 2): the motor station live (hold Start, press Stop), the program's rungs with power flow shown in green, thick and TRUE, the simulated clock with Pause / Step one scan / Reset, and **Check this program**, which runs the challenge's tests and shows each result, with hints after a failure. Help panel (F1), Help ▸ About with "Created by Dr. Ron Eaglin" | ⚠️ **Ron: hand-test 0** in `docs/MANUAL-TESTING.md`. Photographed through PrintWindow: all 3 tests pass with the seal-in program. **UX: walkthrough + heuristics 2026-11-04, 3 major and 6 minor, fixed:** the replay in plain words with each check marked ✓/✗; the results get their own share of the window and scroll by keyboard; Keep pressed boxes for stepping with a button held; why Stop uses a normally open contact; the fingerprint moved to a tooltip; Reset keeps the results; access keys no longer clash; a clear message if the content folder is missing. Not done yet: Help on the web and the privacy link in About wait for the softwareplus.ai page (11.4) |
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
| 1.12 | **Lab comparison** (waits for the lab instructor, Ron 2026-11-04): the reference programs run on the lab's **Allen-Bradley** PLC and the traces compared (spec 4.5, the Phase 1 gate) | ⚠️ **Ready for the lab:** `docs/LAB-COMPARISON.md` has every program as Logix rung text, the instruction map with the differences (CTD's done bit, no TP, divide by zero, the fault codes to confirm), how to run it (a 10 ms periodic task, internal tags first, a Trend), and a results table. Ron / the lab: who runs it |
| 1.13 | Instruction presentation: each instruction's symbol and operand layout for the editor, and the lesson it links to | With phase 3 |

---

## Phase 2 — simulation scenes (FR-3; spec 5.3) ⚠️ built 2026-11-04 — the preview for the instructors' review

Each scene has a challenge, a program that works and a program with a common mistake, all in
`content/plcplus-foundations/` (written by `tools/make-phase2-content.py`). Tests prove every working program passes every
test and every mistake fails a test the learner can see.

| # | Task | Done when |
|---|---|---|
| 2.1 | The scene format (JSON): components, layout, physics parameters, I/O map to tags; physics on the engine's clock in whole milliseconds; probes (`@tank.overflow`, `@door.position`, `@diverter.metal`) so tests can check what the machine itself did | ⚠️ Built 2026-11-04 |
| 2.2 | Scene 1: **pilot light and push-button station** — challenge "Light the pilot lights" (AND in series, a normally closed contact for Manual) | ⚠️ Built 2026-11-04 |
| 2.3 | Scene 2: **motor start/stop with seal-in** | ⚠️ Built 2026-11-04 |
| 2.4 | Scene 3: **traffic light** — "Run a traffic light" (three TONs in a chain; never two lights at once) | ⚠️ Built 2026-11-04 |
| 2.5 | Scene 4: **garage door** — "Open and close a garage door safely" (reversing motor, top and bottom limit switches, safety eye, interlock; the door records a conflict or coming down on something) | ⚠️ Built 2026-11-04 |
| 2.6 | Scene 5: **tank fill and drain** — "Fill and drain a tank" (float switches at 20 % and 80 %, fill and drain valves, latched Filling/Draining memory; never overflows, never both valves) | ⚠️ Built 2026-11-04 |
| 2.7 | Scene 6: **conveyor sorter** — "Sort metal from plastic" (a feeder with a fixed pattern of parts, a metal sensor, a photo-eye, a diverter into Bin A; the program must remember what the sensor saw) | ⚠️ Built 2026-11-04 |
| 2.8 | The component library | ⚠️ Built: push button, selector switch, emergency stop, pilot light, horn, motor, solenoid valve, tank, float switch, garage door, limit switch, safety eye, conveyor (with its feeder), photo-eye, proximity sensor, diverter. **Still to come:** thumbwheel, analog sensors and numeric display (with analog I/O, Unit 9), cylinder, heater, pump, stack light, elevator car |
| 2.9 | The scene view: draws any scene from its file and animates it, with buttons operable by keyboard and screen reader | ⚠️ Built 2026-11-04 (`SceneView`): every control is a real button or switch; every part says its state in words beside it and in "What every part is doing"; "The wiring" lists each tag and what it means. **UX: walkthrough + heuristics 2026-11-04, 0 critical, 3 major and 6 minor, fixed:** Alt+C clash; the garage door's brief states the interlock its hidden test checks; the state list changes only lines that changed (screen readers keep their place); each test has its own hint; the tank's switch is "Mode: Off/Auto"; toggles keep a fixed screen-reader name; hints name the controls as drawn; push-button tooltips mention the Space bar; Tab order follows the screen; picking a program starts the clock again. The garage door only stops (a real opener also reverses): said in the brief and in the review guide |
| 2.10 | Fault injection: a stuck sensor, a failed motor, a wiring error | ⚠️ Built: stuck buttons, switches and sensors, a failed motor or valve, and wiring faults (open wire, short, two inputs swapped). Used by the troubleshooting unit (11); no scene uses them yet |
| 2.11 | **Instructors' review** of the preview: the flow, the six challenges, the wording | ⚠️ Ron: hand-test 2, then send the review build to the PLC instructors: `review\PLCPlus-preview-2026-11-04.zip` (63 MB; self-contained, needs no .NET; unzip and open PLCPlus.exe; the guide `INSTRUCTOR-REVIEW.md` is inside). Their notes become plan items |

---|---|---|
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

## Phase 3 — the ladder editor (FR-2; spec 5.1) ⚠️ built 2026-11-04

The editing is `src/Plc.Editors/LadderEditor.cs` (no UI, 12 tests, including building the seal-in from a blank program and passing
the challenge); the window shows it when Program is **My program (edit it)**. Every change runs at once: the PLC and the machine
restart with the new program. My program is kept per challenge in `%LocalAppData%\PLCPlus\work\`.

| # | Task | Done when |
|---|---|---|
| 3.1 | Place contacts, coils and boxes on rungs by dragging **or by keyboard**, with parallel branches | ⚠️ Built: the tool row (click, or drag onto an element), letters O C P N Q S R B and I (box menu, grouped: timers, counters, compare, math, move and bit logic), Shift+letter puts it before; Branch puts an OR around the selection (Shift+arrow selects several) or adds a path to a branch; a contact added at a coil goes before it |
| 3.2 | Tags: autocomplete, declare on first use, type checking while typing | ⚠️ Built: the tag box lists BOOL tags and timer/counter outputs (T1.Q); a new name is declared on first use (BOOL for contacts and coils; TIME, INT or BOOL for a box's operands by what it needs); a value typed where a tag belongs is refused with a reason; the validator runs on every change. Timers and counters get their instance (T1, C1) when the box is added. A New program for a machine starts with its inputs and outputs declared from the wiring |
| 3.3 | Live mode: energized paths highlighted, live values beside each instruction, forced values clearly marked | ⚠️ Built: right-click a contact or coil of a wired tag ▸ Force ON / OFF / Remove; a forced value shows as "FORCED TRUE" in bold orange; Reset removes every force. Works in every program, not only My program |
| 3.4 | Rung comments, undo/redo, copy/paste of rungs | ⚠️ Built: comment box in the properties panel; Undo/Redo (200 steps, Ctrl+Z/Y); Copy rung/Paste rung (Ctrl+C/V, through the clipboard); New rung, Delete, Rung ▲/▼; Start again ▾ (empty, or a copy of the working program, or of the mistake to fix); File ▸ Open a program / Save My program as (.plcprogram) |
| 3.5 | Validation messages shown on the rung and element they are about | ⚠️ Built: a dashed red box and a "!" on the element (its tooltip has the message), rung-level warnings under the rung's title, and the Problems list under the rungs (choose one to select the element). With problems, the PLC stays stopped and says why |
| 3.6 | Printing a rung (for the report's program evidence, spec 9.2) with the same drawing code | ⚠️ Built: File ▸ Save the program as a picture (PNG at 192 dpi, with a title), drawn by the same `LadderView`; the PDF report (phase 8) uses it |
| 3.6a | UX review of the editor (walkthrough + heuristics, 2026-11-04): 2 critical, 4 major, 5 minor, **fixed** — a typed tag saved against its own element even when another is clicked; keys read before the scroll area takes the arrows; a whole branch can be selected (→, or its right-hand side) so the next element goes after it; typos caught twice ("Did you mean Start?" and a warning that nothing turns the tag on); results marked stale after a change; Open asks first and, like Start again, is one Undo step; Undo says what it undid; the time format explained (T#2s); removed forces mentioned; tooltips complete; a focus border on the rungs | ⚠️ |
| 3.7 | Hand-test 3 and the instructors' review of the editor | ⚠️ Ron: hand-test 3 in `docs/MANUAL-TESTING.md`; the review build now includes the editor |

---

## Phase 4 — the Learning Core: content and the learning path (FR-1; spec 6) ⚠️ built 2026-11-04

Ron, 2026-11-04: *"The overall interface will be similar to the Statistle/BMPFast and other interfaces and divided into
modules. Under each module will be exercises … and they will build upon each other. … each item in a module will have
'Learning' 'Example' 'Reference' and other tabs … The user will progress through modules and modules will indicate the status
of the student."* His answers the same day: **Module → Item** (two levels), the tabs **Learn · Example · Exercise ·
Reference**, **all 12 modules** in the tree, and **first drafts for modules 3 and 5** (D20). The content format is in
`docs/CONTENT-FORMAT.md`.

| # | Task | Done when |
|---|---|---|
| 4.1 | Content package: a folder or zip with `manifest.json`, lessons in Markdown with front-matter, scenes, challenges, media | ⚠️ Built: `manifest.json` lists the module folders; each has `module.md` and one Markdown file per item (front-matter + the Learn page); `reference/` holds shared Reference pages. Loaded by `Learning.Core.Content.ContentPackage`; the front-matter reader is a small YAML subset of our own (no library). A zip comes with signing (4.8) |
| 4.2 | The hierarchy, **Module → Item** (Ron, 2026-11-04; the spec's unit → lesson → activity folded into two levels), with stable IDs for modules, outcomes and items | ⚠️ Built: an item is a reading (complete when read), a lesson with an exercise (complete when it passes), or the module check. Progress keys off the IDs, so a content update keeps it |
| 4.3 | **Every item has the tabs Learn, Example, Exercise and Reference** (Ron, 2026-11-04; D7, D20); a module's first page shows its prerequisites, learning outcomes, topics and items | ⚠️ Built: Example runs the working program on its machine (and the common mistake), Exercise is My program with Check; a module's last item is its **module check**. Ron's first shape; the instructors review the flow (4.9) |
| 4.4 | Prerequisites are **recommended, not enforced** (Ron, question 6): a module whose prerequisites are not complete says what to finish first but opens anyway, and the report shows the instructor it was opened early | ⚠️ Built: the module page and the item header say so in orange; the first item opened early is recorded as an `opened-early` event for the report (phase 8) |
| 4.5 | The lesson viewer **in the program's own window** (Ron, question 7): Markdown into a WPF FlowDocument, with live widgets; no web view | ⚠️ Built with **Gamify+'s MarkdownView, copied** (no Markdig: stay with what works), plus italics, `|` inside code in tables, and widgets on their own line: `[[ladder: programs/x.json]]` draws a program, `[[truth-table: (Start OR Motor) AND Stop]]` lists every row. The mini simulation is the Example tab |
| 4.6 | Submission points declared in the path: which exercises they cover and what counts as complete | ⚠️ Built: the module check (`kind: assess`, `covers: […]`) is complete when every item it covers is; its Exercise tab lists them. Making the report when it is reached is phase 8 |
| 4.7 | `Tools.ContentValidator`: checks links, IDs and challenge definitions from the command line | ⚠️ Built: `dotnet run --project tools/ContentValidator [folder]`: IDs unique, prerequisites exist and come earlier, objectives are outcomes, challenges, programs and scenes exist, reference pages, widgets, module checks. The shipped package passes (a test runs it) |
| 4.8 | Package signing: **Ron signs the content when it is published** (question 8), with a key kept out of git; a signing step in the publish script; the program rejects unsigned or untrusted packages | Not started: the built-in package ships inside the signed MSIX, so signing matters first for packages sent separately. Before the Store release (phase 12, 12.5) |
| 4.9 | **Review of the flow by PLC course instructors**: their comments become plan items | ⚠️ Ron arranges the reviewers; the review build and guide now show the modules |
| 4.10 | The window **in the family layout** (pulled forward from 7.1): menu, the learning path as a tree with ✓ ◐ ○ marks and "n of m complete", the module page, the item page with its four tabs, Previous/Next, Go ▸ Continue where I left off, F1 help | ⚠️ Built (code-behind; MVVM stays in 7.1). The old challenge picker is gone: the six machines are items 5.1–5.5 and 6.1. UX: walkthrough + heuristics, see 4.13 |
| 4.11 | Progress for one learner on this computer (`%LocalAppData%\PLCPlus\progress\preview.json`): items opened, read, attempts with score and program hash, events | ⚠️ Built: `Learning.Core.Progress.LearningProgress`; phase 6 moves the same record into each learner's PIN-protected file |
| 4.12 | Content: **first drafts of modules 3 (relay control) and 5 (basic ladder)**, outlines of 1, 2, 4 and 6, and 7–12 as "coming later" with their outcomes and topics; 14 Reference pages | ⚠️ For the instructors' review. The garage door, tank and sorter use only Unit 5's instructions, so they are 5.3–5.5; the traffic light (TON) is 6.1 |
| 4.14 | **The review on softwareplus.ai** (Ron, 2026-11-04: *"It is easier for them to review there than to open the zip file"*): https://softwareplus.ai/plcplus/review/ — the guide, one page per module with every item's Learn page, its Example and Exercise, the programs drawn by PLC+ (`PLCPlus.exe --export-ladders`), the Reference pages, a comment box under each item, and the preview to download. Generated by `tools/make-review-site.py`; deployed 2026-11-04 | ⚠️ Ron: send reviewers the address with `?user=yes` on the end, so they can comment |
| 4.13 | Hand-test 4 | ⚠️ Ron: hand-test 4 in `docs/MANUAL-TESTING.md`. **UX: walkthrough + heuristics 2026-11-04, 1 critical, 6 major, 8 minor; fixed:** clicking a part (or adding one with a tool button) puts the typing in its Tag box, so "click and type Start" no longer adds parts; "Next" after finishing an item is the next one after it, and Continue goes back to where the learner was; a returning learner opens on that item; a new learner starts on module 3 (the first written one); outline modules are not counted as missing prerequisites and their unwritten pages cannot be "read"; the early-start note is on the module page and the status line (not a fourth header line) and no longer promises a report; a second **Check my program** at the top of the Exercise tab; Start again no longer offers a copy of the working program; an unreadable progress file is reported and never overwritten; "Check my program" in every lesson; Save works from any tab of an item with an exercise, with a name like "5.2 … — My program"; F1 help says what each screen is for; message titles "PLC+ — …" with a cause and a next step; tabs say "(none)" when empty. Not done: Help on the web and Check for a new version wait for the softwareplus.ai page and the Store (7.6); "coming in a later phase" wording goes before the Store. Asked the instructors whether the Example should show the exercise's own answer (review question 11) |

---

## Phase 5 — assessment (FR-4; spec 7) ⚠️ built 2026-11-04

Ron, 2026-11-04: *"Start phase 5, the format is good and easy to follow."* The module checks now have questions, so every
module ends with a real Assess (D7). Choices made here for the instructors to judge (review guide, question 11): questions sit
on the **module checks** (readings stay "I have read this"); **80 %** passes; a check can be retried freely, so a wrong answer
says "look again" without the answer (practice questions, such as 2.3's, show it).

| # | Task | Done when |
|---|---|---|
| 5.1 | The challenge format and grader: scenarios, assertions at times, invariants, hidden scenarios, constraints, points, pass threshold, tiered hints | ⚠️ Built 2026-11-04; 7 tests |
| 5.2 | Assertions still to add: timing windows ("on between 2.0 and 2.2 s") and ordering ("A before B") | ⚠️ Built: `windows` (the first moment a tag reaches a value, watched from `after`, must fall in [from, to]: "too early" or "had not by") and `order` (`first` must come on before `then`; a `then` that never comes fails at the end). The traffic light's first test now checks yellow at 5 s ± 0.05, red at 7 s ± 0.05 and yellow before red. 4 tests |
| 5.3 | Replay a failed test visually from its trace | ⚠️ Built: **Replay the failed test** under Check: the first failed test runs again on the machine, the test pressing the buttons itself, at its own speed (×4 over 15 s), with "REPLAYING …" on the clock line; it stops just after the failure and pauses there, with the reason on the status line. Reset ends it; a change to the program cancels it |
| 5.4 | Each attempt recorded: score, time, hints used, program hash, machine | ⚠️ Built: every check of an exercise or questions is an attempt with its time, score, program fingerprint, **hints shown**, the **computer's name**, and for questions which were right. In the one-learner file now; phase 6 moves it into each learner's file |
| 5.5 | Other assessments: multiple choice, numeric with tolerance, truth-table fill-in, match the symbol, find the fault in a faulted scene | ⚠️ Built: `Learning.Core.Content.Quiz` (graded in the core; a quiz is a JSON file an item names with `quiz:`) and the questions on the Exercise tab (`QuizView`). **Find the fault** shows the machine running the working program with a wiring fault (`Stop:open`, `OpenLimit:shorted`, `PB1:swapped:PB2`). Content: the module 3 check (6 questions), the module 5 check (8, two of them faults), 2.3 (three truth tables, practice). The validator checks every question; a test passes every shipped quiz with its right answers |
| 5.6 | Mastery rules per objective (e.g. "pass two linked challenges at 80% or more"); states: not started, in progress, mastered, needs review | ⚠️ Built (`Learning.Core.Progress.Mastery`), one rule for every outcome until the instructors ask for others: **mastered** when every item that teaches it is complete and the latest answer to every question on it was right; **needs review** when the work is done but a later answer or check went wrong; shown on each module's first page with ✓ ↺ ◐ ○ and the evidence |
| 5.7 | The review site shows each module check's questions with the answers marked, for the instructors | ⚠️ Deployed with this phase |
| 5.8 | Hand-test 5 | ⚠️ Ron: hand-test 5 in `docs/MANUAL-TESTING.md`. **UX: walkthrough + heuristics 2026-11-04, 6 major, 9 minor; fixed:** answers stay for the session (going to read again no longer wipes them), a wrong answer names the item to read again (a fault question: "work the machine again"), half-done answers say what is missing ("Rows 2 and 4 are still ?", "not a number"), the check's status and tooltip include its questions, mastery's "review suggested" says which question and what to read, Reset after a replay starts the clock again, the page of questions scrolls over the fault machines, which have no access keys; Check jumps to the first ✗ and lists the ✗ numbers; screen readers hear each question with its options. Not changed: "Check my answers" next to "module check" (Ron's call if the words should differ) |

---

## Phase 6 — learners and progress files (FR-5, FR-9, FR-10; spec 8) — the shared engine (D5) ⚠️ built 2026-11-05

| # | Task | Done when |
|---|---|---|
| 6.1 | Start screen: every learner on this computer, by name and when they last used it | ⚠️ Built: **Who is learning?** covers the window whenever nobody is signed in: each learner's name, when they last used PLC+ (and on which computer, if another), and their backup (✓ up to date, ⚠ not yet or out of date), all read from the files' headers without a PIN. Choose, type the PIN, **Open**. Help on the right; an empty list says what to do first |
| 6.2 | New learner: display name, optional student ID, a 4–6 digit PIN, and a one-time recovery code to write down or print | ⚠️ Built: **New learner…** (name, required and unique on the computer; student ID, optional, kept inside the locked part; PIN twice), then **Your recovery code** once: five groups of four (Crockford letters, so O/0 and I/1 are not confused), **Print…**, **Copy**, and **Start learning** only after "I have written down or printed my recovery code". The first learner can start with the preview's progress and programs (the old files are renamed, not deleted) |
| 6.3 | The progress file (`.plcprogress`): a small readable header (name, profile ID, last saved, app version) and **one encrypted block of JSON** (Ron, question 4); AES-GCM with a PBKDF2 key from the PIN, a second copy of the key wrapped with the recovery code | ⚠️ Built: `Learning.Core.Learners.LearnerFile` (D22). One JSON file: the readable header (format, app, profile ID, name, last saved, computer, app version, last backup, last change, wrong-PIN count) and `body`, AES-256-GCM. A random key, wrapped with PBKDF2-SHA256 (600,000 rounds, about 0.07 s here) from the PIN and again from the recovery code; each block bound to the learner's ID. A file changed by hand does not open, and says so. Inside: the profile, the progress, My program per exercise, the content versions used, the last backup |
| 6.4 | Wrong PIN: after 5 tries a wait that grows; auto-lock after 15 minutes idle and on Switch learner | ⚠️ Built: after 5 wrong PINs a wait of 30 s, doubling to at most 30 min, counted in the file so restarting PLC+ does not reset it; the start screen counts down. **Forgot my PIN…** sets a new one with the recovery code. Auto-lock after 15 minutes without a key or mouse (5–60 in Settings): work saved, the key forgotten, the start screen says why, and the same learner returns to the same item. **Learner ▸ Switch learner** (Ctrl+Shift+L) saves and locks. **Learner ▸ Change my PIN…** |
| 6.5 | Remove from this computer (needs the PIN; offers to export first) | ⚠️ Built: **Remove from this computer…**: the PIN, then "Back up first?" (**Back up, then remove…** / **Remove without a backup** / **Cancel**); the file goes to the **Recycle Bin** |
| 6.6 | Export and import ("Add learner from file"), showing which computer saved it last and when | ⚠️ Built: export is **Learner ▸ Back up my progress…** (6.10). **Add learner from file…** says whose progress it is and which computer last saved it, and when; a learner new here is copied in (the same PIN opens it) |
| 6.7 | Merging two copies: attempts are append-only with unique IDs, so both copies' attempts are kept and mastery recalculated | ⚠️ Built: a learner already here is **combined**: every attempt and event kept once by its ID (old attempts without one get a key from their content), each item's earliest completion, My program the later of the two; mastery follows. Either copy's PIN works; this computer's PIN stays; the chosen file is never written to. Two sessions with one learner (two windows, or two Windows accounts) merge when they save. 4 tests |
| 6.8 | The shared folder `%ProgramData%\PLCPlus\Learners`, so lab PCs work with one shared login or separate ones; changeable in settings. Lab PCs are not reimaged (Ron, question 10) | ⚠️ Built: `%ProgramData%\PLCPlus\Learners`, created so every Windows account on the computer can change the files in it; **Settings…** on the start screen (the folder and the lock time) for everyone on the computer. ⚠️ For phase 12 (12.4): check the folder under the Store's MSIX install |
| 6.10 | **Backups are required** (Ron, question 10): the program asks the learner to back up (export) their file to a drive or cloud folder they choose, shows when the last backup was made, and reminds them when it is out of date (at each submission point and when they sign out) | ⚠️ Built: **Learner ▸ Back up my progress…** and the button at the top right ("⚠ Not backed up: Back up…", "⚠ Backup out of date …", "✓ Backed up today at …"); saving into the learners' folder is refused (not a backup). PLC+ asks for a backup when there is work since the last one: on Switch learner and on closing (Cancel stays), and when a module check is complete (the submission point, until phase 8's reports) |
| 6.9 | The event log (xAPI-style learning events) inside the progress file | ⚠️ Built: the event log in the file, as xAPI-style statements (the learner, a verb, the item or file, the result in words, a unique ID): created, signed in, signed out, locked, opened (once per item per session), read, attempted, completed, opened early, backed up, merged, PIN changed or reset |
| 6.11 | Hand-test 6 | ⚠️ Ron: hand-test 6 in `docs/MANUAL-TESTING.md`. **UX: walkthrough + heuristics 2026-11-05, 0 critical, 6 major, 9 minor; fixed:** Switch learner chooses nobody (the next student could have typed their PIN against the last one's name and locked them out); no auto-lock while a dialog is open, and a file locked under a dialog is never saved; a backup onto an older copy of the same learner (the USB stick from home) combines it first instead of overwriting it; **Open the learners' folder** and a damaged file can be removed without a PIN (an instructor without a student's PIN: question 15); changing the folder warns how many learners stay behind, and the empty list says where PLC+ is looking; Add learner from file takes several files at once; the preview's progress is no longer ticked by default; wrong-PIN messages say "Press Forgot my PIN"; the countdown keeps "That PIN is not right" and is spoken politely; a new PIN or two combined copies are confirmed on the status line; cancelling the backup when leaving keeps PLC+ open; the recovery code is cleared from the clipboard; error details follow "Details for support:"; the start-screen banner is neutral blue. Not changed: no search in a long list of learners (a class fits), the title still says "(preview)" until the Store |

---

## Phase 7 — the program's window and the learner dashboard (FR-6; spec 8.5) ⚠️ built 2026-11-05

| # | Task | Done when |
|---|---|---|
| 7.1 | The shell in MVVM (CommunityToolkit.Mvvm), **in the family's layout** (Ron, question 11): menu bar, rail of actions, the learning path as a tree (modules → items), the item's tabs on the right, a status line saying what to do next | ⚠️ Step 1 built 2026-11-05 (Ron: the toolkit, step by step): CommunityToolkit.Mvvm 8.4.0 (the family's MAUI apps use it). `ViewModels/ShellViewModel` holds the learner, the path's rows (`PathNode`), the totals and time, the status line, the title, the backup button, the text size and the Go commands (Next, Previous, Continue, My progress); the window binds to them. Still in the window: the item's tabs, the machines, the questions and the dialogs, for later steps |
| 7.2 | Path map: each unit and lesson locked, available, in progress or mastered (with a mark as well as a colour) | ⚠️ Built (`Learning.Core.Progress.Dashboard`): each module is – coming later, ○ outline, ○ not started ("after module N" when its recommended modules are not done: D15, so not locked), ◐ in progress, ✓ complete, ★ mastered (every outcome mastered) or ↺ review suggested, in the tree and on **My progress**, always with words. 7 tests |
| 7.3 | Submission points: not reached, ready to generate, generated (with the date) | ⚠️ Built: each module check is a submission point on My progress: ○ not reached (how far: "3 of 4 items done, questions not passed yet"), ● reached, ✓ report made (with the date; reports are phase 8, recorded as `SubmissionRecord` in the file) |
| 7.4 | Objectives view with evidence, and a "review suggested" list | ⚠️ Built: My progress lists every outcome of the written modules with ✓ ↺ ◐ ○, and **Review suggested** at the top: the outcome, the question that was wrong, and what to read again |
| 7.5 | Time on task and progress totals always visible | ⚠️ Built: above the tree, always: "N of M items complete · N of M modules" and "Time in PLC+: … (today …)". Time counts only while a key or the mouse was used in the last two minutes, per item, in study sessions kept in the learner's file (sessions merge by ID, so a minute is never counted twice). My progress shows time per module |
| 7.6 | Help menu as in the usability checklist: Help for this screen (F1), Help on the web, Check for a new version, About (Store link once there is a Store ID) | ⚠️ Built: Help ▸ Help for this screen (F1), **Help on the web** (softwareplus.ai/plcplus/help/, written and deployed with this phase, with softwareplus.ai/plcplus/ itself, which About already linked to), **Check for a new version…** (copied from Accessibility+; says PLC+ is not on the Store yet until it has a Store ID), About |
| 7.7 | Accessibility: keyboard throughout, screen-reader names, scalable text, colour-safe marks (NFR-6) | ⚠️ Built: **View ▸ Larger text / Smaller text / Normal size** (Ctrl++, Ctrl+−, Ctrl+0), 80–200%, the whole window scaled, kept in the learner's own file so it follows them; My progress from the keyboard (Ctrl+Shift+Home) and read as one page; every mark with words |
| 7.8 | Hand-test 7 | ⚠️ Ron: hand-test 7 in `docs/MANUAL-TESTING.md`. **UX: walkthrough + heuristics 2026-11-05, 3 major, 7 minor; fixed:** larger text no longer pushes Next ▸ and the page off the screen (the columns' smallest widths shrink with the scale, the help panel steps aside above 120% and says so, lesson ladders scroll sideways); the status line is announced to screen readers, and My progress from the menu or Ctrl+Shift+Home puts the reader on the page; a complete module check says the report comes later instead of "Ready"; Ctrl++ works on the main keyboard; the Go commands do nothing behind the start screen; disabled Next/Previous say why; the totals say "the 2 written so far"; ★ means mastered everywhere (outcomes too); the Check for a new version tooltip says PLC+ is not on the Store yet. **Not changed:** the start screen and the dialogs do not scale yet (7.9); time on task is a lower bound (reading without touching the keyboard or mouse for two minutes is not counted), to be said on the reports (8.x) |
| 7.9 | Text size on the start screen and in the dialogs (UX review 2026-11-05): a size for the computer before anyone signs in, and the learner's size on their dialogs | |

---

## Phase 8 — results, reports and submission (FR-7, FR-8, FR-11; spec 9, 10) — the shared engine (D5, D6) ⚠️ built 2026-11-05

| # | Task | Done when |
|---|---|---|
| 8.1 | The progress snapshot: one format-neutral record of everything a report shows (spec 9.5) | ⚠️ Built (`Learning.Core.Reports.ProgressSnapshot`, `ReportBuilder`): one record of everything a report shows (learner, point, status and what is missing, summary, each item's status, best score, attempts, time, hints, completed date and tests, outcomes, program evidence, versions, computer key). Subject-neutral; the PDF is drawn from it, and the same record goes inside the PDF, signed. Exercises now keep each test's pass/fail (a hidden test is "Hidden test N", D9) |
| 8.2 | The **final screen** of a submission point, as in the Gamify+ games: grade, progress, the learner's name, and the ways to hand it in | ⚠️ Built: **Hand in a report**, the second row of the path (Go ▸ Hand in a report, Ctrl+Shift+R; a **Hand in a report…** button on every module check): choose a module check or everything so far; the card shows the status, the name, the overall score, items, outcomes and time, and what is still to do; then **3. Hand it in** |
| 8.3 | The **PDF report**: cover, summary, exercise detail, program evidence (optional), verification (report ID, versions, code, QR); PDF/A and tagged; default name `PLCPlus_<LastName>_<SubmissionPoint>_<yyyy-MM-dd>.pdf` | ⚠️ Built (`src/Learning.Reports.Pdf`): cover with a Complete/Incomplete box, summary, exercise detail with the tests, outcomes, program evidence (pictures of the programs that passed, drawn by the same ladder view, with their SHA-256; the programs themselves are inside the file) and verification (report ID, versions, verification code, computer key, QR). Tagged (headings, paragraphs, lists, tables with header cells, figures with words) and written as PDF/A by PDFsharp, with the fonts embedded; **not yet checked with a PDF/A validator (veraPDF)**. Default name `PLCPlus_Gomez_Module5Check_2026-11-05.pdf`. **PDFsharp in place of QuestPDF** (D23): the family's PDF library, MIT; QRCoder as PunchMonkeyServer |
| 8.4 | The **signed result line** for any LMS, discussion or e-mail, in Gamify+'s shape. **Signed both ways** (Ron, question 3): always with the computer's own key (proves it was not edited), and also with the instructor's **class code** when the learner has entered one (proves it came from that class) | ⚠️ Built: `PLC1|Maria Gomez|Module 5 check: Basic ladder programming|Incomplete, score 33%|2026-11-05|R-YAT9-9KKF|ET35-M3G0-M0WS`. The PDF is signed with this computer's key (ECDSA P-256, made the first time, kept with Windows DPAPI in the data folder) and, when the learner entered one, with the class code. The line's code is signed with the class code, or with the report's own signature (it then checks together with its PDF). How and why: D23 |
| 8.4a | The report cover's fields: **name required**; student ID, course and section, and e-mail optional (Ron, question 14) | ⚠️ Built: **My details for reports…** (asked the first time a report is made): student ID, course, section, the learner's e-mail, the instructor's e-mail, the class code, and whether to put pictures of the programs in. All optional; the name is the profile's. Kept in the learner's own file |
| 8.5 | **Send to my instructor**: opens the learner's e-mail with the result line and says where the saved PDF is, to attach | ⚠️ Built: **Send to my instructor…** starts an e-mail (to the instructor's address, if given) with the result line, and opens the folder with the PDF chosen, to attach it. PLC+ sends nothing itself. Also **Copy the result line** and **Show the file in its folder** |
| 8.6 | Incomplete reports allowed, marked Incomplete with what is missing; regenerate earlier reports; an on-demand full report | ⚠️ Built: a report before the check is complete asks first, then says **Incomplete** on the cover and lists what is missing; any point can be reported again (a new report ID each time); **Everything so far** is the full report |
| 8.7 | Submission history: every report with its ID, date and hash | ⚠️ Built: **Reports you have made**: date, point, Complete/Incomplete, report ID and file name, with the file's SHA-256 in the tooltip; choosing one shows its result line again. Kept in the learner's file (`SubmissionRecord`, merged by report ID). My progress marks the check ✓ report made |
| 8.8 | The report verifier (FR-11, spec Phase 2): checks a PDF or a result line, and makes an instructor's class code | ⚠️ Built in PLC+ itself: **File ▸ Check a report (for instructors)…**, and **Check a report** on the start screen (no learner needed). It says Genuine / Changed / Not a signed report, what the report said when made, whether it carries the class code, and checks a result line; **Make a class code…** makes and copies one. Any changed byte, or an edit saved onto the end of the file, shows. A separate small verifier program can come later (spec Phase 2) |
| 8.9 | Hand-test 8 | ⚠️ Ron: hand-test 8 in `docs/MANUAL-TESTING.md`. UX: walkthrough + heuristics (2026-11-05): 6 major fixed (the hand-in part followed the latest report of any check, so a line could be copied for the wrong one; "✓ report made" for an Incomplete report; the result line and the seal not explained; nothing told learners not to save the PDF again; Cancel on the first details closed without a word; Make a class code replaced the code being checked with). 5 of 6 minor fixed and 3 cosmetic; not done: "_Incomplete" in the file name (the spec names the pattern; the cover and the check say it). Seen in the window: the page, the details, a report made in the program with its ladder pictures, and the check of that report and its line |

---

## Phase 9 — the content for Units 1–5 (spec 6.2) ⚠️ first drafts 2026-11-05

| # | Task | Done when |
|---|---|---|
| 9.1 | Unit 1 Electrical foundations: voltage, current, circuits, AC/DC, safety | ⚠️ First draft (2026-11-05): four lessons (voltage, current and resistance with Ohm's law; series and parallel, and why they make AND and OR; AC and DC in a panel, 480 V → 120 V AC → 24 V DC; working safely with the lockout/tagout steps in order, saying plainly it does not replace the lab's safety training), practice questions on each (19), a 10-question check, Reference pages Ohm's law, AC and DC, Lockout/tagout. ⚠️ Instructors: the LOTO steps and the verify step, the shock figure, the panel voltages, 0.5 A per output point |
| 9.2 | Unit 2 Logic and number systems: binary, hex, BCD, Boolean logic, truth tables | ⚠️ First draft (2026-11-05): binary and hex (place values, conversions both ways, IEC `2#`/`16#`, a 16-point input module as a word), BCD (thumbwheels and displays, why 1010–1111 are wasted), AND/OR/NOT as contacts, truth tables of up to four inputs with XOR and order of operations. Practice questions on each (23), a 10-question check. **A new question kind, the typed answer** (`text`): write 45 in binary or 200 in hex, with leading zeros, spaces and `2#`, `16#`, `0x` accepted; or a BCD pattern. A fourth outcome, plc.m2.o4 (larger truth tables). ⚠️ Instructors: FRD/TOD and BCD_TO_INT names, `Status.3` bit addressing, XOR's place in the order of operations |
| 9.3 | Unit 3 Relay control: relays, contactors, relay ladder diagrams, seal-in circuits | ⚠️ First draft (phase 4), revised: its examples now run the new machines (the press guard, the pump, the reversing conveyor), so module 3 no longer draws module 5's answers (D21); the check says it is ready to hand in |
| 9.4 | Unit 4 PLC fundamentals: hardware, I/O modules, sensors, scan cycle, tags and addressing | ⚠️ First draft (2026-11-05): inside a PLC (compact and modular, Allen-Bradley families as examples), I/O modules (sinking and sourcing, relay/transistor/triac outputs, why an output drives a contactor coil), sensors (limit, float, inductive and capacitive proximity, the three photo-eye setups, wired to fail safe), the scan cycle (double coils, short pulses, Logix's RPI), tags and addresses (BOOL to TIME; Logix, SLC 500 and IEC addresses side by side). Each with practice questions (24) and an Example on a machine like module 5's; a 10-question check with two find-the-fault machines (a broken temperature-switch wire, a shorted guard switch: both fail unsafe). ⚠️ Instructors: sinking/sourcing, the addresses, the RPI |
| 9.5 | Unit 5 Basic ladder programming: contacts, coils, latching, edge detection | ⚠️ First draft (phase 4), revised for D21: every Example is a similar machine (9.7); the check says it is ready to hand in |
| 9.7 | **Each exercise's Example is a similar machine, not the same one** (Ron, 2026-11-04, D21): the learner sees the idea worked out, then applies it. A second scene, challenge and working program per exercise (e.g. a conveyor motor for the seal-in exercise's motor station), and each item's `example:` points to it | ⚠️ Built (2026-11-05): five example machines, each with a working program and a common mistake, each passing (and its mistake failing) its own tests: **a two-hand press guard** (5.1: AND, NOT; mistake: hand buttons in parallel), **a pump with an overload relay** (5.2: the seal-in, so it is not the motor exercise renamed; mistake: Stop with a normally closed contact), **a reversing conveyor motor** (5.3: interlocks; mistake: none, both contactors pull in), **an overheat alarm** (5.4: set and reset; mistake: no memory), **push on, push off** (5.5: a rising edge; mistake: a plain contact, so it flickers). The motor component can now run in reverse (spec 5.3), and shows when it is told to go both ways. Written by `tools/make-example-machines.py`. Modules 3 and 4 use them too, so no Example before module 5 runs a module 5 answer. The review site shows instructors each exercise's own working program |
| 9.6 | Objectives with stable IDs. **The grant's outcomes are written as we go** (Ron, question 12): the grant asks for faculty training on PLCs and student educational resources, so each unit's outcomes are drafted with the unit, kept in one list (`docs/OUTCOMES.md`, once there is a unit), and Ron reviews them | ⚠️ Ron reviews **`docs/OUTCOMES.md`** (written by `tools/make-outcomes.py`): 22 outcomes for modules 1–5 with their IDs, where each is taught and how many questions give evidence for it. Wording sharpened in modules 1, 2 and 4 (IDs unchanged); one added, plc.m2.o4 |
| 9.8 | Hand-test 9 | ⚠️ Ron: hand-test 9 in `docs/MANUAL-TESTING.md`; then the instructors' review of modules 1–5 (the review site is regenerated). UX: walkthrough + heuristics on the content (2026-11-05): 8 major fixed (how to type a binary or hex answer now shows under the box; the overheat alarm's Reset renamed Alarm reset, so it is not the Example tab's Reset; the tree and the help gave the wrong completion rule for items with questions; 2.3 pointed back to module 3; Press Pause again → Run; an ambiguous check question; the Example tab now says when it is a different machine from the exercise; the shock sentence in 1.4). 11 minor and 6 cosmetic fixed, among them: the pump got an overload relay, so it is not the motor exercise renamed; BCD answers accept 2# and 16#. Seen in the window: a typed answer marked wrong with its hint, the pump example with its note, 4.1 and 2.1 questions |

---

## Phase 10 — the content for modules 6, 7, 8 and 11 (spec 6.2, spec Phase 2 units; for the instructors' release) ⚠️ first drafts 2026-11-05

Each module as modules 1–5 are: lessons, an exercise or practice questions on every item, an Example on a machine like the
exercise's (D21), a module check, Reference pages, outcomes in `docs/OUTCOMES.md`, the validator clean.

| # | Task | Done when |
|---|---|---|
| 10.1 | **Module 6, timers and counters:** TON, TOF and TP; retentive timing (RTO on the Allen-Bradley PLC); CTU, CTD and CTUD; cascading. Exercises on machines (the traffic light is the first), each with its example machine | ⚠️ First draft (2026-11-05): TON (the traffic light stays the exercise; a warning-horn conveyor is its example), TOF and TP (an exhaust fan; stair lights), **retentive timing, a new item 6.3** (a pump's service hours; a bake oven), counters (batches of 5 on a conveyor with a display; a car park with CTUD), cascading (a case packer with two counters; a flasher). Nine machines, a 10-question check. **TONR added to the engine** (RTO on Allen-Bradley), with its test |
| 10.2 | **Module 7, data and math:** compare, math, move, data types, arrays. Needs two scene components the spec lists: a **numeric display** (an INT output) and a **thumbwheel** (an INT input); a machine that counts and measures (e.g. a parts counter with a batch size) | ⚠️ First draft (2026-11-05): compare, math (overflow, whole-number division, divide by zero), MOVE and data types, arrays (a recipe table), each with an exercise and an example; eight machines; a 10-question check. **New components: a thumbwheel and a numeric display** (both optionally BCD, a bad code shows Err). ⚠️ The editor cannot declare an array or change a tag's type, so the arrays exercise starts from a program that declares the table (13.x) |
| 10.3 | **Module 8, sequencing and state machines:** step logic, sequencers, a state machine with a step number, an introduction to SFC as diagrams (the SFC editor stays in phase 14). Needs a machine with several steps (e.g. a **cylinder** component and a fill-mix-drain tank, or a stamping station) | ⚠️ First draft (2026-11-05): step logic (a stamping station; a transfer arm), sequencers with a step number (a batch tank; a parts washer), state machines with a stop state (a gate; a rinse tank with a leak state), an introduction to SFC (a reading, practice questions, and the washer's program written SFC-style as a third Example program). Six machines; a 10-question check. **New component: the cylinder** (one or two solenoids, extended and retracted sensors, a stuck fault) |
| 10.4 | **Module 11, troubleshooting:** diagnostics (watching tags, forcing, traces, the replay), and find-the-fault on faulted machines: a stuck sensor, a failed motor, a wiring error (the engine already injects them). Practice faults on every earlier machine | ⚠️ First draft (2026-11-05): the method and PLC+'s tools, input faults (broken wire, short, a wire on the wrong device, stuck sensor), output and device faults (**new fault `Tag:failed`**: the PLC turns the output on, the device does nothing), and three **find-and-fix exercises** that start from a buggy program (**new: a challenge's `startProgram`**): a double coil, a seal-in on the wrong tag, a timer used by two boxes. 22 practice find-the-fault questions; a 10-question check |
| 10.5 | Outcomes for modules 6, 7, 8 and 11 in `docs/OUTCOMES.md` | ⚠️ Ron reviews `docs/OUTCOMES.md`: now 42 outcomes for modules 1–8 and 11 (module 6: o2 reworded for TONR; module 7: o1 compare, o2 types and arrays, o3 math and o4 MOVE new; module 8: o3 a step number new; module 11: o3 find and fix a bug new) |
| 10.6 | UX review of the new content and components; hand-test 10 | ⚠️ Ron: hand-test 10 in `docs/MANUAL-TESTING.md`. UX: walkthrough + heuristics on the four modules, the new components and Pop out (2026-11-05): 4 major fixed: **Pop out stopped the auto-lock and time on task** (it was a ShowDialog, and the idle timer waits for dialogs; now the main window is disabled as a dialog would do, so the lock and the time carry on, and the lock closes the pop-out); **the right answer was nearly always the first option, and the longest**: options are now shown in **an order of each learner's own** (the same on every try; `OptionOrder`, tested), and the wrong options rewritten to be as long and specific; 11.6's steps invited a fix that breaks the mixer; modules 7–8 taught what the editor cannot yet do (named timers, DINT, starting values) without saying so. 13 minor and 3 cosmetic fixed: lessons no longer repeat the hints, steps for rising edges and parallel compares, the find-and-fix exercise's first message now shows, the thumbwheel's arrow keys and a word on bad input, amber buttons, Esc and focus in the pop-out, no access keys on question pages' machines |
| 10.7 | **Pop out** (Ron, 2026-11-05: *"The example and the exercises need more screen space in many cases … allow them to 'Pop Out' into their own resizable screen which contains just the necessary items. These pop-outs should be modal and not allow focus on the spawning screen."*) | ⚠️ Built: **Pop out** at the top of every Example and Exercise tab, and on find-the-fault machines: the same program and machine (as they are, still running) move into a window of their own, resizable, opening at nearly the main window's size (maximized if it is); modal, so the main window waits; a status bar of its own; **Put it back** or closing returns them. The auto-lock closes it first, and moving the mouse in it counts as use |
| 10.8 | The content validator enforces D21: an Example that runs an exercise's machine, a ladder picture of an exercise's answer, or a find-the-fault question before its machine's exercise is reported (with a test) | ⚠️ Built; the package passes |

## Phase 11 — the release gate ⚠️ built 2026-11-05, waiting for Ron

| # | Task | Done when |
|---|---|---|
| 11.1 | UX review of every screen (`ux-reviewer`: cognitive walkthrough and heuristic evaluation) against `..\USABILITY-CHECKLIST.md`; fixes made | ⚠️ Built (2026-11-05): a full sweep of every screen (walkthroughs as a student's first evening and an instructor's first look) against the checklist: 5 blockers, 6 major, 11 minor, 2 cosmetic. Fixed: the Check-for-a-new-version tooltip; "a later phase" on module pages; **a way to give feedback** (Help ▸ Testing guide and feedback, and a button on the start screen); the start screen says what PLC+ is, has a part for **Instructors**, Help on the web and Check for a new version; the questions' tab reads **Exercise (questions)**; the help panel's words for Example, Check and Questions; disabled controls show why; Alt+L no longer clashes (L_earn); screen-reader names for the symbol tools; one name for the tests; "Details for support" everywhere; About adds the testing guide, privacy and the publisher line. Left for phase 12, because they need it: the Store link and ID (12.6), the privacy page (12.7) and the testing page (12.8), which the links point to. Not done: a "look around without signing in" learner for instructors; the start screen says instead to make a learner with any name and remove it after (⚠️ Ron: enough?) |
| 11.2 | The round manual test in `docs/MANUAL-TESTING.md` (the usability round's template); Ron's hand-test of the whole program | ⚠️ Ron: the round **Round 2026-11 — usability** at the top of `docs/MANUAL-TESTING.md` (A first start, B this round's fixes, C every screen, D the same as every program, E look), on `manual-test\PLCPlus.exe` version 1.0.0 |
| 11.3 | Ron's verdicts on hand-tests 0–10, and on the outcomes; what he returns is fixed first | ⚠️ Ron |
| 11.4 | The words "preview" taken out where the release is meant (window title, About, status); the content package's version and note for the release | ⚠️ Built: the window title is **PLC+**; About says **Version 1.0.0** (`Directory.Build.props`); the welcome and unwritten-page words say which modules are written from the content itself; the help panel, the Store-check message and the new-learner form no longer say preview; the content package is 1.0.0, "PLC+ — foundations" |
| 11.5 | The engine matches a lab PLC on the reference programs (1.12). **Not needed for the instructors' release**; it waits for the lab instructor and stays in the spec's Phase 1 gate | ⚠️ waits for the lab instructor |

## Phase 12 — the Microsoft Store, the testing guide and softwareplus.ai — built 2026-11-05; waits for the Partner Center identity

| # | Task | Done when |
|---|---|---|
| 12.1 | **PLC+** reserved in Partner Center (Ron, question 1); the icon with the "+" theme. **Free** (question 2) | ⚠️ **PLC+ reserved by Ron 2026-11-04.** **The icon built 2026-11-05** (`packaging/make-store-assets.ps1`, from Author+'s): one rung of ladder logic in white on steel blue (#14324A), a normally-open contact and, in the coil's place, the suite's amber "+"; "PLC+" under it on tiles of 71 px and up; 52 tiles (none over WACK's cap), five listing images, `src/Learning.Shell/PLCPlus.ico` (the exe's icon), the softwareplus.ai tile. ⚠️ Ron: look at `store/images/asset-contact-sheet.png` (hand-test 11.1) |
| 12.2 | MSIX packaging (from Gamify+ / TrafficLab+), version 1.0.0, WACK, release documents in `docs/release/` | ⚠️ Built 2026-11-05 from Author+'s scripts: `packaging/AppxManifest.xml` (one capability, `runFullTrust`; PLC+ needs no internet capability), `pack.ps1`, `make-msixupload.ps1`, `run-wack.ps1`; `artifacts\PLCPlus_1.0.0.0_x64_bundle.msixupload` (68.8 MB, development identity) builds, holds the content, and its program opens. `docs/release/STORE-SUBMISSION.md`: identity, every Partner Center field, certification notes, listing copy; seven screenshots in `store/screenshots/` (PrintWindow, sample learner). ⚠️ Ron: **the Package/Identity/Name and the Store ID** from Partner Center ▸ Product identity (then I rebuild with them, §3), and **WACK** (`.\packaging
un-wack.ps1`, needs administrator) — hand-test 11.4–11.5 |
| 12.3 | **Visibility: hidden, direct link only** (Ron, 2026-11-05): Partner Center ▸ Pricing and availability ▸ Visibility, "Public audience" with "Make this product available but not discoverable in the Store"; the steps in `docs/release/` | ⚠️ Written: `docs/release/STORE-SUBMISSION.md` §9 (Public audience → not discoverable → **Direct link only**; markets All; free; later, for students, made discoverable without a new package). Ron sets it at submission |
| 12.4 | Lab install: provisioned for every account on a lab PC; tested on a shared login and on separate logins; the learners' folder under the Store's MSIX install (6.8) | ⚠️ Written: `docs/release/LAB-INSTALL.md` (the shared folder `C:\ProgramData\PLCPlus\Learners` is not redirected by MSIX, which redirects only AppData; installing per account or with `winget --scope machine`; reimaged labs and backups; uninstall keeps the files). ⚠️ Ron: its four-step hand-test on a computer with two accounts, once the Store copy is live (hand-test 11.8) |
| 12.5 | Package signing of the content (4.8): the built-in package ships inside the signed MSIX, so it is enough for this release; separate packages later | ⚠️ Decided for 1.0: the content (`content/plcplus-foundations`) ships inside the package the Store signs; `pack.ps1` says so. Signed content packages sent on their own stay with 4.8 |
| 12.6 | Help ▸ About's three links (page, Store, all programs) and Check for a new version | ⚠️ Built: About has the page, testing guide, privacy and all-programs links, and **a Store line that appears once `StoreUpdates.StorePage` has the Store ID** (one line to change, `STORE-SUBMISSION.md` §3); About's links now go through `WebLinks.Open` (a browser that will not open says where the page is). Check for a new version is built (7.x); it asks the Store once the ID is in. ⚠️ Ron: try both in the Store copy (hand-test 11.7) |
| 12.7 | Privacy policy at `softwareplus.ai/plcplus/privacy/`; the program's page and help at `softwareplus.ai/plcplus/`; a row in `status.html` | |
| 12.8 | **The instructors' testing guide** at `softwareplus.ai/plcplus/testing/`: what PLC+ is for and what to review; install from the Store link; a guided tour (a learner, a module, an example, an exercise, a check, a report, checking a report); what to try in each module; how to send feedback (the review site's comments, or e-mail); what is known to be missing. Help ▸ **Testing guide** in the program links to it. Pictures taken from the program with PrintWindow | |
| 12.9 | The review site and the instructors' review guide (`docs/INSTRUCTOR-REVIEW.md`) changed to the Store install (the zip of the preview goes) | ⚠️ Done 2026-11-05: `docs/INSTRUCTOR-REVIEW.md` says 1.0 is on the Store for the reviewers (install from the invitation's link, the testing guide), what it now has (learners, My progress, reports) and what it does not; `tools/make-review-site.py` no longer offers the zip (and removes it from the site); the review site's four pictures retaken from 1.0. Deploy with 12.7 |
| 12.10 | Submitted by Ron; once live, the link goes in the testing guide and the guide is sent to the instructors | ⚠️ Ron |

## Phase 13 — after the instructors' feedback (spec Phase 2, the rest)

The instructors' notes, turned into tasks. Then: more scenes and fault injection, the report verifier as its own small tool, and
report refinements from the faculty pilot. Gate: a student pilot in one course section, with PDF reports handed in through the
LMS and reviewed end to end.

## Phase 14 — spec Phase 3: advanced and dialects

Units 9, 10 and 12 (analog and PID, Structured Text and function blocks, industrial systems); the Structured Text editor
(AvalonEdit) and the SFC editor; the first vendor dialect, **Allen-Bradley (Rockwell)**, as in the lab (question 5); optional report formats (CSV, an LMS
connection). No research analytics: the grant does not require research on outcomes (question 13). Gate: advanced modules validated in the lab on the vendor's hardware.

---

## Open questions for Ron — 14 answered 2026-11-04; one asked 2026-11-05 (waits for the faculty's use cases)

1. ~~**The name.**~~ **PLC+** in front of people; `PLCPlus` in code, folders and the exe. Changed in the program 2026-11-04;
   Ron reserved it in Partner Center the same day (11.1).
2. ~~**Free on the Store?**~~ **Free on the Microsoft Store.**
3. ~~**How a report is verified.**~~ **Both, as recommended**: always signed with the computer's key, and also with the
   instructor's class code when there is one (8.4, 8.8).
4. ~~**What is inside the progress file.**~~ **JSON** in the encrypted block. The files are not likely to get large (6.3).
5. ~~**The lab PLC.**~~ **Allen-Bradley.** The lab comparison runs on it (1.12), and Rockwell is the first vendor dialect
   (phase 14).
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
15. **An instructor removing a student who has left** (asked 2026-11-05, from the UX review). *Ron, 2026-11-05: to be
    answered after working with the faculty and collecting their use cases; until then the folder stays as it is.* The spec has Remove need the
    learner's PIN, which an instructor does not have. For now the start screen has **Open the learners' folder**, and Remove's
    tooltip names the file to delete with PLC+ closed. Is that enough for lab PCs, or should PLC+ have an instructor override
    (for example an instructor PIN set in Settings that can remove any learner, never open one)? *Recommendation: the folder is
    enough for the pilot; add an instructor PIN only if the lab instructors ask.*

---

## References

- `docs/SPECIFICATION.pdf` — Ron's technical specification, the source of every requirement
- `docs/PLAN.md` — decisions D1–D20 and how the engine and grader work
- `docs/CONTENT-FORMAT.md` — how modules, items, Reference pages and widgets are written, and the validator (4.1, 4.7)
- `docs/MANUAL-TESTING.md` — Ron's hand-tests and the hand-test build command
- `docs/LAB-COMPARISON.md` — running the reference programs on the lab's Allen-Bradley PLC (1.12)
- `docs/INSTRUCTOR-REVIEW.md` — the guide for the PLC instructors reviewing the course (2.11, 12.9)
- `docs/OUTCOMES.md` — the learning outcomes of the written modules (9.6, 10.5)
- `docs/release/STORE-SUBMISSION.md` — the Store submission: identity, Partner Center field by field, listing copy (12.2, 12.3)
- `docs/release/LAB-INSTALL.md` — PLC+ on a shared lab computer (12.4)
- `packaging/` — the MSIX scripts and manifest; `store/` — the listing images and screenshots (12.1, 12.2)
- `tests/golden/` — the reference programs R01–R11 and their expected traces (1.11)
- `docs/images/architecture.png` — the architecture diagram from the spec
- `content/plcplus-foundations/` — the learning path (modules, items, Reference pages) and the machines
- `..\USABILITY-CHECKLIST.md` — the usability rules every Store program meets
- `..\GamifyPlus\CLAUDE.md` — the signed result line and the Verify page that D6 and question 3 follow
