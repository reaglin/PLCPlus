---
id: plc.m3.ladder-diagrams
title: Reading a ladder diagram
minutes: 25
objectives: [plc.m3.o2]
builds-on: plc.m3.relays
example: u5.press-guard
reference: [ladder-diagram, contacts, coils, series-parallel]
---


# Reading a ladder diagram

A ladder diagram is drawn like a ladder:

- two upright **rails**: the left one is the power supply, the right one is the return;
- **rungs** across them, one circuit each.

Power flows from the left rail, through the contacts on a rung, to the coil at the right-hand end. If there is a path of closed contacts all the way across, the coil is energized.

Read a rung from left to right and say it in words. This is a program for a small press: two hand buttons, a guard switch and three lights. The press must only close when both of the operator's hands are on its buttons, so neither hand can be under it:

[[ladder: programs/press-guard.json]]

- **Rung 1:** if the guard is shut, Ready is on.
- **Rung 2:** if Left hand **and** Right hand are pressed **and** the guard is shut, the press closes. Contacts one after the other (in **series**) make AND: power must pass through every one.
- **Rung 3:** if the guard is **not** shut, Guard open is on. A normally closed contact makes NOT.

Contacts side by side (in **parallel**) make OR: power can pass along either path. You will see that in the seal-in, next.

## Try it

Open the **Example** tab. The rungs run live beside the press: press the hand buttons (hold the mouse down on them) and turn the Guard switch, and watch which rungs pass power. A rung that passes power is drawn green and thick, and its parts say TRUE. Then choose *A common mistake* in Program: its two hand buttons are side by side (OR). Try one hand alone to see why that is dangerous.
