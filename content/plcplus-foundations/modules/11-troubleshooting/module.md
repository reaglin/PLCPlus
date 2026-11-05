---
id: plc.m11
number: 11
title: Troubleshooting
status: draft
minutes: 330
prerequisites: [plc.m6]
outcomes:
  - plc.m11.o1: Follow a troubleshooting method, and use diagnostic tools (live values on the rungs, the wiring list, stepping one scan, forcing, the replay of a failed test) to find where a fault is.
  - plc.m11.o2: Find the fault in a machine with a wiring or device failure (a broken wire, a short, two wires crossed, a stuck sensor, or an output device that does not work), and tell it apart from a program fault.
  - plc.m11.o3: Find a bug in a program that does not do what its task asks (a double coil, a seal-in on the wrong tag, a timer reset every scan), fix it, and test the fix.
topics:
  - A troubleshooting method
  - PLC+'s diagnostic tools, and forcing
  - Input faults: broken wires, shorts, crossed wires, stuck sensors
  - Output and device faults
  - Program faults: find and fix the bug
---

Machines stop. A good technician does not guess and swap parts until it works: they follow a method, and let the PLC show them where to look. This module teaches that method and the tools that go with it.

The PLC is the best meter on the machine. Its rungs show every input as the program sees it and every output as the program sets it. Compare that with what the machine is really doing, and the fault is in one of three places: **the inputs** (a sensor, a button or their wiring), **the outputs** (a motor, a valve, a lamp or their wiring), or **the program**.

Items 1 to 3 are readings with practice questions: the tools, then input faults, then output faults. Each practice question is a machine with a real fault for you to find, on machines you know from module 5. Items 4 to 6 are exercises of a new kind: My program starts as someone else wrote it, with a bug in it. You find the bug, fix it, and press **Check my program**. The module check mixes every kind of fault.

You need module 6 first, because one of the bugs is in a timer.
