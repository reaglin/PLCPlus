---
title: Timers
---

# Timers

| Box | Name | Q (the output) |
|---|---|---|
| TON | On-delay | turns TRUE when IN has been TRUE for PT; FALSE as soon as IN is FALSE |
| TOF | Off-delay | TRUE while IN is TRUE, and stays TRUE for PT after IN goes FALSE |
| TP | Pulse | TRUE for exactly PT after IN turns TRUE |
| TONR | Retentive on-delay | turns TRUE when IN has been TRUE for PT **in all**; keeps its time while IN is FALSE; only R clears it |

- **PT** is the preset time, written like T#2s (2 seconds) or T#500ms.
- **ET** is the elapsed time so far.
- A timer named T1 has the outputs **T1.Q** and **T1.ET**, usable as tags.
- Allen-Bradley: TON, TOF and RTO (retentive), with .DN for done and .ACC for the time.

## TON, step by step (PT T#3s)

| IN | ET | Q |
|---|---|---|
| turns on at time t | counts from 0 | FALSE |
| still on at t + 3 s | 3 s (stops there) | **TRUE** |
| turns off | back to 0 at once | FALSE |
| on for less than 3 s, then off | back to 0 | never turned on |

## Allen-Bradley names

| IEC (PLC+) | Allen-Bradley | Meaning |
|---|---|---|
| PT | .PRE | the preset, in milliseconds (3 s = 3000) |
| ET | .ACC | the time so far, in milliseconds |
| Q | .DN | done |
| IN | .EN | the rung is TRUE |
| (none) | .TT | timing: enabled and not yet done |

PLC+ scans every 10 ms, so a TON with PT T#3s turns Q on exactly 3.00 s after IN, in the scan that reaches the time.
