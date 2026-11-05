---
title: BCD
---

# BCD (binary-coded decimal)

Each decimal digit gets its **own four bits**, holding 0 to 9.

| Digit | BCD | Digit | BCD |
|---|---|---|---|
| 0 | 0000 | 5 | 0101 |
| 1 | 0001 | 6 | 0110 |
| 2 | 0010 | 7 | 0111 |
| 3 | 0011 | 8 | 1000 |
| 4 | 0100 | 9 | 1001 |

- **Decimal to BCD:** write each digit as four bits, in order. 406 → 0100 0000 0110.
- **BCD to decimal:** groups of four from the right, one digit each. 0111 0011 1001 → 739.
- **Never valid:** 1010, 1011, 1100, 1101, 1110, 1111 (10 to 15). Finding one means a fault.
- **Seen in hex,** a BCD word shows its decimal digits: BCD 25 is `16#25`.
- **Read as plain binary by mistake,** BCD 0010 0101 (25) becomes 37. Convert first.
- **Range:** 16 bits hold 0 to 9,999 in BCD, but 0 to 65,535 in binary.

| Device | PLC points |
|---|---|
| Thumbwheel switch | 4 inputs per digit (worth 1, 2, 4, 8) |
| 7-segment display with a BCD decoder | 4 outputs per digit |

| Convert | Allen-Bradley | IEC 61131-3 |
|---|---|---|
| BCD to a number | FRD | BCD_TO_INT |
| A number to BCD | TOD | INT_TO_BCD |
