---
title: Sensors
---

# Sensors

| Sensor | Senses | Its tag is TRUE when |
|---|---|---|
| Limit switch | a moving part touching its lever | the part is there (unless wired normally closed) |
| Float switch | liquid level | the liquid is at or above the float |
| Proximity sensor | metal near its face, without touching | metal is in front of it |
| Photo eye | something breaking a light beam | depends on how it is set up: "beam clear" or "beam blocked". Read the wiring list |

A safety eye is wired so that a failure looks like a blocked beam: the machine stops.

- An **inductive** prox senses metal only; a **capacitive** prox senses almost anything.
- Photo eyes: **through-beam** (sender and receiver apart), **retro-reflective** (aimed at a reflector), **diffuse** (bounces off the object). **Light operate**: on while the beam is clear. **Dark operate**: on while it is blocked.
- A broken wire reads FALSE, so wire stop and safety signals so that FALSE means stop: they fail safe.
- Switches have two wires; most electronic sensors have three (+24 V, 0 V, output), PNP or NPN to match the input module.
