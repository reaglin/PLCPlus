---
title: Cascading timers and counters
---

# Cascading timers and counters

One timer's or counter's output drives another's input, or resets one.

| Pattern | How |
|---|---|
| A flasher | T1 on *NOT T2.Q* times the on part; T2 on *T1.Q* times the off part; the light is on while *NOT T1.Q*. T2 done resets T1, which resets T2 |
| A chain of times | each TON starts on the one before it being done; the last one done resets the first (the traffic light) |
| Count of a timer | a timer that restarts itself every minute, counted by a CTU with PV 60: an hour |
| Count of a count | C1 counts items (PV 6) and is reset by its own Q; C2 counts C1.Q (PV 4) |
| Reset from two places | R takes one tag: make a memory tag from a branch, such as *PackReset = C1.Q OR CaseRemoved* |

- A counter reset by its own Q makes a **one-scan pulse** each time it finishes: exactly one count for the next counter.
- Rung order matters by one scan: a contact *above* a box reads the box's value from the last scan, one *below* it from this scan. If a cascade misbehaves, **Pause** and **Step one scan**.
- Allen-Bradley: the same with .DN (*XIO T2.DN* on T1's rung; *XIC Bottles.DN* then *RES Bottles* on its own rung).
