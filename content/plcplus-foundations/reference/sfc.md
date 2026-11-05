---
title: Sequential Function Chart (SFC)
---

# Sequential Function Chart (SFC)

An IEC 61131-3 language for sequences. PLC+ has no SFC editor yet (planned later); Studio 5000 has one for ControlLogix and CompactLogix.

| Part | Drawn as | Means |
|---|---|---|
| Step | a box with a name | active while the machine is in it (.X TRUE; .T how long) |
| Initial step | a box with a double border | active when the PLC starts |
| Transition | a bar across the line, with a condition | fires when the step before is active AND the condition is TRUE |
| Action | a box beside a step, with a qualifier | what the step does |
| Selection branch | a split on a single line | one path is taken |
| Simultaneous branch | a split on a double line | every path runs at once, and they join at a double line |

When a transition fires, the step before goes inactive and the step after goes active.

| Qualifier | The action is |
|---|---|
| N | on while the step is active |
| S / R | set (stays on after the step) / reset |
| P | a pulse, once, as the step starts |
| D | on after a delay |
| L | on for a limited time |

**In ladder:** a step is a memory bit (initial step: starting value TRUE) or a step number; a transition is a rung that starts the next step from the step before AND the condition; N actions are coils driven by the steps.
