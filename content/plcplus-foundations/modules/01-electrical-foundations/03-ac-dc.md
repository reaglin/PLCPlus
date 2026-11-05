---
id: plc.m1.ac-dc
title: AC and DC in a control panel
minutes: 30
objectives: [plc.m1.o3]
builds-on: plc.m1.series-parallel
quiz: quizzes/m1-ac-dc.json
reference: [ac-dc]
---


# AC and DC in a control panel

Open a control panel and you will usually find three kinds of power side by side. Knowing which is which tells you what each wire does and how dangerous it is.

## Direct and alternating current

- **Direct current (DC)** flows one way only. Its voltage is steady, and its two terminals are marked **+** and **−** (the − side is often called *common* or 0 V). Batteries and power supplies give DC.
- **Alternating current (AC)** changes direction many times a second. Its voltage rises, falls through zero, reverses and comes back, over and over. The power from the utility is AC, because AC is easy to step up and down with a **transformer**.

How often AC repeats is its **frequency**, measured in **hertz** (Hz): cycles per second. In the United States it is **60 Hz**; in much of the rest of the world it is 50 Hz. A motor or coil made for one may not work properly on the other, which is why the nameplate lists it.

## The three kinds of power in a panel

| Power | Typical use | Where it comes from |
|---|---|---|
| 480 V AC, three-phase | Motors, heaters | The building's supply, through a disconnect |
| 120 V AC, single-phase | Contactor and relay coils, the 24 V DC supply, lights, outlets | A **control transformer** that steps 480 V down to 120 V |
| 24 V DC | PLC inputs and outputs, sensors, pilot lights, small relays | A **DC power supply** that turns 120 V AC into 24 V DC |

Not every panel uses exactly these numbers (208 V and 240 V three-phase are common too), but the pattern is the same: high voltage for the work, low voltage for the control.

## Three-phase power for motors

Large motors use **three-phase** power: three AC wires (often labeled L1, L2, L3), each reaching its peak a third of a cycle after the one before. Three-phase turns a motor smoothly and needs less copper for the same power than single-phase. A 480 V three-phase motor connects to all three lines through the main contacts of its **contactor** (a heavy-duty switch closed by an electromagnet, its *coil*; module 3 shows how it works). The PLC never switches 480 V itself: it switches the contactor's coil, and the contactor switches the motor.

## Why control runs at a lower voltage

The circuits a person touches and works on most, the buttons, sensors and PLC wiring, run at 120 V AC or, more and more, 24 V DC, because:

- a lower voltage is far less likely to give a dangerous shock;
- small wires, buttons and sensors are cheaper and easier to fit;
- a fault in the control wiring does not put full motor voltage on the buttons.

Low voltage is *less* dangerous, not harmless. Treat every circuit as live until you have proved it is not (the next item).

## AC and DC on the PLC

PLC input and output cards are made for one kind of power. A 24 V DC input card must not be wired to 120 V AC: it can be destroyed. Read the card's label before wiring it. The PLC's own power and the I/O power may also be different: a PLC can run on 120 V AC and still have 24 V DC inputs.

## Reading a nameplate

Every motor, transformer and power supply has a **nameplate** with its ratings. On a motor, look for:

- **V**: the voltage it is wound for, such as 460 V (a 460 V motor runs on a 480 V supply);
- **Ph**: phases, 1 or 3;
- **Hz**: 60;
- **FLA**: full-load amps, the current it draws at full load, used to size its overload protection;
- **HP** and **RPM**: its power and speed.

A 24 V DC power supply's nameplate gives its output current, such as 5 A. Add up the current of everything on it (as in the last item) to make sure it is enough.

## Try it

Open the **Exercise** tab and answer its practice questions: match each kind of power to its use, and read a frequency and a nameplate. The Reference tab has AC and DC in a panel on one page.
