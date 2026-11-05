---
title: Timers
---

# Timers

| Box | Name | Q (the output) |
|---|---|---|
| TON | On-delay | turns TRUE when IN has been TRUE for PT; FALSE as soon as IN is FALSE |
| TOF | Off-delay | TRUE while IN is TRUE, and stays TRUE for PT after IN goes FALSE |
| TP | Pulse | TRUE for exactly PT after IN turns TRUE |

- **PT** is the preset time, written like T#2s (2 seconds) or T#500ms.
- **ET** is the elapsed time so far.
- A timer named T1 has the outputs **T1.Q** and **T1.ET**, usable as tags.
- Allen-Bradley: TON, TOF and RTO (retentive), with .DN for done and .ACC for the time.
