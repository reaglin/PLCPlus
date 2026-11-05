---
id: plc.m8.sfc
title: An introduction to SFC (Sequential Function Chart)
minutes: 40
objectives: [plc.m8.o2]
builds-on: plc.m8.state-machines
example: u8.parts-washer
example-program: Written as an SFC
quiz: quizzes/m8-sfc.json
reference: [sfc, step-logic]
---


# An introduction to SFC (Sequential Function Chart)

IEC 61131-3, the standard for PLC languages, has a language made for sequences: the **Sequential Function Chart**, or **SFC**. Instead of rungs, you draw the sequence itself: boxes for the steps, and a short bar between them for each condition. Allen-Bradley ControlLogix and CompactLogix PLCs have an SFC editor in Studio 5000, and so do most other makers' tools.

**PLC+ has no SFC editor yet: it is planned for a later version.** This item teaches you to **read** a chart, and shows how each part of it becomes the ladder you already write.

## The parts of a chart

- A **step** is a box with a name, such as S2. While the machine is in that step, the step is **active**. In Studio 5000 each step has a tag; S2.X is TRUE while it is active, and S2.T is how long it has been active.
- The **initial step** has a double border. It is active when the PLC starts: the idle step.
- A **transition** is a short bar across the line from one step to the next, with its **condition** written beside it, such as *BasketIsDown*. It **fires** when the step before it is active AND its condition is TRUE. Then the step before goes inactive and the step after goes active, in the same moment.
- An **action** is a box beside a step: what the machine does in that step. Its **qualifier** says how: **N** (on while the step is active, the usual one), **S** and **R** (set and reset, stays on after the step), **P** (a pulse, once, as the step starts), **D** (after a delay) and **L** (for a limited time).
- **Branches.** Where the line splits on a **single** line, it is a choice: only one path is taken, the one whose transition comes true (if two come true together, most tools take the left one). It is like a state with two ways out. Where it splits on a **double** line, every path runs **at the same time** (two stations working at once), and they meet again at another double line.

## The parts washer as a chart

No pictures here, so the chart is written as a table, read from top to bottom. The last transition goes back to the top.

| Step | Action (N) | Transition below it |
|---|---|---|
| S0 (initial, double box) | none | StartPB |
| S1 Lower | BasketDown | BasketIsDown |
| S2 Wash | BasketDown, SprayPump, WashTimer 3 s | WashTimer.Q |
| S3 Raise | none | BasketIsUp |
| S4 Drip | DripTimer 2 s | DripTimer.Q, back to S0 |

## From a chart to ladder

The chart maps straight onto what you wrote in this module:

| In the chart | In ladder |
|---|---|
| A step | A memory bit (item 8.1), or one number of a step number or state (8.2, 8.3) |
| The initial step | A bit whose starting value is TRUE, or state 0 |
| A transition | A rung: the step before AND the condition starts the next step |
| Firing ends the step before | The next step's normally closed contact in the step's seal-in, or MOVE of a new number |
| An N action | A coil driven by the step (one rung per output, steps side by side) |
| A single-line branch | A state with two transitions out |

The Example tab runs the parts washer written this way: one BOOL per step, S0 to S4, S0 starting TRUE. (My program cannot give a tag a starting value yet; the Example's program has one.)

[[ladder: programs/m8-parts-washer-sfc.json]]

## Try it

1. Open the **Example** tab. It starts with *Written as an SFC* chosen in **Program**, above the machine. Press **Start** and watch S0 to S4 take turns, and read each rung's comment: which part of the chart is it? Compare it with *Works*, the same machine with a step number.
2. Open the **Exercise** tab and answer the questions; press **Check my answers**. These are practice: a wrong answer shows you the right one. This item is complete when you pass its questions.
