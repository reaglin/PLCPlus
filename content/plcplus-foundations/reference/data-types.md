---
title: Data types
---

# Data types

| Type | Holds | Size | Use it for |
|---|---|---|---|
| BOOL | TRUE or FALSE | 1 bit | buttons, sensors, lights, valves |
| INT | −32,768 to 32,767 | 16 bits | counts, thumbwheels, displays |
| DINT | about −2.1 billion to 2.1 billion | 32 bits | large counts and totals |
| REAL | numbers with a decimal point (72.5) | 32 bits | temperatures, flows, averages |
| TIME | a length of time (T#5s, T#500ms) | | timer presets (IEC) |

- Choose the smallest type that always fits.
- A whole number too big for its type **wraps round** (INT: 32,767 + 1 = −32,768).
- REAL into INT is rounded to the nearest whole number (exactly .5 goes to the even one: 2.5 → 2).
- IEC asks for conversions to be written (REAL_TO_INT, INT_TO_REAL…); in PLC+ and on Allen-Bradley, MOV and math convert to the destination's type by themselves.
- Allen-Bradley Logix: BOOL, SINT (8 bits), INT, DINT, LINT (64 bits), REAL. **DINT is the usual whole number.** No TIME type: a timer's preset (.PRE) and time (.ACC) are DINT milliseconds.
- In PLC+, a new tag typed in a box is declared for you: a TIME in a timer's PT, a BOOL on a contact, a coil or a box's R, an INT anywhere else.
