# PLC+ — hand-tests

**The build to use:** `manual-test\PLCPlus.exe`, rebuilt with this command in the repository folder:

```powershell
dotnet publish src/Learning.Shell/Learning.Shell.csproj -c Release -r win-x64 `
    --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o manual-test
```

The `content\` folder is published beside the exe; keep them together.

Record each step as ✅ / ❌ with a note. A ❌ or a note goes back into `docs/DEVELOPMENT-PLAN.md`.

---

## Hand-test 9 — the lessons of modules 1 to 5, and the example machines (plan 9.1–9.7), 2026-11-05

Use a new learner (so nothing is complete yet). Read as a student would: note anything unclear, wrong, or that you would teach
differently. The instructors will review the same pages on the review site.

| # | Do this | You should see |
|---|---|---|
| 9.1 | Sign in. Look at the path | Modules 1 to 5 say **first draft**; "0 of 27 items". The status bar says to start with 1.1 |
| 9.2 | Open **1.1 Voltage, current and resistance**. Read the Learn tab, then the **Exercise** tab | Practice questions with **Check my answers**. A wrong answer shows the right one and why (practice). Passing them marks 1.1 ✓ |
| 9.3 | Read **1.4 Working safely: lockout/tagout** | The steps in order, and a plain statement that the page does not replace the lab's safety training. Is anything wrong for your lab? |
| 9.4 | **2.1 Binary and hexadecimal**, Exercise tab. Type `101101` for "Write 45 in binary", `0010 1101` too, then `2D` for a hex question, `0x2d`, and `2G` | Each right form is accepted (✓). `2G` says it is not a hexadecimal number and which digits to use. `2#1100_1010` in a question shows in a fixed-width font, without backquotes |
| 9.5 | **2.2 BCD**: type a BCD answer with and without spaces | Both accepted |
| 9.6 | **4.4 The scan cycle**, Example tab (a pump): **Pause**, tick **keep pressed** under Start, **Step one scan** | In one scan Pump turns on in rung 1 and rung 2 already lights PumpLight, as the page says |
| 9.7 | **4.6 Module check**: the two find-the-fault questions (an overheat alarm, a press guard) | Work each machine: the alarm never comes on when too hot; the press closes with the guard open. The right answers are the broken temperature-switch wire and the shorted guard switch |
| 9.8 | **5.1** to **5.5**: open each **Example** tab | Each runs a machine **like** the exercise's, not the same one: a two-hand press guard, a pump, a reversing conveyor motor, an overheat alarm, push on/push off. Choose *A common mistake* on each and see it go wrong |
| 9.9 | **5.3** Example (the reversing conveyor): *A common mistake*, press Forward, then Reverse | The motor says it was told to run **both ways at once**; with *Works*, Reverse does nothing until Stop |
| 9.10 | Pass a module's check (module 1 is quickest), then **Hand in a report** | The module's check is listed ready to hand in; its report shows the practice questions' items and the check |

---

## Hand-test 8 — reports: make one, hand it in, check it (plan 8.1–8.8), 2026-11-05

Use a learner with module 3 finished (hand-test 7.4 makes one), or finish it first.

| # | Do this | You should see |
|---|---|---|
| 8.1 | Sign in. Look at the path | A second row under My progress: **✉ Hand in a report**, "1 module check ready to hand in". My progress, Module checks: module 3 "● Complete: ready to hand in" |
| 8.2 | Choose **Hand in a report** (or Ctrl+Shift+R) | 1. the module checks and **Everything so far**, module 3 chosen; 2. the card: "✓ Complete: ready to hand in", your name, overall score, items, outcomes, time, "On the cover: your name, no class code"; the help on the right says the three steps |
| 8.3 | Press **Save the PDF report…** | First **My details for reports**: every box optional. Type a student ID, a course and the class code `7K3M-9QX2-4F2A-BD30` (try it in lower case, with spaces: it is accepted). Then **Continue** (Cancel says no report was made). A Save window follows, with the name `PLCPlus_<YourLastName>_Module3Check_<today>.pdf`: save it on the desktop |
| 8.4 | Look at section **3. Hand it in** | The report's ID and where it was saved; why not to save the PDF again; what the result line is, and the line starting `PLC1|`; **Copy the result line**, **Send to my instructor…**, **Show the file in its folder**. The report is listed under **Reports you have made**; the tree row now says ✓ report made |
| 8.5 | Open the PDF | Page 1: "PLC+ progress report", the green **Complete** box, your details, the report ID. Then the summary, the exercise detail with each item's tests, the outcomes, and **Verification** with a QR code. In a reader with a "Read aloud" or tag view, the headings and tables are read in order |
| 8.6 | Choose **Module 5 check** (not finished), press **Save the PDF report (marked Incomplete)…** | PLC+ asks first: **Make it, marked Incomplete** / Not now. Make it: the PDF says **Incomplete** in an amber box and lists what is missing. Any exercise you passed is shown as a picture of your program |
| 8.7 | Press **Send to my instructor…** | Your e-mail program opens with the result line in the message, and the folder with the PDF opens beside it, to drag it in. Nothing is sent until you press Send in your e-mail program (close it without sending) |
| 8.8 | **File ▸ Check a report (for instructors)…** Choose the module 3 PDF, paste its result line, type the class code, press **Check** | "✓ THE REPORT IS GENUINE … signed with your class code", what the report says, and "✓ THE RESULT LINE IS GENUINE" |
| 8.9 | Change one thing and check again: a digit of the score in the line; then open the PDF in Edge, add a comment or highlight, save it, and check that file | "✗ THE RESULT LINE DOES NOT CHECK OUT"; for the edited PDF "✗ THE REPORT WAS CHANGED", with what it said when it was made |
| 8.10 | **Switch learner**, then on the start screen press **Check a report (for instructors)…**, then **Make a new class code** | It works without signing in; a new code appears under the button, copied, with how to give it to a class; the class code box is not changed |

---

## Hand-test 7 — My progress, time, text size and Help (plan 7.1–7.7), 2026-11-05

| # | Do this | You should see |
|---|---|---|
| 7.1 | Sign in | Above the path: "N of 11 items complete · N of 2 modules" and "Time in PLC+: …"; the legend ★ ✓ ↺ ◐ ○ –; **My progress** as the first row of the path |
| 7.2 | Choose **My progress** (or Go ▸ My progress, Ctrl+Shift+Home) | So far (items, modules, outcomes, time); Review suggested; the two module checks with where you are; the path with each module's state and time; the outcomes. **Continue with …** opens the first item not done |
| 7.3 | Work for a few minutes on an item, then look again | The time goes up (only while you use the keyboard or mouse); the module's time on My progress too |
| 7.4 | Finish module 3 with one question of 3.5 wrong (5 of 6 passes) | Module 3 is ↺ in the tree ("review suggested"); My progress lists the outcome under Review suggested, with the question and what to read; 3.5 under Module checks is ● "Ready". Answer it right again: ★ mastered |
| 7.5 | **View ▸ Larger text** twice (or Ctrl++) | Everything grows; the status line says the size. Switch learner and sign in again: still larger. **Normal size** (Ctrl+0) puts it back. Another learner keeps their own size |
| 7.6 | **Help ▸ Help on the web** | softwareplus.ai/plcplus/help/ in your browser |
| 7.7 | **Help ▸ Check for a new version…** | "PLC+ is not on the Microsoft Store yet …" |
| 7.8 | On a module's first page, **Go ▸ Next item** (Ctrl+Shift+PgDn); on an item, **◂ Previous** | Next opens the module's first item; Previous goes to the item before (greyed on 1.1) |

---

## Hand-test 6 — learners and progress files (plan 6.1–6.10), 2026-11-05

PLC+ now opens on **Who is learning?** Each learner has a progress file in `C:\ProgramData\PLCPlus\Learners`, locked with
their PIN. The preview's progress (from hand-tests 4 and 5) is offered to the first learner you make. Have a USB stick or
your OneDrive folder ready for the backup steps.

| # | Do this | You should see |
|---|---|---|
| 6.1 | Open `manual-test\PLCPlus.exe` | **Who is learning?**, no learners yet, **New learner…** and **Add learner from file…**; at the bottom, where the files are kept (`C:\ProgramData\PLCPlus\Learners`, shared by everyone on this computer). The help on the right explains the PIN, backups and what the PIN protects |
| 6.2 | **New learner…**: your name, two different PINs, **Create** | "The two PINs are not the same …" in red. Type the same 4–6 digits twice. A box **Start with the progress from the PLC+ preview** says how many items; it is not ticked, so tick it (it is yours) |
| 6.3 | **Create** | **Your recovery code**: five groups of four. **Start learning** is greyed until you tick "I have written down or printed …". Try **Print…** and **Copy**. Close the window with ✕ before ticking: it asks whether you have kept the code |
| 6.4 | **Start learning** | The path with your preview progress (ticks as before; My program on 5.2 as you left it). Your name at the top right, with **⚠ Not backed up: Back up…**; the window title has your name |
| 6.5 | **Learner ▸ Switch learner** | "Before you go: back up your progress first?" Press **Not now**: the start screen, with "… saved and locked. Next learner: choose your name …", **nobody chosen** in the list, and your row saying "⚠ Not backed up yet" |
| 6.6 | **New learner…** "Test Student", PIN 1111 | No preview box this time. An empty path: nothing ticked, the module checks' questions blank, My program on 5.2 a fresh start (none of your work shows) |
| 6.7 | Switch learner; choose yourself; type a wrong PIN five times | "That PIN is not right. Try again (4 tries before a wait) …", then a wait of 30 seconds counting down, with **Open** greyed. After it, your right PIN opens your path |
| 6.8 | **Learner ▸ Back up my progress…** and save into `C:\ProgramData\PLCPlus\Learners` | Refused: "That is the folder PLC+ keeps everyone's progress in, so it is not a backup". Save to the USB stick or OneDrive instead: the top right says **✓ Backed up today at …** |
| 6.9 | Mark a reading read, then **Learner ▸ Switch learner** | The backup question again (there is work since); the row says "⚠ Backup out of date …" if you choose **Not now** |
| 6.10 | Choose **Test Student**, **Forgot my PIN…**: a wrong code, then the code from 6.6 (any case, with or without dashes) and a new PIN | The wrong code is refused with what to check; the right one opens Test Student's path. The new PIN works next time; 1111 does not |
| 6.11 | Signed in as yourself, **Learner ▸ Change my PIN…** | Needs the PIN you use now; the new one works from then on |
| 6.12 | Start screen: **Add learner from file…** and choose your backup from 6.8 | "Combine your two copies?" with when each was saved; your PIN; your path opens with everything from both copies (nothing doubled), and the status line says they were combined. Then **Back up** onto that same file: the status says the copy already there was combined in first |
| 6.13 | If you can: another computer, or a second Windows account on this one. **Add learner from file…** with your backup | "Add {you}?", saying which computer saved it and when; your PIN opens it there. (A second Windows account shares the same learners' folder, so you may find yourself listed already) |
| 6.14 | **Settings…** on the start screen: lock after **5 minutes**. Sign in, open an item, and leave PLC+ alone for 5 minutes | The start screen: "PLC+ locked itself after 5 minutes without use …", you chosen. Your PIN brings you back to the same item and tab |
| 6.15 | Finish a module check (3.5 or 5.6) | After "That completes module …", "Module N is complete: back up your progress?" |
| 6.16 | Close PLC+ with work since your last backup; press **Cancel** | PLC+ stays open. Close again and press **Not now**: it closes |
| 6.17 | Start screen: choose **Test Student**, **Remove from this computer…** | Needs Test Student's PIN; then "Back up … first?" with **Back up, then remove…**, **Remove without a backup** and **Cancel**. Remove: Test Student is gone from the list and the file is in the Recycle Bin |
| 6.18 | **Open the learners' folder**; then **Settings…**, choose another folder, **Save** | File Explorer opens on the folder. Settings warns how many learners stay in the old folder; after saving, the empty list says where PLC+ is looking and how to go back (**Use the usual folder**) |

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

## Hand-test 5 — assessment (plan 5.2–5.6), 2026-11-04

| # | Do this | You should see |
|---|---|---|
| 5.1 | **3.5 Module check**, tab **Exercise** | What it covers, then six questions: a choice, a match (a list beside each symbol), the seal-in truth table (TRUE/FALSE per row), a choice, a "choose every one", and a number in mA |
| 5.2 | Press **Check my answers** with nothing answered | Every question "✗ Not answered yet." and "Not passed yet: 0 of 6 right". The tree shows 3.5 with "best 0 of 6" |
| 5.3 | Answer all six, one of them wrong; check | The wrong one says "Not quite. Look again…" and does **not** give the answer; the right ones say why. Fix it and check: "Passed" |
| 5.4 | Mark 3.1–3.4 read (if not yet) | When the four are read and the questions passed: module 3 ✓ in the tree, "That completes module 3" |
| 5.5 | Open **module 3's first page** | "What you will be able to do": each outcome marked ✓ mastered (or ↺ review suggested, with the reason, if a question on it was answered wrong last time) |
| 5.6 | **5.6 Module check**, Exercise; scroll to question 3 | A motor station with a fault. Press Start: nothing happens, and the Stop contact shows FALSE though nobody presses Stop. Choose "The wire to Stop is broken". Question 4: the garage door will not open |
| 5.7 | **5.2**, Example, Program **A common mistake**, **Check this program**, then **Replay the failed test** | The clock line says "REPLAYING the test …"; the test presses Start itself; the motor runs only while Start is held; it stops at about 1.5 s and pauses, and the status line gives the reason. **Reset** goes back to working it yourself |
| 5.8 | **6.1** (traffic light), Example, **Check this program** | 3 of 3. The first test now also checks yellow at 5 s and red at 7 s, each within 0.05 s, and yellow before red |
| 5.9 | **2.3 AND, OR and NOT**, Exercise: get a row wrong and check | Practice questions show the right answer when yours is wrong |

---

## Hand-test 4 — modules and items (plan 4.1–4.12), 2026-11-04

The window has changed: the learning path is on the left, and the machines are now items in modules 5 and 6. Hand-tests 2 and 3
still apply, inside an item's **Example** and **Exercise** tabs (5.1 pilot lights, 5.2 motor, 5.3 garage door, 5.4 tank, 5.5
sorter, 6.1 traffic light). This test starts from no progress: if you have run this build before, delete
`%LocalAppData%\PLCPlus\progress\preview.json` first.

| # | Do this | You should see |
|---|---|---|
| 4.1 | Open `manual-test\PLCPlus.exe` | The learning path on the left: 12 modules, 1–6 with items, 7–12 "Coming later"; "0 of 32 items complete". Module 3's page on the right (the first written module): Recommended first, What you will be able to do, Topics, the items, and **Start with 3.1 …**. The status line says modules 3 and 5 are written |
| 4.2 | Choose **1.1**, then **6. Timers and counters** | 1.1: "This page is still to be written", and **I have read this** is greyed (its tooltip says why). Module 6's page says module 5 is recommended first (orange), that you can still start, and that PLC+ notes it |
| 4.3 | Choose **5. Basic ladder programming**, then **Start with 5.1 …** | 5.1 opens on its **Learn** tab; the header says about 40 minutes |
| 4.4 | Open **Example** (Ctrl+2) | The pilot station runs the working program; Program has *Works* and *A common mistake*; Check this program passes. The tree does not change (examples do not count) |
| 4.5 | Open **Exercise** (Ctrl+3). **Click** the contact and type **PB1**, Enter (the Tag box takes the typing; nothing is added to the rung). Build the three rungs and press **Check my program** (top right or under the machine) | When it passes: the status line says 5.1 is complete, "Module 5: 1 of 6 complete", and "Next: 5.2 … (Next ▸, top right)"; the tree shows ✓ 5.1. **Start again ▾** offers only an empty program and the mistake, not the working program |
| 4.6 | Open **Reference** (Ctrl+4) | Contacts, coils, and AND/OR as truth tables. Symbols such as -\| \|- appear whole in the tables |
| 4.7 | Choose **3.3 The seal-in circuit** | A drawn ladder diagram (not running) and a truth table in the lesson; **I have read this** at the bottom. Press it: ✓ in the tree, and module 3's count goes up |
| 4.8 | Choose **3.5 Module check**, tab **Exercise** | The four items it covers and where you are with each |
| 4.9 | **Next ▸** and **◂ Previous**; Go ▸ **Continue where I left off** | They move through the path in order; Continue opens the first item not complete |
| 4.10 | On 5.2, **File ▸ Save My program as…** (from any tab) and **Save the program as a picture…** | The file is named "5.2 The seal-in… — My program"; the picture is saved. On a reading such as 3.1 these File items are greyed, and their tooltips say where they work |
| 4.11 | Close PLC+ and open it again | It opens on the item you were on (or the one after it, if you finished it); the marks and counts are as you left them |
| 4.13 | F1 on each tab | The help panel's first part says what this screen is for (module, Learn, Example, Exercise, Reference) |
| 4.12 | A short window (or a laptop screen): 5.2's Exercise tab | The tab scrolls so the rungs keep their room; Problems and Check my program are below |

---

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
