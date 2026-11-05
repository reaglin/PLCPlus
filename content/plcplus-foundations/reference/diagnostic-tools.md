---
title: Diagnostic tools
---

# Diagnostic tools

| In PLC+ | What it shows or does | On an Allen-Bradley PLC |
|---|---|---|
| Live values on the rungs | each tag's value; a contact or coil with power is green and thick | online monitoring in Logix Designer: true instructions highlighted |
| What every part is doing | each part's real state, in words | the machine itself; a sensor's own light |
| The wiring: which tag is which | every I/O tag and what it means | the I/O list and the wiring drawings |
| Pause / Run, Step one scan | stop the clock; run one 10 ms scan | not on a running machine (a real process does not pause) |
| keep pressed | holds a push button down while you step | |
| Force *Tag* ON / OFF (right-click a contact or coil) | the PLC uses that value whatever the machine does; FORCED shows on the rung | I/O forcing; the controller's FORCE light is on while forces are enabled |
| Remove the force / Reset | the tag follows the machine again; Reset removes every force | remove and disable forces |
| Problems | what stops the program running (a missing tag, a wrong type) | Verify (errors and warnings) |
| Check my program, Replay the failed test | the task's tests; a failed test replayed on the machine | (your own test plan) |
| | every rung that uses a tag | Cross Reference |
| | a tag's value over time | Trends |

- An input module's lights show each input at its terminal; an output module's lights show each output the PLC is driving.
- A force bypasses what the program checks. Force only with permission, with everyone clear, and remove it when you are done.
