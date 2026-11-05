---
id: plc.m1.safety
title: Working safely: lockout/tagout
minutes: 30
objectives: [plc.m1.o4]
builds-on: plc.m1.ac-dc
quiz: quizzes/m1-safety.json
reference: [lockout-tagout]
---


# Working safely: lockout/tagout

**This page is an introduction only. It does not replace the safety training your lab or your employer gives you, and it does not qualify you to work on live equipment.** Follow their procedures and your instructor's directions every time.

## The hazards

Electricity in a panel can hurt you in three main ways:

- **Shock:** current passes through your body. About 50 mA (0.05 A, the current of the relay coil in item 1.1) crossing the chest can stop the heart, and 120 V can easily push that much through a person.
- **Arc flash:** a fault makes current jump through the air as a blast of light, heat and pressure. It can burn, blind and throw a person, even someone standing in front of the panel without touching anything.
- **Burns:** from the arc, from hot parts, or from current through the skin.

A machine adds its own hazards: a motor that starts while your hand is in a conveyor, air or hydraulic pressure, a raised load that falls, a spring that releases. These are why the whole machine, not just its wiring, must be made safe.

## The rules that apply

Two documents set the rules in US workplaces:

- **NFPA 70E**, *Standard for Electrical Safety in the Workplace*: how to judge shock and arc-flash risk, who is *qualified* to work near live parts, and what protective equipment they wear.
- **OSHA 29 CFR 1910.147**, *The control of hazardous energy (lockout/tagout)*: the federal regulation that requires an employer to have a written lockout/tagout program, train workers in it, and use it whenever someone services or maintains a machine where an unexpected start-up or release of energy could injure them.

## Lockout/tagout

**Lockout/tagout** (LOTO) makes sure a machine cannot start, or release stored energy, while someone is working on it. Each worker puts their **own lock** on the energy-isolating device, such as a disconnect switch, and keeps the only key. A **tag** with their name says who locked it and why. The machine cannot be turned on until every lock is removed.

The steps, in order:

1. **Prepare and notify.** Know every energy source of this machine (the machine's written procedure lists them). Tell the operators and anyone else affected that it is being shut down and locked out.
2. **Shut down** the machine the normal way, with its Stop controls.
3. **Isolate** it from every energy source: open the disconnect, close the air or hydraulic valves.
4. **Apply your lock and tag** to each isolating device.
5. **Release or block stored energy:** bleed air and hydraulic pressure, block parts that could fall or move, and let capacitors discharge.
6. **Verify zero energy.** Try the normal Start, with everyone clear, and check that nothing moves; then return the control to off. Test for voltage with a meter, using **live-dead-live**: test the meter on a known live source, test the circuit (it must read dead), then test the meter on the live source again. The last test proves the meter did not fail during the check.

Only when the verify step shows zero energy is the machine safe to work on. Removing the locks has its own steps, also set by the procedure: tools and guards back, everyone clear and told, and each person removes only their own lock.

## Things that are not a lockout

- **An emergency stop is not a lockout.** An e-stop stops the machine, but it is a control: a fault, a reset or another person can start the machine again. Never rely on it to keep you safe while you work.
- **A PLC output turned off, a program stopped, or a stop button pressed** is not isolation either. The power is still in the panel.
- **A tag on its own** is a warning, not a barrier. Use a lock wherever the device can take one.

## Live work

Never open or work in a live panel unless you are **qualified**, have been **trained** for that task, are **permitted** by your employer or instructor, and wear the protective equipment the task calls for. In PLC+ you never need to: the machines are simulated. In the lab, your instructor decides what may be done with power on.

## Try it

Open the **Exercise** tab and answer its practice questions: the LOTO steps in order, live-dead-live, and what an e-stop does and does not do. The Reference tab has the lockout/tagout steps on one page.
