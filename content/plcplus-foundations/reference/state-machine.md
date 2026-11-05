---
title: State machines
---

# State machines

- **State:** what the machine is doing now, with a name and a number. Always exactly one. It decides the outputs.
- **Transition:** from one state to another, when a condition is true. A state can have several ways out.
- **Stop or fault state:** entered from **any** state; left only by a deliberate action (Reset), to a state that makes the machine safe (close, drain), then idle.

## Design before ladder

1. A table of states: name, number, outputs on.
2. A table of transitions: from, to, when.
3. Check: every state can be left; every state leads back to idle; what each state assumes about the machine is true whenever it is entered.

## In ladder (State is an INT)

| Rung | Shape |
|---|---|
| A transition | EQ State from, the condition, MOVE to into State |
| From any state | the condition, MOVE into State, with no EQ, after the other transitions so it wins |
| An output | EQ State for each state that needs it, side by side, then the coil |

State 0 is where the PLC starts (an INT starts at 0), so make it the safe, idle state.
