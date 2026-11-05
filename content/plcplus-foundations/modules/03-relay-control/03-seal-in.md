---
id: plc.m3.seal-in
title: The seal-in circuit
minutes: 30
objectives: [plc.m3.o3]
builds-on: plc.m3.ladder-diagrams
example: u5.pump-start-stop
reference: [seal-in, wiring-stop]
---


# The seal-in circuit

A motor, such as a pump's, needs two buttons: **Start** to run it and **Stop** to stop it. Push buttons are *momentary*: they are pressed only while a finger holds them. So how does the motor keep running after you let go of Start?

The answer is the **seal-in** (also called a *holding* or *latching* circuit). The motor contactor has a spare normally open contact, wired in parallel with the Start button. Once the motor is on, that contact is closed, so power has a second path round Start:

[[ladder: programs/pump-station.json]]

Say rung 1 in words: *the pump runs if (Start is pressed OR the pump is already running) AND Stop is not pressed AND the overload relay has not tripped.* The overload relay is a protective part every motor starter has: it trips if the motor draws too much current for too long. Leave it out for a moment and the rung is:

[[truth-table: (PumpStart OR Pump) AND PumpStop]]

Here **PumpStop** is TRUE while nobody presses it. Find the row where PumpStart is FALSE, Pump is TRUE and PumpStop is TRUE: the result is TRUE, so the pump keeps itself running. Press Stop (PumpStop becomes FALSE) and every row is FALSE: the seal-in breaks, and the pump stays off until Start is pressed again. The overload contact works the same way as Stop: it is wired normally closed, so a trip, or a broken wire, breaks the seal-in.

## Why Stop is wired normally closed

The Stop button's contacts are closed until it is pressed, so its input is TRUE while all is well. If the wire to it breaks, the input goes FALSE, which is the same as pressing Stop, and the machine stops. A broken wire fails *safe*. That is why the program checks Stop with a normally **open** contact: the contact passes power while the input is TRUE (not pressed). The Reference tab explains this again; it catches out almost everyone once.

## Try it

Open the **Example** tab and run the pump: press Start, let go, then press Stop. Then choose *A common mistake* in Program: it checks Stop with a normally closed contact, because it is "a stop button". Press Start and see what happens, and why. In module 5 you will write the seal-in yourself, for a motor.
