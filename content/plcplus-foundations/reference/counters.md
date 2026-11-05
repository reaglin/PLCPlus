---
title: Counters
---

# Counters

A counter counts **edges**: its count changes once each time its input turns from FALSE to TRUE, however long the input stays on.

| Box | Counts | Q | Other inputs |
|---|---|---|---|
| CTU | up, on the rung's power (CU) | TRUE while CV is at least PV | R: count to 0 |
| CTD | down, on the rung's power (CD) | TRUE while CV is 0 or less | LD: count to PV |
| CTUD | up on the rung, down on its CD input | QU: CV at least PV; QD: CV 0 or less | R: to 0; LD: to PV |

- **PV** is the preset value; **CV** the count so far (an INT). A counter named C1 has **C1.Q** (or C1.QU, C1.QD) and **C1.CV**.
- A counter **does not stop at PV**: it keeps counting, and Q stays on.
- A counter **keeps its count** through a stop; only R (or LD) changes it.
- To show CV on a display: a **MOVE** box, IN C1.CV, OUT the display's tag.

## Allen-Bradley

| IEC (PLC+) | Allen-Bradley |
|---|---|
| PV | .PRE |
| CV | .ACC |
| Q | .DN (TRUE while .ACC is at least .PRE, for CTU **and** CTD) |
| R | a RES instruction on its own rung, naming the counter tag |
| CTUD | a CTU and a CTD on two rungs naming the **same** counter tag |

.OV and .UN show that the count went over or under its range.
