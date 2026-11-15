---
title: A traffic light with on-delay timers
minutes: 75
equipment: [the lab's Allen-Bradley PLC trainer, red, amber and green pilot lights (or three outputs' indicator lights), a Run switch, Studio 5000 (or the software your lab uses)]
---

*An example lab exercise, to show the shape. The real lab exercises come from the instructors before the release (Ron, 2026-11-15).*

**Before the lab:** finish 6.1's Exercise in PLC+. Bring your program's timings (a picture of it helps: File ▸ Save the program as a picture).

## Safety first

Follow your lab's safety rules. Power off while you wire; your lab instructor checks your wiring before you power on.

## What you do

1. **Wire** the Run switch to an input, and the red, amber and green lights to three outputs.
2. **Make the tags** for them, and one **TIMER** for each step of the cycle.
3. **Build the cycle** you built in PLC+: green, then amber, then red, then green again, each for its time. On an Allen-Bradley PLC
   a timer's preset is in **milliseconds** (a preset of 5000 is 5 seconds), and its done bit is `.DN`.
4. **Download and test** with a stopwatch: does each light stay on for the time you set, within a scan or two?
5. **Turn Run off** part way through a cycle. What happens? Is that what you want at a real crossing?

## Show your lab instructor

- One full cycle, timed.
- What your program does when Run goes off, and why.
- Your timers online: the accumulated value counting up to the preset.
