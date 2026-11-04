# PLC+ — hand-tests

**The build to use:** `manual-test\PLCPlus.exe`, rebuilt with this command in the repository folder:

```powershell
dotnet publish src/Learning.Shell/Learning.Shell.csproj -c Release -r win-x64 `
    --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o manual-test
```

The `content\` folder is published beside the exe; keep them together.

Record each step as ✅ / ❌ with a note. A ❌ or a note goes back into `docs/DEVELOPMENT-PLAN.md`.

---

## Hand-test 2 — the preview for review (plan 2.1–2.11), 2026-11-04

This replaces hand-test 0: the one-machine window grew into the preview the instructors will review. It has six machines,
each with a challenge and two programs. Do this before sending the preview out; `docs/INSTRUCTOR-REVIEW.md` is what the
instructors read.

| # | Do this | You should see |
|---|---|---|
| 2.1 | Open `manual-test\PLCPlus.exe` | "PLC+ — preview for review": Challenge "Light the pilot lights", Program "Works", the task in one sentence, three rungs on the left, two buttons, a Mode switch and three lights on the right, the help panel (F1 hides and shows it) |
| 2.2 | Hold **Button 1**; then hold both; turn **Mode** to Auto | Green on with Button 1; Amber only with both; Red goes off in Auto. Each light says ON or off beside it, and the rungs turn green and TRUE as power passes |
| 2.3 | Open **What every part is doing** and **The wiring** | Every part's state in words; every tag with what it means (PB1 input, normally open, …) |
| 2.4 | Choose **Run a traffic light**, turn **Run** on, watch for 15 s | Green 5 s, yellow 2 s, red 5 s, then green again; never two at once. The TON boxes show ET counting up and Q |
| 2.5 | **Pause**, then **Step one scan** across the change from red to green | Red goes off and green comes on in the same scan; T1, T2 and T3 reset one scan after T3 finishes |
| 2.6 | Choose **Open and close a garage door safely**. Tick **keep pressed** under Open, untick it; when the door is open, do the same with Close, and while it comes down choose **Step into the doorway** | The door rises to the top and stops (Top limit ON); it comes down; with someone in the doorway it stops and does not come down on them. The door's words say where it is and which way it is going |
| 2.7 | Program **A common mistake**, then **Check this program** | 4 of 5 points. "Something in the doorway" failed: "the door must stop coming down. At 6.1 s it was on." A hint for that test (put EyeClear on the down rung), and what happened, moment by moment, ending in ✗ |
| 2.8 | Choose **Fill and drain a tank**, turn **Mode** to Auto, watch 20 s | Fill valve OPEN until the high float, then the drain valve until below the low float, then filling again. The level shows inside the tank; the floats say liquid or dry |
| 2.9 | Choose **Sort metal from plastic**; Tab to the Start button and hold the **Space bar** a moment | The belt runs (it keeps running: seal-in). M (metal) parts are pushed into Bin A at the photo-eye; P (plastic) parts ride on to Bin B. The counts show under each bin |
| 2.10 | For each of the six challenges: **Works** → Check, then **A common mistake** → Check | Works passes every test; the mistake fails at least one test you can see, with a message and a hint that make sense to a student |
| 2.11 | **Reset** after checking | The machine and clock start again; the test results stay |
| 2.11a | Keyboard only: from the Challenge list, press Tab | The order is Program, the machine's Pause/Reset, the machine's buttons and switches, then the two lists under it, then Check this program; the help panel comes last |
| 2.12 | Anything about the look, the words, the order of the challenges | Notes go back into the plan before the preview goes to the instructors |

## Hand-test 3 — the ladder editor (plan 3.1–3.6), 2026-11-04

| # | Do this | You should see |
|---|---|---|
| 3.1 | Challenge **Start and stop a motor**, Program **My program (edit it)** | The tools appear above the rungs; one rung with an empty contact and coil, both marked with a red "!"; Problems lists them; the PLC says it is stopped until the problems are fixed; the help panel hides (F1 shows it) |
| 3.2 | The contact is selected: type **Start** in Tag and press Enter | The contact shows Start; the status says it uses Start |
| 3.3 | Press **Branch**; type **Motor** in the new contact's Tag, Enter | A branch around Start with Motor below it (the seal-in) |
| 3.4 | Click the coil, type **Motor**, Enter; with the coil selected press **-\| \|- Contact** and type **Stop** | The contact goes before the coil; no problems left; the PLC runs: hold Start and the motor runs and stays on, Stop stops it |
| 3.5 | **New rung**: type **Motor** for its contact and **RunLight** for its coil (Tab into the rungs and use → to reach the coil) | The Running light follows the motor |
| 3.6 | **Check this program** | 3 of 3 points |
| 3.7 | Keyboard only: Tab to the rungs; ← → move along a rung, ↑ ↓ between rungs; **O** adds a contact, **Q** a coil, **Delete** deletes, **Ctrl+Z** undoes, **Enter** goes to the tag box | Each does what it says; the status line says what happened |
| 3.8 | Select the **Start** contact and change its tag to **Strat** (a typo) | The status: "'Strat' is a new tag (declared as BOOL). Did you mean Start? If so, Undo (Ctrl+Z) and type Start." Problems warns: "Nothing ever turns 'Strat' on … Check the spelling against the machine's wiring." Undo puts Start back |
| 3.9 | **Box ▾ ▸ Timers ▸ TON** | A TON box with T1 and PT T#1s; its PT box in the properties panel; type T#2s, Enter |
| 3.10 | Drag **-( )- Coil** from the tools onto a contact | A coil is added after it |
| 3.11 | Right-click the **Start** contact ▸ **Force Start ON** | "FORCED TRUE" in orange under Start; the motor runs without the button; Remove the force |
| 3.12 | **Start again ▾ ▸ A copy of "A common mistake"**, then fix it | The mistake's program is now yours to fix; Check passes once it is fixed |
| 3.13 | **File ▸ Save the program as a picture…** | A PNG of the rungs with a title |
| 3.14 | Close PLC+ and open it again; choose My program | Your program is as you left it |

---

## Hand-test 1 — the engine's reference programs (plan 1.11, 1.12), 2026-11-04

Nothing to run: this is reading. (The Allen-Bradley comparison waits for the lab instructor.)

| # | Do this | You should see |
|---|---|---|
| 1.1 | Open `tests\golden\R02.trace.txt` and `R02.json` | The trace lists the moments something changed: the switch on at 0.1 s and off at 0.4 s (too short; the lamp stays off), on again at 0.6 s, the lamp on at 1.1 s (0.5 s later). Say whether this way of showing a program's behaviour is clear |
| 1.2 | Open `R11.trace.txt` | The flasher: green 1 s, yellow 0.51 s (one scan more than 0.5 s, because the timers reset each other a scan apart) |
| 1.3 | Read `docs\LAB-COMPARISON.md` | Each program written for Studio 5000, the table of differences, and how to run the comparison. Say who will run it on the Allen-Bradley PLC, and whether anything in the method does not suit the lab |
