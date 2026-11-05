---
id: plc.m4.scan-cycle
title: The scan cycle
minutes: 30
objectives: [plc.m4.o4]
builds-on: plc.m4.sensors
quiz: quizzes/m4-scan-cycle.json
example: u5.pump-start-stop
reference: [scan-cycle]
---


# The scan cycle

A PLC does not run its program once. It runs it over and over, as fast as it can, for as long as it is in **Run** mode. Each pass is called a **scan**, and every pass has the same steps:

1. **Read the inputs.** The processor reads every input point and copies its state into the input's tag.
2. **Run the program.** It works through every rung, top to bottom, left to right, using those copies.
3. **Write the outputs.** It copies every output tag to its output point.
4. **Housekeeping.** It talks to the programming software and the network, and checks itself. Then it starts again.

The time one pass takes is the **scan time**. On a real PLC it is often a few milliseconds, longer for a large program. The processor has a **watchdog** timer: if a scan ever takes too long, the PLC stops with a fault rather than run a machine it is no longer keeping up with.

In PLC+, one scan is **10 ms** of simulated time, so the program runs 100 times every simulated second.

## What follows from the cycle

**The program sees a snapshot.** The inputs are copied once, at the start. If a button changes while the rungs are being worked through, the program does not see it until the next scan. Every rung in one scan sees the same input values.

**Rungs run in order, and the last word counts.** A coil sets its tag as soon as its rung runs, and a rung further down reads the new value in the same scan. But if two rungs both have a coil of the same tag, the later rung overwrites the earlier one, and only the last value is written to the output. This is the **double coil** mistake:

- rung 1: Start → Motor
- rung 5: Jog → Motor

When Start is pressed and Jog is not, rung 1 turns Motor on, then rung 5 turns it off again, in the same scan. The motor never runs from Start. The fix is one coil per output: put Start and Jog in parallel on one rung. (Set and reset coils, in module 5, are the exception: they are meant to be used in pairs.)

**A very short pulse can be missed.** The inputs are read once per scan. If a sensor turns on and off again between two reads, the program never sees it. With a 10 ms scan, a pulse of 3 ms can fall between two reads. Slow scans and fast parts need care: a faster task, or a special input module that catches pulses.

**Outputs change at the end.** Nothing reaches the machine until the scan's outputs are written, so a coil that is turned on and off within one scan never moves anything.

## Not every PLC works exactly this way

The four steps above are the classic cycle, and SLC 500 and MicroLogix PLCs work this way. Allen-Bradley's Logix controllers (CompactLogix, ControlLogix) update their inputs and outputs on a timer of their own (the **RPI**, requested packet interval), not once per scan, so an input can change in the middle of a scan. Programmers who need a steady value copy their inputs at the top of the program. PLC+ uses the classic cycle.

## Try it

Open the **Example** tab: a pump station. Press **Pause**, tick **keep pressed** under Start, and press **Step one scan**. In that one scan, rung 1 turns Pump on, and rung 2, further down, already sees it and lights PumpLight. Untick **keep pressed** (that lets go of Start) and step again: the Pump contact on the branch keeps rung 1 passing power. Press **Run** (the Pause button, while paused) to let it run on. The pump also has an overload relay in series; you will meet those in module 5. Then answer the questions on the **Exercise** tab.
