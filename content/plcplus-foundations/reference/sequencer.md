---
title: Sequencers with a step number
---

# Sequencers with a step number

One INT tag (Step) holds the number of the step: 0 idle, 1, 2, 3, …

| Rung | Shape | Allen-Bradley |
|---|---|---|
| An output | EQ Step n, then the coil | EQU, OTE |
| A move on | EQ Step n, the condition, MOVE n+1 to Step | EQU, XIC, MOV |
| A timed step | EQ Step n, then TON; the next rung uses its .Q | EQU, TON (.DN) |
| Start | EQ Step 0, StartPB, MOVE 1 to Step | |
| Stop (any step) | NOT StopPB, MOVE 0 to Step, with no EQ | |

- **EQ** passes power when IN1 equals IN2. **MOVE** copies IN to OUT while power reaches it.
- An output on in several steps: **one** rung, EQ boxes side by side. Two coils of the same tag: the lower rung wins.
- Rungs below a MOVE see the new step in the same scan.
- Numbering 10, 20, 30 leaves room to add steps later.
- Allen-Bradley also has **SQO** (sequencer output), which steps through a table of output patterns. PLC+ does not have it.
