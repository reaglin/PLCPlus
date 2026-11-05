---
id: plc.m8.state-machines
title: State machines: a gate that can stop anywhere
minutes: 70
objectives: [plc.m8.o2]
builds-on: plc.m8.sequencers
example: u8.rinse-tank
exercise: u8.gate
reference: [state-machine, sequencer, timers]
---


# State machines: a gate that can stop anywhere

A sequence goes 1, 2, 3, 4 and round again. Real machines are not that tidy. An automatic gate is closing when a car arrives: it must open again. Someone presses Stop with the gate half open. The air pressure drops. A **state machine** handles all of this with three ideas:

- **States.** The machine is always in exactly one state, and each state has a **name** that says what the machine is doing: Closed, Opening, Open, Closing, Stopped. A state decides which outputs are on.
- **Transitions** (moves). Each one goes **from** one state **to** another, **when** a condition is true. A state may have several ways out.
- **A stop or fault state**, entered from **any** state, and one planned way back to idle.

## Design it as a table first

Before any ladder, write two tables. They are the design; the program is a copy of them. These are the gate's.

| State | Number | Outputs on |
|---|---|---|
| Closed (idle) | 0 | none |
| Opening | 1 | GateOpen |
| Open | 2 | none: the cylinder stays out; a 3 s timer runs |
| Closing | 3 | GateClose |
| Stopped | 9 | the Stopped light |

| From | To | When |
|---|---|---|
| Closed | Opening | Open pressed |
| Opening | Open | GateIsOpen |
| Open | Closing | open for 3 s |
| Closing | Closed | GateIsClosed |
| Closing | Opening | Open pressed |
| any state | Stopped | Stop pressed, or the air pressure low |
| Stopped | Closing | Reset, with Stop let go and the air OK |

Check the tables with questions: can every state be left? Is there a way back to idle from every state? What does each state assume about the machine? Closed assumes the gate **is** closed. After a stop the gate could be anywhere, so Reset goes to **Closing**, not straight to Closed; the gate closes first, and only then is the machine idle.

## From the tables to ladder

Keep the state as a number in an INT tag, State, as in item 8.2. Write the number's name in the tag's comment and in each rung's comment, so the next person can read it.

- **One rung per transition:** EQ for the state it leaves, the condition, MOVE the new number. *EQ State 3*, *OpenPB*, *MOVE 1 to State*.
- **"From any state"** has no EQ box: *NOT StopPB OR NOT AirOK*, *MOVE 9 to State*. Put it **after** the other transitions, so if two happen in the same scan, the stop wins.
- **One rung per output**, an EQ for each state that turns it on.
- A state machine starts in state 0 when the PLC starts, because an INT starts at 0: make 0 the safe, idle state.

## The worked example: a rinse tank with a leak alarm

| From | To | When |
|---|---|---|
| Idle (0) | Filling (1) | Start |
| Filling (1) | Draining (2) | the high float |
| Draining (2) | Idle (0) | below the low float |
| Filling or Draining | Idle (0) | Stop |
| any state | Leak alarm (8) | the leak detector is wet |
| Leak alarm (8) | Draining (2) | Alarm reset, with the floor dry |

The leak alarm is a **state**, not just a light. It stays until a person presses Alarm reset, even after the floor dries, and it leaves by draining the tank, the safe way back to idle.

## How to do it

1. Open the **Example** tab: the rinse tank. Press **Start**; while it fills, turn the **Leak detector** to *Wet*, then back to *Dry*. The **State** display stays at 8 until you press **Alarm reset**. Then choose *A common mistake* in **Program**, above the machine: it has no leak state. Try the same, and see what happens when the floor dries.
2. Open the **Exercise** tab: the gate. Its tables are above; write one rung per transition, then one per output. Add the 3 s TON (**Box ▾ ▸ Timers ▸ TON**) on a rung powered by *EQ State 2*. My program calls it T1 (PLC+ cannot rename timers yet), so the move from Open to Closing reads **T1.Q**.
3. Try every row of the transition table on the machine: Open; Open again while it closes; Stop halfway; Reset; the **Air pressure** switch. Then press **Check my program**. One test is hidden.
