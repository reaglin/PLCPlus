---
id: plc.m7.arrays
title: Arrays: a table of values
minutes: 45
objectives: [plc.m7.o2]
builds-on: plc.m7.move-types
example: u7.shift-targets
exercise: u7.recipe-table
reference: [arrays, compare, move]
---


# Arrays: a table of values

A drum filler has four recipes, each with its own fill time. You could make four tags, FillTime1 to FillTime4, and four rungs to choose between them. An **array** does it better: one tag that holds a whole numbered list of values, all of the same type.

## Declaring an array

An array's type says how its elements are numbered and what each one holds:

`FillTime : ARRAY[1..4] OF TIME`

FillTime has four **elements**, numbered 1 to 4, and each is a TIME. You use one element by writing its number in square brackets: **FillTime[3]** is recipe 3's fill time, and works anywhere a TIME tag works. In PLC+, every element of an array starts at 0, so the program fills the table with MOVEs: *MOVE T#5s* into FillTime[3].

On Allen-Bradley, an array is declared with its size, such as a DINT[4], and its elements are **always numbered from 0**: FillTime[0] to FillTime[3]. IEC lets you choose the first number, as [1..4] does here. Keep the difference in mind in the lab.

## The index can be a tag

The number in the brackets is the **index**, and it can be a tag: **FillTime[Recipe]** means "the element whose number is in Recipe". Dial 3 on the Recipe thumbwheel, and FillTime[Recipe] is FillTime[3]. One rung now does the work of four:

*MOVE FillTime[Recipe]* into FillPT, and the fill timer uses FillPT as its preset.
## An index outside the array

What is FillTime[7]? It does not exist. Using it is a serious error: on a real PLC it could read or change some other value in memory. So PLC+, like an Allen-Bradley Logix controller, **stops with a fault** (index out of range) the moment the program uses an index outside the array. Every output goes off, and only Reset starts it again.

A thumbwheel can be turned to any digit, so **check the index before you use it**:

- two compares in series in front of the MOVE: *GE Recipe 1* and *LE Recipe 4*;
- in parallel, *LT Recipe 1* or *GT Recipe 4*, to light a Bad recipe lamp and keep the valve shut.

Allen-Bradley also has a **LIM** (limit test) box that checks a range in one instruction.

**Give a timer a copy, not the element.** A timer reads its PT on every scan, even while its rung is off. So the checked MOVE copies FillTime[Recipe] into FillPT, and the timer's PT is FillPT, which is always safe to read.

## A worked example: production targets

A board shows the target for the shift dialled on a thumbwheel: shift 1 makes 120 parts, shift 2 makes 100, shift 3 makes 80. *Targets : ARRAY[1..3] OF INT*; a rung of three MOVEs fills it; *GE Shift 1* and *LE Shift 3*, then *MOVE Targets[Shift]* into the display; any other number shows 0 and lights No such shift.

## How to do it

**First, the Example tab:** the target board. Turn the Shift thumbwheel with its − and +: 1, 2 and 3 show their targets, and 0 or 4 lights No such shift. Then choose *A common mistake* in **Program**, above the machine: it does not check the shift. Dial 4 and the PLC stops with an index fault. Read the message, then press **Reset**.

**Then the Exercise tab:** the drum filler. This exercise **starts with a program** that is nearly done: the table FillTime is declared and filled (2, 3, 5 and 8 s), and Start fills the drum. But it always uses recipe 1's time, and nothing checks the recipe number. Run it with recipe 3 and see. Then fix it:

1. Click the MOVE that sets FillPT, and change its IN to **FillTime[Recipe]**.
2. Put the two compares in front of that MOVE, so it runs only for recipes 1 to 4. A new box goes in *after* the selected element, so: with the MOVE selected, press **-| |- Contact** (a contact goes in front of it); click that contact and add **Box ▾ ▸ Compare ▸ GE**; with GE selected, add **LE**; then click the contact and press **Delete**.
3. Add a rung that lights **BadRecipe** for any other number: below 1 **or** above 4. Press **New rung**, type **BadRecipe** on its coil, then click its contact, choose **Box ▾ ▸ Compare ▸ LT**, and delete the contact. Now the second compare, side by side with the first: select the **LT** compare and press **Branch**: a second path appears with a contact on it, already selected. With that contact selected, choose **Box ▾ ▸ Compare ▸ GT**, then select the contact again and press **Delete**. Fill in each compare's IN1 and IN2. Then make sure Start does nothing with a bad recipe: what must the fill valve's rung check?
4. Try recipes 3, 7 and 0, then press **Check my program**. If you get lost, **Start again ▾** gives you the starting program back.
