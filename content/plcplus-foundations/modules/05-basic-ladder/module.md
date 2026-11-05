---
id: plc.m5
number: 5
title: Basic ladder programming
status: draft
minutes: 300
prerequisites: [plc.m4]
outcomes:
  - plc.m5.o1: Write rungs with normally open and normally closed contacts in series (AND) and in parallel (OR) to drive coils.
  - plc.m5.o2: Write a seal-in rung that starts and stops a motor, with a Stop button wired normally closed.
  - plc.m5.o3: Use limit switches and an interlock so that a motion stops at its limit and two outputs are never on together.
  - plc.m5.o4: Use set and reset coils to latch a memory tag, and control a process with it.
  - plc.m5.o5: Use an edge contact to act once when a signal changes, and remember an event until it is acted on.
topics:
  - Contacts and coils
  - Series (AND) and parallel (OR)
  - The seal-in
  - Limit switches and interlocks
  - Set and reset (latching)
  - Edge detection
---

Your first PLC programs. Each item has two machines. The **Example** tab runs a working program on a machine like the exercise's, so you see the idea worked out, and a common mistake beside it. Then you write your own program for the exercise's machine on the **Exercise** tab and press **Check my program** to test it. Each item builds on the one before it.

The relay ideas from module 3 come back here as programs: the seal-in, interlocks and limit switches.
