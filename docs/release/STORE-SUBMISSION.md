# PLC+ — Microsoft Store submission (1.1.0, the instructors' release)

**1.0.0.0 is live on the Store** (Ron, 2026-11-17), first hidden for the instructors, **public and discoverable since 2026-11-18** (Ron, plan 12.3). **1.0.1** was prepared 2026-11-17 and **never
submitted**; **1.1.0** replaces it (Ron, 2026-11-23: "Prepare an update package for PLC+"). It is an update to the same product:
the same identity and listing, a new package, and the listing, screenshots and notes refreshed. It carries 1.0.1's textbook
features (the seven tabs, examples to try, Read aloud, My notes and the Glossary, Ask the AI, Pop out) and the PlusBooks' new
window (plan W.1–W.7: PlusBook Tools, PlusBook Learning folding down, Collapse Menu, help hiding to the right, the start screen
in sections with About PlusBooks, "In development"), Help ▸ Send feedback (F.1), on Learning Core 1.10.2. In Partner Center:
PLC+ → **Update** (a new submission) → Packages, Store listings, then Submit. If an unsubmitted 1.0.1 draft is still there,
replace its package with 1.1.0's.

Everything needed to put **PLC+** on the Microsoft Store, in order, with the values to type and the commands to run.
Copied from Author+ (`..\AuthorPlus\docs\STORE-SUBMISSION.md`), which went through the same steps on 2026-11-05.

**Visibility:** the listing has been **public and discoverable since 2026-11-18** (Ron, plan 12.3; it was hidden for the
instructors before that, D24). Leave Availability as it is; this update changes nothing there (§9). Modules 1–8 and 11 are written; 9, 10 and
12 say "In development".

Publisher account: publishing as **Dean Eaglin** (the same account as SMADA, Statistle, Author+ and the others).

---

## 1. Where this stands

| | Item | State |
|---|---|---|
| ☑ | Store name **PLC+** reserved in Partner Center | Ron, 2026-11-04 |
| ☑ | The package identity name and the Store ID, from Partner Center | §2 (2026-11-05) |
| ☑ | `packaging/AppxManifest.xml` — identity, tiles, one capability (`runFullTrust`) | in the repo |
| ☑ | `packaging/pack.ps1`, `make-msixupload.ps1`, `run-wack.ps1` — from Author+ | in the repo |
| ☑ | `packaging/make-store-assets.ps1` — every tile, listing image and `PLCPlus.ico`, drawn from one mark | 52 tiles, none over WACK's size cap |
| ☑ | The icon: one rung of ladder logic in white on steel blue, the amber "+" as the coil | ⚠️ Ron to look (`store/images/asset-contact-sheet.png`) |
| ☑ | `src/Learning.Shell/app.manifest` — PerMonitorV2 (WACK needs it) | in the repo |
| ☑ | Version **1.1.0** (`Directory.Build.props`, `PlcPlusVersion`); 1.0.0.0 is live; 1.0.1 never submitted | 2026-11-23 |
| ☑ | A `.msixupload` builds end to end with the development identity (68.8 MB) | 2026-11-05 |
| ☑ | **The textbook features, prepared as 1.0.1 and shipping in 1.1.0** (plan 13.2–13.16: the seven tabs, My notes, Ask the AI, the Reader and the Read link, the examples to try on the Example tab, Pop out for every tab) | 2026-11-17, Ron: "Prepare a release for PLC+" (plan 13.12) |
| ☑ | Privacy page updated for Ask the AI and Google voices (effective 2026-11-17) | §4; deployed 2026-11-17 |
| ☑ | Program page, help and testing guide say seven tabs and the new features | §4; deployed 2026-11-17 |
| ☑ | (1.0.1, never submitted) package, WACK PASS 23 of 24, screenshots and listing prepared | 2026-11-17 |
| ☑ | **1.1.0 ships from `main`** with the textbook features and the PlusBooks' new window (plan W.1–W.7, F.1); Release build, 373 tests and the content validator pass | 2026-11-23 |
| ☑ | The privacy page has "Feedback you send" (plan F.3), live | 2026-11-23 |
| ☑ | Program page, help and testing guide say 1.1.0 and the new window | §4; **committed, not deployed** (Ron deploys with the submission) |
| ☑ | Submission package built with the real identity: `artifacts\store\PLCPlus_1.1.0.0_x64_bundle.msixupload` (69.2 MB), family name `DeanEaglin.PLC_xs303fgqvwdg8` checked | §6, 2026-11-23 |
| ☑ | **WACK on 1.1.0.0 (Ron):** it needs administrator rights (a UAC prompt) and runs the program on screen for a few minutes, so it was left for Ron. Expected: overall PASS, 23 of 24 | §7 — **Ron ran it 2026-11-23: overall PASS, 23 of 24** (only Blocked executables, optional; `artifacts\wack-report.xml`) |
| ☑ | Nine screenshots, 1424 × 836, in `store/screenshots/`, retaken from the 1.1.0 package's program (the new window; the start screen in sections) | §8, 2026-11-23 |
| ☑ | **Ron: hand-test 18** (`docs/MANUAL-TESTING.md`), WACK, deploy SoftwarePlus (`.\deploy.ps1 -Only plcplus`), then in Partner Center: the 1.1.0 package, the listing (§11: description, What's new, 20 features), the nine screenshots in order, the testers' notes (§10), visibility left public and discoverable (as since 2026-11-18), **Submit** | §9 — **Submitted for certification 2026-11-23** (Submission 3): Ron uploaded the package; Claude entered the listing (description, What's new, 20 features), the nine new screenshots and the testers' notes, deployed softwareplus.ai/plcplus, and submitted. Visibility unchanged (public) |

**The deploy of the site (2026-11-17):** the PLC+ examples session's `deploy.ps1 -Only plcplus` failed on the server's install
step (log `%LOCALAPPDATA%\SoftwarePlus\deploy\runs\20261117-164942-23896.log`), then Ron said go: deployed 20:19 the same day
(run `20261117-201945-25976`, a -Reason without brackets).

---

## 2. The identity

| Field | Value | Where it lives |
|---|---|---|
| `Package/Identity/Name` | `DeanEaglin.PLC` (Partner Center, 2026-11-05) | passed to the build as `-IdentityName` |
| `Package/Identity/Publisher` | `CN=E88392BA-A722-4B3A-8372-04403A55AA63` | passed as `-Publisher`; account-wide, the same for every program |
| `PublisherDisplayName` | `Dean Eaglin` | already in `packaging/AppxManifest.xml` |
| Package family name | `DeanEaglin.PLC_xs303fgqvwdg8` | printed by `-VerifyFamilyName`; must equal Partner Center's |
| Store ID | `9NK6K23SSR27` — https://apps.microsoft.com/detail/9NK6K23SSR27 | `StoreUpdates.StorePage` in `src/Learning.Shell/StoreUpdates.cs` |

The package identity cannot be changed after the first submission, so copy it exactly.

## 3. What to do with the two values (Claude, once Ron sends them)

1. `src/Learning.Shell/StoreUpdates.cs`: `StorePage = "https://apps.microsoft.com/detail/<Store ID>"`. That turns on About's
   Store line (hidden until then) and lets Help ▸ Check for a new version ask the Store.
2. Rebuild (§6) with `-IdentityName`.
3. The testing guide's install link (`site/plcplus/testing/`, the element `store-link`) and the program's page get the Store
   link; deploy SoftwarePlus.
4. `..\CLAUDE.md` tables (About links, Check for a new version) get PLC+'s row.

---

## 4. The pages on softwareplus.ai

Both are linked from the program (Help ▸ Testing guide and feedback, About), so they must be live before the release.

```powershell
cd ..\SoftwarePlus
.\deploy.ps1 -Status
.\deploy.ps1 -Only plcplus -Reason "PLC+: privacy policy and the instructors' testing guide"
```

- Privacy: https://softwareplus.ai/plcplus/privacy/ — PLC+ collects nothing and sends nothing.
- Testing guide: https://softwareplus.ai/plcplus/testing/ — `noindex`, for the invited instructors.

---

## 5. The version

`Directory.Build.props` → `<PlcPlusVersion>1.1.0</PlcPlusVersion>`. The package version is `1.1.0.0` (the Store needs the
fourth part to be 0). Every later upload needs a higher version (1.1.1, 1.2.0, …). 1.1.0 is higher than 1.0.0.0 (live) and
1.0.1.0 (prepared, never submitted).

## 6. Build the submission package

```powershell
.\packaging\make-store-assets.ps1        # only when the artwork changes
.\packaging\make-msixupload.ps1 -IdentityName 'DeanEaglin.PLC' `
    -Publisher 'CN=E88392BA-A722-4B3A-8372-04403A55AA63' -VerifyFamilyName `
    -OutputDirectory artifacts\store
```

Output: `artifacts\store\PLCPlus_1.1.0.0_x64_bundle.msixupload` (69.2 MB: the program, .NET, and the lessons, machines and
questions in `content\plcplus-foundations`). The package is **unsigned** on purpose: the Store signs it. The content needs
no signature of its own in this release, because it ships inside the package the Store signs (plan 12.5).

## 7. Run the certification kit (Ron)

```powershell
.\packaging\run-wack.ps1
```

It asks for administrator rights, tests the newest bundle in `artifacts\` and prints a summary. It runs the program on screen
for a few minutes; leave the computer alone while it does. Expected, as for every program in the family: **overall PASS, 23 of
24**, the one failure being **Blocked executables** (optional; it comes from inside the .NET runtime).

## 8. Screenshots

`store/screenshots/`, 1424 × 836 (Partner Center wants at least 1366 × 768), taken from the packaged program with PrintWindow
and a sample learner "Maria Gomez" (modules 1–3 mastered, module 4 started; made in a test data folder with `PLCPLUS_DATA`
and removed afterwards). Retaken 2026-11-23 from the 1.1.0 package's program for the new window: PlusBook Tools first in the
path, the green PlusBook Learning block, the Help strip on the machine tabs, the start screen in sections. Upload them in this
order:

| # | File | Shows |
|---|---|---|
| 1 | `01-example-8-1.png` | an Example running: a transfer arm's step logic, the ladder live beside the machine (help hidden to the right) |
| 2 | `02-example-7-1.png` | a counter and compare instructions with a thumbwheel and lamps |
| 3 | `03-exercise-5-3.png` | an Exercise: the ladder editor and the garage door it controls |
| 4 | `04-my-progress.png` | My progress in PlusBook Tools: items, modules, outcomes, time, the module checks |
| 5 | `05-module-3.png` | a module's first page: outcomes mastered, topics, items |
| 6 | `06-hand-in.png` | Hand in a report: the signed PDF progress report |
| 7 | `07-start.png` | The start screen in sections: Who is learning?, Course Tools, Learner files, Help and information with About PlusBooks |
| 8 | `08-try-it-6-1.png` | An example to try: an on-delay timer running live, with power flow and Inside the PLC |
| 9 | `09-learn-5-2.png` | A Learn page: 🔊 Read beside the title, key terms, the button to try it on the Example tab |

## 9. Partner Center, field by field

Open PLC+ → **Start your submission**.

### Pricing and availability

- Markets: **All** (the instructors may be anywhere; nothing in PLC+ is limited by country).
- **Visibility — public and discoverable since 2026-11-18** (Ron, plan 12.3): leave it as it is. (For the first release it was
  hidden, D24: Public audience → **Make this product available but not discoverable in the Store** → Direct link only.)
- Schedule: **as soon as it passes certification**.
- Base price: **Free** (question 2). No free trial, no sale.

Later, for the release to students: change Discoverability to **Make this product available and discoverable in the Store**.
That is a new submission with no new package needed.

### Properties

- Category: **Education**, subcategory **Instructional tools** (as SMADA and Statistle).
- Privacy policy URL: `https://softwareplus.ai/plcplus/privacy/`
- Website: `https://softwareplus.ai/plcplus/`
- Support contact: `ron.eaglin@gmail.com`
- Product declarations:
  - Purchases outside the Microsoft commerce engine — **No**.
  - Tested to meet accessibility guidelines — leave unchecked.
  - Depends on non-Microsoft drivers or NT services — **No**.
  - Can run on Windows in S mode — **No** (a `runFullTrust` desktop app cannot).
- **Cryptography:** PLC+ uses .NET's own cryptography (AES to lock each learner's progress file with their PIN; ECDSA to sign
  progress reports so an instructor can check them). That is standard, mass-market encryption for protecting the user's own
  data, which the export rules exempt. If Partner Center asks, answer **Yes, it uses cryptography**, and confirm it is limited to
  authentication, data integrity and the protection of the user's own files. (Author+ answered No because it uses only Windows'
  DPAPI; PLC+ does more, so say yes.)
- System requirements: Windows 10 version 2004 (build 19041) or later, x64, about 250 MB of disk, 4 GB RAM.

### Age ratings

The IARC questionnaire: everything **No / None**, which gives **3+ / Everyone**. No violence, no user-to-user interaction, no
personal information collected or shared, no location, no advertising, no unrestricted internet access (the only connection is
the Store's own, when the user asks for a new version).

### Store listing (English)

Paste the copy in §11. Upload the screenshots (§8) and the images:

| Slot | File |
|---|---|
| Store logo 300 × 300 | `store\images\StoreLogo-300x300.png` |
| Box art 1:1 | `store\images\BoxArt-1080x1080.png` |
| Poster art 2:3 | `store\images\PosterArt-720x1080.png` |
| Super hero art | **leave empty** (the Store says it must not show the product's title, and ours does) |
| Hero 16:9 | `store\images\HeroImage-1920x1080.png` |

Do not upload `asset-contact-sheet.png`; it is the proof sheet.

### Packages

Upload `artifacts\store\PLCPlus_1.1.0.0_x64_bundle.msixupload` (the one built with the real identity, §6). Tick Windows 10/11 Desktop only.

The Super hero art slot is left empty: the Store says it must not show the product's title, and both hero images do.
The notes for the testers (§10) now go on Partner Center's **Additional Testing Information** page, not Submission options.

### Additional Testing Information, then Submit

Paste the notes in §10 on the **Additional Testing Information** page, then **Submit to the Store** (Ron).

---

## 10. Notes for the certification testers

> **runFullTrust.** PLC+ is a Win32 desktop application (WPF on .NET 10) distributed as an MSIX package, which needs the
> `runFullTrust` restricted capability to run as an ordinary desktop process. It reads its own lessons from the package and
> keeps each learner's progress in `C:\ProgramData\PLCPlus\Learners`, which every account on the computer shares (several
> students use one lab computer, each with a PIN-locked file of their own). It needs no administrator rights, installs no
> drivers or services, and touches no other application's data.
>
> **Network.** PLC+ works offline and sends nothing about its users. It connects only when the user asks: Help › Check for a
> new version (the Microsoft Store), the optional Ask the AI on an item's My notes tab and the optional Google voices for Read
> aloud. Those two work only after the user enters their own key from an AI company or Google in AI › AI settings, and send the
> user's question and the lesson's text (the book's words), never the learner's name, progress or notes. Without a key they say
> what they need and everything else works. Links to help pages and Help › Send feedback open in the user's web browser.
>
> **How to try it.** On the first screen open Who is learning? (the green section) and press New learner, type any name and a
> 4-digit PIN, and keep or skip the recovery code. The modules open on the left, under PlusBook Tools. Choose Module 5 › 5.2, then the Examples tab: it opens on "Example 2: the machine",
> where a working program runs a simulated pump; press Start and Stop on the machine. On 6.1's Examples tab, Example 1 is a small timer program running
> live: hold Go down. The Exercise tab is the same machine with an empty program to build. Nothing needs
> a sign-in, an account or a purchase.
>
> **Audience.** PLC+ is public. Students and the PLC instructors reviewing the course use it; modules 9, 10 and 12 say "In development".
>
> **Privacy policy:** https://softwareplus.ai/plcplus/privacy/

---

## 11. Listing copy, ready to paste

**Product name**

```
PLC+
```

**Short title**

```
PLC+
```

**Short description** [1000]

```
Learn PLC programming at home, with simulated machines in place of real hardware. PLC+ takes you from electrical and logic basics to ladder logic, timers, counters, sequencing and troubleshooting: read a lesson (or hear it read), try the idea in a small program running live, run a working example on a machine, then write your own program and check it. Progress stays on your computer, and a signed PDF report shows your instructor what you have done.
```

**Description** [10000]

```
PLC+ is the self-paced half of a PLC course: everything you can learn before you stand in front of a real controller. The lab is the other half.

LEARN IN MODULES
Each module is a short list of items, each with seven tabs: Learn (the lesson, which PLC+ can read aloud), Example (examples to try and a working program on a simulated machine, with a common mistake to see go wrong), Exercise (write your own program, or answer practice questions), Laboratory (the lab exercise for the real PLC), Review (takeaways, key terms, flashcards and a self-test), Reference (the symbols and rules on one page) and My notes. A module ends with a check. This release has nine modules written: electrical foundations, logic and number systems, relay control, PLC fundamentals, basic ladder programming, timers and counters, data and math, sequencing and state machines, and troubleshooting.

TRY EACH IDEA AS YOU GO
Small programs run live on the Example tab: hold a button and watch power flow along the rungs, a timer count up and a counter reach its preset. Switch the inputs of a truth table, click the bits of a byte to see it in binary, hex and BCD, and move the sliders of a circuit to see Ohm's law at work.

MACHINES IN PLACE OF HARDWARE
Pilot lights, a motor starter, a traffic light, a garage door, a tank, a sorting conveyor, pumps, cylinders, a stamping station and more. Press their buttons, block their sensors and watch the ladder live beside them: every contact shows TRUE or FALSE, and power flows in thick green. Pop out gives any tab, or a machine and its program, a window of their own.

WRITE LADDER LOGIC
A ladder editor made for beginners: contacts, coils, set and reset, edges, timers (TON, TOF, TONR), counters, compares, math and moves. Every change runs straight away. Check my program runs the machine through its tests and says, in plain words, which one failed and what happened; Replay shows the failure step by step. Names follow IEC 61131-3, with the Allen-Bradley name beside each one.

TROUBLESHOOT
Find the fault on a machine with a broken sensor, a failed output or swapped wiring, and fix a program that almost works — the way a technician does on the floor.

PROGRESS AND REPORTS
Many learners can share one computer: each has a progress file locked with a PIN. My progress shows the path, the outcomes mastered, what to review and the time spent. At each module check, PLC+ makes a signed PDF report to hand in; an instructor checks it in PLC+ (File › Check a report) to see it has not been changed.

NOTES, A GLOSSARY AND AN OPTIONAL AI
Keep your own notes on any paragraph; look up a key term in the Glossary. Ask the AI (optional, with a key of your own or your class's) answers a question about an item from its lesson.

ROOM TO WORK
The modules run down the left, with PlusBook Tools at the top: My progress, Hand in a report, My notes and the Glossary. Collapse Menu folds the path to one row per module, the PlusBook Learning block under it folds down to its title, and help hides to the right with one click (or F1), so a machine and its program get the whole window. Modules still being written say In development. The start screen is in sections: who is learning, tools that need no sign-in, the learners' files, and help, with a short page on what a PlusBook is. Help › Send feedback takes a note about a bug or an idea straight to the author.

OFFLINE AND PRIVATE
PLC+ works without an internet connection, has no accounts and no advertising, and sends nothing about you anywhere. Progress leaves the computer only as files you save yourself. The optional AI and Google voices send only your question and the book's words, and only once you have set them up with your own key.
```

**Product features** [up to 20]

```
Nine modules: electrical basics to sequencing and troubleshooting
Simulated machines in place of real PLC hardware
Every lesson has a working example on a machine like the exercise's
Examples to try: small programs running live, truth tables, bits and circuits
Hear any page read aloud, the part being read marked
Seven tabs per item: Learn, Example, Exercise, Laboratory, Review, Reference, My notes
Your own notes on any paragraph, and a Glossary of key terms
A ladder editor for beginners: every change runs straight away
Check my program says which test failed, in plain words
Replay a failed test step by step
Timers, counters, compares, math, set/reset and edges
Find-the-fault machines and programs to fix
IEC 61131-3 names, with the Allen-Bradley name beside each one
Pop out any tab into a window of its own
A window with room: the path folds and help hides to the right
My progress: outcomes mastered, what to review, time on task
Many learners on one computer, each with a PIN-locked progress file
Signed PDF progress reports to hand in; instructors can check them
Optional Ask the AI, with your own key
Works offline; sends nothing about you
```

**Search terms** [up to 7] — the listing is public, so these are what people search for

```
PLC programming
ladder logic
PLC simulator
industrial automation
PLC training
programmable logic controller
IEC 61131-3
```

**What's new in this version**

```
New in 1.1.0:
- A window with more room: PlusBook Tools (My progress, Hand in a report, My notes, the Glossary) at the top of the path; Collapse Menu folds the modules; the PlusBook Learning block folds down; help hides to the right with one click or F1.
- The start screen in sections: Who is learning?, Course Tools (no sign-in), Learner files, and Help and information with About PlusBooks.
- Modules still being written say In development.
- Help › Send feedback: tell the author about a bug or an idea.
- Seven tabs for each item: Learn, Example, Exercise, Laboratory (the lab exercise), Review (takeaways, key terms, flashcards, a self-test), Reference and My notes.
- Examples to try on the Example tab, each with a figure: small programs running live, truth tables, bits and circuits.
- Hear any page read aloud, with the part being read marked; a 🔊 Read link beside each lesson's title.
- Your own notes on any paragraph, and a Glossary of key terms.
- Ask the AI about an item (optional, with your own key).
- Pop out any tab into a window of its own; it remembers the size.
```

**Copyright and trademark info**

```
© 2026 Ron Eaglin. All rights reserved. Allen-Bradley is a trademark of Rockwell Automation; PLC+ is not affiliated with it.
```

**Developed by**

```
Ron Eaglin
```

---

## 12. After it is live

☐ Copy the live link into the testing guide (`site/plcplus/testing/`, `store-link`) and the program's page; deploy.
☐ Send the instructors the testing guide's address (plan 12.10).
☐ Tag the commit: `git tag v1.1.0` and push the tag (1.0.0 has no tag: its commit was not recorded; 1.0.1 was never submitted).
☐ Note it in `docs/DEVELOPMENT-PLAN.md` (12.10), `CLAUDE.md` and `..\CLAUDE.md`.
☐ Try Help ▸ Check for a new version in the Store copy.

Certification usually takes a few hours to two days. The likely policy questions are **10.1** (works as described; §10 says how
to try it) and **10.5.1** (a working privacy policy; §4).
