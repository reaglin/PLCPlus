---
title: Arrays
---

# Arrays

An array is one tag that holds a numbered list of values of one type.

| | IEC (PLC+) | Allen-Bradley Logix |
|---|---|---|
| Declare | `FillTime : ARRAY[1..4] OF TIME` | `FillTime` of type `DINT[4]` |
| Numbered | from the first number you choose (here 1 to 4) | always from 0 (here 0 to 3) |
| One element | `FillTime[3]` | `FillTime[3]` |
| Index from a tag | `FillTime[Recipe]` | `FillTime[Recipe]` |

- An element works anywhere a tag of its type works.
- In PLC+, elements start at 0: fill a table with MOVEs.
- **An index outside the array stops the PLC** with an index-out-of-range fault (a major fault on a Logix controller). Check it first: *GE Recipe 1* and *LE Recipe 4* in front of the MOVE (or **LIM** on Allen-Bradley).
- Give a timer a checked copy (FillPT), not FillTime[Recipe]: a timer reads its PT on every scan, even when its rung is off.
- In PLC+ an array is declared in the program's tags; this module's exercise gives it to you in the program it starts with.
