---
id: plc.m3.interlocks
title: Interlocks
minutes: 25
objectives: [plc.m3.o4]
builds-on: plc.m3.seal-in
example: u5.reversing-conveyor
reference: [interlocks]
---


# Interlocks

Some outputs must never be on together. A motor that runs a door up or down has two contactors: one for each direction. Energize both and the motor is shorted across two phases. Something will burn.

An **interlock** stops that. Each direction's rung has a normally closed contact of the *other* direction's coil:

- the Up rung passes power only while Down is **not** on;
- the Down rung passes power only while Up is **not** on.

So whichever starts first locks the other out. In a relay panel this is often done twice: in the wiring (*electrical interlock*) and with a mechanical bar between the two contactors (*mechanical interlock*), because a welded contact could defeat the first.

Each direction also needs to stop on its own at the end of its travel. A **limit switch** at the top and one at the bottom do that: each is a normally closed contact in its direction's rung, so reaching the limit breaks the rung.

## Try it

The **Example** tab runs a conveyor motor that can run forward or in reverse: two seal-ins, one per direction, each with an interlock. Press Forward, then press Reverse while it is still running: nothing happens until you press Stop. Choose *A common mistake* in Program to see the same circuit without the interlocks: press Forward, then Reverse, and read what the motor says.

In module 5 you will write an interlocked program yourself, for a garage door, with limit switches as well.
