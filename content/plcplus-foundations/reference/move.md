---
title: MOVE
---

# MOVE

**MOVE** (IEC) or **MOV** (Allen-Bradley): while power reaches it, copies IN (a tag or a number) into OUT (a tag), on every scan. With no power, OUT keeps its last value. It passes power on.

| Job | Rung |
|---|---|
| Set a preset from a recipe | `-|/|- RecipeB` then MOVE 5 into Target |
| Show a count on a display | MOVE C1.CV into the display's tag |
| Take a snapshot | `-| |- AcceptPB` then MOVE OrderQty into Accepted |
| Clear a value | `-| |- ClearPB` then MOVE 0 into Accepted |

- Two MOVEs into one tag with no contacts: both run, and the **lower rung wins**.
- Writing into another type converts the value: a REAL into an INT is rounded; a number too big for an INT wraps round.
- A numeric display shows only what the program writes to its tag.
- BCD: convert before a BCD display (TOD on Allen-Bradley) and after a BCD thumbwheel (FRD). PLC+ has no BCD instruction yet.
- Allen-Bradley's **COP** copies a whole block of an array at once.
