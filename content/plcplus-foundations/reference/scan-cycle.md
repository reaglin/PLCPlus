---
title: The scan cycle
---

# The scan cycle

A PLC repeats one cycle, over and over:

1. **Read the inputs:** copy every input's state into its tag.
2. **Run the program:** every rung, top to bottom, using those copies.
3. **Write the outputs:** copy the output tags to the real outputs.

So an input that changes in the middle of the program is not seen until the next scan, and a later rung can overwrite what an earlier rung did to the same tag. In PLC+ one scan is 10 ms of simulated time.
