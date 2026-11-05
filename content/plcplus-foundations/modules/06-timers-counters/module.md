---
id: plc.m6
number: 6
title: Timers and counters
status: draft
minutes: 300
prerequisites: [plc.m5]
outcomes:
  - plc.m6.o1: Use on-delay, off-delay and pulse timers (TON, TOF, TP) to time events.
  - plc.m6.o2: Use a retentive timer (TONR; RTO on Allen-Bradley) to add up time over several runs, and explain when a timer must keep its time through a stop.
  - plc.m6.o3: Count events with up, down and up/down counters (CTU, CTD, CTUD).
  - plc.m6.o4: Cascade timers and counters to time or count beyond one instruction.
topics:
  - On-delay timers (TON)
  - Off-delay and pulse timers (TOF, TP)
  - Retentive timing (TONR, RTO)
  - Counters (CTU, CTD, CTUD)
  - Cascading timers and counters
---

Module 5's programs react at once: press a button, and the coil turns on in the same scan. Real machines also need to **wait** and to **count**. A conveyor sounds a warning before it moves; a fan runs on after a heater turns off; a pump needs a service after so many hours; a packer stops after so many bottles. This module gives you the instructions that do it: **timers**, which measure how long something has been on or off, and **counters**, which count how many times something has happened.

Each is a **box** on a rung, with a few values to fill in, and members you can use on contacts, such as T1.Q (the timer is done) and C1.CV (the count so far). You will meet the Allen-Bradley names beside the IEC ones, so the lab's PLC looks familiar.

Each item has two machines, as in module 5: the **Example** tab runs a working program on a machine like the exercise's, with a common mistake beside it, and the **Exercise** tab is yours to program and check. Many tests here check *when* something happens, to a few hundredths of a second, so watch the timers' times count on the rungs as the machines run.
