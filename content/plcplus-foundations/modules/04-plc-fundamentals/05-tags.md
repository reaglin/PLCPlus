---
id: plc.m4.tags
title: Tags and addresses
minutes: 30
objectives: [plc.m4.o5]
builds-on: plc.m4.scan-cycle
quiz: quizzes/m4-tags.json
example: u5.push-on-push-off
reference: [tags]
---


# Tags and addresses

Every value a program uses has a place in the PLC's memory. An **address** says *where* that place is: which module, which point. A **tag** gives it a *name* people can read. `Local:1:I.Data.0` is an address; **Start** is a tag. A program written with good tag names reads almost like the brief.

## Three kinds of tag

| Kind | What it holds | Who sets it | Example in PLC+ |
|---|---|---|---|
| Input | The state of an input point | The PLC, at the start of each scan | Start, HighFloat, Prox |
| Output | What an output point should do | The program; written out at the end of the scan | Motor, FillValve, Divert |
| Memory (internal) | A value only the program uses | The program | Filling, MetalSeen |

A memory tag is not wired to anything. It is the PLC's version of a spare relay: a place to remember something, such as "the tank is filling".

## Data types

Each tag has a **data type**: what kind of value it holds and how much memory it takes.

| Type | Holds | Example |
|---|---|---|
| BOOL | TRUE or FALSE (1 or 0): one bit | Start, Motor |
| INT | A whole number, −32,768 to 32,767 (16 bits) | A count of parts |
| DINT | A whole number, about ±2.1 billion (32 bits) | A large count, a time in ms |
| REAL | A number with a decimal point | 72.5 (a temperature) |
| TIME | A length of time (IEC) | `T#5s` for a timer's preset |

Every discrete input and output is a BOOL. Allen-Bradley's Logix controllers use DINT for most whole numbers, and their timers keep the preset as a DINT number of milliseconds instead of a TIME.

## Addresses on different PLCs

The same input point is written differently on different PLCs. These are examples of the forms you will meet:

| PLC | An input | An output | A memory bit |
|---|---|---|---|
| IEC 61131-3 | `%IX0.0` | `%QX0.1` | `%MX0.0` |
| Allen-Bradley Logix (CompactLogix) | `Local:1:I.Data.0` | `Local:2:O.Data.3` | a tag you create |
| Allen-Bradley SLC 500, MicroLogix | `I:1/0` | `O:2/3` | `B3:0/1` |

Read them like this:

- `%IX0.0` is an **I**nput, one bit (**X**), byte 0, bit 0. `%Q` is an output, `%M` memory.
- `Local:1:I.Data.0` is the module in slot 1 of this controller's own rack, its **I**nput data, bit 0. The exact form depends on the module, and the software shows it for you.
- `I:1/0` is the **I**nput file, slot 1, bit 0. `O:2/3` is the **O**utput file, slot 2, bit 3. `B3:0/1` is the **B**it file number 3, word 0, bit 1: a memory bit.

Micro800 controllers name their built-in points another way again, such as `_IO_EM_DI_00` (embedded digital input 0). On a Logix controller you give a point a readable name with an **alias** tag: Start, an alias for `Local:1:I.Data.0`.

## Good tag names

- Say what it *means*, not where it is wired: **HighFloat**, not Input3.
- For a signal wired normally closed, say which way is TRUE: **StopPB** with a comment "TRUE until pressed", or **EyeClear**.
- Be consistent: PB for push buttons, a noun for an output (**FillValve**).
- No spaces. Many PLCs do not allow them.

## Try it

Open the **Example** tab: one button that turns a work light on and off. Open **The wiring: which tag is which**. In PLC+ that list *is* the tag list for the machine's inputs and outputs: LightPB is an input and WorkLight an output. Pulse is a memory tag, so it is not in the list. Press the button and watch all three on the rungs. Pulse is TRUE for only one scan; to catch it, press **Pause**, tick **keep pressed** under the Light button, and press **Step one scan**: Pulse is TRUE. Press **Step one scan** again and it is FALSE, though the button is still held. (The `-|P|-` contact that does this is explained in 5.5.) Then answer the questions on the **Exercise** tab.
