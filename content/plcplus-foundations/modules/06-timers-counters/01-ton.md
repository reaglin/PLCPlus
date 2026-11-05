---
id: plc.m6.ton
title: On-delay timers: a traffic light
minutes: 50
objectives: [plc.m6.o1]
example: u6.traffic-light
exercise: u6.traffic-light
reference: [timers]
---


# On-delay timers: a traffic light

*An early draft, so the traffic light can be tried; module 6 is written in a later phase.*

An **on-delay timer** (TON) waits. While its input IN is TRUE it counts time; when the time reaches its preset PT, its output Q turns TRUE. When IN goes FALSE, the timer resets: Q goes FALSE and the time goes back to 0.

The traffic light runs three timers in a chain: green for 5 s, then yellow for 2 s, then red for 5 s, and the last one restarts the first. Each timer is a **box** on a rung (Box ▾ ▸ Timers ▸ TON). A timer called T1 has the output **T1.Q**, which you can use on a contact like any other tag.

Run the **Example** and watch the timers' times count up beside the boxes. Then build it on the **Exercise** tab.
