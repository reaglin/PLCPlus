---
id: plc.m5.contacts-coils
title: Contacts and coils: a pilot light station
minutes: 40
objectives: [plc.m5.o1]
example: u5.press-guard
exercise: u5.pilot-lights
reference: [contacts, coils, series-parallel]
terms: [Contact, Normally open, Normally closed, Coil, Series, Parallel, Truth table]
self-test: quizzes/m5-contacts-coils-review.json
takeaways:
  - A rung reads tags with contacts on the left and writes one tag with a coil on the right.
  - A normally open contact passes power while its tag is TRUE; a normally closed contact passes power while its tag is FALSE.
  - Contacts in series make AND: power passes only through all of them. Contacts in parallel make OR: any one is enough.
  - A coil is TRUE only while its rung passes power, so it follows the contacts scan by scan.
---


# Contacts and coils: a pilot light station

Every rung you write has the same shape: **contacts** on the left that read tags, and a **coil** at the right-hand end that sets one.

- A **normally open** contact `-| |-` passes power while its tag is TRUE.
- A **normally closed** contact `-|/|-` passes power while its tag is FALSE.
- A **coil** `-( )-` makes its tag TRUE while power reaches it, and FALSE when it does not.

Put contacts one after the other (**in series**) and power must pass through all of them: that is **AND**. Put them side by side (**in parallel**, a branch) and power can pass through either: that is **OR**.

Try OR on a door chime. Hold **FrontDoor**: the chime sounds. Let go and hold **BackDoor**: it sounds again, because power has a second path through the branch. Rung 2 shows that a contact can read a coil's tag: ChimeLight follows Chime, scan by scan.

[[try-ladder: programs/learn/m5-door-chime.json]]

## The task

A station has two push buttons (PB1, PB2), a Mode switch and three lights. Light them like this:

- **Green** while Button 1 is pressed;
- **Amber** only while Button 1 AND Button 2 are both pressed;
- **Red** while the Mode switch is in **Manual**. Mode is TRUE in Auto and FALSE in Manual, so which kind of contact passes power when its tag is FALSE?

## How to do it

1. Open the **Example** tab: a press guard station worked out with the same ideas (series contacts for AND, a normally closed contact for NOT). Press the hand buttons, turn the Guard switch, and watch the rungs. *A common mistake* (choose it in **Program**, above the machine) puts the hand buttons in parallel: try one hand alone. Then write your own program for the pilot lights:
2. Open the **Exercise** tab. Your program starts with the station's tags and one rung. Click the contact: its **Tag** box (above the rungs) is ready for typing. Type **PB1** and press Enter. Click the coil, type **Green** and press Enter.
3. Add a rung (**New rung**) for each of the other lights. For Amber, add a second contact after the first (select the first contact, then **Contact**).
4. Press **Check my program**. Each test says what it expected and when.

When every test passes, this item is complete.
