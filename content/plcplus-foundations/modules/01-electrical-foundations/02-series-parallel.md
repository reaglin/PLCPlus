---
id: plc.m1.series-parallel
title: Series and parallel circuits
minutes: 30
objectives: [plc.m1.o2]
builds-on: plc.m1.vir
quiz: quizzes/m1-series-parallel.json
reference: [series-parallel, ohms-law]
---


# Series and parallel circuits

Parts in a circuit can be joined in two basic ways. Which way they are joined decides how the current and the voltage are shared, and, for switches, what logic they make.

## Series: one path

Parts are in **series** when they are joined end to end, so there is only one path for the current. The same current flows through every part.

- **Current:** the same everywhere in the loop.
- **Voltage:** shared. The voltages across the parts add up to the supply voltage.
- **Resistance:** the total is the sum: R = R1 + R2 + …
- **A break anywhere** stops the current everywhere. Open one part and the whole loop goes dead.

### Worked example

Two resistors, 200 Ω and 400 Ω, are in series across a 24 V DC supply.

1. Total resistance: 200 Ω + 400 Ω = 600 Ω.
2. Current: I = 24 V / 600 Ω = 0.04 A = 40 mA, through both.
3. Voltage across the 200 Ω: 0.04 A × 200 Ω = 8 V. Across the 400 Ω: 0.04 A × 400 Ω = 16 V. Check: 8 V + 16 V = 24 V.

## Parallel: several paths

Parts are in **parallel** when each one is joined straight across the same two points, so each has its own path. This is how a panel is wired: every pilot light, coil and sensor connects between the + and − of the 24 V DC supply (or the two wires of 120 V AC).

- **Voltage:** the same across every branch: each gets the full supply voltage.
- **Current:** each branch draws its own current, and the supply provides the total: I = I1 + I2 + …
- **Resistance:** the total is *less* than the smallest branch. For two branches, R = (R1 × R2) / (R1 + R2).
- **A break in one branch** turns off only that branch; the others keep working.

### Worked example

Three 24 V DC pilot lights are wired in parallel. Each draws 25 mA. A relay coil of 480 Ω is also across the same supply.

1. Each light gets 24 V and draws 25 mA: 3 × 25 mA = 75 mA.
2. The coil draws 24 V / 480 Ω = 50 mA.
3. The supply provides 75 mA + 50 mA = **125 mA** in total.

## Side by side

| | Series | Parallel |
|---|---|---|
| Paths for current | One | One per branch |
| Current | Same through every part | Adds up across branches |
| Voltage | Shared between parts | Same across every branch |
| Total resistance | Sum of the parts | Less than the smallest branch |
| One part opens | Everything stops | Only that branch stops |

## Switches in series make AND

Wire two switches, A and B, in series with a lamp. The lamp lights only if A **and** B are both closed: one open switch breaks the only path. Write *closed* as TRUE and *lit* as TRUE:

[[truth-table: A AND B]]

A two-hand start on a press works this way: the operator must press both buttons, so both hands are clear of the tool.

## Switches in parallel make OR

Wire A and B in parallel, then to the lamp. The lamp lights if A **or** B (or both) is closed: either one gives the current a path.

[[truth-table: A OR B]]

Two Start buttons, one at each end of a conveyor, are wired this way.

This is the bridge to ladder logic. In a ladder program, contacts in series are AND and contacts side by side are OR, exactly as the switches here. You will meet that again in modules 2 and 3.

## Try it

Open the **Exercise** tab and answer its practice questions: a series total, a parallel current, and the truth table for switches in series. The Reference tab has AND and OR with contacts, and Ohm's law, on one page each.
