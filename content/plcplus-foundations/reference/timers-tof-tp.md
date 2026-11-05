---
title: Off-delay and pulse timers
---

# Off-delay and pulse timers

## TOF: off-delay

- IN TRUE: Q TRUE at once, ET 0.
- IN goes FALSE: ET counts; Q stays TRUE until ET reaches PT.
- IN back on before then: Q stays on; the off-delay starts again from 0 next time.
- Use it to keep something on **after** its signal goes off: a fan running on after a heater, a light after a door shuts.
- Allen-Bradley: **TOF**, with .PRE, .ACC and .DN (TRUE while the rung is TRUE and until the time runs out).

## TP: pulse

- IN turns TRUE: Q TRUE for exactly PT.
- What IN does during the pulse does not matter: let go, hold on, or press again.
- A new pulse needs IN to turn on again after the last pulse has ended.
- Use it for a fixed-length action per press: a stair light, a purge, a squirt of lubricant.
- Allen-Bradley ladder has no TP: a one-shot (ONS) latches a bit, and a TON unlatches it when its time is up.

## Which timer?

| Q must… | Use |
|---|---|
| come on PT after IN comes on | TON |
| go off PT after IN goes off | TOF |
| be on for PT from the moment IN comes on | TP |
| come on when IN has been on for PT in all, over several runs | TONR (RTO) |
