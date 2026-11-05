---
title: The scan cycle
---

# The scan cycle

A PLC repeats one cycle, over and over:

1. **Read the inputs:** copy every input's state into its tag.
2. **Run the program:** every rung, top to bottom, using those copies.
3. **Write the outputs:** copy the output tags to the real outputs.
4. **Housekeeping:** talk to the programming software and the network, check itself.

So an input that changes in the middle of the program is not seen until the next scan, and a later rung can overwrite what an earlier rung did to the same tag. In PLC+ one scan is 10 ms of simulated time.

- **Double coil:** two coils of one tag on different rungs. The later rung wins. Use one coil per output.
- **Short pulses:** an input that turns on and off between two reads is missed.
- **Watchdog:** a scan that takes too long stops the PLC with a fault.
- Allen-Bradley Logix controllers update their I/O on their own timer (the RPI), not once per scan.
