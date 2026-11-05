---
title: Coils
---

# Coils

| Symbol | Name | What it does to its tag | Allen-Bradley |
|---|---|---|---|
| `-( )-` | Coil | TRUE while power reaches it, FALSE when not | OTE |
| `-(/)-` | Negated coil | the opposite | (none) |
| `-(S)-` | Set (latch) | power turns it on, and it stays on | OTL |
| `-(R)-` | Reset (unlatch) | power turns it off | OTU |

Use **one** normal coil per tag. Two normal coils for the same tag fight: the last rung wins, and PLC+ warns you.
