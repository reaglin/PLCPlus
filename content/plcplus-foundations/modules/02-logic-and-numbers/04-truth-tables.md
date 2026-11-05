---
id: plc.m2.truth-tables
title: Truth tables for larger expressions
minutes: 40
objectives: [plc.m2.o4]
builds-on: plc.m2.and-or-not
quiz: quizzes/m2-truth-tables.json
reference: [boolean-logic]
---


# Truth tables for larger expressions

A real rung often has three or four conditions. A truth table shows what it does in every case, before you ever run the machine. This page shows how to build one, which part of an expression is worked first, one more operation (XOR), and how to turn a sentence into an expression.

## How many rows

Each input can be FALSE or TRUE, so every new input doubles the number of rows.

| Inputs | Rows |
|---|---|
| 1 | 2 |
| 2 | 4 |
| 3 | 2 × 2 × 2 = 8 |
| 4 | 2 × 2 × 2 × 2 = 16 |

To be sure no row is missed, list the rows in **counting order**: treat FALSE as 0 and TRUE as 1, and count in binary from 000 to 111. The last input changes on every row, the one before it every two rows, and the first input every four rows. PLC+ always lists rows this way, all FALSE first.

## Building one, step by step

Write the truth table of `(A OR B) AND NOT C`.

1. There are three inputs, so eight rows. Fill in A, B and C in counting order.
2. Add a column for each part, inside out. First `A OR B`, then `NOT C`.
3. Combine the parts: the result is TRUE only where both columns are TRUE (AND).

| A | B | C | A OR B | NOT C | Result |
|---|---|---|---|---|---|
| 0 | 0 | 0 | 0 | 1 | 0 |
| 0 | 0 | 1 | 0 | 0 | 0 |
| 0 | 1 | 0 | 1 | 1 | 1 |
| 0 | 1 | 1 | 1 | 0 | 0 |
| 1 | 0 | 0 | 1 | 1 | 1 |
| 1 | 0 | 1 | 1 | 0 | 0 |
| 1 | 1 | 0 | 1 | 1 | 1 |
| 1 | 1 | 1 | 1 | 0 | 0 |

Here 1 means TRUE and 0 means FALSE. PLC+ draws the same table with the words:

[[truth-table: (A OR B) AND NOT C]]

## Order of operations

In arithmetic, 2 + 3 × 4 is 14, because multiplying comes before adding. Logic has the same kind of rule:

1. **Brackets** first.
2. Then **NOT**, which applies only to the input (or bracket) just after it.
3. Then **AND**.
4. Then **OR** last.

So `A OR B AND C` means `A OR (B AND C)`, not `(A OR B) AND C`. The two give different answers. Look at the row where A is TRUE and B and C are FALSE:

[[truth-table: A OR B AND C]]

[[truth-table: (A OR B) AND C]]

The first is TRUE on that row; the second is FALSE. When in doubt, **write the brackets**. They cost nothing and make your meaning plain to the next person who reads the program.

## XOR: one or the other, not both

**XOR** (exclusive OR) is TRUE when its two inputs are different, and FALSE when they are the same.

[[truth-table: A XOR B]]

A PLC use: a tank has two level sensors at the same height, so they should always agree. `Level1 XOR Level2` is TRUE only when they disagree, which means one sensor has failed. That can raise an alarm. PLC+ works XOR after AND and before OR; brackets make it plain.

## From a sentence to an expression

Machine requirements come as sentences. Turn them into expressions one phrase at a time.

*The alarm sounds if the tank is high or the pump has failed, unless the alarm is silenced.*

1. Name each condition as a tag: High, PumpFault, Silenced.
2. "The tank is high or the pump has failed" is `High OR PumpFault`. It is one idea, so put it in brackets.
3. "Unless the alarm is silenced" means "AND NOT Silenced".
4. Together: `(High OR PumpFault) AND NOT Silenced`.

[[truth-table: (High OR PumpFault) AND NOT Silenced]]

Check the table against the sentence: every row with Silenced TRUE is FALSE, and every other row is TRUE when High or PumpFault is. Without the brackets, `High OR PumpFault AND NOT Silenced` would let a high tank sound the alarm even when silenced.

In ladder (module 5), this is High and PumpFault in parallel, then a normally closed contact of Silenced in series.

## Try it

Open the **Exercise** tab for six practice questions: three truth tables to fill in (choose TRUE or FALSE for each row), the number of rows, the order of operations, and a sentence to match to its expression. Press **Check my answers** when you have answered them all.
