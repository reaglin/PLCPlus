---
id: plc.m11.output-faults
title: Output and device faults
minutes: 40
objectives: [plc.m11.o2, plc.m11.o1]
builds-on: plc.m11.input-faults
quiz: quizzes/m11-output-faults.json
example: u5.reversing-conveyor
reference: [fault-types, troubleshooting]
---


# Output and device faults

An output fault is the other half of the machine: the PLC turns an output on, and nothing happens. The motor does not turn, the valve does not open, the lamp stays dark, the cylinder does not move.

## The lesson of this item

**An output's tag being TRUE does not prove the device works.** The coil on the rung shows what the program *asks for*. Between that and the moving part are the output terminal, a fuse, the wire, often a contactor or a relay with its overload, and the device itself. Any of them can fail, and the rung will still show TRUE.

So when an output's coil is TRUE and the machine does not respond, stop looking at the program and the inputs: they have done their job.

## What fails on the output side

- **A blown fuse** on the output module or the output circuit.
- **A broken wire** between the output terminal and the device.
- **A contactor or relay** that does not pull in, or an **overload** that has tripped (it protects the motor by opening the circuit).
- **The device itself:** a burned-out motor, a solenoid valve stuck shut, a lamp with a dead bulb, a cylinder that is jammed or has no air.

On a real PLC, the output module has a small light for each output. If the light is on, the PLC is driving the terminal, and the fault is further out. If the coil is TRUE but the module's light is off, suspect the output module or its fuse.

## Finding an output fault

1. **Work the machine until the output should come on**, and look at its coil. FALSE? Then it is not an output fault: go back to the inputs and the program (items 2, 4 to 6).
2. **TRUE, but What every part is doing says the device is stopped, closed or dark?** It is an output fault.
3. **Confirm with a force.** Force the output ON. If the device still does nothing, the PLC and the program are not the cause. (Forcing an output ON on a real machine moves it, so only with permission and everyone clear.)
4. **Then follow the circuit out:** the module's light, the fuse, the contactor, the overload, the device.

## Watch for knock-on symptoms

A failed device often shows up somewhere else first. If the fill valve of a tank fails, nobody sees the valve: they see a tank that never fills, and a high float that never trips. If a cylinder does not extend, a sequence waits for its "fully out" sensor for ever. Follow the method from the symptom back to the first thing that did not happen: that is where to look.

## A worked example

A reversing conveyor runs forward but not in reverse. Press Reverse: the Reverse light comes on, the Reverse coil on rung 2 is TRUE, and What every part is doing says the conveyor motor is stopped. RevPB, StopPB and the interlock all passed power, so the inputs and the program are fine. The light is lit by the same Reverse tag, so the PLC is asking for reverse. The fault is in the reverse contactor or its wiring.

## Try it

On the **Example** tab is the reversing conveyor from module 5, working. Run it forward, then stop it, and right-click the **Reverse** coil: choose **Force Reverse ON**. The motor runs in reverse, though the program did not ask it to, and the rung shows FORCED. That is how a technician checks an output: if the device moves when forced, it works. Choose **Remove the force**, or press **Reset**.

Then open the **Exercise** tab. Each question is a machine with one fault; the wrong answers include input faults, so rule them out. Work it, compare the rungs with **What every part is doing**, and choose what is wrong. Press **Check my answers**.
