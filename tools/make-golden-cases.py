"""Writes the reference programs in tests/golden/ (plan 1.11, 1.12).

Each case is a small program, the inputs a person (or the lab) gives it at set times, and the tags to
watch. The expected change log beside it (<id>.trace.txt) is written by the golden test the first time
(PLCPLUS_UPDATE_GOLDEN=1) and then checked by hand: it is the behaviour the engine is held to, and what
the Allen-Bradley PLC in the lab is compared against (docs/LAB-COMPARISON.md).

Run from the repository folder:  python tools/make-golden-cases.py
"""
import json
import os

OUT = os.path.join(os.path.dirname(__file__), '..', 'tests', 'golden')


def no(tag): return {"kind": "contact", "tag": tag, "contact": "normallyOpen"}
def nc(tag): return {"kind": "contact", "tag": tag, "contact": "normallyClosed"}
def p(tag): return {"kind": "contact", "tag": tag, "contact": "positiveEdge"}
def n(tag): return {"kind": "contact", "tag": tag, "contact": "negativeEdge"}
def coil(tag, kind="normal"): return {"kind": "coil", "tag": tag, "coil": kind}
def s(*items): return {"kind": "series", "items": list(items)}
def par(*branches): return {"kind": "parallel", "branches": list(branches)}


def box(instr, instance=None, **ops):
    b = {"kind": "box", "instruction": instr, "operands": ops}
    if instance:
        b["instance"] = instance
    return b


def inp(name): return {"name": name, "type": "BOOL", "io": "input"}
def out(name): return {"name": name, "type": "BOOL", "io": "output"}
def mem(name, type_="BOOL", initial=None):
    d = {"name": name, "type": type_}
    if initial is not None:
        d["initial"] = initial
    return d


def press(name, at, length=0.1):
    return [{"t": at, "input": name, "value": True}, {"t": round(at + length, 3), "input": name, "value": False}]


def case(id_, title, teaches, tags, rungs, stimulus, duration, watch, types=None):
    project = {"schemaVersion": 1, "name": title, "dialect": "iec61131-3", "scanTimeMs": 10,
               "tags": tags, "programs": [{"name": "Main", "rungs": [{"comment": c, "logic": l} for c, l in rungs]}]}
    if types:
        project["types"] = types
    data = {"id": id_, "title": title, "teaches": teaches, "project": project,
            "stimulus": sorted(stimulus, key=lambda e: e["t"]), "durationSeconds": duration, "watch": watch}
    with open(os.path.join(OUT, f"{id_}.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)
        f.write("\n")


os.makedirs(OUT, exist_ok=True)

case("R01", "Motor start/stop with seal-in",
     "Seal-in (holding) contact; a normally closed Stop button wired so its input is TRUE until pressed.",
     [inp("Start"), inp("Stop"), out("Motor")],
     [("Start, or the seal-in, runs the motor until Stop opens.", s(par(no("Start"), no("Motor")), no("Stop"), coil("Motor")))],
     [{"t": 0, "input": "Stop", "value": True}] + press("Start", 0.1, 0.2)
     + [{"t": 1.0, "input": "Stop", "value": False}, {"t": 1.2, "input": "Stop", "value": True}],
     1.5, ["Start", "Stop", "Motor"])

case("R02", "On-delay timer (TON)",
     "TON: the output comes on only after the input has stayed on for the preset; a short pulse does nothing.",
     [inp("Switch"), out("Lamp"), mem("T1", "TON")],
     [("Lamp on after Switch has been on for 0.5 s.", s(no("Switch"), box("TON", "T1", PT="T#500ms"), coil("Lamp")))],
     press("Switch", 0.1, 0.3) + press("Switch", 0.6, 0.9),
     2.0, ["Switch", "Lamp"])

case("R03", "Off-delay timer (TOF)",
     "TOF: the output follows the input on, and stays on for the preset after it turns off; turning on again restarts it.",
     [inp("Switch"), out("Lamp"), mem("T1", "TOF")],
     [("Lamp stays on 0.5 s after Switch turns off.", s(no("Switch"), box("TOF", "T1", PT="T#500ms"), coil("Lamp")))],
     press("Switch", 0.1, 0.2) + press("Switch", 1.0, 0.2) + press("Switch", 1.4, 0.2),
     2.5, ["Switch", "Lamp"])

case("R04", "Pulse timer (TP)",
     "TP: a rising input starts a pulse of fixed length; the input changing during the pulse does not change it.",
     [inp("Button"), out("Lamp"), mem("T1", "TP")],
     [("A 0.5 s pulse on each press.", s(no("Button"), box("TP", "T1", PT="T#500ms"), coil("Lamp")))],
     press("Button", 0.1, 0.1) + press("Button", 0.3, 0.1) + press("Button", 1.0, 1.0),
     2.2, ["Button", "Lamp"])

case("R05", "Up counter (CTU) with reset",
     "CTU counts rising edges only; Q at the preset; the count keeps going past it; reset clears it.",
     [inp("Count"), inp("Reset"), out("Lamp"), mem("C1", "CTU")],
     [("Lamp on after 3 presses of Count; Reset clears.", s(no("Count"), box("CTU", "C1", PV="3", R="Reset"), coil("Lamp")))],
     press("Count", 0.1) + press("Count", 0.3) + press("Count", 0.5) + press("Count", 0.7) + press("Reset", 1.0),
     1.3, ["Count", "Reset", "C1.CV", "Lamp"])

case("R06", "Down counter (CTD) with load",
     "CTD: load sets the count to the preset; each press counts down; Q at zero or below.",
     [inp("Load"), inp("Count"), out("Lamp"), mem("C1", "CTD")],
     [("Load 2, count down to 0.", s(no("Count"), box("CTD", "C1", PV="2", LD="Load"), coil("Lamp")))],
     press("Load", 0.1) + press("Count", 0.3) + press("Count", 0.5) + press("Count", 0.7),
     1.0, ["Load", "Count", "C1.CV", "Lamp"])

case("R07", "Up/down counter (CTUD)",
     "CTUD: one input counts up, the other down; both at once cancel out.",
     [inp("Up"), inp("Down"), out("Lamp"), mem("C1", "CTUD")],
     [("Lamp on at 2 or more.", s(no("Up"), box("CTUD", "C1", PV="2", CD="Down"), coil("Lamp")))],
     press("Up", 0.1) + press("Up", 0.3) + press("Down", 0.5) + press("Up", 0.7) + press("Down", 0.7),
     1.0, ["Up", "Down", "C1.CV", "Lamp"])

case("R08", "Latch, unlatch and edges",
     "Set/reset coils hold their state; rising- and falling-edge contacts pass power for exactly one scan.",
     [inp("SetBtn"), inp("ResetBtn"), inp("Button"), out("Lamp"), out("Rise"), out("Fall")],
     [("Set latches the lamp on.", s(no("SetBtn"), coil("Lamp", "set"))),
      ("Reset unlatches it.", s(no("ResetBtn"), coil("Lamp", "reset"))),
      ("One scan when Button is pressed.", s(p("Button"), coil("Rise"))),
      ("One scan when Button is released.", s(n("Button"), coil("Fall")))],
     press("SetBtn", 0.1) + press("ResetBtn", 0.4) + press("Button", 0.6, 0.2),
     1.0, ["SetBtn", "ResetBtn", "Lamp", "Button", "Rise", "Fall"])

case("R09", "Compare and math",
     "ADD on a rising edge accumulates; GE compares; MOVE clears.",
     [inp("AddBtn"), inp("ClearBtn"), out("Full"), mem("Total", "INT")],
     [("Each press adds 5.", s(p("AddBtn"), box("ADD", IN1="Total", IN2="5", OUT="Total"))),
      ("Full at 15 or more.", s(box("GE", IN1="Total", IN2="15"), coil("Full"))),
      ("Clear sets the total back to 0.", s(p("ClearBtn"), box("MOVE", IN="0", OUT="Total")))],
     press("AddBtn", 0.1) + press("AddBtn", 0.3) + press("AddBtn", 0.5) + press("ClearBtn", 0.8),
     1.0, ["AddBtn", "ClearBtn", "Total", "Full"])

case("R10", "Array with an index, and the out-of-range fault",
     "An array element chosen by a tag; an index past the end stops the PLC with a fault and turns the outputs off.",
     [inp("Step"), out("Lamp"), mem("Index", "INT"), mem("Values", "ARRAY[0..3] OF INT")],
     [("Each press moves the index on by one.", s(p("Step"), box("ADD", IN1="Index", IN2="1", OUT="Index"))),
      ("Values[Index] := Index x 10.", s(box("MUL", IN1="Index", IN2="10", OUT="Values[Index]"))),
      ("Lamp on once Values[2] reaches 20.", s(box("GE", IN1="Values[2]", IN2="20"), coil("Lamp")))],
     press("Step", 0.1) + press("Step", 0.3) + press("Step", 0.5) + press("Step", 0.7),
     1.0, ["Step", "Index", "Values[1]", "Values[2]", "Values[3]", "Lamp"])

case("R11", "Timer cascade (flasher)",
     "Two TONs that reset each other make a repeating cycle; the rung order decides the exact timing.",
     [inp("Run"), out("Green"), out("Yellow"), mem("T1", "TON"), mem("T2", "TON")],
     [("Green time: 1 s, restarted when T2 finishes.", s(no("Run"), nc("T2.Q"), box("TON", "T1", PT="T#1s"))),
      ("Yellow time: 0.5 s after T1.", s(no("T1.Q"), box("TON", "T2", PT="T#500ms"))),
      ("Green while T1 is timing.", s(no("Run"), nc("T1.Q"), coil("Green"))),
      ("Yellow once T1 is done.", s(no("T1.Q"), coil("Yellow")))],
     [{"t": 0.1, "input": "Run", "value": True}, {"t": 3.5, "input": "Run", "value": False}],
     4.0, ["Run", "Green", "Yellow"])

print("written to", os.path.abspath(OUT))
