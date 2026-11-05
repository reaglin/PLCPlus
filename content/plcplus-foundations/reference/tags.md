---
title: Tags
---

# Tags

A **tag** is a named value in the PLC: Start, Motor, T1.

| Kind | Example | Set by |
|---|---|---|
| Input | Start, HighFloat | the machine, at the start of each scan |
| Output | Motor, FillValve | the program; sent to the machine at the end of the scan |
| Memory | Filling, MetalSeen | the program only |

BOOL tags are TRUE or FALSE. Timers' presets are TIME (T#5s). Counters and math use INT or DINT numbers. The **wiring** list under each machine says which tag is which.
