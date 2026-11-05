---
id: plc.m7
number: 7
title: Data and math
status: draft
minutes: 200
prerequisites: [plc.m5]
outcomes:
  - plc.m7.o1: Compare numbers with EQ, NE, GT, GE, LT and LE (EQU, NEQ, GRT, GEQ, LES, LEQ on Allen-Bradley) to make decisions in a program.
  - plc.m7.o2: Choose a data type (BOOL, INT, DINT, REAL, TIME) for a value, and use an array with an index, checking the index before it is used.
  - plc.m7.o3: Calculate with ADD, SUB, MUL, DIV and MOD, and explain whole-number division, overflow and dividing by zero.
  - plc.m7.o4: Copy values with MOVE (MOV) to set presets, displays and recipes.
topics:
  - Compare (EQ, NE, GT, GE, LT, LE)
  - Math (ADD, SUB, MUL, DIV, MOD)
  - Move (MOVE)
  - Data types
  - Arrays
---

Until now every tag in your programs was on or off. Real machines also work with **numbers**: how many parts are in the box, how long a drum fills, which recipe the operator chose. This module is about the instructions that handle numbers.

- **Compare** boxes ask a question about two numbers (is the count at least the batch size?) and pass power when the answer is yes.
- **Math** boxes add, subtract, multiply and divide, and put the answer in a tag.
- **MOVE** copies a number from one place to another: a recipe's value into a preset, a count onto a display.
- **Data types** say what kind of number a tag holds, and **arrays** keep a whole table of numbers under one name.

The machines have two new parts: a **thumbwheel**, where the operator dials a number, and a **numeric display**, which shows a number the program sends it. Each item has an Example on one machine and an exercise on another, as in module 5. The counters from module 6 come back here, so do that module first if you can.
