---
id: plc.m6.ton
title: On-delay timers: a traffic light
minutes: 50
objectives: [plc.m6.o1]
builds-on: plc.m5.seal-in
example: u6.warning-horn
exercise: u6.traffic-light
reference: [timers]
---

# On-delay timers: a traffic light

A **timer** is a box on a rung that measures time. The PLC keeps the time itself, from its own clock, so a program can say "wait 3 seconds" without counting scans.

The most used timer is the **on-delay timer**, **TON**. It waits: when power reaches it, it starts timing, and only after a set time does it say "done". A TON has four values:

| Value | What it is |
|---|---|
| **IN** | the power into the box from the left: TRUE starts the timing |
| **PT** | the *preset time*: how long to wait, written like T#3s (3 seconds) or T#500ms |
| **ET** | the *elapsed time*: how long IN has been on so far; it stops at PT |
| **Q** | the output: turns TRUE when ET reaches PT, while IN is still on |

The most important rule: **when IN goes FALSE, a TON resets at once.** Q goes FALSE and ET goes back to 0, however far it had got. A TON only says "done" if IN stays on for the whole time.

## A timing diagram, in a table

A TON with PT T#3s. IN turns on at 1 s, off at 6 s, on again at 7 s and off at 8 s:

| Time | IN | ET | Q |
|---|---|---|---|
| 0 s | FALSE | 0 s | FALSE |
| 1 s | TRUE | 0 s | FALSE |
| 2.5 s | TRUE | 1.5 s | FALSE |
| 4 s | TRUE | 3 s | **TRUE** |
| 5 s | TRUE | 3 s (stops at PT) | TRUE |
| 6 s | FALSE | 0 s | FALSE |
| 7.5 s | TRUE | 0.5 s | FALSE |
| 8 s | FALSE | 0 s | FALSE (it never got to 3 s) |

Q turns on exactly PT after IN turns on: 1 s + 3 s = 4 s. PLC+ scans every 10 ms, so it is on time to the scan.

## Using the timer's output

A timer called T1 has members you use like tags: **T1.Q** on a contact (normally open passes power once the time is up; normally closed passes power *until* it is up) and **T1.ET** in a box that compares numbers (module 7).

**Worked example: a start-up warning.** A conveyor must sound a horn for 3 s before it moves, so nobody is caught by it:

1. A seal-in remembers Start: RunRequest = (StartPB OR RunRequest) AND StopPB.
2. RunRequest drives a TON, T1, with PT T#3s.
3. The horn sounds while RunRequest AND NOT T1.Q: during the 3 s.
4. The motor runs on T1.Q: after the 3 s.

Stop drops RunRequest, which resets the timer, so the next start gets the full 3 s again.

## Allen-Bradley

The lab's PLC has the same TON, drawn as a box with a **timer tag** (such as Warn_Timer). Its members have other names:

| IEC (PLC+) | Allen-Bradley | Meaning |
|---|---|---|
| PT | **.PRE** | the preset, in milliseconds: 3 s is 3000 |
| ET | **.ACC** | the accumulated time, in milliseconds |
| Q | **.DN** | done |
| IN | **.EN** | enabled: the rung is TRUE |
| | **.TT** | timer timing: enabled and not yet done |

So NOT T1.Q in the horn rung would be an XIO of Warn_Timer.DN, and T1.Q in the motor rung an XIC of Warn_Timer.DN.

## How to do it

1. Open the **Example** tab: the conveyor with the warning horn. Press **Start**, let go, and watch T1's ET count up beside its box; the motor starts when it reaches 3 s. Press **Stop** during the warning, then **Start** again. Then choose *A common mistake* in **Program**, above the machine: its timer is started by the Start button itself. Let go of Start and watch ET go back to 0.
2. Open the **Exercise** tab: the traffic light. With Run on: green 5 s, yellow 2 s, red 5 s, over and over. Use three TONs in a chain: each starts when the one before it is done, and the last one finishing restarts the first: how? Think about what resets a TON.
3. To add a timer, select the contact on a rung and choose **Box ▾ ▸ Timers ▸ TON**. The box gets its own tag (T1, then T2, …) and PT T#1s: type the time in its **PT** box above the rungs and press Enter. If the rung has a coil you do not need, select it and press **Delete**.
4. Each light's rung uses the timers' Q contacts. Press **Check my program**. The tests check *when* yellow and red come on, to within 0.05 s.
