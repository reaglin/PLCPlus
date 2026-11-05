---
title: Set and reset coils (latching)
---

# Set and reset coils (latching)

| | Normal coil | Set coil `-(S)-` | Reset coil `-(R)-` |
|---|---|---|---|
| Power reaches it | tag TRUE | tag TRUE | tag FALSE |
| Power stops | tag FALSE | tag stays as it is | tag stays as it is |

Pair each set with a reset somewhere, or the tag never turns off. If both are powered in the same scan, the one on the **lower** rung wins (it runs last).

A **memory tag** is one not wired to anything: the program uses it to remember (Filling, MetalSeen). Type a new name and PLC+ declares it.
