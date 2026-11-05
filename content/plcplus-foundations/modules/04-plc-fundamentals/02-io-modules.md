---
id: plc.m4.io-modules
title: Input and output modules
minutes: 30
objectives: [plc.m4.o2]
builds-on: plc.m4.hardware
quiz: quizzes/m4-io-modules.json
example: u5.press-guard
reference: [io-modules, plc-hardware]
---


# Input and output modules

The modules are where the PLC meets the machine. An **input module** turns a voltage from a field device into a TRUE or FALSE the program can read. An **output module** turns a TRUE or FALSE from the program into a voltage that works a field device.

This item is about **discrete** (on/off) inputs and outputs: a button is pressed or not, a light is on or off. Inputs and outputs that carry a range of values, such as a temperature or a valve position, are **analog**; they come later, in module 9.

## From a button to a tag

Follow one signal, from the machine to the program:

1. A **field device** (the Start button) closes and passes voltage down its wire.
2. The wire lands on a **terminal** of the input module.
3. That terminal is one **input point** (also called a channel). Each point has a number: point 0, point 1, …
4. The processor reads the point and stores TRUE or FALSE in its **tag** (Start). The program reads the tag.

An output runs the other way: the program sets a tag, the processor sends it to an output point, and the module switches voltage to the field device on that point's terminal.

Most modules have a **status LED** for each point. An input LED lit means there is voltage at that terminal. An output LED lit means the program has told the point to turn on. It does not prove the device got power: a blown fuse or a broken wire beyond the terminal still leaves the device off. Checking the LED first, then the wire, is the start of most troubleshooting.

## Voltages

Modules are made for one kind of signal. The common ones are **24 V DC** (most sensors and new machines) and **120 V AC** (common in older US plants). Wire a device of one voltage to a module of another and it will not work, or the module will be damaged. The module's label and its wiring diagram say what it takes.

## Sinking and sourcing

With 24 V DC, current must flow in a loop: from +24 V, through the device, through the input point, and back to 0 V (the *common*). The two words say which side of the load a device is on. Here "current" means conventional current, flowing from + to −.

- A **sourcing** device *supplies* current: it switches the +24 V side and sends current out. A **PNP** sensor is sourcing.
- A **sinking** device *takes in* current and gives it a path to 0 V. An **NPN** sensor is sinking.

The rule: **one side sources, the other sinks.** A sourcing (PNP) sensor needs a **sinking** input module, whose points lead through the module to 0 V. A sinking (NPN) sensor needs a **sourcing** input module. Pair two sourcing devices, or two sinking ones, and no current can flow: the input never turns on.

Many DC input modules can be wired either way, by what you connect to the module's common terminal. Always check the module's wiring diagram. The same words are used for DC outputs: a sourcing output sends +24 V out to the load.

## Kinds of output

| Output | Switches | Good for | Watch out |
|---|---|---|---|
| Relay | AC or DC | Mixed voltages, a few amps | Slow, and the contacts wear out |
| Transistor | DC only | Fast switching, long life | Small currents; sinking or sourcing |
| Triac | AC only | AC loads, long life | Small leakage current when off |

An output point can carry only a small current, often an amp or less. A motor needs far more. So the output drives the **coil of a contactor** (or a motor starter), and the contactor's heavy contacts switch the motor, exactly as in module 3. This also keeps the motor's power wiring away from the PLC.

## Try it

Open the **Example** tab: a press guard station. Open **The wiring: which tag is which** under the machine: LeftHand, RightHand and GuardShut are inputs, and Ready, PressClose and GuardOpen are outputs. Turn the Guard switch and watch its tag turn TRUE on the rungs, then the Ready light's tag. Then answer the questions on the **Exercise** tab.
