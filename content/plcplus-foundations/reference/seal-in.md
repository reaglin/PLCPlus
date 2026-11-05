---
title: The seal-in (holding) circuit
---

# The seal-in (holding) circuit

[[ladder: programs/motor-seal-in.json]]

*Motor = (Start OR Motor) AND Stop.* The Motor's own contact, in parallel with Start, keeps the motor running after Start is let go. Pressing Stop (Stop goes FALSE) breaks the rung.

[[truth-table: (Start OR Motor) AND Stop]]
