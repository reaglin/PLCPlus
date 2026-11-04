# Comparing the engine with the lab's Allen-Bradley PLC (plan 1.12)

The spec's Phase 1 gate (section 4.5) is met when the engine **matches a real PLC on a reference set of programs**. The lab
has an **Allen-Bradley** PLC (Ron, 2026-11-04). This guide is for whoever runs that comparison.

The reference programs are `tests/golden/R01.json` … `R11.json`. Each file holds:
- the program, in PLC+'s generic IEC 61131-3;
- the inputs and when they change;
- the tags to watch.

Beside each is `R0x.trace.txt`, what PLC+ does, one line for every scan in which something changed. Those traces were checked
by hand on 2026-11-04 and the tests hold the engine to them.

PLC+ Phase 1 is **generic IEC**, not Logix. Most instructions map one to one; where Logix differs, the program below is
written so that it **should** behave the same, and the difference is noted. A difference found in the lab becomes a plan item:
either an engine fix or a lesson note ("on an Allen-Bradley PLC this works differently").

---

## How to run a comparison

1. **Make a periodic task of 10 ms** in Studio 5000 and put the program in it. PLC+ scans every 10 ms on a simulated clock,
   so a 10 ms periodic task is the closest match. A continuous task scans as fast as it can and timings will differ by a scan
   or two.
2. **Use internal BOOL tags for the inputs first**, toggled from Studio 5000 online. Real inputs are updated at the module's
   RPI, not at the start of each scan, which adds a few milliseconds of jitter. When the internal-tag run matches, a second run
   with real push buttons is a good check.
3. **Record with a Trend** (sample period 10 ms) of every tag the case watches.
4. **Compare with the case's `.trace.txt`.** These must match exactly:
   - the order of events;
   - the counts and values;
   - which rung faults.

   Timings must match **within one scan (±10 ms)**, plus the trend's sampling. A person toggling an input will not hit the
   exact times in the case file. Compare durations, measured from when the input actually changed.
5. Write the result in the table at the end: ✅ same, or ❌ with what differed.

The text after each program is Logix **ASCII rung text**: in Studio 5000, double-click a rung's number to type or paste it.
Check each rung as it is entered; the editor shows it as ladder.

---

## Instruction map

| PLC+ (IEC) | Logix ladder | Note |
|---|---|---|
| Normally open contact `-\| \|-` | `XIC` | same |
| Normally closed contact `-\|/\|-` | `XIO` | same |
| Coil `-( )-` | `OTE` | same |
| Set / Reset coils `(S)` `(R)` | `OTL` / `OTU` | same |
| Negated coil `(/)` | — | none in Logix: `XIO` before an `OTE` |
| Rising / falling edge contacts `\|P\|` `\|N\|` | `OSR` / `OSF` (or `ONS` for rising) | Logix needs a storage bit per one-shot. Use `OSF`, not `XIO`+`ONS`, for falling edges: an `ONS` after `XIO` fires once at power-up |
| `TON` (IN, PT → Q, ET) | `TON` (timer tag: `.PRE`, `.ACC`, `.DN`, `.TT`, `.EN`) | Q = `.DN`, ET = `.ACC`, PT = `.PRE` in ms. In Logix the timer's done bit is read on the **next** rung, not passed along the same rung |
| `TOF` | `TOF` | Q = `.DN` |
| `TP` | — | none in Logix ladder; made from `ONS` + `OTL` + `TON` + `OTU` (R04) |
| Retentive timing | `RTO` + `RES` | IEC has no retentive timer in the standard set; it comes with Unit 6 |
| `CTU` (CU, R, PV → Q, CV) | `CTU` + `RES` (counter tag: `.PRE`, `.ACC`, `.DN`) | Q = `.DN` (ACC ≥ PRE), CV = `.ACC` |
| `CTD` (CD, LD, PV → Q, CV) | `CTD` + `MOV` | **Different:** Logix's `CTD` `.DN` is on while ACC ≥ PRE; IEC's Q is on while CV ≤ 0. No LD: `MOV` the preset into `.ACC` (R06) |
| `CTUD` | `CTU` and `CTD` on the **same** counter tag | QU = `.DN` |
| EQ NE GT GE LT LE | `EQU` `NEQ` `GRT` `GEQ` `LES` `LEQ` | same |
| ADD SUB MUL DIV MOD | `ADD` `SUB` `MUL` `DIV` `MOD` | **Divide by zero:** PLC+ stops with a fault. Logix records a minor fault (arithmetic overflow) and keeps running. **Check in the lab** what the destination holds |
| MOVE | `MOV` | same |
| AND OR XOR NOT | `AND` `OR` `XOR` `NOT` | bitwise, same |
| `ARRAY[0..3] OF INT` | `INT[4]` | Logix arrays always start at 0. Logix prefers DINT; INT is fine for these tests |
| Index out of range | Major fault | Expected: type 4, code 20 (array subscript too large). **Check in the lab** |

---

## The reference programs

### R01 Motor start/stop with seal-in
Tags: `Start`, `Stop`, `Motor` (BOOL). `Stop` is TRUE until pressed (a normally closed button), so start the run with `Stop` = 1.
```
[XIC(Start) ,XIC(Motor) ]XIC(Stop)OTE(Motor);
```

### R02 On-delay timer
Tags: `Switch`, `Lamp` (BOOL), `T1` (TIMER).
```
XIC(Switch)TON(T1,500,0);
XIC(T1.DN)OTE(Lamp);
```
Expect: a 0.3 s press does nothing; held, `Lamp` comes on 0.5 s after `Switch`.

### R03 Off-delay timer
Tags: `Switch`, `Lamp`, `T1` (TIMER).
```
XIC(Switch)TOF(T1,500,0);
XIC(T1.DN)OTE(Lamp);
```
Expect: `Lamp` goes off 0.5 s after `Switch` goes off. Turning `Switch` on again inside the 0.5 s restarts it.

### R04 Pulse timer
Tags: `Button`, `Lamp`, `Pulse`, `B_os` (BOOL), `T1` (TIMER).
```
XIC(Button)ONS(B_os)OTL(Pulse);
XIC(Pulse)TON(T1,500,0);
XIC(T1.DN)OTU(Pulse);
XIC(Pulse)OTE(Lamp);
```
Expect: a 0.5 s pulse on each press. A press during the pulse is ignored. Holding the button gives one pulse.

### R05 Up counter
Tags: `Count`, `Reset`, `Lamp` (BOOL), `C1` (COUNTER).
```
XIC(Count)CTU(C1,3,0);
XIC(C1.DN)OTE(Lamp);
XIC(Reset)RES(C1);
```
Expect: `C1.ACC` 1, 2, 3 (`Lamp` on), 4. Reset clears it and `Lamp` goes off.

### R06 Down counter with load
Tags: `Load`, `Count`, `Lamp` (BOOL), `C1` (COUNTER).
```
XIC(Count)CTD(C1,2,0);
XIC(Load)MOV(2,C1.ACC);
LEQ(C1.ACC,0)OTE(Lamp);
```
Expect: `Lamp` on at power-up (count 0). Load sets 2 and `Lamp` goes off. Presses count 1, 0 (`Lamp` on), −1. The `LEQ` gives
IEC's Q; Logix's own `C1.DN` would mean something else (see the map).

### R07 Up/down counter
Tags: `Up`, `Down`, `Lamp` (BOOL), `C1` (COUNTER).
```
XIC(Up)CTU(C1,2,0);
XIC(Down)CTD(C1,2,0);
XIC(C1.DN)OTE(Lamp);
```
Expect: 1, 2 (`Lamp` on), 1 (`Lamp` off). Both pressed together: no change.

### R08 Latch, unlatch and edges
Tags: `SetBtn`, `ResetBtn`, `Button`, `Lamp`, `Rise`, `Fall`, `Rise_sb`, `Fall_sb` (BOOL).
```
XIC(SetBtn)OTL(Lamp);
XIC(ResetBtn)OTU(Lamp);
XIC(Button)OSR(Rise_sb,Rise);
XIC(Button)OSF(Fall_sb,Fall);
```
Expect: `Lamp` stays on after `SetBtn` is released, until `ResetBtn`. `Rise` and `Fall` are each on for one scan. A trend at
10 ms should catch them; if it does not, count them with a `CTU` on each.

### R09 Compare and math
Tags: `AddBtn`, `ClearBtn`, `Full`, `Add_os`, `Clr_os` (BOOL), `Total` (INT).
```
XIC(AddBtn)ONS(Add_os)ADD(Total,5,Total);
GEQ(Total,15)OTE(Full);
XIC(ClearBtn)ONS(Clr_os)MOV(0,Total);
```
Expect: 5, 10, 15 (`Full` on), cleared to 0. `Full` goes off **one scan after** Clear, because the compare rung runs before
the clear rung.

### R10 Array with an index, and the fault
Tags: `Step`, `Lamp`, `Step_os` (BOOL), `Index` (INT), `Values` (INT[4]).
```
XIC(Step)ONS(Step_os)ADD(Index,1,Index);
MUL(Index,10,Values[Index]);
GEQ(Values[2],20)OTE(Lamp);
```
Expect: `Values[1]` = 10, `Values[2]` = 20 (`Lamp` on), `Values[3]` = 30. **The 4th press faults the controller on rung 2**
(index 4 in a 4-element array). Clear the fault before the next test.

### R11 Timer cascade (flasher)
Tags: `Run`, `Green`, `Yellow` (BOOL), `T1`, `T2` (TIMER).
```
XIC(Run)XIO(T2.DN)TON(T1,1000,0);
XIC(T1.DN)TON(T2,500,0);
XIC(Run)XIO(T1.DN)OTE(Green);
XIC(T1.DN)OTE(Yellow);
```
Expect: green 1 s, yellow 0.5 s, repeating. Each cycle is **one or two scans longer than 1.5 s**, because each timer is reset
from the other's done bit a scan later. In PLC+: green at 0.10 s, yellow at 1.10 s, green at 1.61 s, yellow at 2.62 s.

---

## Results

| Case | Date | Who | Same? | What differed |
|---|---|---|---|---|
| R01 | | | | |
| R02 | | | | |
| R03 | | | | |
| R04 | | | | |
| R05 | | | | |
| R06 | | | | |
| R07 | | | | |
| R08 | | | | |
| R09 | | | | |
| R10 | | | | Fault type and code: |
| R11 | | | | |
| Divide by zero (put `DIV(10,Zero,Out)` on a rung, `Zero` = 0) | | | | What `Out` holds, and the fault recorded: |
