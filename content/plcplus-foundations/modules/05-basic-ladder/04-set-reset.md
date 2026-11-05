---
id: plc.m5.set-reset
title: Set and reset: fill and drain a tank
minutes: 50
objectives: [plc.m5.o4]
builds-on: plc.m5.interlocks
example: u5.overheat-alarm
exercise: u5.tank
reference: [set-reset, sensors]
---


# Set and reset: fill and drain a tank

A seal-in remembers that Start was pressed. A **set** coil and a **reset** coil are another way to remember:

- a **set** coil `-(S)-` turns its tag on when power reaches it, and leaves it on;
- a **reset** coil `-(R)-` turns its tag off when power reaches it.

When power stops reaching a set coil, its tag stays on, unlike a normal coil. On the Allen-Bradley PLC these are **OTL** (latch) and **OTU** (unlatch).

## The task

In Auto, fill the tank until the high float, then drain it until the liquid is below the low float, then fill again, over and over. The two valves are never open together. Off closes both.

The floats alone are not enough. Halfway up, the low float is covered and the high float is not, both when filling and when draining. The program has to remember which it is doing. Use two **memory tags** (tags that are not wired to anything), Filling and Draining:

- below the low float: **set** Filling and **reset** Draining;
- at the high float: **reset** Filling and **set** Draining;
- the fill valve follows Filling, and the drain valve follows Draining, only in Auto.

## How to do it

1. In the **Example** tab, see set and reset on an overheat alarm: turn the Thermostat to Too hot and back to Normal. The alarm stays on until you press **Alarm reset** on the machine. *A common mistake* (choose it in **Program**, above the machine) has no memory: the alarm clears itself as soon as the oven cools.
2. Think about the tank the same way: what must the program remember between the low float and the high float? Without memory, the valves follow the high float alone, and the level chatters around 80 %.
3. In the **Exercise** tab, type a new tag name such as **Filling** on a coil, and PLC+ declares it for you. Change the coil's kind to **Set** or **Reset** in the box beside the tag.
