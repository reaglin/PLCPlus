# PLC Plus — hand-tests

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
| 0.1 | Open `manual-test\PLCPlus.exe` | "Motor start/stop — a first look", with the challenge in one sentence, the program's two rungs on the left, the machine in the middle and the help panel on the right. The clock counts up under "The PLC" |
| 0.2 | Read the help panel | It says what the screen is for and what to do. Press **F1**: the panel hides; F1 again shows it |
| 0.3 | Hold **Start** down with the mouse for a second, then let go | While held: the Start contact and the wires turn green and thick and show TRUE; the Motor coil turns on; the motor spins up ("starting", then "running"); the Running light comes on. After you let go, the motor keeps running, because the Motor contact below Start (the seal-in) now carries the power |
| 0.4 | Press **Stop** | The Stop contact goes grey (FALSE), the motor coasts down and stops, and the light goes off. Stop is wired normally closed, so its input is TRUE until it is pressed |
| 0.5 | Tab to **Start** and hold the **Space bar** | The motor starts, as with the mouse |
| 0.6 | Press **Pause**, then **Step one scan** a few times | The clock stops and moves on exactly 0.01 s per step. Step is greyed out while running, and its tooltip says why |
| 0.7 | Press **Check this program** | Three lines: ✓ Start runs the motor…, ✓ Stop stops the motor, ✓ Hidden test 1. "Passed: 3 of 3 points", and the program's fingerprint |
| 0.8 | Choose **Without seal-in (fails a test)** in Program, then **Check this program** | ✗ on the first test: "The motor should keep running after Start is released. At 1 s it was FALSE." and Hint 1 of 3. Check again: Hint 2. Hold Start and let go: the motor stops when you let go |
| 0.9 | **Help ▸ About PLC Plus…** | "Created by Dr. Ron Eaglin" with the picture, the version, and the links to softwareplus.ai. **The softwareplus.ai/plcplus page does not exist yet** (plan 11.4) |
| 0.10 | Anything about the look, the words or the flow | Notes welcome: the instructors' review comes later, and this window is replaced by the real one |
