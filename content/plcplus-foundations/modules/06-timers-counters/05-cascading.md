---
id: plc.m6.cascading
title: Cascading timers and counters: a case packer
minutes: 50
objectives: [plc.m6.o4]
builds-on: plc.m6.counters
example: u6.flasher
exercise: u6.case-packer
reference: [cascading, counters, timers]
---

# Cascading timers and counters: a case packer

One timer measures one stretch of time, and one counter counts one kind of event. Many jobs need more: a light that flashes on and off for ever, a time longer than one timer can hold, or bottles counted into packs and packs into cases. You get them by **cascading**: one timer's or counter's output drives the next one's input, or resets itself or another.

## A flasher from two timers

A beacon must flash: on 0.5 s, off 0.5 s, over and over. One TON cannot do it: it times once and then sits at "done". Two can, each starting the other:

1. T1 (TON, 0.5 s) on the rung *Alarm AND NOT T2.Q*: it times the **on** part.
2. T2 (TON, 0.5 s) on the rung *T1.Q*: when T1 is done, T2 times the **off** part.
3. The beacon is on while *Alarm AND NOT T1.Q*: on while T1 is timing.

When T2 is done, its normally closed contact breaks T1's rung: T1 resets, so T1.Q goes off, so T2 resets too, and T1 starts again. One round takes 1 s (and a scan or two: PLC+ shows you exactly how many if you step through it).

This is the same trick as the traffic light in 6.1: a chain of timers, the last one restarting the first.

## A timer driving a counter

Count the flashes: a CTU on the rung *Beacon* adds one every time the beacon comes on. With PV 3, its Q can sound a horn from the third flash. The same idea makes long times: a timer that restarts itself every minute, counted by a CTU with PV 60, measures an hour.

## A counter driving a counter

Bottles go into packs of 6, and packs into cases of 4:

| Counter | Counts | PV | Its Q means |
|---|---|---|---|
| C1 | bottles at the eye | 6 | a pack is complete |
| C2 | each time C1.Q comes on | 4 | the case is full |

For C2 to count *every* pack, C1 must start again from 0 after each one. So C1.Q must reset C1: put a tag in C1's R that comes on when C1.Q does. In the scan after C1 reaches 6, R clears it, C1.Q goes off, and C2 has seen exactly one rising edge. C1.Q is TRUE for one scan per pack: a one-shot made by the counter itself.

R takes one tag, so if C1 must also be cleared by a button, make a memory tag from a branch: *PackReset = C1.Q OR CaseRemoved*, and put PackReset in R.

## Watch the order of the rungs

In cascades, which rung comes first matters by one scan (10 ms). A contact of C1.Q on a rung *above* C1's box sees the value from the last scan; on a rung *below* it, the value from this scan. That is rarely a problem, but it is why a one-scan pulse can be missed by a rung that reads it before it is made, and then cleared. If a cascade does not work, step through it one scan at a time.

## Allen-Bradley

The same patterns work with .DN: *XIO T2.DN* on T1's rung for the flasher; *XIC Bottles.DN* as the rung of the pack counter's CTU; and a rung *XIC Bottles.DN, RES Bottles* to clear the bottle counter. On the lab's PLC the .DN of a counter stays on until RES, so the RES rung is needed there too.

## How to do it

1. Open the **Example** tab: the alarm beacon. Turn **Alarm** to On and watch T1 and T2 take turns, and the **Flashes** display count. The horn joins in at the third flash. Press **Pause** and **Step one scan** around the moment T2 finishes to see T1 and T2 reset. Then choose *A common mistake* in **Program**, above the machine: one timer that resets itself. The beacon looks on all the time: it is off for one scan in every 0.5 s.
2. Open the **Exercise** tab: the case packer. Start the belt and watch the bottles pass the eye. You need two CTUs, the belt's seal-in, and a MOVE for each display. Ask yourself: what should stop the belt? And the bottle counter must be cleared for two different reasons: which, and how can one R input take both? The section above shows a way.
3. Press **Check my program**. A full case takes 24 bottles, about 12.5 s of test time.
