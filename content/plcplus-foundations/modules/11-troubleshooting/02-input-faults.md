---
id: plc.m11.input-faults
title: Input faults: wires, shorts and stuck sensors
minutes: 50
objectives: [plc.m11.o2, plc.m11.o1]
builds-on: plc.m11.method-tools
quiz: quizzes/m11-input-faults.json
example: u5.press-guard
reference: [fault-types, troubleshooting]
---


# Input faults: wires, shorts and stuck sensors

An input fault means the PLC sees something different from what the button or sensor is doing. The program may be perfect: it acts on what it is told, and it is being told wrong.

## The four kinds

| Fault | What the PLC sees | What you notice |
|---|---|---|
| **A broken wire** (open circuit) | the input is always FALSE | pressing the button or covering the sensor does nothing to its contact |
| **A short** (to the supply) | the input is always TRUE | its contact is TRUE with nothing there |
| **Two wires crossed** | the input follows the wrong device | its contact changes when you work another button or sensor |
| **A stuck sensor** | always TRUE, or always FALSE | the same as a short or a broken wire, but the fault is in the sensor itself |

A stuck sensor and a bad wire look exactly the same from the PLC. You tell them apart at the machine: a sensor has its own small light that shows what *it* thinks, and with the power safely isolated a meter can test the wire. In PLC+, **What every part is doing** plays the part of looking at the machine: it says what the part really does, and the rung says what the PLC sees.

## Why the wiring of Stop matters

A Stop button is wired **normally closed** so that its input is TRUE while all is well. If that wire breaks, the input goes FALSE, and the machine stops or will not start: it **fails safe**. But a short on a Stop input makes it TRUE for ever, and Stop no longer stops anything. That is why emergency stops are wired straight into the power circuit as well, never only through a PLC input.

## Finding an input fault

1. **Find the output that misbehaves**, and read its rung.
2. **Work each input on that rung, one at a time**, and watch its contact. Does it change when you press or cover it? Does it change when you work something else?
3. **Compare with The wiring: which tag is which.** A normally closed Stop that reads TRUE while not pressed is right, not a fault.
4. **Confirm with a force.** Force the suspect input to the value it should have. If the machine now works, the program is fine and the fault is in that input's device or wire.

## A worked example

A motor station's motor will not start. Start's contact turns green when the button is pressed, but the Stop contact is FALSE and stays FALSE, though nobody is pressing Stop. The wiring list says Stop is TRUE while it is NOT pressed, so FALSE means "pressed" to the PLC. Forcing Stop ON lets the motor start. The fault: the wire to the Stop button is broken, or the button's contact has failed open. The motor failed safe.

## Try it

On the **Example** tab is the press guard from module 5, working. Practise reading inputs: press each hand button alone and watch which contacts on rung 2 turn green; turn the Guard switch and watch the three rungs. Then right-click the **GuardShut** contact and choose **Force GuardShut ON** with the guard open: Ready lights and the press can close, with the guard open. That is what a shorted guard switch would do, and why safety guards are not trusted to one input alone. Choose **Remove the force**.

Then open the **Exercise** tab. Each question is a machine you know from module 5, running its working program with one real fault. Work it as the question says, watch the rungs, and choose what is wrong. Press **Check my answers**.
