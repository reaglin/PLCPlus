---
id: plc.m11.seal-in-bug
title: Find and fix the bug: a seal-in on the wrong tag
minutes: 40
objectives: [plc.m11.o3, plc.m11.o1]
builds-on: plc.m11.double-coil
example: u11.fan-light
exercise: u11.two-pumps
reference: [troubleshooting, seal-in]
---


# Find and fix the bug: a seal-in on the wrong tag

A seal-in holds a coil on after its Start button is let go: a contact **of the coil's own tag**, in parallel with Start (module 5). The contact must read the same tag as the coil it holds. If it reads another tag, the program still runs, the Problems list stays empty, and the machine does two strange things at once.

## How it happens

Copy and paste. A machine has two motors that work the same way, so the programmer writes the first rung, copies it, and changes the tags for the second motor, all but one:

- rung 1: (FanStart OR **Fan**) AND FanStop → Fan
- rung 2: (LightOn OR **Fan**) AND LightOff → WorkLight

Rung 2's seal-in contact still reads Fan.

## The two symptoms

Read rung 2 as the PLC does:

1. **The light does not stay on.** Press Light on: WorkLight turns on. Let go: LightOn is FALSE, and the seal-in contact reads Fan, which is off. Nothing holds the light, so it goes out. It behaves like a light with no seal-in at all.
2. **The light comes on by itself.** Start the fan: Fan is TRUE, so rung 2's seal-in contact passes power, and the light comes on though nobody pressed Light on. Light off turns it off only while held.

Neither symptom says "wrong tag" by itself. Together, and with the rung's live values in front of you, they do: the contact beside LightOn turns green when the fan starts, and that is the clue.

## Reading a rung contact by contact

This is step 4 of the method, *compare with what the program expects*, done slowly:

1. Pick the output that misbehaves, and find its rung (only one, after item 4!).
2. For each contact, say aloud which tag it reads and why it is there: "LightOn starts it; this one holds it on, so it must be WorkLight; LightOff stops it."
3. Watch each contact's value while you work the machine, and find the one that is green when it should not be, or dark when it should be green.

## The fix

Change the seal-in contact's tag to the coil's own tag. Click the contact, type the right tag in its **Tag** box, and press Enter. One word, but test everything afterwards: each pump or motor on its own, each Stop, and both together.

## How to do it

1. **Example tab:** a workshop fan and light. Choose *A common mistake* in **Program**, above the machine: press Light on and let go; then start the fan and watch the light. Choose **Works** to see the fix, then try the same.
2. **Exercise tab:** a two-pump station, as someone else wrote it. Make it fail first: start pump 1 alone and watch pump 2; then press Pump 2 start and let go. Read rung 2 contact by contact.
3. Fix the bug, test each pump and each Stop yourself, and press **Check my program**. One of the tests is hidden: it checks that each Stop stops only its own pump.

If you change too much, **Start again ▾** gives you the program as it was given to you, and **Undo** takes back the last change.
