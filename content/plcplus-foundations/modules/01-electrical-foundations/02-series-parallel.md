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

[[see-example: 1]]

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

[[see-example: 2]]

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

## Practise

Open the **Exercise** tab and answer its practice questions: a series total, a parallel current, and the truth table for switches in series. The Reference tab has AND and OR with contacts, and Ohm's law, on one page each.

[[example: Two resistors in series]]

This is the Learn page's first worked example: two resistors, 200 Ω and 400 Ω, in series across a 24 V DC supply. The circuit starts there: the total is 600 Ω, the current **40 mA** through both, and the voltages **8 V** across R1 and **16 V** across R2, which add up to the 24 V.

1. **Make R2 bigger.** Its share of the 24 V grows, R1's shrinks, and the current through both falls: one path, one current.
2. **Make R1 and R2 the same.** Each gets half the supply, 12 V.
3. Tick **Open the switch**: one break anywhere stops the current through every part. That is why a broken wire in a series circuit turns everything off.

[[try-circuit: series 24V R1=200 R2=400]]

[[example: Three pilot lights and a relay coil in parallel]]

This is the Learn page's second worked example: three 24 V DC pilot lights, each drawing 25 mA (960 Ω), and a relay coil of 480 Ω, all wired in parallel across the same supply. The circuit starts there: each light draws **25 mA**, the coil **50 mA**, and the supply gives the total, **125 mA**.

1. **Move one light's resistance.** Only its own current changes; the coil and the other lights carry on, because each branch has the full 24 V across it.
2. Tick **Open** on a light, as if its bulb burned out: that light's current goes to 0, and the total drops by 25 mA to 100 mA. The rest keep working.
3. **Turn the supply up or down**: every branch's current changes together, and the total is still the branches added up.

[[try-circuit: parallel 24V Coil=480 Light1=960 Light2=960 Light3=960]]
