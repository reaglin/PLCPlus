---
title: Glossary
---

A first set of key terms for modules 1–5 (2026-11-15), for the author to extend. Each term is a heading; an "Also:" line gives
the other ways the lessons write it; then the definition, in one or two sentences a beginner can use.

## PLC
Also: programmable logic controller
A programmable logic controller: an industrial computer that reads inputs (buttons, switches, sensors), runs a program over and
over, and sets outputs (motors, lights, valves).

## Scan
Also: scan cycle
One pass of the PLC's work: read every input, run the program from the first rung to the last, then write every output. A PLC
repeats it many times a second.

## Scan time
How long one scan takes, usually a few milliseconds. An input that changes and changes back within one scan can be missed.

## Ladder logic
Also: ladder diagram
A way of writing a PLC program that looks like a relay wiring diagram: two side rails with rungs between them.

## Rung
One line of a ladder program: contacts on the left that decide whether power reaches the output instructions on the right.

## Contact
An instruction that tests a tag: it passes power to the right when the tag's state is what it looks for.

## Normally open
Also: normally open contact, examine if closed, XIC
A contact (-| |-) that passes power while its tag is TRUE. On a real switch: open until it is pressed or operated.

## Normally closed
Also: normally closed contact, examine if open, XIO
A contact (-|/|-) that passes power while its tag is FALSE. On a real switch: closed until it is pressed or operated, which is why
Stop buttons are wired normally closed.

## Coil
Also: output coil, OTE
An output instruction (-( )-): it sets its tag TRUE while the rung passes power to it, and FALSE when it does not.

## Tag
A named piece of PLC memory, such as Start, Motor or Count, that instructions read and write. On an Allen-Bradley PLC, inputs and
outputs are reached through tags too.

## Input
A signal into the PLC from the machine: a push button, a switch or a sensor, wired to an input module.

## Output
A signal from the PLC to the machine: a motor starter, a light, a valve or a buzzer, wired to an output module.

## Series
Contacts one after another on a rung. Power passes only if every one passes it: that is AND.

## Parallel
Also: branch
Contacts side by side, on branches of a rung. Power passes if any one of them passes it: that is OR.

## Truth table
A table that lists every combination of the inputs and what the output is for each.

## Seal-in
Also: sealing contact, holding contact, seal-in circuit
A contact of the output itself, in parallel with the Start button, so that the output stays on after Start is released, until
Stop breaks the rung.

## Three-wire control
Start and Stop push buttons with a seal-in: the motor stays off after a power cut until someone presses Start again.

## Interlock
Logic that stops two things happening together, such as a normally closed contact of MotorDown in the MotorUp rung, so Up and
Down can never be on at once.

## Limit switch
A switch worked by the machine's movement, for example at the end of a door's travel, used to stop the motion there.

## Push button
A switch that is operated only while it is pressed. Its contacts can be normally open (Start) or normally closed (Stop).

## Relay
A switch worked by an electromagnet: current in its coil moves its contacts.

## Contactor
A heavy-duty relay that switches a motor's power.

## Overload relay
A device in a motor's circuit that opens when the motor draws too much current for too long, to protect the motor.

## Set coil
Also: latch coil, OTL
An output instruction that makes its tag TRUE when the rung passes power and leaves it TRUE afterwards, until a reset makes it
FALSE.

## Reset coil
Also: unlatch coil, OTU
An output instruction that makes its tag FALSE when the rung passes power, and leaves it FALSE.

## Rising edge
Also: positive edge, one-shot
A contact that passes power for one scan only, when its tag changes from FALSE to TRUE.

## Falling edge
Also: negative edge
A contact that passes power for one scan only, when its tag changes from TRUE to FALSE.

## Force
Setting an input or output to a value by hand, whatever the machine or the program says. Used carefully, for troubleshooting.

## Sinking
An input or output that connects the load to the negative (0 V) side of the supply.

## Sourcing
An input or output that connects the load to the positive side of the supply.

## Binary
Numbers written with only the digits 0 and 1, as a PLC stores them. Each digit is a bit.

## Bit
One binary digit, 0 or 1: the smallest piece of PLC memory, such as one input or a BOOL tag.

## BCD
Also: binary-coded decimal
Each decimal digit written as its own four bits, as thumbwheel switches and numeric displays use.

## Hexadecimal
Also: hex
Numbers in base 16 (0–9, then A–F). One hex digit is four bits, so hex is a short way to write binary.
