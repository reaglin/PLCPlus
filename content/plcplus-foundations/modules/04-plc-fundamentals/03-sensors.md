---
id: plc.m4.sensors
title: Sensors
minutes: 30
objectives: [plc.m4.o3]
builds-on: plc.m4.io-modules
quiz: quizzes/m4-sensors.json
example: u5.overheat-alarm
reference: [sensors, wiring-stop]
---


# Sensors

A program can only act on what it is told. **Sensors** tell it where things are: is the door at the top, is the tank full, is a part in front of the diverter? Each sensor here is discrete: its input is TRUE or FALSE.

## The common sensors

- A **limit switch** has a lever or plunger. A moving part pushes it, and its contacts change over. It tells the program that something has reached a place: the garage door at the top or the bottom of its travel.
- A **float switch** floats on a liquid. When the level rises to it, it lifts and its contacts change over. The tank in PLC+ has two: a low float at 20 % and a high float at 80 %.
- A **proximity sensor** (a *prox*) senses an object near its face without touching it. An **inductive** prox senses only **metal**, which is how the conveyor sorter tells metal parts from plastic. A **capacitive** prox senses almost anything, including plastic, wood and liquid, even through the wall of a plastic tank.
- A **photo eye** (photoelectric sensor) uses a beam of light. Something in the way breaks the beam.

## Three ways to set up a photo eye

| Kind | How it works |
|---|---|
| Through-beam | A sender on one side, a receiver on the other. Longest range, most reliable |
| Retro-reflective | Sender and receiver in one body, aimed at a reflector across the gap |
| Diffuse | Sender and receiver in one body; the light bounces back off the object itself. Shortest range |

Most photo eyes can be set to **light operate** (the output is on while the receiver sees light: the beam is clear) or **dark operate** (on while the beam is blocked). In PLC+, the garage door's safety eye works like light operate: EyeClear is TRUE while the doorway is clear. The conveyor's photo eye works like dark operate: Eye is TRUE while a part is in front of it.

## Wiring to fail safe

A sensor's contacts can be wired **normally open** (the input turns TRUE when it senses its target) or **normally closed** (the input is TRUE until it senses its target). Choose with this question: *if this wire breaks, what will the program think?*

A broken wire always reads FALSE. So a signal that must stop the machine, such as a Stop button or a safety eye, is wired so that FALSE means "stop". Then a broken wire stops the machine, just as pressing Stop would. That is **failing safe**, and it is why the Stop button and the safety eye in PLC+ are TRUE while all is well. The wiring list under each machine says which way each sensor is wired; read it before you write a contact.

## Two-wire and three-wire sensors

A limit switch or float switch is just a contact: two wires, in series with the input. Electronic sensors such as a prox or a photo eye need power to run, so most are **three-wire**: +24 V, 0 V, and an output wire to the input point. The output is either PNP (sourcing) or NPN (sinking), and must match the input module (item 4.2). Some electronic sensors are two-wire, wired in series like a switch; they let a small current through even when off, which can confuse some input modules.

## Try it

Open the **Example** tab: an overheat alarm. Its temperature switch is a sensor: a thermostat whose contact closes when the oven is too hot (in PLC+ you turn it by hand, to play the oven). Turn it to Too hot and watch Overheat turn TRUE on the rungs. Then ask: if the wire to this switch broke, what would Overheat read, and would anyone know? (It would read FALSE, the same as Normal, so nobody would know: this switch fails unsafe. A real overheat switch is wired normally closed, like a Stop button.) The `-(S)-` and `-(R)-` coils in its program are explained in 5.4; for now, watch the Overheat contact. You will meet the conveyor's proximity sensor and photo eye in module 5. Then answer the questions on the **Exercise** tab.
