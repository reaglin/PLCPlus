---
id: plc.m6.counters
title: Counters: parts in a batch
minutes: 50
objectives: [plc.m6.o3]
builds-on: plc.m6.retentive
example: u6.car-park
exercise: u6.batch-counter
reference: [counters, edges]
---

# Counters: parts in a batch

A **counter** is a box that counts how many times its input has turned on: parts past a photo eye, cars through a gate, strokes of a press. It counts **edges**, not scans: a part that sits in front of the eye for 2 s is still one count, because the counter adds one only at the moment its input goes from FALSE to TRUE (as a `-|P|-` contact does, module 5).

## The up counter, CTU

| Value | What it is |
|---|---|
| **CU** | the power into the box: each time it turns on, the count goes up by 1 |
| **PV** | the *preset value*: the count at which Q turns on, such as 5 |
| **R** | a tag that sets the count back to 0 while it is TRUE |
| **CV** | the *current value*: the count so far, a whole number (INT) |
| **Q** | TRUE while CV is at least PV |

Two things surprise beginners:

- **A counter does not stop at PV.** It keeps counting: 6, 7, 8 … and Q stays on. Something must stop the parts, or reset the count.
- **A counter does not reset itself, and a stop does not reset it.** Its count stays until R clears it. That is what you want for a batch that is stopped part-way: the count carries on when it starts again.

## The down counter, CTD

A **CTD** counts down. Its **LD** (load) input puts PV into CV; each edge on its input takes 1 off; Q is TRUE when CV is 0 or less. Use it when you think "how many are left": load 12 when a new box arrives, count down as bottles go in, and Q says the box is full.

## The up/down counter, CTUD

A **CTUD** counts both ways: the rung's power counts **up**, and a tag in its **CD** input counts **down**. It has R (to 0) and LD (to PV), and two outputs: **QU**, TRUE while CV is at least PV, and **QD**, TRUE while CV is 0 or less.

**Worked example: a car park with 5 spaces.** Entry sensor CarIn on the rung, exit sensor CarOut in CD, PV 5, a Count reset button in R. Full = C1.QU; Spaces = NOT C1.QU. Five cars in: CV 5, Full. One out: CV 4, Spaces again.

## Showing the count

To show a count on a numeric display, copy it there with a **MOVE** box: IN C1.CV, OUT the display's tag (such as PartCount). A MOVE on a rung with no contacts copies every scan. Module 7 has more on numbers; this is all you need for now.

## Allen-Bradley

The lab's PLC has **CTU** and **CTD** with a counter tag; its members are **.PRE** (PV), **.ACC** (CV), **.DN** (done), **.CU**/**.CD** (the rung is on), and **.OV**/**.UN** (it went over or under its range). The counter is cleared by a **RES** instruction on another rung, as an RTO is. There is no CTUD box: you put a CTU and a CTD on two rungs **naming the same counter tag**, and both change the same .ACC. Note one difference: Allen-Bradley's .DN is TRUE while .ACC is at least .PRE for **both** CTU and CTD, unlike the IEC CTD's Q, which is TRUE at zero.

## How to do it

1. Open the **Example** tab: the car park. Press **Car in** five times and watch CV count on the rung and on the **Cars inside** display; Full lights at 5. Press **Car out**. Then choose *A common mistake* in **Program**, above the machine: nothing is wired to CD, so a car leaving changes nothing.
2. Open the **Exercise** tab: the batch counter. Start the belt and watch the parts pass the photo eye. Write the belt's seal-in (the belt must stop by itself when the batch is done: what does that add to the rung?), then **Box ▾ ▸ Counters ▸ CTU** with a contact of PartEye before it.
3. Add a rung with **Box ▾ ▸ Move and bit logic ▸ MOVE** to show the count on PartCount, and a rung for Batch done.
4. Press **Check my program**. One of the tests is hidden: it checks a case the brief describes but the visible tests do not.
