---
title: Compare
---

# Compare

A compare box passes power on when power reaches it **and** its comparison is true. IN1 and IN2 are tags or typed numbers.

| IEC | Allen-Bradley | True when |
|---|---|---|
| EQ | EQU | IN1 = IN2 |
| NE | NEQ | IN1 ≠ IN2 |
| GT | GRT | IN1 > IN2 |
| GE | GEQ | IN1 ≥ IN2 |
| LT | LES | IN1 < IN2 |
| LE | LEQ | IN1 ≤ IN2 |

- In series: AND. *GE X 1* then *LE X 4* is true for 1 to 4 (a range). Allen-Bradley's **LIM** tests a range in one box.
- In parallel: OR. *LT X 1* beside *GT X 4* is true outside 1 to 4.
- A target reached: use **GE**, not EQ (EQ misses a count that goes past).
- "3 or fewer" is **LE 3**; "fewer than 3" is **LT 3**.
- A counter's count is **C1.CV** (.ACC on Allen-Bradley); a timer's time so far is **T1.ET**.
