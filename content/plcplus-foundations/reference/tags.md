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

| Type | Holds |
|---|---|
| BOOL | TRUE or FALSE |
| INT | whole number, -32,768 to 32,767 |
| DINT | whole number, about ±2.1 billion |
| REAL | number with a decimal point |
| TIME | a length of time, such as T#5s |

| Addresses | Input | Output | Memory bit |
|---|---|---|---|
| IEC 61131-3 | `%IX0.0` | `%QX0.1` | `%MX0.0` |
| Allen-Bradley Logix | `Local:1:I.Data.0` | `Local:2:O.Data.3` | a tag you create |
| Allen-Bradley SLC 500, MicroLogix | `I:1/0` | `O:2/3` | `B3:0/1` |

Name a tag for what it means (HighFloat), not where it is wired (Input3).
