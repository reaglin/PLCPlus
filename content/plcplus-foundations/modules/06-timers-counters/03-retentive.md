---
id: plc.m6.retentive
title: Retentive timing: a pump's service light
minutes: 40
objectives: [plc.m6.o2]
builds-on: plc.m6.tof-tp
example: u6.bake-oven
exercise: u6.pump-service
reference: [tonr, timers]
---

# Retentive timing: a pump's service light

A TON forgets. The moment its IN goes off, ET goes back to 0. That is right for a start-up warning, but some jobs need the time **added up** over many runs:

- a pump's seals need a service after 500 hours of running, spread over hundreds of starts and stops;
- a part must be baked for 20 minutes in all, even if someone opens the oven door half-way;
- a machine's hour meter must show the total running time.

For these, the timer must **keep its time while IN is off**, and carry on from there when IN comes back. A timer that keeps its time is called **retentive**.

## The retentive timer, TONR

PLC+ calls it **TONR** (retentive on-delay); on the Allen-Bradley PLC it is **RTO** (retentive timer on). It works like a TON with two differences:

- when IN goes off, ET **stays** where it is; when IN comes back on, ET carries on from there;
- once ET reaches PT, Q turns on and **stays on**, even when IN goes off.

So nothing in IN can clear it. It has a second input for that: **R** (reset). While the tag in R is TRUE, ET goes back to 0 and Q goes off.

A TONR with PT T#10s. The pump runs from 0 to 4 s, stops, runs again from 6 s, and stops at 14 s:

| Time | IN | ET | Q |
|---|---|---|---|
| 2 s | TRUE | 2 s | FALSE |
| 4 s | FALSE | 4 s (kept) | FALSE |
| 5 s | FALSE | 4 s | FALSE |
| 6 s | TRUE | 4 s, counting again | FALSE |
| 12 s | TRUE | 10 s | **TRUE** |
| 14 s | FALSE | 10 s | TRUE (stays on) |

4 s plus 6 s make 10 s, so Q turns on at 12 s. A TON in the same place would have started again from 0 at 6 s, and finished at 16 s, *if* the pump were still running.

## Worked example: an oven that pauses

An oven must bake for 8 s in all. Opening the door turns the heat off, and shutting it carries on where it left off:

1. Heater = BakeSW AND DoorShut AND NOT T1.Q: heat with Bake on, the door shut, and the bake not done.
2. A TONR, T1, on the Heater, with PT T#8s and **ClearPB** in R.
3. DoneLight = T1.Q.

Open the door at 3 s and shut it at 5 s: T1 holds 3 s while the door is open, and Done lights at 10 s, after 8 s of heat.

## When must a timer keep its time?

Ask: *if the machine stops part-way, should the time so far still count?* A warning horn: no, every start needs the whole warning, so use a TON. Service hours, total baking time, the run time of a batch: yes, so use a TONR, and decide what clears it (a key switch for the maintenance technician, a button for the next batch).

On a real PLC a retentive timer also keeps its time when the PLC is switched off and on again, as long as its tag is retentive memory. That is why RTO is used for hour meters.

## Allen-Bradley

**RTO** has the same .PRE, .ACC, .DN, .EN and .TT as TON. It has no R input on the box: a separate **RES** (reset) instruction on another rung, naming the same timer tag, clears it. A rung such as *XIC ResetKey, RES Pump_Hours* does what R does in PLC+.

## How to do it

1. Open the **Example** tab: the oven. Turn **Bake** to On and watch T1's ET count. Turn **Oven door** to Open part-way: the heater goes off and ET stays put. Shut it again: ET carries on. Done lights after 8 s of heat; press **Clear** to clear it. Then choose *A common mistake* in **Program**, above the machine: it uses a TON. Open the door and watch ET go back to 0.
2. Open the **Exercise** tab: a pump with a **Service due** light. Write the pump's seal-in first (module 5). Then add **Box ▾ ▸ Timers ▸ TONR** on a rung of its own, with a contact of Pump before it, PT T#10s, and the right tag in **R**.
3. Press **Check my program**. One test runs the pump twice, 4 s and then 6 s, and expects Service due at the moment the total reaches 10 s.
