---
id: plc.m3.seal-in
title: The seal-in circuit
minutes: 30
objectives: [plc.m3.o3]
builds-on: plc.m3.ladder-diagrams
example: u5.motor-start-stop
reference: [seal-in, wiring-stop]
---


# The seal-in circuit

A motor needs two buttons: **Start** to run it and **Stop** to stop it. Push buttons are *momentary*: they are pressed only while a finger holds them. So how does the motor keep running after you let go of Start?

The answer is the **seal-in** (also called a *holding* or *latching* circuit). The motor contactor has a spare normally open contact, wired in parallel with the Start button. Once the motor is on, that contact is closed, so power has a second path round Start:

[[ladder: programs/motor-seal-in.json]]

Say rung 1 in words: *the motor runs if (Start is pressed OR the motor is already running) AND Stop is not pressed.*

[[truth-table: (Start OR Motor) AND Stop]]

Here **Stop** is TRUE while nobody presses it. Find the row where Start is FALSE, Motor is TRUE and Stop is TRUE: the result is TRUE, so the motor keeps itself running. Press Stop (Stop becomes FALSE) and every row is FALSE: the seal-in breaks, and the motor stays off until Start is pressed again.

## Why Stop is wired normally closed

The Stop button's contacts are closed until it is pressed, so its input is TRUE while all is well. If the wire to it breaks, the input goes FALSE, which is the same as pressing Stop, and the machine stops. A broken wire fails *safe*. That is why the program checks Stop with a normally **open** contact: the contact passes power while the input is TRUE (not pressed). The Reference tab explains this again; it catches out almost everyone once.

## Try it

Open the **Example** tab and run the motor. Then choose *A common mistake* in Program: that program has no seal-in contact. Press Start, let go, and see what the motor does. In module 5 you will write the seal-in yourself.
