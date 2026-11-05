---
title: Retentive timers (TONR, RTO)
---

# Retentive timers (TONR, RTO)

- **TONR** (PLC+) and **RTO** (Allen-Bradley) add up the time IN is on, over every run.
- IN goes FALSE: ET **stays** where it is. IN back on: ET carries on from there.
- ET reaches PT: Q turns TRUE and **stays** TRUE, even when IN goes off.
- Only the reset clears it: in PLC+ the tag in the box's **R** input; on Allen-Bradley a **RES** instruction naming the same timer tag, on its own rung.

| IN (PT T#10s) | ET | Q |
|---|---|---|
| on 0 to 4 s | 4 s | FALSE |
| off 4 to 6 s | 4 s (kept) | FALSE |
| on again from 6 s | 4 s + time since 6 s | TRUE at 12 s |
| off at 14 s | 10 s | TRUE (until R) |

**Use it when** the time so far must still count after a stop: service hours, total baking time, an hour meter. **Do not use it** when every start needs the full time again (a start-up warning): that is a TON.
