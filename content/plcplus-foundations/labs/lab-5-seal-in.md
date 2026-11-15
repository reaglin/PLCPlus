---
title: Wire and program a motor start/stop with a seal-in
minutes: 60
equipment: [the lab's Allen-Bradley PLC trainer, a normally open Start push button, a normally closed Stop push button, a pilot light or contactor for the motor, Studio 5000 (or the software your lab uses)]
---

*An example lab exercise, to show the shape. The real lab exercises come from the instructors before the release (Ron, 2026-11-15).*

**Before the lab:** finish 5.2's Exercise in PLC+, so you know the program works. In the lab you build the same thing on a real PLC.

## Safety first

- Follow your lab's safety rules. **Power off** while you wire; your lab instructor checks your wiring **before** you power on.
- The Stop button is wired **normally closed**, so a broken wire stops the motor (the Learn tab says why). Never wire Stop normally open.

## What you do

1. **Wire the inputs.** Start (normally open) to one input, Stop (normally closed) to another. Write down which: you need the
   addresses for your tags.
2. **Wire the output.** The pilot light (or the contactor's coil) to one output.
3. **Make the tags** `Start`, `Stop` and `Motor` (BOOL), and alias them to the addresses you wrote down.
4. **Enter the rung you wrote in PLC+**, contact by contact, in the ladder editor. Bring a picture of it (in PLC+, File ▸ Save
   the program as a picture) so you can copy it.
5. **Download and test.** Press Start: the motor runs, and **keeps** running when you let go (the seal-in). Press Stop: it stops.
6. **The broken wire.** With the power **off**, take the Stop wire off its terminal. When your lab instructor says so, power on and
   press Start: the motor must not start. Power off and put the wire back.

## Show your lab instructor

- The running machine: start, the seal-in, stop, and the broken Stop wire.
- Your tags and the rung online, with power flow showing.
- One difference you noticed between PLC+ and the real PLC (there may be none; say so).
