"""Writes the phase 2 scenes, challenges and example programs into content/plcplus-foundations/.

The JSON files are the content (spec NFR-4: content is data); this script is how they were first made,
kept so the example programs can be read as rungs. Edit the JSON directly afterwards if that is easier,
or change this script and run it again from the repository folder:  python tools/make-phase2-content.py
"""
import json
import os

ROOT = os.path.join(os.path.dirname(__file__), '..', 'content', 'plcplus-foundations')


def no(tag): return {"kind": "contact", "tag": tag, "contact": "normallyOpen"}
def nc(tag): return {"kind": "contact", "tag": tag, "contact": "normallyClosed"}
def ne(tag): return {"kind": "contact", "tag": tag, "contact": "negativeEdge"}
def coil(tag, kind="normal"): return {"kind": "coil", "tag": tag, "coil": kind}
def s(*items): return {"kind": "series", "items": list(items)}
def par(*branches): return {"kind": "parallel", "branches": list(branches)}
def ton(inst, pt): return {"kind": "box", "instruction": "TON", "instance": inst, "operands": {"PT": pt}}
def inp(n, c=None): return {"name": n, "type": "BOOL", "io": "input", **({"comment": c} if c else {})}
def out(n, c=None): return {"name": n, "type": "BOOL", "io": "output", **({"comment": c} if c else {})}
def mem(n, t="BOOL", c=None): return {"name": n, "type": t, **({"comment": c} if c else {})}
def ev(t, op, active): return {"t": t, "operate": op, "active": active}
def press(op, t, length=0.2): return [ev(t, op, True), ev(round(t + length, 3), op, False)]
def check(at, tag, expect, says, tol=None):
    d = {"at": at, "tag": tag, "expect": expect, "says": says}
    if tol is not None:
        d["tolerance"] = tol
    return d
def never(*tags, says): return {"neverAllTrue": list(tags), "says": says}


def write(sub, name, data):
    path = os.path.join(ROOT, sub, name)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")


def program(file, title, tags, rungs):
    write("programs", file, {"schemaVersion": 1, "name": title, "dialect": "iec61131-3", "scanTimeMs": 10, "tags": tags,
                             "programs": [{"name": "Main", "rungs": [{"comment": c, "logic": l} for c, l in rungs]}]})


def scenario(name, duration, events, checks, invariants=(), hidden=False, points=1, hint=None):
    d = {"name": name, "points": points, "durationSeconds": duration, "events": sorted(events, key=lambda e: e["t"]),
         "assertions": checks}
    if invariants:
        d["invariants"] = list(invariants)
    if hidden:
        d["hidden"] = True
    if hint:
        d["hint"] = hint
    return d


# ---- 1. Pilot light and push-button station ----------------------------------------------------------------

write("scenes", "pilot-station.json", {
    "schemaVersion": 1, "id": "pilot-station", "title": "Pilot light and push-button station",
    "description": "Two push buttons and a Manual/Auto selector switch wired to the PLC's inputs; three pilot lights on its outputs.",
    "width": 440, "height": 330,
    "components": [
        {"type": "pushButton", "id": "pb1", "label": "Button 1", "tag": "PB1", "color": "green", "x": 30, "y": 30},
        {"type": "pushButton", "id": "pb2", "label": "Button 2", "tag": "PB2", "color": "green", "x": 30, "y": 120},
        {"type": "selectorSwitch", "id": "mode", "label": "Mode", "tag": "Mode", "positions": ["Manual", "Auto"], "x": 30, "y": 220},
        {"type": "pilotLight", "id": "green", "label": "Green", "tag": "Green", "color": "green", "x": 250, "y": 30},
        {"type": "pilotLight", "id": "amber", "label": "Amber", "tag": "Amber", "color": "amber", "x": 250, "y": 120},
        {"type": "pilotLight", "id": "red", "label": "Red", "tag": "Red", "color": "red", "x": 250, "y": 220},
    ]})
pilot_tags = [inp("PB1", "Button 1 (normally open)"), inp("PB2", "Button 2 (normally open)"),
              inp("Mode", "Selector: TRUE in Auto, FALSE in Manual"),
              out("Green"), out("Amber"), out("Red")]
program("pilot-lights.json", "Pilot lights (works)", pilot_tags, [
    ("Green while Button 1 is pressed.", s(no("PB1"), coil("Green"))),
    ("Amber only while both buttons are pressed: two contacts in series (AND).", s(no("PB1"), no("PB2"), coil("Amber"))),
    ("Red in Manual: a normally closed contact passes power while Mode is FALSE.", s(nc("Mode"), coil("Red"))),
])
program("pilot-lights-mistake.json", "Pilot lights (a common mistake)", pilot_tags, [
    ("Green while Button 1 is pressed.", s(no("PB1"), coil("Green"))),
    ("Amber only while both buttons are pressed.", s(no("PB1"), no("PB2"), coil("Amber"))),
    ("Red in Manual — but with a normally OPEN contact, so it lights in Auto instead.", s(no("Mode"), coil("Red"))),
])
write("challenges", "pilot-lights.json", {
    "schemaVersion": 1, "id": "u5.pilot-lights", "title": "Light the pilot lights",
    "brief": "Green is lit while Button 1 is pressed. Amber is lit only while Button 1 AND Button 2 are both pressed. Red is lit while the Mode switch is in Manual.",
    "scene": "pilot-station", "constraints": {"maxRungs": 6}, "passThreshold": 1.0,
    "hints": [
        "Each light needs its own rung, with its coil at the right-hand end.",
        "For AND, put two contacts one after the other (in series). Power must pass through both.",
        "Mode is FALSE in Manual. Which kind of contact passes power when its tag is FALSE?",
    ],
    "scenarios": [
        scenario("Button 1 lights green", 1.0, press("pb1", 0, 0.5), [
            check(0.1, "Green", "TRUE", "Green should be lit while Button 1 is pressed."),
            check(0.1, "Amber", "FALSE", "Amber should stay off with only one button pressed."),
            check(0.7, "Green", "FALSE", "Green should go off when Button 1 is released.")],
            hint="One normally open contact of PB1, then the Green coil."),
        scenario("Both buttons light amber", 1.0, press("pb1", 0, 0.5) + press("pb2", 0, 0.3), [
            check(0.1, "Amber", "TRUE", "Amber should be lit while both buttons are pressed."),
            check(0.4, "Amber", "FALSE", "Amber should go off when Button 2 is released.")],
            hint="For AND, put two contacts one after the other (in series): PB1, then PB2, then the Amber coil."),
        scenario("Red shows Manual", 1.0, [ev(0.3, "mode", True)], [
            check(0.1, "Red", "TRUE", "Red should be lit with the Mode switch in Manual."),
            check(0.4, "Red", "FALSE", "Red should go off when the switch is turned to Auto.")],
            hint="Mode is FALSE in Manual. A normally closed contact passes power while its tag is FALSE."),
        scenario("Button 2 alone does nothing", 1.0, press("pb2", 0, 0.5), [
            check(0.1, "Amber", "FALSE", "Amber needs both buttons."),
            check(0.1, "Green", "FALSE", "Green belongs to Button 1.")], hidden=True),
    ]})

# ---- 3. Traffic light --------------------------------------------------------------------------------------

write("scenes", "traffic-light.json", {
    "schemaVersion": 1, "id": "traffic-light", "title": "Traffic light",
    "description": "One traffic signal head (red, amber, green) and a Run switch.",
    "width": 440, "height": 330,
    "components": [
        {"type": "selectorSwitch", "id": "run", "label": "Run", "tag": "Run", "positions": ["Off", "Run"], "x": 30, "y": 130},
        {"type": "pilotLight", "id": "red", "label": "Red", "tag": "Red", "color": "red", "x": 260, "y": 30},
        {"type": "pilotLight", "id": "yellow", "label": "Yellow", "tag": "Yellow", "color": "amber", "x": 260, "y": 120},
        {"type": "pilotLight", "id": "green", "label": "Green", "tag": "Green", "color": "green", "x": 260, "y": 210},
    ]})
traffic_tags = [inp("Run", "Run switch"), out("Red"), out("Yellow"), out("Green"),
                mem("T1", "TON", "green time"), mem("T2", "TON", "yellow time"), mem("T3", "TON", "red time")]
traffic_timers = [
    ("Green time, 5 s. T3 finishing (end of red) restarts the cycle.", s(no("Run"), nc("T3.Q"), ton("T1", "T#5s"))),
    ("Yellow time, 2 s, after green.", s(no("T1.Q"), ton("T2", "T#2s"))),
    ("Red time, 5 s, after yellow.", s(no("T2.Q"), ton("T3", "T#5s"))),
]
program("traffic-light.json", "Traffic light (works)", traffic_tags, traffic_timers + [
    ("Green while the green timer is running.", s(no("Run"), nc("T1.Q"), coil("Green"))),
    ("Yellow after green, until yellow time is up.", s(no("T1.Q"), nc("T2.Q"), coil("Yellow"))),
    ("Red after yellow.", s(no("T2.Q"), coil("Red"))),
])
program("traffic-light-mistake.json", "Traffic light (a common mistake)", traffic_tags, traffic_timers + [
    ("Green while the green timer is running.", s(no("Run"), nc("T1.Q"), coil("Green"))),
    ("Yellow after green — but nothing turns it off when red comes on.", s(no("T1.Q"), coil("Yellow"))),
    ("Red after yellow.", s(no("T2.Q"), coil("Red"))),
])
one_light = [never("Red", "Green", says="Red and green must never be on together."),
             never("Red", "Yellow", says="Red and yellow must never be on together."),
             never("Green", "Yellow", says="Green and yellow must never be on together.")]
write("challenges", "traffic-light.json", {
    "schemaVersion": 1, "id": "u6.traffic-light", "title": "Run a traffic light",
    "brief": "With the Run switch on: green for 5 s, then yellow for 2 s, then red for 5 s, over and over. Only one light at a time. With Run off, all the lights are off.",
    "scene": "traffic-light", "constraints": {"requiredInstructions": ["TON"], "maxRungs": 10}, "passThreshold": 1.0,
    "hints": [
        "Use one on-delay timer (TON) for each light's time, each started by the one before it finishing.",
        "A light is on while its own timer is running and the next one has not finished: a normally open and a normally closed contact.",
        "To repeat, let the last timer finishing reset the first: a normally closed contact of T3.Q on the first timer's rung.",
    ],
    "scenarios": [
        scenario("Green, yellow, red in turn", 13.5, [ev(0, "run", True)], [
            check(2.5, "Green", "TRUE", "Green should be on for the first 5 s."),
            check(6.0, "Yellow", "TRUE", "Yellow should follow green, for 2 s."),
            check(9.5, "Red", "TRUE", "Red should follow yellow, for 5 s."),
            check(13.0, "Green", "TRUE", "After red, green should come back on.")], one_light,
            hint="Each light is on while its own timer is running and the next one has not finished. Yellow: a normally open contact of T1.Q and a normally closed contact of T2.Q."),
        scenario("Off turns every light off", 4.0, [ev(0, "run", True), ev(3.0, "run", False)], [
            check(3.1, "Green", "FALSE", "With Run off, green should be off."),
            check(3.1, "Yellow", "FALSE", "With Run off, yellow should be off."),
            check(3.1, "Red", "FALSE", "With Run off, red should be off.")],
            hint="With Run off, the first timer resets, and the others follow. Make sure every light's rung depends on a timer, or on Run."),
        scenario("Keeps cycling", 31.0, [ev(0, "run", True)], [
            check(30.5, "Yellow", "TRUE", "The cycle should keep repeating.")], one_light, hidden=True),
    ]})

# ---- 4. Garage door ----------------------------------------------------------------------------------------

write("scenes", "garage-door.json", {
    "schemaVersion": 1, "id": "garage-door", "title": "Garage door",
    "description": "A door driven up and down by a reversing motor, limit switches at the top and bottom, a safety eye across the doorway, and Open, Close and Stop buttons.",
    "width": 500, "height": 330,
    "components": [
        {"type": "pushButton", "id": "open", "label": "Open", "tag": "OpenPB", "color": "green", "x": 20, "y": 20},
        {"type": "pushButton", "id": "close", "label": "Close", "tag": "ClosePB", "color": "green", "x": 20, "y": 110},
        {"type": "pushButton", "id": "stop", "label": "Stop", "tag": "StopPB", "normallyClosed": True, "color": "red", "x": 20, "y": 200},
        {"type": "garageDoor", "id": "door", "label": "Door", "upTag": "MotorUp", "downTag": "MotorDown",
         "travelTimeMs": 4000, "initialPosition": 0, "safetyEye": "eye", "x": 200, "y": 30, "width": 160, "height": 160},
        {"type": "limitSwitch", "id": "toplimit", "label": "Top limit", "tag": "OpenLimit", "door": "door", "at": "open", "x": 370, "y": 30},
        {"type": "limitSwitch", "id": "bottomlimit", "label": "Bottom limit", "tag": "ClosedLimit", "door": "door", "at": "closed", "x": 370, "y": 170},
        {"type": "safetyEye", "id": "eye", "label": "Safety eye", "tag": "EyeClear", "x": 200, "y": 240},
    ]})
door_tags = [inp("OpenPB", "Open button (normally open)"), inp("ClosePB", "Close button (normally open)"),
             inp("StopPB", "Stop button (normally closed: TRUE until pressed)"),
             inp("OpenLimit", "Top limit switch: TRUE when the door is fully open"),
             inp("ClosedLimit", "Bottom limit switch: TRUE when the door is fully closed"),
             inp("EyeClear", "Safety eye: TRUE while nothing is in the doorway"),
             out("MotorUp"), out("MotorDown")]
up_rung = ("Open: seal in until the top limit or Stop. Never while going down (interlock).",
           s(par(no("OpenPB"), no("MotorUp")), no("StopPB"), nc("OpenLimit"), nc("MotorDown"), coil("MotorUp")))
program("garage-door.json", "Garage door (works)", door_tags, [
    up_rung,
    ("Close: seal in until the bottom limit, Stop, or something in the doorway. Never while going up.",
     s(par(no("ClosePB"), no("MotorDown")), no("StopPB"), nc("ClosedLimit"), no("EyeClear"), nc("MotorUp"), coil("MotorDown"))),
])
program("garage-door-mistake.json", "Garage door (a common mistake)", door_tags, [
    up_rung,
    ("Close: seal in until the bottom limit or Stop — but the safety eye is not checked.",
     s(par(no("ClosePB"), no("MotorDown")), no("StopPB"), nc("ClosedLimit"), nc("MotorUp"), coil("MotorDown"))),
])
no_short = never("MotorUp", "MotorDown", says="The motor must never be told to go up and down at once: that shorts the reversing contactors.")
write("challenges", "garage-door.json", {
    "schemaVersion": 1, "id": "u5.garage-door", "title": "Open and close a garage door safely",
    "brief": "Open runs the door up until the top limit switch. Close runs it down until the bottom limit switch. Stop stops it either way. While the door is moving one way, the other button does nothing (an interlock), and up and down are never on together. If something is in the doorway (the safety eye is blocked), the door must stop coming down. (A real opener also reverses; that comes in a later challenge.)",
    "scene": "garage-door", "constraints": {"maxRungs": 6}, "passThreshold": 1.0,
    "hints": [
        "Each direction is a motor start/stop circuit: a seal-in contact in parallel with its button.",
        "The limit switch for each direction stops it: a normally closed contact of the Top limit (tag OpenLimit) on the up rung, and of the Bottom limit (ClosedLimit) on the down rung.",
        "The safety eye's tag, EyeClear, is TRUE while the doorway is clear. A normally open contact of it on the down rung breaks the circuit when something steps in.",
    ],
    "scenarios": [
        scenario("Open runs the door to the top", 5.0, press("open", 0), [
            check(0.1, "MotorUp", "TRUE", "The door should start going up when Open is pressed."),
            check(4.3, "MotorUp", "FALSE", "The motor should stop when the door reaches the top limit."),
            check(4.3, "@door.open", "TRUE", "The door should be fully open.")], [no_short],
            hint="The up rung needs a seal-in contact (MotorUp in parallel with Open) and a normally closed contact of the Top limit (OpenLimit) to stop at the top."),
        scenario("Close brings it back down", 10.0, press("open", 0) + press("close", 5.0), [
            check(5.1, "MotorDown", "TRUE", "The door should start going down when Close is pressed."),
            check(9.4, "MotorDown", "FALSE", "The motor should stop at the bottom limit."),
            check(9.4, "@door.closed", "TRUE", "The door should be fully closed.")], [no_short],
            hint="The down rung is the same shape as the up rung, with Close, MotorDown and the Bottom limit (ClosedLimit)."),
        scenario("Stop stops it", 3.5, press("open", 0) + press("stop", 2.0), [
            check(2.1, "MotorUp", "FALSE", "Stop should stop the door."),
            check(3.0, "MotorUp", "FALSE", "The door should stay stopped after Stop is released.")], [no_short],
            hint="StopPB is TRUE until Stop is pressed: a normally open contact of it on each rung breaks the seal-in when Stop is pressed."),
        scenario("Something in the doorway", 10.0, press("open", 0) + press("close", 5.0) + [ev(6.0, "eye", True)], [
            check(6.1, "MotorDown", "FALSE", "With something in the doorway, the door must stop coming down."),
            check(9.5, "@door.hit", "FALSE", "The door must never come down on what is in the doorway.")], [no_short],
            hint="EyeClear is TRUE while the doorway is clear. Put a normally open contact of it on the down rung, so a blocked beam breaks the circuit and drops the seal-in."),
        scenario("Close while it is opening", 5.0, press("open", 0) + press("close", 1.0), [
            check(4.5, "@door.open", "TRUE", "Close while opening should not stop the door or reverse it in this challenge.")],
            [no_short], hidden=True),
    ]})

# ---- 5. Tank fill and drain --------------------------------------------------------------------------------

write("scenes", "tank.json", {
    "schemaVersion": 1, "id": "tank", "title": "Tank fill and drain",
    "description": "A tank with a fill valve, a drain valve, a low float switch at 20 % and a high float switch at 80 %, and an Off/Auto switch.",
    "width": 440, "height": 340,
    "components": [
        {"type": "selectorSwitch", "id": "auto", "label": "Mode", "tag": "Auto", "positions": ["Off", "Auto"], "x": 20, "y": 40},
        {"type": "solenoidValve", "id": "fill", "label": "Fill valve", "tag": "FillValve", "tank": "tank", "inlet": True,
         "ratePercentPerSecond": 10, "x": 190, "y": 10},
        {"type": "tank", "id": "tank", "label": "Tank", "initialLevelPercent": 10, "x": 180, "y": 60, "width": 120, "height": 200},
        {"type": "solenoidValve", "id": "drain", "label": "Drain valve", "tag": "DrainValve", "tank": "tank", "inlet": False,
         "ratePercentPerSecond": 8, "x": 190, "y": 280},
        {"type": "floatSwitch", "id": "high", "label": "High float (80 %)", "tag": "HighFloat", "tank": "tank", "levelPercent": 80, "x": 315, "y": 90},
        {"type": "floatSwitch", "id": "low", "label": "Low float (20 %)", "tag": "LowFloat", "tank": "tank", "levelPercent": 20, "x": 315, "y": 210},
    ]})
tank_tags = [inp("Auto", "Off/Auto switch: TRUE in Auto"),
             inp("HighFloat", "TRUE when the liquid is at 80 % or above"),
             inp("LowFloat", "TRUE when the liquid is at 20 % or above"),
             out("FillValve"), out("DrainValve"),
             mem("Filling", c="remembers that the tank is filling"), mem("Draining", c="remembers that the tank is draining")]
program("tank.json", "Tank fill and drain (works)", tank_tags, [
    ("Below the low float: start filling (latch).", s(no("Auto"), nc("LowFloat"), coil("Filling", "set"))),
    ("At the high float: stop filling.", s(no("HighFloat"), coil("Filling", "reset"))),
    ("At the high float: start draining (latch).", s(no("Auto"), no("HighFloat"), coil("Draining", "set"))),
    ("Below the low float: stop draining.", s(nc("LowFloat"), coil("Draining", "reset"))),
    ("The fill valve, only in Auto.", s(no("Auto"), no("Filling"), nc("Draining"), coil("FillValve"))),
    ("The drain valve, only in Auto.", s(no("Auto"), no("Draining"), coil("DrainValve"))),
])
program("tank-mistake.json", "Tank fill and drain (a common mistake)", tank_tags, [
    ("Fill whenever the liquid is below the high float — with nothing to remember which way it is going.",
     s(no("Auto"), nc("HighFloat"), coil("FillValve"))),
    ("Drain whenever it is at the high float.", s(no("Auto"), no("HighFloat"), coil("DrainValve"))),
])
tank_safe = [never("FillValve", "DrainValve", says="The fill and drain valves must never be open together."),
             never("@tank.overflow", says="The tank must never overflow.")]
write("challenges", "tank.json", {
    "schemaVersion": 1, "id": "u5.tank", "title": "Fill and drain a tank",
    "brief": "In Auto, fill the tank until the high float, then drain it until the liquid falls below the low float, then fill again, over and over. The two valves are never open together. Off closes both valves.",
    "scene": "tank", "constraints": {"maxRungs": 10}, "passThreshold": 1.0,
    "hints": [
        "The floats alone cannot tell filling from draining: between 20 % and 80 % the tank could be doing either. The program has to remember.",
        "Use set and reset coils (latches) on memory tags: Filling and Draining.",
        "Set Filling while LowFloat is FALSE (the low float is dry), reset it when HighFloat is TRUE. Do the opposite for Draining.",
    ],
    "scenarios": [
        scenario("Fills to the high float, then drains to the low", 15.5, [ev(0, "auto", True)], [
            check(0.1, "FillValve", "TRUE", "In Auto, a tank below the low float should start filling."),
            check(7.3, "FillValve", "FALSE", "Filling should stop at the high float (80 %)."),
            check(7.3, "DrainValve", "TRUE", "Draining should start at the high float."),
            check(11.0, "DrainValve", "TRUE", "It should keep draining all the way down to the low float."),
            check(15.2, "FillValve", "TRUE", "Below the low float (20 %), filling should start again.")], tank_safe,
            hint="The floats alone cannot tell filling from draining. Latch a Filling tag (set when LowFloat is FALSE, reset when HighFloat is TRUE) and a Draining tag (the opposite), and drive the valves from them."),
        scenario("Off closes both valves", 6.0, [ev(0, "auto", True), ev(3.0, "auto", False)], [
            check(3.1, "FillValve", "FALSE", "With the switch Off, the fill valve should close."),
            check(3.1, "DrainValve", "FALSE", "With the switch Off, the drain valve should close."),
            check(5.0, "@tank.level", "40.0", "The level should stay where it was when the switch was turned Off.", 1.5)], tank_safe,
            hint="Put a normally open contact of Auto on both valve rungs."),
        scenario("A minute without overflowing or running dry", 60.0, [ev(0, "auto", True)], [
            check(59.0, "@tank.empty", "FALSE", "The tank should never run dry.")],
            tank_safe + [never("@tank.empty", says="The tank must never run dry.")], hidden=True),
    ]})

# ---- 6. Conveyor sorter ------------------------------------------------------------------------------------

write("scenes", "conveyor-sorter.json", {
    "schemaVersion": 1, "id": "conveyor-sorter", "title": "Conveyor sorter",
    "description": "A conveyor carries metal and plastic parts past a proximity sensor (metal only) and a photo-eye (any part). A diverter can push a part off the belt into Bin A; the rest fall into Bin B at the end.",
    "width": 460, "height": 330,
    "components": [
        {"type": "pushButton", "id": "start", "label": "Start", "tag": "StartPB", "color": "green", "x": 20, "y": 20},
        {"type": "pushButton", "id": "stop", "label": "Stop", "tag": "StopPB", "normallyClosed": True, "color": "red", "x": 130, "y": 20},
        {"type": "conveyor", "id": "belt", "label": "Conveyor", "runTag": "BeltMotor", "crossTimeMs": 10000, "feedIntervalMs": 4000,
         "feedPattern": ["metal", "plastic", "metal", "metal", "plastic"], "length": 380, "endBinLabel": "Bin B", "x": 30, "y": 190},
        {"type": "proximitySensor", "id": "prox", "label": "Metal sensor", "tag": "Prox", "conveyor": "belt", "at": 0.3, "zone": 0.03, "x": 0, "y": 0},
        {"type": "photoEye", "id": "eye", "label": "Photo-eye", "tag": "Eye", "conveyor": "belt", "at": 0.6, "zone": 0.03, "x": 0, "y": 0},
        {"type": "diverter", "id": "diverter", "label": "Diverter", "tag": "Divert", "conveyor": "belt", "at": 0.6, "zone": 0.03,
         "strokeTimeMs": 200, "binLabel": "Bin A", "x": 0, "y": 0},
    ]})
sorter_tags = [inp("StartPB", "Start button (normally open)"), inp("StopPB", "Stop button (normally closed)"),
               inp("Prox", "Metal sensor at 30 % of the belt: TRUE while a metal part is in front of it"),
               inp("Eye", "Photo-eye at the diverter: TRUE while any part is in front of it"),
               out("BeltMotor"), out("Divert"), mem("MetalSeen", c="remembers that the part on its way is metal")]
belt_rung = ("Start/stop the belt with a seal-in.", s(par(no("StartPB"), no("BeltMotor")), no("StopPB"), coil("BeltMotor")))
program("conveyor-sorter.json", "Conveyor sorter (works)", sorter_tags, [
    belt_rung,
    ("The metal sensor sees a metal part: remember it.", s(no("Prox"), coil("MetalSeen", "set"))),
    ("A remembered metal part reaches the eye: push it into Bin A.", s(no("Eye"), no("MetalSeen"), coil("Divert"))),
    ("When the part leaves the eye, forget it.", s(ne("Eye"), coil("MetalSeen", "reset"))),
])
program("conveyor-sorter-mistake.json", "Conveyor sorter (a common mistake)", sorter_tags, [
    belt_rung,
    ("Push whatever reaches the eye — the metal sensor is never used.", s(no("Eye"), coil("Divert"))),
])
write("challenges", "conveyor-sorter.json", {
    "schemaVersion": 1, "id": "u5.conveyor-sorter", "title": "Sort metal from plastic",
    "brief": "Start and Stop run the conveyor. Metal parts must be pushed into Bin A by the diverter; plastic parts ride on to Bin B. The metal sensor is earlier on the belt than the diverter, so the program must remember what it saw.",
    "scene": "conveyor-sorter", "constraints": {"maxRungs": 8}, "passThreshold": 1.0,
    "hints": [
        "The diverter is at the photo-eye, but only the metal sensor can tell metal from plastic, and it is upstream (earlier on the belt).",
        "Latch a memory tag (set coil) when the metal sensor sees a part.",
        "Divert when the photo-eye sees a part and the memory is set; reset the memory when the part leaves the photo-eye (a falling-edge contact).",
    ],
    "scenarios": [
        scenario("Metal to Bin A, plastic to Bin B", 25.0, press("start", 0), [
            check(24.5, "@diverter.metal", "3", "Three metal parts should be in Bin A by now."),
            check(24.5, "@diverter.plastic", "0", "No plastic part should be in Bin A."),
            check(24.5, "@belt.plastic", "1", "The plastic part that has reached the end should be in Bin B.")],
            hint="Latch MetalSeen when Prox sees a part; divert when Eye sees a part and MetalSeen is set; reset MetalSeen when the part leaves the eye (a falling-edge contact of Eye)."),
        scenario("Stop stops the belt", 6.0, press("start", 0) + press("stop", 3.0), [
            check(3.1, "BeltMotor", "FALSE", "Stop should stop the belt."),
            check(5.0, "@belt.fed", "1", "With the belt stopped, no new parts should come on.")],
            hint="StopPB is TRUE until Stop is pressed: a normally open contact of it in the belt's seal-in rung."),
        scenario("A longer run", 45.0, press("start", 0), [
            check(44.5, "@diverter.plastic", "0", "No plastic part should ever be diverted."),
            check(44.5, "@belt.metal", "0", "No metal part should reach Bin B.")], hidden=True),
    ]})

# ---- The list of challenges the program shows -------------------------------------------------------------

def entry(challenge, works, mistake, works_note, mistake_note):
    return {"file": f"challenges/{challenge}.json", "programs": [
        {"title": "Works", "file": f"programs/{works}.json", "note": works_note},
        {"title": "A common mistake", "file": f"programs/{mistake}.json", "note": mistake_note}]}

manifest = {
    "id": "plcplus-foundations", "version": "0.2.0", "title": "PLC+ — foundations (preview)",
    "dependencies": [], "minimumAppVersion": "0.1.0",
    "note": "Preview content for the instructors' review. The package format (spec 6.3: lessons in Markdown with front-matter, signing) is plan phase 4.",
    "challenges": [
        entry("pilot-lights", "pilot-lights", "pilot-lights-mistake",
              "Three rungs: one contact for Green, two in series (AND) for Amber, a normally closed contact for Red.",
              "Red uses a normally open contact, so it lights in Auto instead of Manual."),
        entry("motor-start-stop", "motor-seal-in", "motor-no-seal-in",
              "Rung 1: Start, or the Motor's own contact in parallel with it (the seal-in), passes power through Stop to the Motor coil. Let go of Start and the seal-in keeps the motor running. Stop is wired normally closed (TRUE until pressed), so the program checks it with a normally open contact: pressing Stop opens it and breaks the circuit.",
              "Rung 1 has no seal-in contact: the motor runs only while Start is held down."),
        entry("traffic-light", "traffic-light", "traffic-light-mistake",
              "Three on-delay timers in a chain, each started by the one before finishing; the last one restarts the first.",
              "Yellow has nothing to turn it off when red comes on, so the two are on together."),
        entry("garage-door", "garage-door", "garage-door-mistake",
              "Two start/stop circuits, one per direction, each stopped by its limit switch and locked out by the other; the down rung also needs the safety eye clear.",
              "The down rung does not check the safety eye, so the door comes down on whatever is in the doorway."),
        entry("tank", "tank", "tank-mistake",
              "Two latched memory tags, Filling and Draining, set and reset by the floats; the valves follow them.",
              "Without memory, the valves follow the high float alone: the tank chatters around 80 % and never drains."),
        entry("conveyor-sorter", "conveyor-sorter", "conveyor-sorter-mistake",
              "The metal sensor latches MetalSeen; the diverter pushes when the photo-eye sees a part and MetalSeen is set; the part leaving the eye resets it.",
              "The diverter pushes every part that reaches the eye, metal or plastic."),
    ]}
with open(os.path.join(ROOT, "manifest.json"), "w", encoding="utf-8") as f:
    json.dump(manifest, f, indent=2, ensure_ascii=False)
    f.write("\n")
print("written")
