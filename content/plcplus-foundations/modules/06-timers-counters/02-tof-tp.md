---
id: plc.m6.tof-tp
title: Off-delay and pulse timers: an exhaust fan
minutes: 40
objectives: [plc.m6.o1]
builds-on: plc.m6.ton
example: u6.stair-lights
exercise: u6.exhaust-fan
reference: [timers, timers-tof-tp]
---

# Off-delay and pulse timers: an exhaust fan

A TON delays turning **on**. Two more timers handle the other jobs that come up again and again: keeping something on for a while **after** its signal goes off, and giving a fixed-length **pulse** whatever the signal does.

## The off-delay timer, TOF

An **off-delay timer** keeps its output on for a while after its input goes off:

- while IN is TRUE, Q is TRUE (at once, with no delay) and ET is 0;
- when IN goes FALSE, the timing starts: Q stays TRUE until ET reaches PT, then goes FALSE;
- if IN comes back on before then, Q simply stays on, and the timing starts again from 0 the next time IN goes off.

Uses: a fan that clears fumes after a heater turns off, a cooling pump that runs on after a motor stops, a porch light that stays on after a door shuts.

A TOF with PT T#4s; IN on at 1 s, off at 3 s:

| Time | IN | ET | Q |
|---|---|---|---|
| 0 s | FALSE | 0 s | FALSE |
| 1 s | TRUE | 0 s | **TRUE** |
| 3 s | FALSE | 0 s | TRUE |
| 5 s | FALSE | 2 s | TRUE |
| 7 s | FALSE | 4 s | **FALSE** |

Q goes off PT after IN goes off: 3 s + 4 s = 7 s.

## The pulse timer, TP

A **pulse timer** gives one pulse of an exact length each time its input turns on:

- when IN turns on, Q turns on for exactly PT;
- what IN does during the pulse does not matter: let go early, hold it down, or press again, and the pulse still lasts PT;
- a new pulse can start only after the last one has ended and IN has turned on again.

Uses: a stairwell light that stays on for a fixed time per press, a purge or a squirt of lubricant of a set length, a buzzer that beeps once.

A TP with PT T#5s. The button is pressed at 1 s and **held** until 8 s:

| Time | IN | Q |
|---|---|---|
| 1 s | TRUE | **TRUE** |
| 4 s | TRUE | TRUE |
| 6 s | TRUE | **FALSE**: 5 s are up, even though IN is still on |
| 8 s | FALSE | FALSE |

## Choosing the timer

Ask what Q must do:

| Q must… | Use |
|---|---|
| come on a while *after* IN comes on | TON |
| go off a while *after* IN goes off | TOF |
| be on for a fixed time from the moment IN comes on | TP |

## Allen-Bradley

The lab's PLC has **TOF** too, with the same .PRE, .ACC and .DN as its TON: .DN is TRUE while the rung is TRUE and until the time runs out after it goes FALSE. Its ladder has **no TP**: a pulse is built from a one-shot (ONS) that latches a bit and a TON that unlatches it when its time is up. The idea is the same: a fixed time from the press.

## How to do it

1. Open the **Example** tab: a stairwell light on a TP and a porch light on a TOF. Press **Bottom of stairs** and let go: the light stays on for 5 s. Then tick **keep pressed** under the button, so it stays held: the light still goes off after 5 s. Untick it to let go. Turn the **Front door** to Open and back to Shut, and watch the porch light stay on for 4 s. Then choose *A common mistake* in **Program**, above the machine: its stair light uses a TOF. Hold the button down with **keep pressed** and see the difference.
2. Open the **Exercise** tab: a heater and its exhaust fan. The fan runs while the heater is on and for 5 s after; each press of **Purge** runs it for exactly 3 s. Which timer does each job? The table *Choosing the timer* above helps; the timers are in **Box ▾ ▸ Timers**.
3. Two timers, but only one Fan coil (two coils of one tag fight each other). When should the fan run, in words? Then build that sentence on the Fan's rung.
4. Press **Check my program**. Two tests check the moment the fan stops, to within 0.05 s, and one holds Purge down for 5.5 s.
