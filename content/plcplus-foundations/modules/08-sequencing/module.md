---
id: plc.m8
number: 8
title: Sequencing and state machines
status: draft
minutes: 270
prerequisites: [plc.m6]
outcomes:
  - plc.m8.o1: Program a machine as a sequence of steps, one memory bit per step, each step moving on only when a sensor says the step before has finished.
  - plc.m8.o2: Design a state machine (named states, the moves between them and their conditions, a stop or fault state and the way back to idle), and read an introductory SFC chart.
  - plc.m8.o3: Run a sequence from a step number (an INT tag), with compare instructions choosing the outputs and MOVE choosing the next step.
topics:
  - Step logic
  - Sequencers with a step number
  - State machines
  - Introduction to SFC
---

Most machines do things **in an order**: clamp the part, stamp it, let it go; fill the tank, mix it, drain it. Each part of the order is a **step**, and the program's job is to know which step the machine is in and to move on only when the machine says that step is done.

This module gives you three ways to write that, from the simplest to the most flexible: one memory bit per step (step logic), one number for the step (a sequencer), and a **state machine**, where the machine can move from one state to several others, stop in the middle and find its way back. The last item reads a **Sequential Function Chart** (SFC), the IEC 61131-3 language drawn as steps and transitions, and shows how a chart becomes ladder.

The machines are new too: air **cylinders** that take time to move and tell the PLC when they have arrived. As in module 5, each item has a working Example on one machine and an Exercise on another. You will use seal-ins and set/reset coils (module 5) and on-delay timers (module 6).
