# PLC+ — a guide for instructors reviewing the preview

Thank you for looking at PLC+. This is an **early preview**. We are asking PLC instructors to judge the **flow** of the
software and whether its exercises teach the right things, before the rest of it is built. Nothing here is final. Your
notes will shape what comes next.

## What PLC+ is for

PLC+ is a free Windows program for the **home, self-paced half of a PLC course**; the lab, with real PLCs, is the other half.
A learner moves from electrical and logic basics to ladder programming. They practise on **simulated machines** instead of
hardware, and each exercise is **checked automatically by what the program does**, not by how it looks. At set points the
learner saves a **PDF progress report** and hands it in through the instructor's LMS. Everything works offline. Several
students can share one computer, each with a PIN-protected progress file they can carry to another machine.

## What this preview shows, and what it does not

**It shows:**
- the PLC engine running ladder programs, with power flow drawn live;
- six simulated machines;
- a challenge for each machine, with two programs: one that works, and one with a mistake students commonly make;
- automatic checking with hints, and a plain-words replay of what went wrong.

**It does not have yet:**
- the lessons;
- the ladder editor (so you cannot write your own program yet);
- learner progress;
- the PDF reports.

Those come next, and your notes decide their shape.

The plan for each topic (module): **Learn** (the lesson), **Assess** (the challenges), and **Reference** (the instructions
and ideas, to look things up). Each module opens with its prerequisites, its learning outcomes and its topics.

## Running it

Unzip the folder you were sent and open **PLCPlus.exe**. Keep the `content` folder beside it. Windows may say it does not
recognise the program, because this preview is not yet signed; choose **More info**, then **Run anyway**. PLC+ makes no
internet connection.

Press **F1** at any time for help on the screen.

## The screen

- **Top:**
  - **Challenge** picks the machine and its task.
  - **Program** picks the program the PLC runs: *Works*, or *A common mistake*.
- **Left:** the program as ladder rungs, running live. A contact or wire that passes power is **green, thick and shows
  TRUE**; one that does not is grey, thin and shows FALSE.
- **Right:** the machine. Hold a push button down to press it, or tick its *keep pressed* box. Switches stay where you turn
  them. Every part says what it is doing in words.
  - **What every part is doing** lists the state of every part.
  - **The wiring** lists every input and output tag and what it means.
- **Pause / Step one scan / Reset:** the PLC runs on a simulated clock, 10 ms per scan, so a program and a test always give the
  same result. Step through one scan at a time to watch the order rungs run in.
- **Bottom: Check this program.** This runs the challenge's tests much faster than real time. Each test presses buttons at set
  times and watches the machine. A failed test:
  - says what should have happened, and when;
  - gives a hint for that test;
  - replays what actually happened, moment by moment.

  Some tests are **hidden**, so a program cannot be fitted to the visible ones.

## The six challenges — what to try

| # | Challenge | Try this | The common mistake |
|---|---|---|---|
| 1 | **Light the pilot lights** | Press Button 1, then both buttons; turn Mode to Auto | Red uses a normally open contact, so it lights in Auto instead of Manual |
| 2 | **Start and stop a motor** | Press Start and let go; press Stop. Note Stop is wired normally closed | No seal-in: the motor runs only while Start is held |
| 3 | **Run a traffic light** | Turn Run on and watch a full cycle (green 5 s, yellow 2 s, red 5 s); step through the change from red to green | Yellow is never turned off, so yellow and red are on together |
| 4 | **Open and close a garage door safely** | Open, Close, Stop; while it closes, choose *Step into the doorway* | The down rung does not check the safety eye: the door comes down on whatever is in the doorway |
| 5 | **Fill and drain a tank** | Turn Mode to Auto and watch two fill/drain cycles; turn it Off mid-way | No memory of which way it is going: the valves chatter around the high float and the tank never drains |
| 6 | **Sort metal from plastic** | Start the belt; watch metal go to Bin A and plastic to Bin B | The diverter pushes every part, metal or plastic |

For each, choose *A common mistake* and press **Check this program** to see what a student sees when they get it wrong.

## Simplifications we know about

- **The garage door only stops** when something is in the doorway. A real opener must also reverse (UL 325); that is planned
  as a later challenge. Say if you would rather have it from the start.
- **Analog signals** (levels, temperatures, a numeric display) are not wired to the PLC yet; they come with the analog unit.
  The tank uses two float switches.
- **Time** runs on a simulated clock of exactly 10 ms per scan, so results never vary. A real PLC's scan varies a little.

## What we would most like to know

Please answer whichever you can. Short notes are fine.

1. **The flow.** Is it clear what to do on this screen? What would a student new to PLCs be stuck on?
2. **The challenges.** Are these six the right first exercises? Right difficulty, right order? What would you add or drop?
3. **Correct practice.** Does anything teach a habit you would correct in the lab? For example: the normally closed Stop
   wiring, the door's interlock and safety eye, latching in the tank.
4. **The checking.** Are the tests fair? Are the failure messages and hints helpful, or do they give too much away?
5. **Learn / Assess / Reference.** Does that split suit how you teach a topic? What should a module's opening page show?
6. **Allen-Bradley.** The lab uses Allen-Bradley PLCs; PLC+ starts with the generic IEC 61131-3 names (TON with PT/ET/Q,
   contacts and coils). Should lessons show the Allen-Bradley names beside them from the start (XIC/XIO/OTE, .PRE/.ACC/.DN)?
7. **Your course.** Which topics and machines from your course would you most want to see?
8. **Reports.** When students hand in a progress report, what do you need to see on it?

## Sending your notes

Send your notes to Ron Eaglin, in any form: an email, a marked-up copy of this page, or screenshots. If something went
wrong, say which challenge and program it was, and what you did just before.

| Your name | Date | Challenge or screen | What you noticed or suggest |
|---|---|---|---|
| | | | |
