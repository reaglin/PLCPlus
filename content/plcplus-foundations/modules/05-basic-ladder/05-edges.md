---
id: plc.m5.edges
title: Edges and memory: sort metal from plastic
minutes: 60
objectives: [plc.m5.o5]
builds-on: plc.m5.set-reset
example: u5.conveyor-sorter
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

Run the **Example** first and watch MetalSeen turn on at the sensor and off as the part leaves the eye. Then write it in the **Exercise** tab. *A common mistake* diverts using Prox directly. Try it to see why that sends metal to the wrong bin.
