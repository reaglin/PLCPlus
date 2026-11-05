---
id: plc.m7.compare
title: Compare numbers
minutes: 40
objectives: [plc.m7.o1]
builds-on: plc.m6.counters
example: u7.parts-low
exercise: u7.batch-counter
reference: [compare, data-types]
---


# Compare numbers

A contact asks a question about one BOOL: is it on? A **compare** box asks a question about two numbers, and passes power to the rest of the rung when the answer is yes. It works like a contact that looks at numbers.

## The six compares

Each box has two inputs, **IN1** and **IN2**: a tag or a number you type. The box tests IN1 against IN2.

| IEC box | Allen-Bradley | Passes power when |
|---|---|---|
| EQ | EQU | IN1 = IN2 (equal) |
| NE | NEQ | IN1 ≠ IN2 (not equal) |
| GT | GRT | IN1 > IN2 (greater than) |
| GE | GEQ | IN1 ≥ IN2 (greater than or equal) |
| LT | LES | IN1 < IN2 (less than) |
| LE | LEQ | IN1 ≤ IN2 (less than or equal) |

Like contacts, compares can be put **in series** (both must be true: AND) or **in parallel** (either one: OR). Two compares in series test a range: *GT Count 0* then *LT Count 10* passes power while the count is 1 to 9.

## Where the numbers come from

- **A thumbwheel** on the machine: the operator dials a number, and the PLC reads it as an INT tag (a whole number).
- **A counter's count.** A counter called C1 keeps its count in **C1.CV** (the current value; .ACC on Allen-Bradley). You can use C1.CV in a compare like any tag.
- A number you type, such as 0 or 10.

## A worked example: the parts bin

A bin beside a bench holds 10 parts. A down counter (CTD) keeps the number left: Refill loads 10, and each Part used counts down one. The operator dials **Warn at** on a thumbwheel. The rungs:

- **Parts low**: *LE C1.CV WarnAt* in series with *GT C1.CV 0*. Lit while there are some parts left, but no more than WarnAt.
- **Bin empty**: *LE C1.CV 0*.

Why LE 0 and not EQ 0? If someone presses Part used once too often, a down counter goes on to −1. EQ 0 would turn the light off just when the bin is most empty. LE 0 keeps it on.

## Choose < or ≤ with care

The most common compare mistake is to pick "less than" where you meant "less than or equal". With Warn at 3, *LT C1.CV 3* waits until only 2 parts are left: the warning comes one part late. Say the rule in words first ("lit when 3 or fewer are left"), and the words choose the box: "or fewer" is LE.

The same goes for equal. **EQ** is true at exactly one value, so a count that jumps past it, or keeps going, is missed. When a count reaches a target, use **GE**, not EQ.

## How to do it

**First, the Example tab:** the parts bin. Press **Refill**, then press **Part used** a few times and watch C1.CV go down on the rungs. Turn the **Warn at** thumbwheel with its − and + and see Parts low follow. Then choose *A common mistake* in **Program**, above the machine: it uses LT, and the warning comes one part late.

**Then the Exercise tab:** a batch counter. Each press of Part is one part into a box; Batch done lights when the count reaches the batch size on the thumbwheel, and stays lit if one more part comes; Filling batch is lit while the box has parts but is not full yet.

1. Rung 1 counts the parts. Type **PartPB** on the contact. With the contact selected, choose **Box ▾ ▸ Counters ▸ CTU**: the box goes in after the contact, and is called C1. Its values are shown above the rungs: type **ResetPB** in **R** and press Enter. This rung needs no coil: click the coil and press **Delete**.
2. **New rung** for Batch done. Click its contact and choose **Box ▾ ▸ Compare**: the compare goes in after the contact, in front of the coil. Then click the contact and press **Delete**, so the compare alone decides. Which compare, and which IN1 and IN2? The section above tells you.
3. One more rung for Filling batch. What two things must both be true while a batch is filling? Each is a compare; how do you make a rung need both?
4. Press Part, Reset and the thumbwheel's − and +, and watch. Then press **Check my program**.
