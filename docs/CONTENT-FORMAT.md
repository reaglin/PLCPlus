# PLC+ — how content is written

PLC+'s lessons are files, not code (NFR-4): an author edits Markdown and JSON, runs the validator, and the program shows the
change. This page is for whoever writes or reviews them. The format was set in phase 4 (2026-11-04, Ron: *modules → items, each
item with Learn, Example, Exercise and Reference*); expect it to grow as the instructors' review comes back.

## The package

```
content/plcplus-foundations/
  manifest.json          id, version, title; "modules" (the module folders, in path order); "challenges" (the machines' tasks)
  modules/
    05-basic-ladder/
      module.md          the module: settings, then its introduction
      01-contacts-coils.md   an item: settings, then its Learn page
      02-seal-in.md
      …
      06-check.md        the module check, last
  reference/
    seal-in.md           a Reference page, shared by every item that lists it
  challenges/ programs/ scenes/   the machines (see docs/PLAN.md, the grader)
```

Items are shown in **file-name order**, and numbered from it (the third file of module 5 is item 5.3), so start each file name
with `01-`, `02-`, …

## A module (`module.md`)

```
---
id: plc.m5
number: 5
title: Basic ladder programming
status: draft
minutes: 300
prerequisites: [plc.m4]
outcomes:
  - plc.m5.o1: Write rungs with normally open and normally closed contacts …
  - plc.m5.o2: Write a seal-in rung that starts and stops a motor …
topics:
  - Contacts and coils
  - The seal-in
---

The introduction, in Markdown: shown under "About this module" on the module's first page.
```

| Setting | Meaning |
|---|---|
| `id` | Never changes once published: progress and reports key off it |
| `status` | `ready`, `draft` (a first draft for review), `outline` (titles and outcomes only), `coming-later` (no items yet) |
| `prerequisites` | Module IDs to finish first. **Recommended, not enforced**: the module opens anyway and the report shows it was early. Each must come earlier in the path |
| `outcomes` | `id: text`. The ID is stable, so a course's or a grant's outcomes can be matched to it |
| `minutes` | About how long the module takes |

## An item

```
---
id: plc.m5.seal-in
title: The seal-in: start and stop a motor
minutes: 40
objectives: [plc.m5.o2]
builds-on: plc.m5.contacts-coils
example: u5.motor-start-stop
exercise: u5.motor-start-stop
reference: [seal-in, wiring-stop, contacts, coils]
---

# The seal-in: start and stop a motor

The Learn page, in Markdown …
```

| Setting | The tab it fills |
|---|---|
| (the Markdown after the settings) | **Learn** |
| `example` (a challenge ID), `example-program` (which of its programs first; the first if left out) | **Example**: the machine running that program; the learner can switch to the common mistake |
| `exercise` (a challenge ID) | **Exercise**: My program, checked against that challenge. **The item is complete when it passes.** An item with no exercise is complete when the learner presses *I have read this* |
| `reference` (Reference page names) | **Reference**: those pages, one after another |
| `objectives` | The module outcomes this item teaches |
| `builds-on` | The item before it in the learner's mind, shown in the header |

**The module check** is the module's last item, with `kind: assess` and `covers: [item IDs]`. It is complete when every item it
covers is complete; its Exercise tab lists them, and its Reference tab lists the module's outcomes. In phase 8 it is the
submission point that makes the progress report.

## The Markdown PLC+ shows

Headings (`#`, `##`, `###`), paragraphs, `- ` bullets, `1. ` numbered steps, tables, `**bold**`, `*italic*` and `` `code` ``.
A `|` inside `` `code` `` in a table stays in the cell, so ladder symbols such as `` `-| |-` `` can be written there. Web addresses
(https://…) become links. Anything else shows as written.

**Widgets**, each on a line of its own:

| Line | Shows |
|---|---|
| `[[ladder: programs/motor-seal-in.json]]` | The program's rungs, drawn as on the Example tab but not running |
| `[[truth-table: (Start OR Motor) AND Stop]]` | Every row of the expression: AND, OR, XOR, NOT and brackets; up to four inputs |

## Before publishing: the validator

```
dotnet run --project tools/ContentValidator                  (the foundations package)
dotnet run --project tools/ContentValidator path/to/package
```

It checks that every ID is used once; prerequisites exist and come earlier; objectives are outcomes; examples and exercises name
challenges in the manifest, whose programs and scenes exist; `example-program` is one of that challenge's programs; Reference
pages exist; widgets can be drawn; each module has one check, which covers real items; and a draft or ready module has no empty
Learn page. It prints one sentence per problem, naming the file, and ends with exit code 1 if there are any. A test runs it on
the shipped package, so a broken package fails the build's tests.

## Words

The rule for every page is the program's rule: **words for a beginner**. Say what a thing does before its name; give the
Allen-Bradley name beside the IEC one (XIC, XIO, OTE, OTL, OTU, ONS); end a lesson with what to do on the Example and Exercise
tabs, using the buttons' own names.
