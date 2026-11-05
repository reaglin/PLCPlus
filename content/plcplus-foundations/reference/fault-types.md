---
title: Kinds of fault
---

# Kinds of fault

| Fault | What the PLC sees or does | How it shows | How to confirm |
|---|---|---|---|
| Broken wire to an input (open) | always FALSE | its contact never changes | force it ON: the machine works |
| Short on an input | always TRUE | its contact is TRUE with nothing there | the device's own light is off; meter |
| Two input wires crossed | the input follows another device | its contact changes when another device is worked | work each device in turn |
| Sensor stuck on or off | always TRUE or always FALSE | the same as a short or a broken wire | the sensor's own light; swap or test the sensor |
| Failed output device (motor, valve, lamp, cylinder) | the PLC turns the output on | coil TRUE, the device does nothing | force the output ON: still nothing |
| Program bug | inputs right, output wrong | the rung does not do what the task says | read the rung; step one scan |

- A **normally closed** Stop with a broken wire reads like a press: the machine stops or will not start. It fails safe. A short on it means Stop no longer stops: that is why an emergency stop is also wired into the power circuit.
- A failed device often shows up as something else that did not happen: a tank that never fills, a sequence that waits for a sensor for ever.
- **An output's tag being TRUE does not prove the device works.**
