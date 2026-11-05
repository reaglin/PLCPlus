"""Writes the five example machines of plan 9.7 (D21): for each module 5 exercise, a similar machine whose Example
tab shows the idea worked out, so the learner then applies it to the exercise's own machine.

    python tools/make-example-machines.py

Writes scenes/, challenges/ and programs/ files in content/plcplus-foundations, and adds them to the manifest.
Run once; after that the JSON files are the source (edit them, not this script).
"""
import json
import os

ROOT = os.path.join(os.path.dirname(__file__), '..', 'content', 'plcplus-foundations')


def write(rel, data):
    with open(os.path.join(ROOT, rel), 'w', encoding='utf-8', newline='\n') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write('\n')


# ---- program building blocks ----------------------------------------------------------------------------------
def no(tag): return {"kind": "contact", "tag": tag, "contact": "normallyOpen"}
def nc(tag): return {"kind": "contact", "tag": tag, "contact": "normallyClosed"}
def rising(tag): return {"kind": "contact", "tag": tag, "contact": "positiveEdge"}
def coil(tag, kind="normal"): return {"kind": "coil", "tag": tag, "coil": kind}
def series(*items): return {"kind": "series", "items": list(items)}
def parallel(*branches): return {"kind": "parallel", "branches": [b if b["kind"] == "series" else series(b) for b in branches]}
def rung(comment, *items): return {"comment": comment, "logic": series(*items)}


def program(name, tags, rungs):
    return {"schemaVersion": 1, "name": name, "dialect": "iec61131-3", "scanTimeMs": 10,
            "tags": [dict(name=n, type="BOOL", **({"io": io} if io else {}), **({"comment": c} if c else {})) for n, io, c in tags],
            "programs": [{"name": "Main", "rungs": rungs}]}


def press(cid, at, until): return [{"t": at, "operate": cid, "active": True}, {"t": until, "operate": cid, "active": False}]
def turn(cid, at, on): return [{"t": at, "operate": cid, "active": on}]
def expect(at, tag, value, says): return {"at": at, "tag": tag, "expect": "TRUE" if value else "FALSE", "says": says}


machines = []

# ---- 1. Press guard: contacts and coils (AND, NOT) -- the example for 5.1 (the pilot lights) ---------------------
machines.append(dict(
    scene={"schemaVersion": 1, "id": "press-guard", "title": "Press guard station",
           "description": "A small press that must only close when both of the operator's hands are on its buttons and the guard is shut. Two hand buttons and a guard switch on the inputs; Ready, Press closing and Guard open lights on the outputs.",
           "width": 440, "height": 330, "components": [
               {"type": "pushButton", "id": "left", "label": "Left hand", "tag": "LeftHand", "color": "green", "x": 30, "y": 30},
               {"type": "pushButton", "id": "right", "label": "Right hand", "tag": "RightHand", "color": "green", "x": 30, "y": 120},
               {"type": "selectorSwitch", "id": "guard", "label": "Guard", "tag": "GuardShut", "positions": ["Open", "Shut"], "x": 30, "y": 220},
               {"type": "pilotLight", "id": "ready", "label": "Ready", "tag": "Ready", "color": "green", "x": 250, "y": 30},
               {"type": "pilotLight", "id": "closing", "label": "Press closing", "tag": "PressClose", "color": "amber", "x": 250, "y": 120},
               {"type": "pilotLight", "id": "guardopen", "label": "Guard open", "tag": "GuardOpen", "color": "red", "x": 250, "y": 220}]},
    challenge={"schemaVersion": 1, "id": "u5.press-guard", "title": "A two-hand press guard",
               "brief": "Ready is lit while the guard is shut. The press closes (Press closing is lit) only while the Left hand AND the Right hand buttons are both pressed AND the guard is shut. Guard open is lit while the guard is NOT shut.",
               "scene": "press-guard", "constraints": {"maxRungs": 6}, "passThreshold": 1.0,
               "hints": ["Three rungs, one for each light.", "Contacts one after another (in series) make AND.", "A normally closed contact of GuardShut passes power while the guard is NOT shut."],
               "scenarios": [
                   {"name": "The guard shut lights Ready", "points": 1, "durationSeconds": 0.5, "events": turn("guard", 0, True),
                    "assertions": [expect(0.2, "Ready", True, "Ready should be lit while the guard is shut."), expect(0.2, "GuardOpen", False, "Guard open should be off while the guard is shut.")]},
                   {"name": "Both hands with the guard shut close the press", "points": 1, "durationSeconds": 1.0,
                    "events": turn("guard", 0, True) + press("left", 0.1, 0.8) + press("right", 0.1, 0.8),
                    "assertions": [expect(0.5, "PressClose", True, "The press should close while both hand buttons are pressed and the guard is shut."), expect(0.95, "PressClose", False, "The press should stop when the hands are taken away.")]},
                   {"name": "One hand is not enough", "points": 1, "durationSeconds": 1.0,
                    "events": turn("guard", 0, True) + press("left", 0.1, 0.8),
                    "assertions": [expect(0.5, "PressClose", False, "With only one hand on a button the press must NOT close: the other hand could be in the press.")]},
                   {"name": "The guard open stops everything", "points": 1, "durationSeconds": 1.0,
                    "events": press("left", 0.1, 0.8) + press("right", 0.1, 0.8),
                    "assertions": [expect(0.5, "PressClose", False, "With the guard open the press must not close, even with both hands on the buttons."), expect(0.5, "GuardOpen", True, "Guard open should be lit while the guard is open.")]}]},
    tags=[("LeftHand", "input", "Left hand button: TRUE while pressed"), ("RightHand", "input", "Right hand button: TRUE while pressed"),
          ("GuardShut", "input", "Guard switch: TRUE while the guard is shut"),
          ("Ready", "output", None), ("PressClose", "output", None), ("GuardOpen", "output", None)],
    works=[rung("Ready while the guard is shut.", no("GuardShut"), coil("Ready")),
           rung("Close the press only with both hands on their buttons AND the guard shut: three contacts in series.", no("LeftHand"), no("RightHand"), no("GuardShut"), coil("PressClose")),
           rung("Guard open while the guard is NOT shut: a normally closed contact.", nc("GuardShut"), coil("GuardOpen"))],
    mistake=[rung("Ready while the guard is shut.", no("GuardShut"), coil("Ready")),
             rung("MISTAKE: the hand buttons are in parallel (OR), so one hand closes the press.", parallel(no("LeftHand"), no("RightHand")), no("GuardShut"), coil("PressClose")),
             rung("Guard open while the guard is NOT shut.", nc("GuardShut"), coil("GuardOpen"))],
    works_note="Three rungs: Ready follows the guard; Press closing needs Left hand AND Right hand AND the guard shut (three contacts in series); Guard open uses a normally closed contact (NOT).",
    mistake_note="The two hand buttons are side by side (OR), so the press closes with one hand on a button: the other hand could be under it.",
    exercise="pilot-lights"))

# ---- 2. Pump: the seal-in -- the example for 5.2 (the motor) ----------------------------------------------------
machines.append(dict(
    scene={"schemaVersion": 1, "id": "pump-station", "title": "Pump start/stop station",
           "description": "A pump on a contactor with an overload relay (it trips if the pump draws too much current), a green Start button, a red Stop button wired normally closed, and a Pump running light.",
           "width": 440, "height": 330, "components": [
               {"type": "pushButton", "id": "start", "label": "Start", "tag": "PumpStart", "color": "green", "x": 40, "y": 40},
               {"type": "pushButton", "id": "stop", "label": "Stop", "tag": "PumpStop", "normallyClosed": True, "color": "red", "x": 40, "y": 140},
               {"type": "selectorSwitch", "id": "overload", "label": "Overload relay", "tag": "OverloadOK", "positions": ["Tripped", "OK"], "on": True, "x": 40, "y": 240},
               {"type": "pilotLight", "id": "light", "label": "Pump running", "tag": "PumpLight", "color": "green", "x": 170, "y": 40},
               {"type": "motor", "id": "pump", "label": "Pump P1", "runTag": "Pump", "startTimeMs": 800, "x": 300, "y": 90}]},
    challenge={"schemaVersion": 1, "id": "u5.pump-start-stop", "title": "Start and stop a pump",
               "brief": "Press Start and the pump runs, and keeps running after you let go. Press Stop and it stops. If the overload relay trips (the pump is drawing too much current), the pump stops too, and stays stopped until someone presses Start again. The Pump running light is lit while the pump is told to run. Stop and the overload relay are wired normally closed: each input is TRUE while all is well.",
               "scene": "pump-station", "constraints": {"maxRungs": 4}, "passThreshold": 1.0,
               "hints": ["What keeps power flowing to the Pump coil after Start is let go?", "Put a contact of Pump in parallel with PumpStart: the seal-in.", "PumpStop and OverloadOK are TRUE while all is well, so check each with a normally open contact, in series."],
               "scenarios": [
                   {"name": "Start runs the pump, and it keeps running", "points": 1, "durationSeconds": 1.5, "events": press("start", 0, 0.2),
                    "assertions": [expect(0.1, "Pump", True, "The pump should run 0.1 s after Start is pressed."), expect(1.0, "Pump", True, "The pump should keep running after Start is let go."), expect(1.0, "PumpLight", True, "The Pump running light should be lit while the pump runs.")]},
                   {"name": "Stop stops the pump", "points": 1, "durationSeconds": 2.5, "events": press("start", 0, 0.2) + press("stop", 1.0, 1.2),
                    "assertions": [expect(1.1, "Pump", False, "The pump should stop 0.1 s after Stop is pressed."), expect(2.0, "Pump", False, "The pump should stay stopped after Stop is let go."), expect(2.0, "PumpLight", False, "The light should go out when the pump stops.")]},
                   {"name": "An overload trip stops the pump", "points": 1, "durationSeconds": 2.5,
                    "events": press("start", 0, 0.2) + turn("overload", 1.0, False) + turn("overload", 1.5, True),
                    "assertions": [expect(1.1, "Pump", False, "The pump should stop when the overload relay trips."), expect(2.0, "Pump", False, "The pump should stay stopped after the overload relay is reset, until Start is pressed again.")]}]},
    tags=[("PumpStart", "input", "Start button (normally open): TRUE while pressed"), ("PumpStop", "input", "Stop button (normally closed): TRUE while NOT pressed"),
          ("OverloadOK", "input", "Overload relay contact (normally closed): TRUE while it has NOT tripped"),
          ("Pump", "output", None), ("PumpLight", "output", None)],
    works=[rung("(Start OR the pump already running) AND Stop not pressed AND the overload not tripped: the seal-in.", parallel(no("PumpStart"), no("Pump")), no("PumpStop"), no("OverloadOK"), coil("Pump")),
           rung("The light follows the pump.", no("Pump"), coil("PumpLight"))],
    mistake=[rung("MISTAKE: Stop is checked with a normally closed contact. Stop is wired normally closed, so its input is TRUE while nobody presses it, and this contact never passes power.", parallel(no("PumpStart"), no("Pump")), nc("PumpStop"), no("OverloadOK"), coil("Pump")),
             rung("The light follows the pump.", no("Pump"), coil("PumpLight"))],
    works_note="Rung 1: PumpStart, or the pump's own contact beside it (the seal-in), through PumpStop and OverloadOK to the Pump coil. Both are wired normally closed, so normally open contacts pass power until Stop is pressed or the overload trips. Rung 2 lights the lamp.",
    mistake_note="PumpStop is checked with a normally closed contact. The button is wired normally closed (TRUE while not pressed), so that contact is open all the time and the pump never starts. The most common mistake with Stop buttons.",
    exercise="motor-start-stop"))

# ---- 3. Reversing conveyor: interlocks -- the example for 5.3 (the garage door) --------------------------------
machines.append(dict(
    scene={"schemaVersion": 1, "id": "reversing-conveyor", "title": "Reversing conveyor motor",
           "description": "A conveyor motor with two contactors, one to run it forward and one to run it in reverse; Forward, Reverse and Stop buttons. The two contactors must never be on together: on a real panel that shorts the supply.",
           "width": 440, "height": 300, "components": [
               {"type": "pushButton", "id": "fwd", "label": "Forward", "tag": "FwdPB", "color": "green", "x": 30, "y": 20},
               {"type": "pushButton", "id": "rev", "label": "Reverse", "tag": "RevPB", "color": "green", "x": 30, "y": 110},
               {"type": "pushButton", "id": "stop", "label": "Stop", "tag": "StopPB", "normallyClosed": True, "color": "red", "x": 30, "y": 200},
               {"type": "pilotLight", "id": "fwdlight", "label": "Forward", "tag": "FwdLight", "color": "green", "x": 170, "y": 20},
               {"type": "pilotLight", "id": "revlight", "label": "Reverse", "tag": "RevLight", "color": "amber", "x": 170, "y": 110},
               {"type": "motor", "id": "motor", "label": "Conveyor motor", "runTag": "Forward", "reverseTag": "Reverse", "startTimeMs": 600, "x": 320, "y": 110}]},
    challenge={"schemaVersion": 1, "id": "u5.reversing-conveyor", "title": "Run a conveyor forward and in reverse",
               "brief": "Forward runs the motor forward and Reverse runs it backwards; each keeps running after the button is let go, and Stop stops it either way. While it runs one way, the other button does nothing (an interlock): Forward and Reverse are never on together. The lights show which way it is told to run.",
               "scene": "reversing-conveyor", "constraints": {"maxRungs": 6}, "passThreshold": 1.0,
               "hints": ["Write two start/stop circuits with seal-ins, one for Forward and one for Reverse.", "In the Forward rung, add a normally closed contact of Reverse; in the Reverse rung, one of Forward. That is the interlock."],
               "scenarios": [
                   {"name": "Forward runs, Stop stops", "points": 1, "durationSeconds": 2.0, "events": press("fwd", 0, 0.2) + press("stop", 1.0, 1.2),
                    "assertions": [expect(0.5, "Forward", True, "Forward should run after the Forward button."), expect(1.5, "Forward", False, "Stop should stop it.")],
                    "invariants": [{"neverAllTrue": ["Forward", "Reverse"], "says": "Forward and Reverse must never be on together: that shorts the supply."}]},
                   {"name": "Reverse runs, Stop stops", "points": 1, "durationSeconds": 2.0, "events": press("rev", 0, 0.2) + press("stop", 1.0, 1.2),
                    "assertions": [expect(0.5, "Reverse", True, "Reverse should run after the Reverse button."), expect(0.5, "RevLight", True, "The Reverse light should be lit."), expect(1.5, "Reverse", False, "Stop should stop it.")],
                    "invariants": [{"neverAllTrue": ["Forward", "Reverse"], "says": "Forward and Reverse must never be on together: that shorts the supply."}]},
                   {"name": "Reverse while running forward does nothing", "points": 1, "durationSeconds": 2.0, "events": press("fwd", 0, 0.2) + press("rev", 0.8, 1.0),
                    "assertions": [expect(1.5, "Forward", True, "The motor should keep running forward."), expect(1.5, "Reverse", False, "Reverse must not come on while Forward is on.")],
                    "invariants": [{"neverAllTrue": ["Forward", "Reverse"], "says": "Forward and Reverse must never be on together: that shorts the supply."}]}]},
    tags=[("FwdPB", "input", "Forward button: TRUE while pressed"), ("RevPB", "input", "Reverse button: TRUE while pressed"),
          ("StopPB", "input", "Stop button (normally closed): TRUE while NOT pressed"),
          ("Forward", "output", None), ("Reverse", "output", None), ("FwdLight", "output", None), ("RevLight", "output", None)],
    works=[rung("Forward: a seal-in, stopped by Stop, and locked out while Reverse is on (the interlock).", parallel(no("FwdPB"), no("Forward")), no("StopPB"), nc("Reverse"), coil("Forward")),
           rung("Reverse: the same, locked out while Forward is on.", parallel(no("RevPB"), no("Reverse")), no("StopPB"), nc("Forward"), coil("Reverse")),
           rung("The Forward light.", no("Forward"), coil("FwdLight")),
           rung("The Reverse light.", no("Reverse"), coil("RevLight"))],
    mistake=[rung("Forward: a seal-in and Stop, but MISTAKE: no interlock.", parallel(no("FwdPB"), no("Forward")), no("StopPB"), coil("Forward")),
             rung("Reverse: no interlock either, so both contactors can pull in.", parallel(no("RevPB"), no("Reverse")), no("StopPB"), coil("Reverse")),
             rung("The Forward light.", no("Forward"), coil("FwdLight")),
             rung("The Reverse light.", no("Reverse"), coil("RevLight"))],
    works_note="Two start/stop circuits with seal-ins. Each has a normally closed contact of the other direction (the interlock), so only one can be on.",
    mistake_note="There is no interlock: press Reverse while the motor runs forward, and both contactors are told to pull in at once.",
    exercise="garage-door"))

# ---- 4. Overheat alarm: set and reset -- the example for 5.4 (the tank) ----------------------------------------
machines.append(dict(
    scene={"schemaVersion": 1, "id": "overheat-alarm", "title": "Overheat alarm",
           "description": "A thermostat on an oven (Normal or Too hot), an Alarm reset button, a red Alarm light and a horn.",
           "width": 440, "height": 260, "components": [
               {"type": "selectorSwitch", "id": "temp", "label": "Thermostat", "tag": "Overheat", "positions": ["Normal", "Too hot"], "x": 30, "y": 30},
               {"type": "pushButton", "id": "reset", "label": "Alarm reset", "tag": "ResetPB", "color": "green", "x": 30, "y": 140},
               {"type": "pilotLight", "id": "alarm", "label": "Alarm", "tag": "AlarmLight", "color": "red", "x": 250, "y": 30},
               {"type": "horn", "id": "horn", "label": "Horn", "tag": "Horn", "x": 250, "y": 140}]},
    challenge={"schemaVersion": 1, "id": "u5.overheat-alarm", "title": "An alarm that stays on until it is reset",
               "brief": "When the oven gets too hot, the Alarm light and the horn come on, and they stay on even after it cools down, so nobody misses that it happened. Alarm reset turns them off, but only once the temperature is Normal again.",
               "scene": "overheat-alarm", "constraints": {"maxRungs": 6}, "passThreshold": 1.0,
               "hints": ["Remember the alarm in a memory tag, such as Alarm.", "Overheat sets Alarm (a set coil). ResetPB (the Alarm reset button) AND NOT Overheat resets it (a reset coil).", "The light and the horn follow Alarm."],
               "scenarios": [
                   {"name": "Too hot starts the alarm, and it stays on after it cools", "points": 1, "durationSeconds": 2.0,
                    "events": turn("temp", 0.1, True) + turn("temp", 0.6, False),
                    "assertions": [expect(0.3, "AlarmLight", True, "The alarm should come on when it is too hot."), expect(0.3, "Horn", True, "The horn should sound."),
                                   expect(1.5, "AlarmLight", True, "The alarm should stay on after the temperature is back to Normal, until someone resets it.")]},
                   {"name": "Reset clears it once it is Normal", "points": 1, "durationSeconds": 2.0,
                    "events": turn("temp", 0.1, True) + turn("temp", 0.5, False) + press("reset", 1.0, 1.2),
                    "assertions": [expect(0.8, "AlarmLight", True, "The alarm should still be on before Reset."), expect(1.5, "AlarmLight", False, "Reset should turn the alarm off."), expect(1.5, "Horn", False, "Reset should silence the horn.")]},
                   {"name": "Reset does nothing while it is still too hot", "points": 1, "durationSeconds": 2.0,
                    "events": turn("temp", 0.1, True) + press("reset", 0.6, 0.8),
                    "assertions": [expect(1.0, "AlarmLight", True, "While it is still too hot, Reset must not clear the alarm.")]}]},
    tags=[("Overheat", "input", "Thermostat: TRUE while too hot"), ("ResetPB", "input", "Alarm reset button: TRUE while pressed"),
          ("AlarmLight", "output", None), ("Horn", "output", None), ("Alarm", None, "remembers that the oven got too hot")],
    works=[rung("Too hot: set the alarm (it stays set).", no("Overheat"), coil("Alarm", "set")),
           rung("Reset, but only once the temperature is Normal: reset the alarm.", no("ResetPB"), nc("Overheat"), coil("Alarm", "reset")),
           rung("The light follows the alarm.", no("Alarm"), coil("AlarmLight")),
           rung("The horn follows the alarm.", no("Alarm"), coil("Horn"))],
    mistake=[rung("MISTAKE: the light follows the switch, with no memory.", no("Overheat"), coil("AlarmLight")),
             rung("The horn follows the switch too, so the alarm clears itself when the oven cools.", no("Overheat"), coil("Horn"))],
    works_note="Overheat sets a memory tag, Alarm; ResetPB AND NOT Overheat resets it. The light and the horn follow Alarm, so the alarm stays on until someone resets it.",
    mistake_note="The light and the horn follow the temperature switch directly. The moment the oven cools, the alarm clears itself, and nobody knows it was ever too hot.",
    exercise="tank"))

# ---- 5. Push on, push off: a rising edge -- the example for 5.5 (the sorter) -----------------------------------
machines.append(dict(
    scene={"schemaVersion": 1, "id": "push-on-push-off", "title": "Push on, push off",
           "description": "One push button that turns a work light on with one press and off with the next.",
           "width": 400, "height": 200, "components": [
               {"type": "pushButton", "id": "button", "label": "Light button", "tag": "LightPB", "color": "green", "x": 40, "y": 50},
               {"type": "pilotLight", "id": "light", "label": "Work light", "tag": "WorkLight", "color": "amber", "x": 230, "y": 50}]},
    challenge={"schemaVersion": 1, "id": "u5.push-on-push-off", "title": "One button: push on, push off",
               "brief": "Press the Light button once and the work light comes on. Press it again and the light goes off. It must change once per press, however long the button is held down.",
               "scene": "push-on-push-off", "constraints": {"maxRungs": 4}, "passThreshold": 1.0,
               "hints": ["The light must change once, at the moment the button is pressed: a rising edge.", "Rung 1: a rising-edge contact of LightPB turns on Pulse for one scan.", "Rung 2: the light is on when (Pulse AND the light is off) OR (NOT Pulse AND the light is on)."],
               "scenarios": [
                   {"name": "One press turns it on, the next turns it off", "points": 1, "durationSeconds": 2.5, "events": press("button", 0.1, 0.4) + press("button", 1.2, 1.5),
                    "assertions": [expect(0.3, "WorkLight", True, "The light should come on with the first press, while the button is still held."),
                                   expect(1.0, "WorkLight", True, "The light should stay on after the button is let go."),
                                   expect(2.0, "WorkLight", False, "The second press should turn the light off.")]},
                   {"name": "Holding the button down changes it once", "points": 1, "durationSeconds": 3.0, "events": press("button", 0.1, 2.0),
                    "assertions": [expect(1.0, "WorkLight", True, "Holding the button down should not change the light again."), expect(2.5, "WorkLight", True, "The light should still be on after a long press.")]}]},
    tags=[("LightPB", "input", "Light button: TRUE while pressed"), ("WorkLight", "output", None),
          ("Pulse", None, "TRUE for one scan when the button is pressed")],
    works=[rung("A rising edge of the button: Pulse is TRUE for one scan per press.", rising("LightPB"), coil("Pulse")),
           rung("Toggle: on if (Pulse AND it was off) OR (NOT Pulse AND it was on). That is Pulse XOR WorkLight (item 2.4).", parallel(series(no("Pulse"), nc("WorkLight")), series(nc("Pulse"), no("WorkLight"))), coil("WorkLight"))],
    mistake=[rung("MISTAKE: a normally open contact, not a rising edge, so Pulse is TRUE for every scan the button is held.", no("LightPB"), coil("Pulse")),
             rung("Toggle: on if (Pulse AND it was off) OR (NOT Pulse AND it was on).", parallel(series(no("Pulse"), nc("WorkLight")), series(nc("Pulse"), no("WorkLight"))), coil("WorkLight"))],
    works_note="Rung 1 turns the press into a one-scan Pulse with a rising-edge contact (ONS on Allen-Bradley). Rung 2 flips the light on each Pulse.",
    mistake_note="Without the edge, Pulse is TRUE for every scan the button is held (one scan is 10 ms), so the light flips on and off many times a press and ends up wherever it happens to stop.",
    exercise="conveyor-sorter"))

# ---- write ----------------------------------------------------------------------------------------------------
manifest_path = os.path.join(ROOT, 'manifest.json')
with open(manifest_path, encoding='utf-8') as f:
    manifest = json.load(f)
known = {c['file'] for c in manifest['challenges']}
for m in machines:
    sid = m['scene']['id']
    title = m['challenge']['title']
    write(f"scenes/{sid}.json", m['scene'])
    write(f"challenges/{sid}.json", m['challenge'])
    write(f"programs/{sid}.json", program(f"{title} (works)", m['tags'], m['works']))
    write(f"programs/{sid}-mistake.json", program(f"{title} (a common mistake)", m['tags'], m['mistake']))
    entry = {"file": f"challenges/{sid}.json", "example": True,
             "programs": [{"title": "Works", "file": f"programs/{sid}.json", "note": m['works_note']},
                          {"title": "A common mistake", "file": f"programs/{sid}-mistake.json", "note": m['mistake_note']}]}
    if entry['file'] in known:
        manifest['challenges'] = [entry if c['file'] == entry['file'] else c for c in manifest['challenges']]
    else:
        manifest['challenges'].append(entry)
with open(manifest_path, 'w', encoding='utf-8', newline='\n') as f:
    json.dump(manifest, f, indent=2, ensure_ascii=False)
    f.write('\n')
print('wrote', len(machines), 'example machines')
