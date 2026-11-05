---
id: plc.m3.relays
title: Relays and contactors
minutes: 25
objectives: [plc.m3.o1]
reference: [relays, contacts]
---


# Relays and contactors

A **relay** is a switch worked by electricity. It has two parts:

- a **coil**: a small electromagnet. Put voltage across it and it is *energized*;
- one or more **contacts**: switches that the coil's magnet moves.

When the coil is energized, every contact it owns changes over at once. A **normally open** (NO) contact is open while the coil is off and closes when it is energized. A **normally closed** (NC) contact is the other way round: closed while the coil is off, open when it is energized. "Normally" means *with the coil off*.

A **contactor** is a heavy-duty relay. Its coil is worked by a small control voltage (often 24 V DC or 120 V AC), and its main contacts switch the large current a motor needs. One small signal controls a big load, safely away from the operator's hands.

| Part | What it does | Drawn as |
|---|---|---|
| Coil | Energized by the control circuit | `-( )-` |
| NO contact | Closed while its coil is energized | `-| |-` |
| NC contact | Open while its coil is energized | `-|/|-` |

## Why this matters for a PLC

A PLC replaces the relay coils and their wiring with a program, but keeps their language. In a PLC program a **coil** sets a tag, and a **contact** reads one. The rules are the same: a normally open contact passes power while its tag is TRUE, and a normally closed contact passes power while its tag is FALSE.

On the lab's Allen-Bradley PLC the normally open contact is called **XIC** (examine if closed), the normally closed contact **XIO** (examine if open), and the coil **OTE** (output energize).

The Reference tab has the symbols on one page.
