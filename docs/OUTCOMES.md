# PLC+ — outcomes

Every module's outcomes with their **stable IDs** (progress, mastery and reports key off them, so a course's or the grant's
outcomes can be matched to them), the items that teach each one, and the questions that give evidence for it. The grant asks
for faculty training on PLCs and student educational resources (Ron, question 12); the outcomes are written with each unit,
and **Ron reviews them** (plan 9.6). Written by `tools/make-outcomes.py`; do not edit by hand.

## Module 1: Electrical foundations (draft)

| ID | The learner can | Taught in | Questions (check / practice) |
|---|---|---|---|
| `plc.m1.o1` | Explain voltage, current and resistance, and use Ohm's law to find one from the other two. | 1.1 Voltage, current and resistance | 3 / 5 |
| `plc.m1.o2` | Tell a series circuit from a parallel one, say what each does to current and voltage, and explain why switches in series make AND and in parallel make OR. | 1.2 Series and parallel circuits | 3 / 4 |
| `plc.m1.o3` | Say where AC and DC are used in a control panel, such as 120 V AC control power, 24 V DC for PLC inputs and outputs, and three-phase power for motors. | 1.3 AC and DC in a control panel | 2 / 5 |
| `plc.m1.o4` | Describe safe working practice at a control panel, including the steps of lockout/tagout in order. | 1.4 Working safely: lockout/tagout | 2 / 5 |

## Module 2: Logic and number systems (draft)

| ID | The learner can | Taught in | Questions (check / practice) |
|---|---|---|---|
| `plc.m2.o1` | Convert whole numbers between decimal, binary and hexadecimal, and read a PLC word (an input module, an INT tag) as its bits. | 2.1 Binary and hexadecimal | 3 / 6 |
| `plc.m2.o2` | Read and write a number in BCD (binary-coded decimal), as thumbwheel switches and displays use it. | 2.2 BCD: binary-coded decimal | 3 / 6 |
| `plc.m2.o3` | Describe AND, OR and NOT, and the contacts that make each: in series, in parallel, and normally closed. | 2.3 AND, OR and NOT | 2 / 5 |
| `plc.m2.o4` | Write the truth table of an expression with up to four inputs, using the order of operations, brackets and XOR, and turn a sentence into an expression. | 2.4 Truth tables for larger expressions | 2 / 6 |

## Module 3: Relay control (draft)

| ID | The learner can | Taught in | Questions (check / practice) |
|---|---|---|---|
| `plc.m3.o1` | Explain how a relay and a contactor work: a coil that, when energized, opens or closes contacts. | 3.1 Relays and contactors | 2 / 0 |
| `plc.m3.o2` | Read a relay ladder diagram: its rails and rungs, its contacts and coils, and what each rung does. | 3.2 Reading a ladder diagram | 1 / 0 |
| `plc.m3.o3` | Explain how a seal-in (holding) circuit keeps a motor running after Start is let go, and why Stop is wired normally closed. | 3.3 The seal-in circuit | 2 / 0 |
| `plc.m3.o4` | Explain an interlock: why two outputs that must never be on together each check the other. | 3.4 Interlocks | 1 / 0 |

## Module 4: PLC fundamentals (draft)

| ID | The learner can | Taught in | Questions (check / practice) |
|---|---|---|---|
| `plc.m4.o1` | Name the parts of a PLC (power supply, processor, memory, input and output modules, rack, programming software) and say what each does. | 4.1 Inside a PLC | 2 / 4 |
| `plc.m4.o2` | Explain how discrete input and output modules connect field devices to tags, including sinking and sourcing and the kinds of output. | 4.2 Input and output modules | 2 / 5 |
| `plc.m4.o3` | Describe the common sensors (limit switch, float switch, proximity sensor and photo eye) and how they are wired to fail safe. | 4.3 Sensors | 2 / 5 |
| `plc.m4.o4` | Explain the scan cycle (read the inputs, run the program top to bottom, write the outputs) and what follows from it. | 4.4 The scan cycle | 2 / 5 |
| `plc.m4.o5` | Explain tags and addresses, how a tag names an input, an output or a memory value, and its data type. | 4.5 Tags and addresses | 2 / 5 |

## Module 5: Basic ladder programming (draft)

| ID | The learner can | Taught in | Questions (check / practice) |
|---|---|---|---|
| `plc.m5.o1` | Write rungs with normally open and normally closed contacts in series (AND) and in parallel (OR) to drive coils. | 5.1 Contacts and coils: a pilot light station | 2 / 0 |
| `plc.m5.o2` | Write a seal-in rung that starts and stops a motor, with a Stop button wired normally closed. | 5.2 The seal-in: start and stop a motor | 1 / 0 |
| `plc.m5.o3` | Use limit switches and an interlock so that a motion stops at its limit and two outputs are never on together. | 5.3 Limit switches and interlocks: a garage door | 2 / 0 |
| `plc.m5.o4` | Use set and reset coils to latch a memory tag, and control a process with it. | 5.4 Set and reset: fill and drain a tank | 2 / 0 |
| `plc.m5.o5` | Use an edge contact to act once when a signal changes, and remember an event until it is acted on. | 5.5 Edges and memory: sort metal from plastic | 1 / 0 |

## Module 6: Timers and counters (outline)

| ID | The learner can | Taught in | Questions (check / practice) |
|---|---|---|---|
| `plc.m6.o1` | Use on-delay, off-delay and pulse timers (TON, TOF, TP) to time events. | 6.1 On-delay timers: a traffic light; 6.2 Off-delay and pulse timers | — |
| `plc.m6.o2` | Explain retentive timing, and when a timer must keep its time through a stop. | 6.2 Off-delay and pulse timers | — |
| `plc.m6.o3` | Count events with up, down and up/down counters (CTU, CTD, CTUD). | 6.3 Counters | — |
| `plc.m6.o4` | Cascade timers and counters to time or count beyond one instruction. | 6.4 Cascading timers and counters | — |

## Module 7: Data and math (coming-later)

| ID | The learner can | Taught in | Questions (check / practice) |
|---|---|---|---|
| `plc.m7.o1` | Compare, calculate with and move numbers in a program. | — | — |
| `plc.m7.o2` | Choose data types and use arrays. | — | — |

## Module 8: Sequencing and state machines (coming-later)

| ID | The learner can | Taught in | Questions (check / practice) |
|---|---|---|---|
| `plc.m8.o1` | Program a machine as a sequence of steps. | — | — |
| `plc.m8.o2` | Design a state machine, and read an introductory SFC chart. | — | — |

## Module 9: Analog and process control (coming-later)

| ID | The learner can | Taught in | Questions (check / practice) |
|---|---|---|---|
| `plc.m9.o1` | Scale an analog signal to engineering units and raise alarms on it. | — | — |
| `plc.m9.o2` | Explain PID control and tune a loop. | — | — |

## Module 10: Structured programming (coming-later)

| ID | The learner can | Taught in | Questions (check / practice) |
|---|---|---|---|
| `plc.m10.o1` | Write logic in Structured Text. | — | — |
| `plc.m10.o2` | Organize a program into function blocks and routines. | — | — |

## Module 11: Troubleshooting (coming-later)

| ID | The learner can | Taught in | Questions (check / practice) |
|---|---|---|---|
| `plc.m11.o1` | Use diagnostics to find a fault. | — | — |
| `plc.m11.o2` | Find the fault in a machine with a wiring or device failure. | — | — |

## Module 12: Industrial systems (coming-later)

| ID | The learner can | Taught in | Questions (check / practice) |
|---|---|---|---|
| `plc.m12.o1` | Explain HMI concepts and basic industrial networking. | — | — |
| `plc.m12.o2` | Describe safety systems and how vendor dialects differ. | — | — |
