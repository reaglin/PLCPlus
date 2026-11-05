---
id: plc.m1.vir
title: Voltage, current and resistance
minutes: 30
objectives: [plc.m1.o1]
quiz: quizzes/m1-vir.json
reference: [ohms-law]
---


# Voltage, current and resistance

Electricity in a circuit is described by three quantities. Every later lesson uses them, so it is worth learning them well.

## The three quantities

- **Voltage** is the push that makes electricity move. It is measured *between two points*, such as the two terminals of a power supply. Its unit is the **volt** (V). A control panel often has 24 V DC for sensors and 120 V AC for relay coils.
- **Current** is how much electricity flows through a wire. Its unit is the **ampere**, or amp (A). Current flows only when there is a complete path, a *circuit*, from one side of the supply, through the load, and back to the other side.
- **Resistance** is how hard a part makes it for current to pass. Its unit is the **ohm** (Ω). A thick copper wire has almost none; a lamp or a relay coil (the electromagnet that closes a switch called a relay; module 3 shows how it works) has some; a dry plastic insulator has so much that almost no current passes.

A water pipe is a rough picture: voltage is the pressure, current is the flow, and resistance is how narrow the pipe is.

## Ohm's law

The three are linked by **Ohm's law**:

**V = I × R**

Here V is voltage in volts, I is current in amps, and R is resistance in ohms. Knowing any two, you can find the third:

- I = V / R
- R = V / I

## Units and prefixes

Control circuits use small currents and large resistances, so the numbers carry prefixes:

| Prefix | Means | Example |
|---|---|---|
| m (milli) | one thousandth | 100 mA = 0.1 A |
| k (kilo) | one thousand | 2.4 kΩ = 2400 Ω |
| M (mega) | one million | 1 MΩ = 1 000 000 Ω |

Change to plain volts, amps and ohms before you use Ohm's law, then change back.

## Worked example: a relay coil

A relay coil rated 24 V DC has a resistance of 480 Ω. How much current does it draw?

I = V / R = 24 V / 480 Ω = 0.05 A = **50 mA**.

A PLC output can supply only so much current (the data sheet says how much; 0.5 A per point is common for a 24 V DC output). 50 mA is well inside that, so the PLC can drive this coil directly.

## Power

**Power** is how fast energy is used, in **watts** (W): **P = V × I**. The coil above uses 24 V × 0.05 A = **1.2 W**. A 24 V DC pilot light that draws 25 mA uses 24 V × 0.025 A = 0.6 W. Adding up the watts of everything on a power supply tells you whether the supply is big enough.

## Open and short circuits

Two faults are worth naming now, because you will meet them again when you find faults:

- An **open circuit** is a break in the path: a cut wire, a loose terminal, a switch that does not close. No current flows, and the load gets no voltage. If the wire to a PLC input is broken, that input reads **FALSE** all the time, whatever the button or sensor does.
- A **short circuit** is a path with almost no resistance, where there should be a load. By Ohm's law, a tiny R makes a huge I. Fuses and circuit breakers are there to open the circuit before the wires overheat. A shorted switch makes its input read **TRUE** all the time.

## Try it

Open the **Exercise** tab and answer its practice questions: Ohm's law with real panel numbers, prefixes, and what an open wire does to an input. A wrong answer shows the right one and why. The Reference tab has Ohm's law on one page.
