---
title: Input and output modules
---

# Input and output modules

The path of a signal: **field device** → wire → **terminal** → **input point** (numbered from 0) → **tag** in the program. An output runs the other way.

| Word | Means |
|---|---|
| Discrete | On or off: TRUE or FALSE |
| Analog | A range of values (module 9) |
| Sourcing | Supplies current: switches +24 V out (PNP sensors) |
| Sinking | Takes current in and leads it to 0 V (NPN sensors) |

**One side sources, the other sinks:** a PNP (sourcing) sensor needs a sinking input; an NPN (sinking) sensor needs a sourcing input. Check the module's wiring diagram.

| Output | Switches | Notes |
|---|---|---|
| Relay | AC or DC | Slow; contacts wear |
| Transistor | DC only | Fast; sinking or sourcing |
| Triac | AC only | Small leakage when off |

- An output point carries a small current, so it drives a **contactor coil**, and the contactor switches the motor.
- Input LED lit: voltage at the terminal. Output LED lit: the PLC has told the point to turn on, not proof the device has power.
- Match the voltage: a 24 V DC module for 24 V DC devices, a 120 V AC module for 120 V AC devices.
