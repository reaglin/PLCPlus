---
title: Why Stop is checked with a normally open contact
---

# Why Stop is checked with a normally open contact

A Stop button is **wired normally closed**: its contacts are closed until it is pressed. So:

| Stop button | Its input tag | A normally open contact of it |
|---|---|---|
| not pressed | TRUE | passes power |
| pressed | FALSE | blocks power |
| wire broken | FALSE | blocks power: the machine stops (fails safe) |

The program uses a normally **open** contact: it passes power while all is well. Using a normally *closed* contact would stop the motor only while nobody is pressing Stop, which is backwards.

On the lab PLC this is XIC on the Stop input.
