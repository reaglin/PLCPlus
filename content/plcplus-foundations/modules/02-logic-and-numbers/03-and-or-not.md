---
id: plc.m2.and-or-not
title: AND, OR and NOT
minutes: 30
quiz: quizzes/m2-and-or-not.json
objectives: [plc.m2.o3]
reference: [boolean-logic, series-parallel, contacts]
---


# AND, OR and NOT

A PLC decides what to do by combining signals that are each either **TRUE** (on, 1) or **FALSE** (off, 0). A button is pressed or not. A guard is closed or not. A tank is full or not. This kind of on/off math is called **Boolean logic**, after George Boole, who wrote it down in the 1850s.

Three operations do almost all the work: AND, OR and NOT. Each one has a ladder-logic twin, which you will program in module 5.

## AND: every input must be TRUE

**AND** is TRUE only when all of its inputs are TRUE. If any input is FALSE, the result is FALSE.

*The press runs only if the left hand is on its button AND the right hand is on its button AND the guard is closed.* Lift one hand, or open the guard, and the press stops.

A **truth table** lists every combination of the inputs and the result for each. With two inputs there are four combinations, so four rows:

[[truth-table: A AND B]]

**In ladder:** normally open contacts, drawn `-| |-` (XIC on the Allen-Bradley PLC), one after the other, in **series**. A normally open contact passes power while its tag is TRUE. Power must pass through every contact to reach the coil, so every input must be TRUE. The two-hand press is three contacts in series:

[[truth-table: LeftHand AND RightHand AND Guard]]

Only the last row, where all three are TRUE, lets the press run.

## OR: at least one input must be TRUE

**OR** is TRUE when any input is TRUE, or more than one. It is FALSE only when every input is FALSE.

*The horn sounds if the tank level is high OR someone presses the Test button.*

[[truth-table: A OR B]]

**In ladder:** contacts side by side, in **parallel**. Power can take either path, so one TRUE input is enough. You will meet this again in module 3, where a motor keeps itself running with a contact in parallel with its Start button (the *seal-in*).

## NOT: the opposite

**NOT** has one input and turns it round: TRUE becomes FALSE, and FALSE becomes TRUE.

*The fill valve stays open while the tank is NOT full.*

[[truth-table: NOT A]]

**In ladder:** a **normally closed** contact, drawn `-|/|-` (XIO on the Allen-Bradley PLC). It passes power while its tag is FALSE. So a normally closed contact of TankFull, in front of the FillValve coil, opens the valve while the tank is not full.

## Putting them together

Real rungs mix all three. *The fill valve opens if Auto is on AND the tank is NOT full* is `Auto AND NOT TankFull`: a normally open contact of Auto in series with a normally closed contact of TankFull.

[[truth-table: Auto AND NOT TankFull]]

NOT is worked first. It applies only to the input just after it, here TankFull. The next item covers this order in full.

## How it is written

Different books and programs write the same three operations in different ways. They all mean the same thing.

| Operation | In PLC+ and IEC text | In textbooks | In ladder |
|---|---|---|---|
| AND | `A AND B` | A · B, or AB | contacts in series |
| OR | `A OR B` | A + B | contacts in parallel |
| NOT | `NOT A` | Ā (a bar over A), or A′ | a normally closed contact |

Careful: in a textbook A + B means OR, not addition. 1 + 1 is 1 in Boolean logic, because "TRUE OR TRUE" is just TRUE.

## Try it

Open the **Exercise** tab for five practice questions. In the three truth tables, choose TRUE or FALSE as the result of each row. Answer them all, then press **Check my answers** at the bottom. The last two ask how a press's contacts are wired, and when a normally closed contact passes power. The Reference tab has the three tables and their contacts on one page.
