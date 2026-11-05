---
id: plc.m5.seal-in
title: The seal-in: start and stop a motor
minutes: 40
objectives: [plc.m5.o2]
builds-on: plc.m5.contacts-coils
example: u5.motor-start-stop
exercise: u5.motor-start-stop
reference: [seal-in, wiring-stop, contacts, coils]
---


# The seal-in: start and stop a motor

In module 3 you met the seal-in circuit as relays. Now you write it as a program.

The motor station has a **Start** button, a **Stop** button wired normally closed, a **Motor** output and a **Running** light (tag RunLight). The task:

- press Start and the motor runs, and keeps running after you let go;
- press Stop and it stops;
- if both are pressed, Stop wins.

## The rung

The motor's coil needs power while *(Start OR Motor) AND Stop*:

- a **branch** puts the Motor's own contact in parallel with Start: that is the seal-in;
- after the branch, a contact of **Stop**. Stop is TRUE until it is pressed, so a normally **open** contact passes power until someone presses Stop.

A second rung lights RunLight while Motor is on.

## How to do it

1. In the **Exercise** tab, click the first contact. Its **Tag** box (above the rungs) is ready for typing: type **Start** and press Enter.
2. Press **Branch**: a new contact appears below Start, in parallel with it, with its Tag box ready. Type **Motor** and press Enter.
3. Click the coil, type **Motor** and press Enter. Then press **Contact**: with the coil selected, the new contact goes in before the coil. Type **Stop** and press Enter.
4. **New rung**: a contact of Motor and a coil of RunLight.
5. Hold Start, let go, then press Stop, and watch the rungs. Then press **Check my program**.

The Example tab's *A common mistake* leaves out the seal-in: try it to see what the motor does when you let go of Start. Another mistake students often make is a normally *closed* contact for Stop, because the button is "a stop button". Try that too and see what the tests say; the Reference tab explains why.
