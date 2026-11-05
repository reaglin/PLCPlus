---
id: plc.m2.bcd
title: BCD: binary-coded decimal
minutes: 30
objectives: [plc.m2.o2]
builds-on: plc.m2.binary-hex
quiz: quizzes/m2-bcd.json
reference: [bcd, number-systems]
---


# BCD: binary-coded decimal

Some devices on a machine think in decimal digits, one digit at a time. A **thumbwheel switch** on an operator panel lets someone dial a number such as 308, one wheel per digit. A **7-segment display** shows a count, one digit per window. These devices use **BCD**, short for *binary-coded decimal*.

## The rule

BCD gives **each decimal digit its own group of four bits**. The group holds that digit in binary, 0 to 9.

| Digit | BCD bits |
|---|---|
| 0 | 0000 |
| 1 | 0001 |
| 2 | 0010 |
| 3 | 0011 |
| 4 | 0100 |
| 5 | 0101 |
| 6 | 0110 |
| 7 | 0111 |
| 8 | 1000 |
| 9 | 1001 |

That is all of it. Ten digits, ten codes.

## Decimal to BCD, step by step

Write 406 in BCD.

1. Split the number into digits: 4, 0, 6.
2. Write each digit as four bits: 4 = 0100, 0 = 0000, 6 = 0110.
3. Put the groups side by side, in the same order: **0100 0000 0110**.

Compare plain binary: 406 = 256 + 128 + 16 + 4 + 2 = 1 1001 0110. The two are different. BCD is not "the number in binary"; it is "each digit in binary".

## BCD to decimal, step by step

Read 0111 0011 1001 from BCD.

1. Split the bits into groups of four from the right: 0111, 0011, 1001.
2. Read each group as one digit: 0111 = 7, 0011 = 3, 1001 = 9.
3. Write the digits in order: **739**.

A handy check: if you look at a BCD word in hex, the hex digits are the decimal digits. BCD 0010 0101 shows as `16#25`, and it means 25.

## The common mistake

If a program reads a BCD word as if it were plain binary, it gets the wrong number. BCD 0010 0101 means 25, but as plain binary it is 32 + 4 + 1 = **37**. A recipe dialed in as 25 seconds would run for 37. A PLC therefore converts a BCD input before using it, and converts a number to BCD before sending it to a display. On the lab's Allen-Bradley PLC these instructions are **FRD** (from BCD) and **TOD** (to BCD). IEC 61131-3 names them BCD_TO_INT and INT_TO_BCD.

## Thumbwheels and displays

Each thumbwheel digit has four wires, worth 1, 2, 4 and 8. Each wire goes to one PLC input. So three wheels (000 to 999) take **12 inputs**, and four wheels take 16: one whole input module.

A 7-segment display works the other way: four PLC outputs per digit carry the BCD code, and a decoder chip in the display lights the right segments.

## Why BCD wastes codes

Four bits have sixteen codes, 0000 to 1111. BCD uses only ten of them. The six codes **1010, 1011, 1100, 1101, 1110 and 1111** (10 to 15) are never valid BCD digits. If a PLC finds one, something is wrong: a broken wire, or a word that was never BCD.

The waste adds up. Sixteen bits in BCD hold 0 to 9,999. The same sixteen bits in plain binary hold 0 to 65,535. BCD is used because it is easy for simple devices, not because it is efficient.

## Try it

Open the **Exercise** tab for six practice questions: write numbers in BCD, read BCD back, spot a code that cannot be BCD, and count the inputs a set of thumbwheels needs. Type BCD answers in groups of four, such as `0010 0101`; the spaces are optional.
