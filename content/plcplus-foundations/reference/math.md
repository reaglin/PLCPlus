---
title: Math
---

# Math

While power reaches it, a math box works out IN1 and IN2 and writes the answer to OUT (a tag), on every scan. It passes power on, so boxes in series run left to right.

| IEC | Allen-Bradley | OUT |
|---|---|---|
| ADD | ADD | IN1 + IN2 |
| SUB | SUB | IN1 − IN2 |
| MUL | MUL | IN1 × IN2 |
| DIV | DIV | IN1 ÷ IN2 |
| MOD | MOD | the remainder of IN1 ÷ IN2 |

- **Whole numbers drop the remainder:** 50 DIV 12 = 4, 50 MOD 12 = 2, 2 DIV 3 = 0. For a percentage, multiply first: `part × 100 ÷ total`.
- **Divide by zero:** in PLC+, DIV or MOD by 0 stops the PLC with a math fault (outputs off; Reset to restart). Check first: *GT X 0* in front of the DIV.
- **Overflow:** an INT holds −32,768 to 32,767. A bigger result wraps round: 32,767 + 1 = −32,768. Use a DINT for large numbers.
- **Counting with ADD:** a rising-edge contact (-|P|-) in front, or it adds on every scan.
- Allen-Bradley also has **CPT** (compute), which works out a whole expression in one box.
