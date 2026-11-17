---
id: plc.m5.interlocks
title: Limit switches and interlocks: a garage door
minutes: 50
objectives: [plc.m5.o3]
builds-on: plc.m5.seal-in
example: u5.reversing-conveyor
exercise: u5.garage-door
reference: [interlocks, seal-in, sensors]
terms: [Interlock, Limit switch, Seal-in, Normally closed]
takeaways:
  - An interlock puts a normally closed contact of one output in the other's rung, so the two can never be on together.
  - A limit switch at the end of travel breaks the rung of the motion that reaches it.
  - Every rule that keeps people and the machine safe is one more contact in series on the rung it must stop.
---


# Limit switches and interlocks: a garage door

A garage door opener is two seal-in circuits, one per direction (MotorUp and MotorDown), with three extra rules:

1. **Limit switches.** Each direction stops on its own at the end of its travel. The top limit (OpenLimit) breaks the Up rung; the bottom limit (ClosedLimit) breaks the Down rung.
2. **An interlock.** Up and Down must never be on together: each rung has a normally closed contact of the other direction's output.
3. **The safety eye.** While something is in the doorway, the eye is blocked (EyeClear is FALSE), and the door must not come down. The Down rung needs EyeClear.

Stop (StopPB) stops either direction. Like the motor station's Stop, it is wired normally closed.

Try a limit switch on its own, on a barrier arm that rises while its button is held. Tick **keep pressed** under **RaisePB**: ArmUp comes on. Now press **TopLimit**, as the arm would when it reaches the top: ArmUp goes off although RaisePB is still pressed, and AtTop lights. The normally closed TopLimit contact has broken the rung.

[[try-ladder: programs/learn/m5-limit-switch.json]]

## How to build it

Start from your seal-in. The Up rung is:

*(OpenPB OR MotorUp) AND StopPB AND NOT OpenLimit AND NOT MotorDown*, then the MotorUp coil.

Write the Down rung the same way, with ClosePB, ClosedLimit, MotorUp and EyeClear.

The **Example** tab shows the interlock on a simpler machine: a conveyor motor that runs forward or in reverse. Press Forward, then Reverse while it runs: nothing happens until Stop. *A common mistake* (choose it in **Program**, above the machine) has no interlocks: try the same and read what the motor says. Your door adds the limit switches and the safety eye; test each rule on the Exercise tab as you go: open the door, close it, press Close while it is opening, and step into the doorway (the button beside the safety eye).

Then write it in the **Exercise** tab and press **Check my program**. One of the tests is hidden: it checks that the door is safe in a case the brief describes but the visible tests do not.
