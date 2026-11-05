---
title: Number systems
---

# Number systems

**Binary** (base 2) uses 0 and 1; each place is worth twice the one to its right. **Hexadecimal** (base 16) uses 0–9 and A–F; one hex digit is four bits.

| Bit | 15 | 14 | 13 | 12 | 11 | 10 | 9 | 8 |
|---|---|---|---|---|---|---|---|---|
| Worth | 32768 | 16384 | 8192 | 4096 | 2048 | 1024 | 512 | 256 |

| Bit | 7 | 6 | 5 | 4 | 3 | 2 | 1 | 0 |
|---|---|---|---|---|---|---|---|---|
| Worth | 128 | 64 | 32 | 16 | 8 | 4 | 2 | 1 |

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

- **Decimal to binary:** subtract the largest place value that fits, again and again; or divide by 2 and read the remainders bottom up.
- **Binary to hex:** groups of four bits from the right, one hex digit each. **Hex to binary:** four bits per hex digit.
- **Hex to decimal:** each place is worth 16 times the one to its right: 1, 16, 256, 4096.

| Written as | Means | Also seen as |
|---|---|---|
| `2#1010` | binary 1010 = 10 | 1010₂, 0b1010 |
| `16#FF` | hex FF = 255 | 0xFF, FFh |
| `10#99` or `99` | decimal 99 | |

| Type | Bits | Holds |
|---|---|---|
| BOOL | 1 | FALSE or TRUE |
| SINT | 8 | −128 to 127 |
| INT | 16 | −32,768 to 32,767 |
| DINT | 32 | about −2.1 billion to 2.1 billion |

A 16-point input module is one 16-bit word: input 0 is bit 0. On the Allen-Bradley PLC, `Status.3` is bit 3 of the tag Status.
