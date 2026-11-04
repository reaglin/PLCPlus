# PLC+ — hand-tests

**The build to use:** `manual-test\PLCPlus.exe`, rebuilt with this command in the repository folder:

```powershell
dotnet publish src/Learning.Shell/Learning.Shell.csproj -c Release -r win-x64 `
    --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o manual-test
```

The `content\` folder is published beside the exe; keep them together.

Record each step as ✅ / ❌ with a note. A ❌ or a note goes back into `docs/DEVELOPMENT-PLAN.md`.

---

## Hand-test 0 — the first look (plan 0.4), 2026-11-04

What it shows: the engine, one simulated machine and the grader. There are no lessons, editor or learners yet.

| # | Do this | You should see |
|---|---|---|
| 0.1 | Open `manual-test\PLCPlus.exe` | The title bar says "PLC+ — first look at the engine"; the page heading "Motor start/stop — a first look", with the challenge in one sentence, the program's two rungs on the left, the machine in the middle and the help panel on the right. The clock counts up under "The PLC" |
| 0.2 | Read the help panel | It says what the screen is for and what to do. Press **F1**: the panel hides; F1 again shows it |
| 0.3 | Hold **Start** down with the mouse for a second, then let go | While held: the Start contact and the wires turn green and thick and show TRUE; the Motor coil turns on; the motor spins up ("starting", then "running"); the Running light comes on. After you let go, the motor keeps running, because the Motor contact below Start (the seal-in) now carries the power |
| 0.4 | Press **Stop** | The Stop contact goes grey (FALSE), the motor coasts down and stops, and the light goes off. Stop is wired normally closed, so its input is TRUE until it is pressed |
| 0.5 | Tab to **Start** and hold the **Space bar** | The motor starts, as with the mouse |
| 0.6 | Press **Pause**, then **Step one scan** a few times. Then tick **Keep pressed: Start** and step again | The clock stops and moves on exactly 0.01 s per step; Step is greyed out while running, and its tooltip says why. With Start kept pressed, the next step turns the Motor coil on. Untick it before going on |
| 0.7 | Press **Check this program** | Three lines: ✓ Start runs the motor…, ✓ Stop stops the motor, ✓ Hidden test 1, and "Passed: 3 of 3 points" (point at it to see the program's fingerprint). Press **Reset**: the results stay |
| 0.8 | Choose **Without seal-in (fails a test)** in Program, then **Check this program** | ✗ on the first test: "The motor should keep running after Start is released. At 1 s it was FALSE." and Hint 1 of 3. Check again: Hint 2. Hold Start and let go: the motor stops when you let go |
| 0.9 | **Help ▸ About PLC+…** | "Created by Dr. Ron Eaglin" with the picture, the version, and the links to softwareplus.ai. **The softwareplus.ai/plcplus page does not exist yet** (plan 11.4) |
| 0.8a | (Added with phase 1.) With **Without seal-in** checked, scroll the results (mouse wheel, or Tab to them and use the arrow keys) | After the hint, "What happened in the test …", in plain words: 0.00 s Start pressed, Motor turned on; 0.10 s the test checks: Motor should be on; it was on ✓; 0.20 s Start released, Motor turned off; 1.00 s the test checks: Motor should be on; it was off ✗ |
| 0.10 | Anything about the look, the words or the flow | Notes welcome: the instructors' review comes later, and this window is replaced by the real one |

---

## Hand-test 1 — the engine's reference programs (plan 1.11, 1.12), 2026-11-04

Nothing to run: this is reading.

| # | Do this | You should see |
|---|---|---|
| 1.1 | Open `tests\golden\R02.trace.txt` and `R02.json` | The trace lists the moments something changed: the switch on at 0.1 s and off at 0.4 s (too short; the lamp stays off), on again at 0.6 s, the lamp on at 1.1 s (0.5 s later). Say whether this way of showing a program's behaviour is clear |
| 1.2 | Open `R11.trace.txt` | The flasher: green 1 s, yellow 0.51 s (one scan more than 0.5 s, because the timers reset each other a scan apart) |
| 1.3 | Read `docs\LAB-COMPARISON.md` | Each program written for Studio 5000, the table of differences, and how to run the comparison. Say who will run it on the Allen-Bradley PLC, and whether anything in the method does not suit the lab |
