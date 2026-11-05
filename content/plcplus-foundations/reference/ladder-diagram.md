---
title: Reading a ladder diagram
---

# Reading a ladder diagram

- The **left rail** is power; the **right rail** is the return.
- Each **rung** is one circuit, read left to right: contacts, then a coil at the right-hand end.
- Contacts **in series** (one after the other) = **AND**. Contacts **in parallel** (a branch) = **OR**.
- A PLC runs the rungs **top to bottom**, once per scan.
- In PLC+, a part that passes power is drawn **green and thick** and says TRUE; one that does not is grey and thin and says FALSE.
