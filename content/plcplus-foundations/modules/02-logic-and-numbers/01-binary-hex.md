---
id: plc.m2.binary-hex
title: Binary and hexadecimal
minutes: 40
objectives: [plc.m2.o1]
quiz: quizzes/m2-binary-hex.json
reference: [number-systems]
---


# Binary and hexadecimal

Every number in a PLC is stored as a row of **bits**. A bit is one on/off value: 1 or 0. Counting with only the digits 0 and 1 is called **binary** (base 2).

## Place values

In decimal, each place is worth ten times the place to its right: ones, tens, hundreds. In binary, each place is worth **two** times the place to its right. The places are numbered from 0, starting at the right.

| Bit | 7 | 6 | 5 | 4 | 3 | 2 | 1 | 0 |
|---|---|---|---|---|---|---|---|---|
| Worth | 128 | 64 | 32 | 16 | 8 | 4 | 2 | 1 |

To read a binary number, add up the places that hold a 1. For example, 1011 0010 has 1s in bits 7, 5, 4 and 1: 128 + 32 + 16 + 2 = **178**.

Binary numbers get long, so they are often written in groups of four bits, with a space or an underscore between groups. IEC 61131-3, the PLC programming standard, writes a binary number with `2#` in front: `2#1011_0010`. A textbook may write it as 10110010₂ instead.

## Decimal to binary, step by step

**Method 1: subtract place values.** Write 37 in binary.

1. The largest place value that fits in 37 is 32. Put a 1 in the 32 place. 37 − 32 = 5 is left.
2. 16 does not fit in 5: 0. 8 does not fit: 0.
3. 4 fits: 1. 5 − 4 = 1 is left.
4. 2 does not fit: 0. 1 fits: 1. Nothing is left.
5. Reading the places 32, 16, 8, 4, 2, 1: **100101**. As eight bits: `2#0010_0101`.

**Method 2: divide by 2 again and again.** Write down each remainder.

| Divide | Answer | Remainder |
|---|---|---|
| 37 ÷ 2 | 18 | 1 |
| 18 ÷ 2 | 9 | 0 |
| 9 ÷ 2 | 4 | 1 |
| 4 ÷ 2 | 2 | 0 |
| 2 ÷ 2 | 1 | 0 |
| 1 ÷ 2 | 0 | 1 |

Read the remainders from the **bottom up**: 100101. Both methods agree. Check it: 32 + 4 + 1 = 37.

## Hexadecimal: binary in short form

A group of four bits holds 0 to 15. **Hexadecimal** (hex, base 16) gives each of those sixteen values one digit: 0 to 9, then A to F for 10 to 15.

| Decimal | Binary | Hex |
|---|---|---|
| 0 | 0000 | 0 |
| 1 | 0001 | 1 |
| 2 | 0010 | 2 |
| 3 | 0011 | 3 |
| 4 | 0100 | 4 |
| 5 | 0101 | 5 |
| 6 | 0110 | 6 |
| 7 | 0111 | 7 |
| 8 | 1000 | 8 |
| 9 | 1001 | 9 |
| 10 | 1010 | A |
| 11 | 1011 | B |
| 12 | 1100 | C |
| 13 | 1101 | D |
| 14 | 1110 | E |
| 15 | 1111 | F |

**Binary to hex:** split the bits into groups of four from the right, and swap each group for its hex digit. `2#1011_0010` is 1011 = B and 0010 = 2, so it is **16#B2**. Check: 11 × 16 + 2 = 178, the same as before.

**Hex to binary:** swap each hex digit for its four bits. `16#3C` is 3 = 0011 and C = 1100, so it is `2#0011_1100`. In decimal: 3 × 16 + 12 = 60.

IEC writes hex as `16#FF`; you will also see 0xFF (in C and on many drives) and FFh. All three mean 255.

## Where you meet this on a PLC

- **An input module as a word.** A 16-point input module reports its inputs as one 16-bit number: input 0 is bit 0, input 15 is bit 15. If inputs 0, 1 and 3 are on, the word is `2#0000_0000_0000_1011`, which is 11, or `16#000B`.
- **Bits of a tag.** On the lab's Allen-Bradley PLC you can read one bit of an integer tag by its number: `Status.3` is bit 3 of the tag Status.
- **Status words and fault codes.** A drive or module often reports a word of status bits in hex. `16#0041` is `2#0000_0000_0100_0001`: bits 0 and 6 are on. The manual tells you what each bit means.

## Sizes of data

| Type | Bits | Holds |
|---|---|---|
| BOOL | 1 | FALSE or TRUE |
| SINT | 8 | −128 to 127 |
| INT | 16 | −32,768 to 32,767 |
| DINT | 32 | about −2.1 billion to 2.1 billion |

INT and DINT use their top bit for the sign (plus or minus); module 7 explains how. Counted without a sign, 16 bits hold 0 to 65,535, which is `16#FFFF`.

## Try it

Open the **Exercise** tab for six practice questions: write numbers in binary and hex, read them back in decimal, and read an input module's word. Type a binary answer with only 0s and 1s (spaces are fine: 0010 1101). Type a hex answer with 0–9 and A–F; 16#, 0x and a trailing h are accepted. Each answer tells you why, and shows the right one when yours is wrong. Use the Reference tab for the 0–15 table.
