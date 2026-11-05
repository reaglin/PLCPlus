---
id: plc.m11.timer-bug
title: Find and fix the bug: a timer reset every scan
minutes: 50
objectives: [plc.m11.o3, plc.m11.o1]
builds-on: plc.m11.seal-in-bug
example: u11.wash-rinse
exercise: u11.mixer
reference: [troubleshooting, timers]
---


# Find and fix the bug: a timer reset every scan

A machine that should move on after a few seconds, and never does, usually has a timer that never finishes. The timer's rung has power, yet its time (**ET**, elapsed time) stays at 0. Something is resetting it.

## How an on-delay timer resets

An on-delay timer (TON) counts while the power into it (IN) is on. The moment IN is off, it resets: ET goes back to 0 and Q (done) goes off. It does this **every time its box runs**, and the box runs once per scan, where it sits in the program.

So a timer resets every scan when:

- **its rung loses power every scan**, or flickers: for example, IN comes from a rising-edge contact, TRUE for one scan only; or
- **the same timer is used by two boxes.** One box, with power, starts it; the other, further down and without power, resets it, in the same scan. Next scan the same again. ET never gets past 0.

The second is the one in this item, and it comes from copy and paste: a rung with a timer box is copied for a second timing job, and the box's timer (T1) is not changed to a new one (T2).

## A worked example

A spray washer: Start runs the wash pump for 4 s, then the rinse pump for 3 s.

- rung 2: WashPump → TON **T1**, 4 s
- rung 4: RinsePump → TON **T1**, 3 s (copied from rung 2)

Press Start: the wash pump runs, rung 2 has power and starts T1. Rung 4 runs later in the same scan: the rinse pump is off, so its box has no power, and it resets T1. At the end of every scan T1's ET is 0. The wash never ends, and the rinse never starts.

The clue on the rungs: rung 2 is green right up to the box, but the box's ET does not climb. The fix: give the rinse its own timer. Delete the box on rung 4 and add a new TON box in its place (**Box ▾**): PLC+ gives a new box a timer of its own, T2, and you set its PT to T#3s. Then check every contact that reads a timer: rung 3, which ends the rinse, must read T2.Q, not T1.Q.

On an Allen-Bradley PLC the same mistake is two TON instructions with the same timer tag, and Cross Reference finds it. A second classic there is a RES (reset) instruction on a rung that is always true: it, too, resets the timer every scan.

## How to do it

1. **Example tab:** the spray washer. Choose *A common mistake* in **Program**, above the machine, press **Start**, and watch the two T1 boxes and their ET. Choose **Works** and try again: T1 counts the wash, T2 the rinse.
2. **Exercise tab:** a batch mixer that sounds a warning horn for 3 s before it mixes for 10 s. As it was given to you, it never gets past the horn. Press **Start**, watch the boxes, and find out why.
3. Fix it. The box to replace is the mixing timer's, on rung 5: first read its PT (T#10s). Select that box and press **Delete**; then, with the rung's contact selected, add a TON with **Box ▾ ▸ Timers ▸ TON** and type the same PT, **T#10s**, in its **PT** box above the rungs. The new box gets its own timer, such as T2. Now look at rung 4, the mixer's rung. It reads T1.Q twice: the contact that **starts** the mixer must still read the warning timer, T1.Q. The normally closed contact that **stops** it must read the new timer: click it and type **T2.Q** in its **Tag** box. Test the whole batch yourself: the warning, the mixing, the stop after 10 s, and Stop at each stage. Then press **Check my program**. One test is hidden.

The batch takes 13 s: press **Pause** and **Step one scan** to watch the moment the horn stops and the mixer starts.
