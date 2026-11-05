---
id: plc.m5.edges
title: Edges and memory: sort metal from plastic
minutes: 60
objectives: [plc.m5.o5]
builds-on: plc.m5.set-reset
example: u5.push-on-push-off
exercise: u5.conveyor-sorter
reference: [edges, set-reset]
---


# Edges and memory: sort metal from plastic

Sometimes a program must act **once**, at the moment a signal changes, not all the time it is on. That moment is an **edge**:

- a **rising edge** contact `-|P|-` passes power for one scan when its tag turns TRUE;
- a **falling edge** contact `-|N|-` passes power for one scan when its tag turns FALSE.

On the Allen-Bradley PLC a one-shot (**ONS**) after a contact does the same job.

## The task

A conveyor carries metal and plastic parts. A proximity sensor (Prox) sees metal; further along, a photo eye (Eye) sees every part, and beside it a diverter (Divert) can push a part into Bin A. Metal parts go to Bin A; plastic parts ride on to Bin B. Start and Stop run the belt with a seal-in (from 5.2).

The sensor and the diverter are in different places, so the program must **remember** that it saw metal until that part reaches the diverter:

- Prox sees metal: **set** MetalSeen;
- a remembered metal part reaches the eye: Divert while MetalSeen AND Eye;
- when the part **leaves** the eye (a falling edge of Eye): **reset** MetalSeen.

## How to do it

Run the **Example** first: one button that turns a work light on with one press and off with the next. Its rung 1 makes a one-scan Pulse from a rising edge of the button. *A common mistake* (choose it in **Program**, above the machine) uses a plain contact instead, so the light flips on and off every scan the button is held. Then write the sorter in the **Exercise** tab. Watch MetalSeen turn on at the sensor and off as the part leaves the eye. If you are tempted to divert using Prox directly, try it, and see which bin the metal ends up in.
