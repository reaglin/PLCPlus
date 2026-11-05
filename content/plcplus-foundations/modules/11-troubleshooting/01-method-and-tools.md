---
id: plc.m11.method-tools
title: A method, and the tools to use
minutes: 45
objectives: [plc.m11.o1]
quiz: quizzes/m11-tools.json
example: u5.pump-start-stop
reference: [troubleshooting, diagnostic-tools]
---


# A method, and the tools to use

When a machine misbehaves, it is tempting to guess: change a part, change the program, try again. Guessing is slow, costs parts, and can hide the real fault. A technician follows a **method**, the same way every time.

## The method

1. **Observe the symptom.** What should happen, and what happens instead? Make it happen again on purpose, and watch.
2. **Check the outputs.** On the rungs, is the output's coil TRUE? If it is TRUE and the device does not move, the fault is on the output side (item 3). If it is FALSE, keep going.
3. **Check the inputs.** On the rungs, does each input's contact change when you work its button or sensor? An input that never changes, or that is TRUE when nothing is there, points at that input (item 2).
4. **Compare with what the program expects.** Read the rung that drives the output, contact by contact: which one does not pass power, and should it?
5. **Divide the problem.** Each check cuts the machine in half (program or machine, inputs or outputs) until one part is left.
6. **Fix it**, the smallest change that cures the cause, not the symptom.
7. **Test it.** Work the machine through everything it should do, not only the part that failed.
8. **Record it:** what you saw, what was wrong, what you changed.

## PLC+'s tools

Every machine in PLC+ has these tools; the Reference tab lists their twins on a real PLC.

- **Live values on the rungs.** Under every contact and coil is its tag's value, TRUE or FALSE. A contact that passes power is green and thick. This is your meter: it shows what the PLC *sees* and what it *sets*.
- **What every part is doing**, under the machine (open it). Each part's real state in words: "Pump P1: running", "Stop button: not pressed", "Photo-eye: senses a part in front of it". When a rung says one thing and this list says another, the fault is between the PLC and that part.
- **The wiring: which tag is which**, under the machine. Every input and output tag and what it means: "PumpStop (input): Stop push button (normally closed): TRUE while it is NOT pressed". Read it before deciding a value is wrong.
- **Pause / Run** stops and starts the simulated clock. **Step one scan** (while paused) runs exactly one scan, 10 ms, so you can see which rung changes first.
- **keep pressed**, under each push button, holds the button down for you while you step.
- **Reset** starts again from power-up: every tag off, the machine as it began. It also removes every force.
- **Forcing.** Right-click a contact or coil of a wired tag and choose **Force *Tag* ON** or **Force *Tag* OFF**. The PLC then uses that value whatever the machine does, and the rung shows FORCED in place of the value. Choose **Remove the force** to let the tag follow the machine again. Forcing an input asks "does the program work if this input does?"; forcing an output asks "does the device work if the PLC turns it on?"

On an exercise there are two more. **Check my program** runs the task's tests; when one fails, **Replay the failed test** runs it again on the machine, pressing the buttons itself, and stops just after the moment it failed; the results say *What happened in the test*, step by step. And the **Problems** list under My program names anything the PLC cannot run, such as a contact with no tag. The Problems list cannot see a program that runs but does the wrong thing: only you and the tests can.

## A word about forcing a real machine

A force can move a machine past the guards and interlocks the program checks. On a real machine, force only with permission and everyone clear, and remove the force when you are done. Allen-Bradley controllers light a **FORCE** light while forces are on, so the next person knows.

## Try it

On the **Example** tab is the pump station from module 5, running its working program. Start the pump, then:

1. Open **What every part is doing** and **The wiring: which tag is which**.
2. Press **Pause**, tick **keep pressed** under Stop, and press **Step one scan**: watch PumpStop turn FALSE and the Pump coil drop in that one scan. Untick it and press **Run**.
3. Right-click the **OverloadOK** contact and choose **Force OverloadOK OFF**: the pump stops, as if the overload had tripped, though the switch on the machine still says OK. Choose **Remove the force**, or press **Reset**.

Then answer the questions on the **Exercise** tab and press **Check my answers**.
