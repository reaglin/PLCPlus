---
title: AC and DC in a panel
---

# AC and DC in a panel

**DC** (direct current) flows one way, at a steady voltage, from **+** to **−** (common, 0 V).

**AC** (alternating current) reverses direction many times a second. Its **frequency** is in hertz (Hz): **60 Hz** in the United States, 50 Hz in much of the world. A **transformer** steps AC voltage up or down.

| Power | Typical use | Comes from |
|---|---|---|
| 480 V AC, three-phase (L1, L2, L3) | Motors, heaters | The building's supply, through the main disconnect |
| 120 V AC, single-phase | Contactor and relay coils, lights, the DC supply | A control transformer |
| 24 V DC | PLC inputs and outputs, sensors, pilot lights | A DC power supply |

The PLC switches a contactor's **coil**; the contactor's main contacts switch the motor.

**Wire I/O only to the power it is made for.** A 24 V DC input card on 120 V AC can be destroyed.

| On a motor nameplate | Means |
|---|---|
| V | The voltage it is wound for (460 V runs on a 480 V supply) |
| Ph | Phases: 1 or 3 |
| Hz | Frequency: 60 |
| FLA | Full-load amps: the current at full load |
| HP, RPM | Power and speed |
