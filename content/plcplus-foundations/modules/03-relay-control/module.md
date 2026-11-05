---
id: plc.m3
number: 3
title: Relay control
status: draft
minutes: 150
prerequisites: [plc.m1, plc.m2]
outcomes:
  - plc.m3.o1: Explain how a relay and a contactor work: a coil that, when energized, opens or closes contacts.
  - plc.m3.o2: Read a relay ladder diagram: its rails and rungs, its contacts and coils, and what each rung does.
  - plc.m3.o3: Explain how a seal-in (holding) circuit keeps a motor running after Start is let go, and why Stop is wired normally closed.
  - plc.m3.o4: Explain an interlock: why two outputs that must never be on together each check the other.
topics:
  - Relays and contactors
  - Relay ladder diagrams
  - The seal-in circuit
  - Interlocks
---

Before PLCs, machines were controlled by panels full of relays wired together. Ladder logic is drawn the way those panels were drawn, so the ideas here (contacts, coils, the seal-in, interlocks) are exactly the ideas you will program in module 5.

This module has examples to run but no exercises: you write these circuits yourself as PLC programs in module 5.
