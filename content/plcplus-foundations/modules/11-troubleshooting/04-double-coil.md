---
id: plc.m11.double-coil
title: Find and fix the bug: a double coil
minutes: 40
objectives: [plc.m11.o3, plc.m11.o1]
builds-on: plc.m11.output-faults
example: u11.doorbell
exercise: u11.compressor-alarm
reference: [troubleshooting, scan-cycle]
---


# Find and fix the bug: a double coil

Items 2 and 3 were faults in the machine: the program was right, and a wire or a device was not. Now the machine is fine and the **program** is wrong. Program bugs are found with the same method, and they leave the same kind of clue: the inputs are right, but an output is not what the task asks for.

From here on, My program on the Exercise tab does not start empty. It starts as someone else wrote it, with a bug in it. Your job is a technician's: make it fail, find the bug, fix it, and test the fix.

## The double coil

You met it in module 4: two rungs with a coil of the same tag. The PLC runs the rungs from top to bottom in every scan, and each coil sets its tag as its rung runs. So the **lower rung always has the last word**:

- rung 2: FrontPB → Bell
- rung 4: BackPB → Bell

Press the front button and rung 2 turns Bell on; two rungs later, in the same scan, rung 4 turns it off again, because BackPB is not pressed. At the end of the scan Bell is FALSE, and that is what goes to the output. The bell never rings from the front door.

How it gets there: a program grows. Someone adds "the back door rings the bell too" months later, on a new rung, without looking for the coil that is already there.

## How to spot it

- An output works from one condition and not from another, though both inputs read correctly on the rungs.
- Look at the coils: one is green (power reaches it), yet the value under it says FALSE. The value is the tag's value at the end of the scan, after the lower rung has had its say.
- On a real PLC, search for every place a tag is written: Allen-Bradley's **Cross Reference** lists them, and Logix Designer's **Verify** warns about a duplicate destructive bit reference. PLC+'s **Problems** list does not warn about it, so count the coils yourself.

## The fix

**One coil per output.** Put every condition that should turn it on onto one rung, side by side in a branch (OR):

- rung 3: (FrontPB OR BackPB) → Bell

Then delete the extra coil's rung. (Set and reset coils are the exception: they are meant to come in pairs, a set in one place and a reset in another.)

## How to do it

1. **Example tab:** a doorbell with two buttons. Choose *A common mistake* in **Program**, above the machine: that is the bug. Press each door button alone, watch the two Bell coils, and press **Pause** and **Step one scan** with **keep pressed** ticked under Front door. Then choose **Works** to see it fixed.
2. **Exercise tab:** an air compressor's alarms. My program is the program someone else wrote. First make it fail: press **Start**, turn **Temperature** to **Too hot**, and watch the horn and the rungs; turn it back, press Start again, and try **Oil level** on its own.
3. Find the bug, and fix it with the tools above the rungs: **Branch** adds a contact side by side with the selected one; **Delete** removes the selected element, or the whole rung when the rung is selected (click the words **Rung 5** above it).
4. Test it yourself, then press **Check my program**. If a test fails, **Replay the failed test** shows it on the machine.

If you want the program as it was given to you again, **Start again ▾** offers it.
