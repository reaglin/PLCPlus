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
| 4.8 | Package signing: **Ron signs the content when it is published** (question 8), with a key kept out of git; a signing step in the publish script; the program rejects unsigned or untrusted packages | Not started: the built-in package ships inside the signed MSIX, so signing matters first for packages sent separately. Before the Store release (phase 11) |
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
| 7.1 | The shell in MVVM (CommunityToolkit.Mvvm), **in the family's layout** (Ron, question 11): menu bar, rail of actions, the learning path as a tree (modules → items), the item's tabs on the right, a status line saying what to do next | The layout is built (4.10, code-behind); this moves it to MVVM with the learners |
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
| 9.7 | **Each exercise's Example is a similar machine, not the same one** (Ron, 2026-11-04, D21): the learner sees the idea worked out, then applies it. A second scene, challenge and working program per exercise (e.g. a conveyor motor for the seal-in exercise's motor station), and each item's `example:` points to it | The preview's examples are still the exercises' own answers; the review guide says so |
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
- `docs/PLAN.md` — decisions D1–D20 and how the engine and grader work
- `docs/CONTENT-FORMAT.md` — how modules, items, Reference pages and widgets are written, and the validator (4.1, 4.7)
- `docs/MANUAL-TESTING.md` — Ron's hand-tests and the hand-test build command
- `docs/LAB-COMPARISON.md` — running the reference programs on the lab's Allen-Bradley PLC (1.12)
- `docs/INSTRUCTOR-REVIEW.md` — the guide for the PLC instructors reviewing the preview (2.11)
- `tests/golden/` — the reference programs R01–R11 and their expected traces (1.11)
- `docs/images/architecture.png` — the architecture diagram from the spec
- `content/plcplus-foundations/` — the learning path (modules, items, Reference pages) and the machines
- `..\USABILITY-CHECKLIST.md` — the usability rules every Store program meets
- `..\GamifyPlus\CLAUDE.md` — the signed result line and the Verify page that D6 and question 3 follow
