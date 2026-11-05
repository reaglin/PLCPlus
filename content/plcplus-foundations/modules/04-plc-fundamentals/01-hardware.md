---
id: plc.m4.hardware
title: Inside a PLC
minutes: 30
objectives: [plc.m4.o1]
quiz: quizzes/m4-hardware.json
reference: [plc-hardware]
---


# Inside a PLC

A **PLC** (programmable logic controller) is a computer built for a factory floor. It reads switches and sensors, decides what to do with a program, and turns on lights, valves and motor contactors. It is built to survive heat, dust, vibration and electrical noise, and to run the same program day and night for years.

## Why PLCs replaced relay panels

A relay panel's logic is in its wiring. To change what the machine does, an electrician rewires the panel, and every change risks a mistake. A large panel also has hundreds of relays, each of which can wear out.

With a PLC, the buttons and sensors are wired to its inputs, and the lights and contactors to its outputs, once. The logic between them is a program. Changing it means editing the program, not the wires. A PLC also shows you, live, the state of every input and output, which makes faults much faster to find.

The program is still drawn as a ladder, so electricians who knew relay panels could read it. That is why module 3 came first.

## The parts

| Part | What it does |
|---|---|
| Power supply | Turns the plant's power (often 120 V AC) into the low DC voltage the PLC's electronics need |
| Processor (CPU) | Runs the program, over and over. Also called the controller |
| Memory | Holds the program and the data (the values of every tag). Kept when the power is off |
| Input modules | Connect the field devices that tell the PLC something: buttons, switches, sensors |
| Output modules | Connect the field devices the PLC controls: lights, solenoid valves, contactor coils |
| Rack (chassis) | The frame the modules plug into. Each position is a numbered **slot** |
| Communication ports | Connect a programming computer, a touch screen (HMI), other PLCs or a network |

**Field devices** are the real things out on the machine: the Start button, the float switch, the motor contactor. The PLC never touches a motor directly; it switches the small current of a contactor's coil, and the contactor switches the motor (more in the next item).

## Programming software

You write the program on a PC, in the PLC maker's software, and **download** it to the processor over a cable or a network. The software also lets you go **online**: watch the program run, with each contact and coil showing whether it passes power, just as PLC+ does. You can **upload** a program from a PLC to the PC to see what it is running.

## Compact and modular PLCs

- A **compact** (or *fixed*) PLC has the power supply, processor and a set number of inputs and outputs in one box. It is cheap and small: good for one small machine. Many can take a few plug-in modules for more.
- A **modular** PLC is built from separate modules in a rack or a row. You choose the processor and add the input and output modules the machine needs. It is good for larger machines and is easy to repair: swap the failed module.

Your lab uses Allen-Bradley PLCs, made by Rockwell Automation. As examples only: the **Micro800** family (such as the Micro850) are compact controllers programmed with Connected Components Workbench, and **CompactLogix** controllers are modular, programmed with Studio 5000 Logix Designer. Older panels may still have **SLC 500** or **MicroLogix** PLCs. Your lab instructor will tell you which model you will use.

## Try it

This item has no machine. Open the **Exercise** tab and answer the practice questions. Each answer says why, and shows the right one when yours is wrong. Pass them (80 %) and the item is complete.
