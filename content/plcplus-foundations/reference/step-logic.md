---
title: Step logic (one bit per step)
---

# Step logic (one bit per step)

One memory tag per step (Step1, Step2, …). Each step's rung is a seal-in:

| Part of the rung | What it does |
|---|---|
| (step before AND its "finished" sensor) | starts the step |
| OR the step's own contact | holds it on |
| StopPB (normally open: the button is wired normally closed) | Stop ends it |
| NOT the next step | the next step ends it |

- Step 1 starts from Start, only when **no** step is on. The last step ends on its own sensor; then the machine is idle.
- Outputs come last, **one rung per output**: the steps that need it side by side (OR).
- With latches: step before AND sensor **sets** the next step (OTL); the next step **resets** this one (OTU).

## Air cylinders

| | One solenoid | Two solenoids |
|---|---|---|
| Output on | extends | the extend solenoid pushes it out; the retract solenoid pulls it in |
| Output off | a spring pulls it back | it stays where it is |
| Both on | — | it does not move |

A cylinder takes its **stroke time** to travel. Move on when its **fully out** or **fully in** sensor says it has arrived, never just because the output is on.
