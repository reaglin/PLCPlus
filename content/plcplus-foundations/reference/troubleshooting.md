---
title: Troubleshooting: the method
---

# Troubleshooting: the method

1. **Observe the symptom.** What should happen, what happens instead? Make it happen again on purpose.
2. **Check the outputs.** Is the output's coil TRUE? TRUE while the device does nothing: an output fault. FALSE: keep going.
3. **Check the inputs.** Does each contact on that rung change when its device does, and only then?
4. **Compare with what the program expects.** Read the rung contact by contact: which one blocks the power, and should it?
5. **Divide the problem.** Program or machine; inputs or outputs; this rung or that. Halve it until one part is left.
6. **Fix** the cause with the smallest change.
7. **Test** everything the machine does, not only what failed. Remove every force.
8. **Record** what you saw, what was wrong and what you changed.

| What you see | Where to look |
|---|---|
| Coil TRUE, device does nothing | the output side: module light, fuse, wire, contactor, overload, device |
| A contact never changes, or is TRUE with nothing there | that input: its device, wire or terminal |
| A contact changes when another device is worked | crossed wires |
| Inputs right, coil wrong | the program: read its rungs |
| Works from one condition, not another | a double coil: count the coils of that tag |
| A machine that should move on never does | a timer that never finishes: is its ET climbing? |

Safety first: on a real machine, isolate and lock out before you touch wiring (module 1), and force only with permission and everyone clear.
