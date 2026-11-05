---
id: plc.m4
number: 4
title: PLC fundamentals
status: draft
minutes: 200
prerequisites: [plc.m3]
outcomes:
  - plc.m4.o1: Name the parts of a PLC (power supply, processor, memory, input and output modules, rack, programming software) and say what each does.
  - plc.m4.o2: Explain how discrete input and output modules connect field devices to tags, including sinking and sourcing and the kinds of output.
  - plc.m4.o3: Describe the common sensors (limit switch, float switch, proximity sensor and photo eye) and how they are wired to fail safe.
  - plc.m4.o4: Explain the scan cycle (read the inputs, run the program top to bottom, write the outputs) and what follows from it.
  - plc.m4.o5: Explain tags and addresses, how a tag names an input, an output or a memory value, and its data type.
topics:
  - PLC hardware
  - Input and output modules
  - Sinking and sourcing
  - Sensors
  - The scan cycle
  - Tags, addresses and data types
---

In module 3 you saw machines controlled by panels of relays. A **PLC** (programmable logic controller) does the same job with a small industrial computer: the wiring stays at the edges, and the logic in the middle becomes a program you can change from a laptop.

This module is about the PLC itself and how it connects to a machine:

- what is inside it, and how its parts fit in a rack;
- how input and output modules connect buttons, sensors, lights and motor contactors, including the **sinking and sourcing** question that trips up almost everyone in the lab;
- the sensors that tell a program where things are;
- the **scan cycle**, the loop every PLC program runs in;
- **tags and addresses**, the names a program uses for every input, output and memory value.

The items are readings with practice questions. Several have an **Example** tab: one of PLC+'s machines, with things to watch for. Nothing here asks you to write a program yet; that starts in module 5, and everything in this module is what you will need there and in the lab.
