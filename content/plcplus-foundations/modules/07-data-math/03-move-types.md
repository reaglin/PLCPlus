---
id: plc.m7.move-types
title: MOVE and data types
minutes: 40
objectives: [plc.m7.o4, plc.m7.o2]
builds-on: plc.m7.math
example: u7.order-display
exercise: u7.recipe-select
reference: [move, data-types, bcd]
---


# MOVE and data types

**MOVE** copies a value from one place to another. While power flows into it, it copies its **IN** (a tag or a typed number) into its **OUT** (a tag), on every scan. With no power it does nothing, and OUT keeps the last value copied. On Allen-Bradley it is **MOV** (Source and Dest).

That one idea does a lot of work:

- **Set a value**: *MOVE 8* into a counter's preset tag.
- **Show a value**: a numeric display shows only what the program sends it, so *MOVE C1.CV* into the display's tag shows the count.
- **Take a snapshot**: with a push button in front, MOVE copies only while the button is pressed, and the copy then stays put.
- **Choose between values**: two MOVEs into the same tag, each with its own contact, so only one runs.

## Two MOVEs into one tag

Rungs run top to bottom, and a tag holds whatever was written to it last. If two MOVEs with no contacts write to the same tag, both run on every scan and the **lower one always wins**. To choose, give each a condition that cannot be true at the same time as the other's: a normally closed contact of a switch on one rung, and a normally open contact of the same switch on the other.

## Data types

A tag's **data type** says what it can hold (module 4 introduced them):

| Type | Holds | Use it for |
|---|---|---|
| BOOL | TRUE or FALSE | buttons, lights, valves |
| INT | whole numbers, −32,768 to 32,767 | counts, thumbwheels, displays |
| DINT | whole numbers, about ±2.1 billion | large counts, totals that keep growing |
| REAL | numbers with a decimal point, such as 72.5 | temperatures, flows, averages |
| TIME | a length of time, such as T#5s | timer presets |

Choose the smallest type that always fits. A day's production of 50,000 parts does not fit an INT: use a DINT. A temperature of 72.5 needs a REAL. My program declares new number tags as INT; in these exercises every number fits. Allen-Bradley Logix controllers have no TIME type: a timer's preset (.PRE) is a DINT number of milliseconds.

## When the types differ

When MOVE (or a math box) writes into a tag of another type, PLC+ **converts** the value to that tag's type:

- a REAL into an INT is **rounded** to the nearest whole number (2.7 becomes 3; exactly .5 goes to the even number, so 2.5 becomes 2);
- a number too big for an INT **wraps round**, as in item 7.2, without any warning.

IEC 61131-3 PLCs ask you to write the conversion yourself, such as REAL_TO_INT; Allen-Bradley's MOV converts by itself, as PLC+ does. Either way, check that the number fits where it goes.

**BCD needs converting too.** A thumbwheel or display wired for BCD (item 2.2) sends each digit in its own four bits, so the bits mean something else as a plain number: BCD 25 is 0010 0101, which a plain INT reads as 37. A BCD display that is sent a code that is not a digit (1010 to 1111) cannot show it: PLC+ shows **Err**. Allen-Bradley converts with **FRD** (from BCD) and **TOD** (to BCD). PLC+ has no BCD instruction yet, so its exercises use plain thumbwheels and displays.

## How to do it

**First, the Example tab:** an order panel. Dial a quantity, press **Accept**, then turn the thumbwheel again: the display keeps the accepted number. **Clear** sets it to 0. Then choose *A common mistake* in **Program**, above the machine: its MOVE has no contact, so the display just follows the thumbwheel.

**Then the Exercise tab:** a dosing station with two recipes. Recipe A needs 5 scoops, recipe B needs 8. The Recipe switch chooses; the Scoops needed display shows the target; a counter counts scoops, with the target as its preset; Scoops added shows the count.

1. Rung 1: a **-|/|-** contact of RecipeB, then **Box ▾ ▸ Move and bit logic ▸ MOVE** with IN **5** and OUT **Target**. Delete the coil.
2. Rung 2: the same with a **-| |-** contact and 8.
3. A CTU for the scoops (**Box ▾ ▸ Counters ▸ CTU**). How many scoops should it count to, and which button clears it? Then make the **Scoops added** display show the count, and add a rung that lights Done.
4. Turn the switch, press Scoop and Reset, then press **Check my program**.
