---
id: plc.m8.step-logic
title: Step logic: a stamping station
minutes: 60
objectives: [plc.m8.o1]
example: u8.transfer-arm
exercise: u8.stamping
reference: [step-logic, seal-in, set-reset]
---


# Step logic: a stamping station

A stamping station puts a mark on a part, always in the same order: clamp the part, bring the stamp down, lift the stamp, let the part go. Each of these is a **step**. The program must know which step the machine is in, and move to the next step only when the machine says the step is finished.

## Air cylinders take time

The clamp and the stamp are **air cylinders**: a rod that slides out when air is let in. A **solenoid** (an electric valve) lets the air in; the PLC turns it on with an output.

- A cylinder with **one solenoid** extends while its output is on, and a spring pulls it back when the output is off.
- A cylinder with **two solenoids** has one to push it out and one to pull it in. With neither on, it stays where it is.
- Two **sensors** on the cylinder say when it is **fully out** and **fully in**.

A cylinder takes time to move: the stamping station's clamp takes half a second. So the program must not bring the stamp down just because it told the clamp to go out. It waits for the **fully out** sensor, ClampIsOut: that is the machine saying "done".

## One memory bit per step

Give each step a **memory tag** of its own: Step1, Step2, Step3, Step4. Exactly one is on while the machine works, and none while it is idle. Each step's rung is a **seal-in** (module 5):

- a step **starts** when the step before it is on AND the sensor says the step before has finished;
- it **holds** itself on with its own contact, in parallel;
- it **ends** when the next step starts: a normally closed contact of the next step, in series. Stop ends it too.

For the transfer arm on the Example tab, step 2 (arm out) starts when step 1 (grip) is on and the gripper is fully closed:

*(Step1 AND GripIsClosed) OR Step2*, then *StopPB*, then *NOT Step3*, to the Step2 coil.

The first step starts from Start, but only when no step is on. The last step ends when its own sensor says it has finished (the arm is fully in), and the machine is idle again.

## The outputs come last

Write the outputs in rungs of their own, after the steps, **one rung per output**. Where an output is on in more than one step, put those steps side by side in a branch (OR). The gripper holds the part in steps 1 and 2: *Step1 OR Step2* drives GripClose.

## The worked example

| Step | What moves | It has finished when |
|---|---|---|
| 1 Grip | GripClose on | GripIsClosed |
| 2 Arm out | GripClose and ArmOut on | ArmIsOut |
| 3 Let go | everything off: the gripper springs open | GripIsOpen |
| 4 Arm back | ArmIn on | ArmIsIn: the cycle is over |

[[ladder: programs/m8-transfer-arm.json]]

With set and reset coils (OTL and OTU on Allen-Bradley) the same idea is: step 1 AND GripIsClosed **sets** Step2, and Step2 **resets** Step1. Either way works; just never leave a step without something to end it.

## How to do it

1. Open the **Example** tab: the transfer arm. Press **Start** and watch Step1 to Step4 take turns on the rungs while the cylinders move. Use **Pause** and **Step one scan** at the moment a sensor turns on to see the next step start and the one before end in the next scan. Then choose *A common mistake* in **Program**, above the machine: nothing ends step 1. Where does the arm stop, and why?
2. Open the **Exercise** tab: the stamping station. Write down its four steps first, as in the table above: what moves, and which sensor says it has finished.
3. Write one rung per step: type **Step1** on a coil and PLC+ declares it. Use **Branch** for the seal-in. Then one rung per output.
4. Turn **Part in place** to *Part in*, press **Start**, and watch. Then press **Check my program**. One test is hidden: it checks something the brief asks for that the visible tests do not.
