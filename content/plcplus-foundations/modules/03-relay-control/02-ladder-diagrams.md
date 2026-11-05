---
id: plc.m3.ladder-diagrams
title: Reading a ladder diagram
minutes: 25
objectives: [plc.m3.o2]
builds-on: plc.m3.relays
example: u5.pilot-lights
reference: [ladder-diagram, contacts, coils, series-parallel]
---


# Reading a ladder diagram

A ladder diagram is drawn like a ladder:

- two upright **rails**: the left one is the power supply, the right one is the return;
- **rungs** across them, one circuit each.

Power flows from the left rail, through the contacts on a rung, to the coil at the right-hand end. If there is a path of closed contacts all the way across, the coil is energized.

Read a rung from left to right and say it in words. This is a program for a station with two buttons, a Mode switch and three lights:

[[ladder: programs/pilot-lights.json]]

- **Rung 1:** if PB1 is pressed, Green is on.
- **Rung 2:** if PB1 **and** PB2 are pressed, Amber is on. Two contacts one after the other (in **series**) make AND: power must pass through both.
- **Rung 3:** if Mode is **not** on, Red is on. A normally closed contact makes NOT.

Contacts side by side (in **parallel**) make OR: power can pass along either path. You will see that in the seal-in, next.

## Try it

Open the **Example** tab. The rungs run live beside the station: press the buttons (hold the mouse down on them) and turn the Mode switch, and watch which rungs pass power. A rung that passes power is drawn green and thick, and its parts say TRUE.
