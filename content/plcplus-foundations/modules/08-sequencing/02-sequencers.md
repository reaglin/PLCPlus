---
id: plc.m8.sequencers
title: Sequencers with a step number: a batch tank
minutes: 60
objectives: [plc.m8.o3, plc.m8.o1]
builds-on: plc.m8.step-logic
example: u8.parts-washer
exercise: u8.batch-tank
reference: [sequencer, timers]
---


# Sequencers with a step number: a batch tank

A memory bit per step works well for four steps. With twenty, there are twenty bits to keep straight. The other common way is **one number** for the whole sequence: an INT tag, here called **Step**, that holds the number of the step the machine is in. Step 0 is idle; step 1 is the first step, and so on. An operator can read it on a display: "Step 2" says exactly where the machine is.

## Two boxes do the work

- **EQ** (compare equal; **EQU** on Allen-Bradley) passes power when IN1 equals IN2. With IN1 = Step and IN2 = 2, it passes power only in step 2. It works like a contact that asks "are we in step 2?".
- **MOVE** (**MOV** on Allen-Bradley) copies IN into OUT while power reaches it. With IN = 3 and OUT = Step, it moves the machine to step 3.

## Outputs: one EQ each

Each output's rung is an EQ box for its step, then the coil: *EQ Step 1* then the FillValve coil. If an output is on in two steps, put two EQ boxes side by side (a branch) on **one** rung. Never write two rungs with coils of the same tag: the PLC runs both, and the lower one decides. The Example's common mistake shows what that does.

## Moving on: EQ, the condition, MOVE

Each move to the next step is a rung of three parts: an EQ box for the step it **leaves**, the condition that says that step has finished, and a MOVE of the next number into Step:

*EQ Step 1*, *HighFloat*, *MOVE 2 to Step*: "in step 1, when the tank is full, go to step 2."

There is nothing to end: when Step changes, the step before is over, because Step can hold only one number. The rungs below see the new number in the same scan.

- **A step that waits for a time:** a TON on a rung of its own, powered by *EQ Step 2*. The next rung moves on when its **.Q** turns on. When the step ends, the timer's input goes off and it resets, ready for the next batch.
- **Start** belongs to step 0 only: *EQ Step 0*, *StartPB*, *MOVE 1*.
- **Stop** works in every step, so its rung has **no** EQ box: *NOT StopPB*, *MOVE 0*. Put it after the other moves, so it has the last word.

## The worked example: a parts washer

| Step | Outputs on | Moves on when | To |
|---|---|---|---|
| 0 Idle | none | Start | 1 |
| 1 Lower the basket | BasketDown | BasketIsDown | 2 |
| 2 Wash | BasketDown, SprayPump | WashTimer.Q (3 s) | 3 |
| 3 Raise the basket | none (the spring lifts it) | BasketIsUp | 4 |
| 4 Drip dry | none | DripTimer.Q (2 s) | 0 |

Many plants number steps 10, 20, 30 instead of 1, 2, 3, so that a step added later can be 15 without renumbering the rest. Allen-Bradley PLCs also have a sequencer instruction, **SQO**, that steps through a table of output patterns; PLC+ does not have it, and the step number with EQ and MOVE works on every PLC.

## How to do it

1. Open the **Example** tab: the parts washer. Press **Start** and watch the **Step** display count 1, 2, 3, 4 and back to 0, and the EQ boxes pass power one at a time. Then choose *A common mistake* in **Program**, above the machine: BasketDown has two rungs. Press Start, and watch which rung turns it off.
2. Open the **Exercise** tab: the batch tank. Fill the tank to the high float, mix for 4 s, drain it below the low float. Write the table first.
3. Add a box with **Box ▾ ▸ Compare ▸ EQ** and **Box ▾ ▸ Move and bit logic ▸ MOVE**. Type the values in the box's **IN1**, **IN2**, **IN** and **OUT** boxes above the rungs and press Enter. Step is already declared, as an INT, because it drives the display. The mixing time is **Box ▾ ▸ Timers ▸ TON**. In My program your timers are called T1, T2 … (PLC+ cannot rename them yet), so where the Example reads WashTimer.Q, yours reads T1.Q.
4. Press **Check my program**. One test is hidden. Try pressing Start in the middle of a batch: what should happen?
