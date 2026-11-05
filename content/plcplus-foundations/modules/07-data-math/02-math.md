---
id: plc.m7.math
title: Math: add, subtract, multiply, divide
minutes: 45
objectives: [plc.m7.o3]
builds-on: plc.m7.compare
example: u7.packing
exercise: u7.inspection
reference: [math, data-types]
---


# Math: add, subtract, multiply, divide

A math box works out a sum and writes the answer into a tag. While power flows into it, it runs on every scan; with no power, it does nothing, and its output tag keeps its last value.

| IEC box | Allen-Bradley | Puts in OUT |
|---|---|---|
| ADD | ADD | IN1 + IN2 |
| SUB | SUB | IN1 − IN2 |
| MUL | MUL | IN1 × IN2 |
| DIV | DIV | IN1 ÷ IN2 |
| MOD | MOD | the remainder of IN1 ÷ IN2 |

**IN1** and **IN2** are tags or typed numbers; **OUT** must be a tag. A math box passes power on to whatever follows it, so several can sit in series and run one after another, left to right.

## Whole numbers drop the remainder

An INT holds only whole numbers. When DIV divides two of them, the part after the point is **dropped**, not rounded: 50 ÷ 12 is 4 (not 4.17), and 2 ÷ 3 is 0. **MOD** gives what was dropped: 50 MOD 12 is 2, because 4 boxes of 12 are 48 and 2 are left over. Allen-Bradley does the same when both numbers are whole.

This changes how you write a percentage. Rejects as a percentage of the total is rejects × 100 ÷ total. With 1 reject in 3 parts:

- **divide first**: 1 ÷ 3 = 0, and 0 × 100 = 0. Wrong.
- **multiply first**: 1 × 100 = 100, and 100 ÷ 3 = 33. Right (33.3, with the .3 dropped).

With whole numbers, multiply first, then divide.

## Dividing by zero

Nothing can be divided by 0. In PLC+, a DIV or MOD by 0 stops the PLC with a **math fault**: the program stops, every output goes off, and only Reset starts it again. PLCs differ here: some stop, and others set an overflow flag and go on with a wrong answer. Either way it is a bug, so **check before you divide**: put a compare in front, such as *GT PerBox 0*, so DIV runs only when there is something to divide by.

## Too big for an INT

An INT holds −32,768 to 32,767. A result bigger than that does not fit, and PLC+ does what a 16-bit INT does: it **wraps round**, so 32,767 + 1 gives −32,768. A count that suddenly goes negative is the sign. Use a DINT (up to about 2.1 billion) for numbers that can grow large; item 7.3 says more. My program declares new number tags as INT; in these exercises every number fits.

## Counting with ADD

ADD can count: *ADD GoodCount 1* into GoodCount adds one. But it adds one on **every scan** that power reaches it, and a press lasts many scans (one scan is 10 ms). Put a **rising-edge** contact in front (item 5.5), and it adds one per press.

## A worked example: packing

Parts are packed in boxes. One rung: *GT PerBox 0*, then *DIV Parts PerBox* into FullBoxes, then *MOD Parts PerBox* into LeftOver. 50 parts at 12 a box: 4 full boxes, 2 left over. A second rung, *EQ PerBox 0*, lights a warning when there is nothing to divide by.

## How to do it

**First, the Example tab:** the packing station. Turn the two thumbwheels with their − and + and watch the displays. Set Parts per box to 0: the warning lights and the PLC keeps running. Then choose *A common mistake* in **Program**, above the machine, and do the same: it has no check, so the PLC stops with a math fault. Read the message, then press **Reset**.

**Then the Exercise tab:** an inspection station. Count good parts and rejects, show the total, and show the rejects as a percentage.

1. Count the good parts. Click the rung's contact, type **GoodPB** in its **Tag** box, and choose **Rising edge -|P|-** in its **kind** box above the rungs. Then, with the contact selected, choose **Box ▾ ▸ Math ▸ ADD** and type IN1 **GoodCount**, IN2 **1**, OUT **GoodCount**. Click the coil and press **Delete**. Do the same on a new rung for rejects.
2. Total: one ADD with no contact in front: click a new rung's contact, add the box, then delete the contact and the coil.
3. The percentage needs a MUL and a DIV in series. Which goes first? The section on whole numbers says. And what must be true before the DIV may run? The section on dividing by zero says. A new box goes in after the selected element: click the rung's contact and add the first box, select that box and add the next, and so on; then delete the contact and the coil. MUL needs a tag to hold its answer: type a new name, such as Scaled, in its OUT, and PLC+ declares it as an INT.
4. Press **Check my program**.
