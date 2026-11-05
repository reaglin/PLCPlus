---
id: plc.m2
number: 2
title: Logic and number systems
status: draft
minutes: 180
prerequisites: []
outcomes:
  - plc.m2.o1: Convert whole numbers between decimal, binary and hexadecimal, and read a PLC word (an input module, an INT tag) as its bits.
  - plc.m2.o2: Read and write a number in BCD (binary-coded decimal), as thumbwheel switches and displays use it.
  - plc.m2.o3: Describe AND, OR and NOT, and the contacts that make each: in series, in parallel, and normally closed.
  - plc.m2.o4: Write the truth table of an expression with up to four inputs, using the order of operations, brackets and XOR, and turn a sentence into an expression.
topics:
  - Binary and hexadecimal
  - BCD
  - Boolean logic: AND, OR, NOT
  - Truth tables, XOR and the order of operations
---

A PLC only knows two things: on and off. Every input it reads, every output it drives and every number it keeps is stored as a row of **bits**, each one either 1 (on, TRUE) or 0 (off, FALSE).

This module gives you the two ideas that sit under every PLC program.

- **Numbers as bits.** A 16-point input module reports its inputs as one 16-bit number. A fault code on a drive's display is written in hexadecimal. A thumbwheel switch on an operator panel sends a number in BCD. To read these, you need to move between decimal, binary and hexadecimal, and to know what BCD is.
- **Logic on TRUE and FALSE.** "The press runs only if both hands are on the buttons AND the guard is closed" is a logic expression. AND, OR and NOT are exactly what contacts in series, contacts in parallel and a normally closed contact do in a ladder rung.

You will use the number systems again in module 7 (data and math), and the logic in every rung you write from module 5 on. Each item ends with practice questions on its **Exercise** tab, and the module check has the questions that count.
