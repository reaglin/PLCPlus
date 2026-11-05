---
title: PLC hardware
---

# PLC hardware

| Part | What it does |
|---|---|
| Power supply | Turns plant power (often 120 V AC) into the low DC voltage the PLC runs on |
| Processor (CPU, controller) | Runs the program, scan after scan |
| Memory | Holds the program and every tag's value; kept with the power off |
| Input modules | Connect buttons, switches and sensors |
| Output modules | Connect lights, valves and contactor coils |
| Rack (chassis) | Holds the modules in numbered slots |
| Communication ports | Connect the programming PC, a touch screen (HMI), a network |

- **Compact** (fixed) PLC: power supply, processor and a set number of points in one box. For small machines.
- **Modular** PLC: separate modules in a rack or row, chosen for the machine. Swap a failed module to repair it.
- **Download**: copy the program from the PC to the PLC. **Upload**: from the PLC to the PC. **Online**: watch it run.

Allen-Bradley examples: Micro800 (compact; Connected Components Workbench), CompactLogix (modular; Studio 5000 Logix Designer), and the older SLC 500 and MicroLogix (RSLogix 500).
