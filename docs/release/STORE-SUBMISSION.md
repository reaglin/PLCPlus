# PLC+ — Microsoft Store submission (1.0.0, the instructors' release)

Everything needed to put **PLC+** on the Microsoft Store, in order, with the values to type and the commands to run.
Copied from Author+ (`..\AuthorPlus\docs\STORE-SUBMISSION.md`), which went through the same steps on 2026-11-05.

**This release is for the PLC instructors who give feedback (D24, Ron 2026-11-05):** the listing is **hidden** — available
only through its direct link, not found by searching the Store (§9, Availability). Modules 1–8 and 11 are written; 9, 10 and
12 say "coming later".

Publisher account: publishing as **Dean Eaglin** (the same account as SMADA, Statistle, Author+ and the others).

---

## 1. Where this stands

| | Item | State |
|---|---|---|
| ☑ | Store name **PLC+** reserved in Partner Center | Ron, 2026-11-04 |
| ☐ | **NEEDS RON:** the package identity name and the Store ID, copied out of Partner Center | §3 |
| ☑ | `packaging/AppxManifest.xml` — identity, tiles, one capability (`runFullTrust`) | in the repo |
| ☑ | `packaging/pack.ps1`, `make-msixupload.ps1`, `run-wack.ps1` — from Author+ | in the repo |
| ☑ | `packaging/make-store-assets.ps1` — every tile, listing image and `PLCPlus.ico`, drawn from one mark | 52 tiles, none over WACK's size cap |
| ☑ | The icon: one rung of ladder logic in white on steel blue, the amber "+" as the coil | ⚠️ Ron to look (`store/images/asset-contact-sheet.png`) |
| ☑ | `src/Learning.Shell/app.manifest` — PerMonitorV2 (WACK needs it) | in the repo |
| ☑ | Version 1.0.0 (`Directory.Build.props`, `PlcPlusVersion`) | |
| ☑ | A `.msixupload` builds end to end with the development identity (68.8 MB) | 2026-11-05 |
| ☐ | Privacy page live at https://softwareplus.ai/plcplus/privacy/ | §4 |
| ☐ | Testing guide live at https://softwareplus.ai/plcplus/testing/ | §4 |
| ☐ | Submission package built with the real identity | §6, after §3 |
| ☐ | WACK run and passing (Ron: it needs an administrator window) | §7 |
| ☑ | Seven screenshots, 1424 × 836, in `store/screenshots/` | §8 |
| ☐ | Partner Center submission | §9 |

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

`Directory.Build.props` → `<PlcPlusVersion>1.0.0</PlcPlusVersion>`. The package version is `1.0.0.0` (the Store needs the
fourth part to be 0). Every later upload needs a higher version (1.0.1, …).

## 6. Build the submission package

```powershell
.\packaging\make-store-assets.ps1        # only when the artwork changes
.\packaging\make-msixupload.ps1 -IdentityName 'DeanEaglin.PLC' `
    -Publisher 'CN=E88392BA-A722-4B3A-8372-04403A55AA63' -VerifyFamilyName `
    -OutputDirectory artifacts\store
```

Output: `artifacts\PLCPlus_1.0.0.0_x64_bundle.msixupload` (about 69 MB: the program, .NET, and the lessons, machines and
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
and the sample learner "Maria Gomez":

| # | File | Shows |
|---|---|---|
| 1 | `01-example-8-1.png` | an Example running: a transfer arm's step logic, the ladder live beside the machine |
| 2 | `02-example-7-1.png` | a counter and compare instructions with a thumbwheel and lamps |
| 3 | `03-exercise-5-3.png` | an Exercise: the ladder editor and the garage door it controls |
| 4 | `04-my-progress.png` | My progress: items, modules, outcomes, time, the module checks |
| 5 | `05-module-3.png` | a module's first page: outcomes mastered, topics, items |
| 6 | `06-hand-in.png` | Hand in a report: the signed PDF progress report |
| 7 | `07-start.png` | Who is learning? — learners on a shared computer |

## 9. Partner Center, field by field

Open PLC+ → **Start your submission**.

### Pricing and availability

- Markets: **All** (the instructors may be anywhere; nothing in PLC+ is limited by country).
- **Visibility — hidden (D24):** choose **Public audience**, then under Discoverability **Make this product available but not
  discoverable in the Store** → **Direct link only**. Anyone with the link can install it; it does not appear in search or
  lists. (Not "Private audience": that needs every instructor's Microsoft account e-mail in a group first.)
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
| Super hero art | `store\images\SuperHeroArt-2400x1200.png` |
| Hero 16:9 | `store\images\HeroImage-1920x1080.png` |

Do not upload `asset-contact-sheet.png`; it is the proof sheet.

### Packages

Upload `artifacts\store\PLCPlus_1.0.0.0_x64_bundle.msixupload` (the one built with the real identity, §6). Tick Windows 10/11 Desktop only.

The Super hero art slot is left empty: the Store says it must not show the product's title, and both hero images do.
The notes for the testers (§10) now go on Partner Center's **Additional Testing Information** page, not Submission options.

### Submission options

Paste the notes in §10, then **Submit to the Store**.

---

## 10. Notes for the certification testers

> **runFullTrust.** PLC+ is a Win32 desktop application (WPF on .NET 10) distributed as an MSIX package, which needs the
> `runFullTrust` restricted capability to run as an ordinary desktop process. It reads its own lessons from the package and
> keeps each learner's progress in `C:\ProgramData\PLCPlus\Learners`, which every account on the computer shares (several
> students use one lab computer, each with a PIN-locked file of their own). It needs no administrator rights, installs no
> drivers or services, and touches no other application's data.
>
> **No network.** PLC+ works offline and sends nothing. The only connection is to the Microsoft Store itself, when the user
> chooses Help › Check for a new version. Links to help pages open in the user's web browser.
>
> **How to try it.** On the first screen press New learner, type any name and a 4-digit PIN, and keep or skip the recovery
> code. The learning path opens on the left. Choose Module 5 › 5.2, then the Example tab: a working program runs a simulated
> motor; press Start and Stop on the machine. The Exercise tab is the same machine with an empty program to build. Nothing needs
> a sign-in, an account or a purchase.
>
> **Audience.** This 1.0 release is hidden (direct link) for the PLC instructors reviewing the course before students use it.
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
Learn PLC programming at home, with simulated machines in place of real hardware. PLC+ takes you from electrical and logic basics to ladder logic, timers, counters, sequencing and troubleshooting: read a lesson, run a working example on a machine, then write your own program and check it. Progress stays on your computer, and a signed PDF report shows your instructor what you have done.
```

**Description** [10000]

```
PLC+ is the self-paced half of a PLC course: everything you can learn before you stand in front of a real controller. The lab is the other half.

LEARN IN MODULES
Each module is a short list of items, each with four tabs: Learn (the lesson), Example (a working program on a simulated machine, and a common mistake to see go wrong), Exercise (write your own program, or answer practice questions) and Reference (the symbols and rules on one page). A module ends with a check. This release has nine modules written: electrical foundations, logic and number systems, relay control, PLC fundamentals, basic ladder programming, timers and counters, data and math, sequencing and state machines, and troubleshooting.

MACHINES IN PLACE OF HARDWARE
Pilot lights, a motor starter, a traffic light, a garage door, a tank, a sorting conveyor, pumps, cylinders, a stamping station and more. Press their buttons, block their sensors and watch the ladder live beside them: every contact shows TRUE or FALSE, and power flows in thick green. Pop out gives a machine and its program a window of their own.

WRITE LADDER LOGIC
A ladder editor made for beginners: contacts, coils, set and reset, edges, timers (TON, TOF, TONR), counters, compares, math and moves. Every change runs straight away. Check my program runs the machine through its tests and says, in plain words, which one failed and what happened; Replay shows the failure step by step. Names follow IEC 61131-3, with the Allen-Bradley name beside each one.

TROUBLESHOOT
Find the fault on a machine with a broken sensor, a failed output or swapped wiring, and fix a program that almost works — the way a technician does on the floor.

PROGRESS AND REPORTS
Many learners can share one computer: each has a progress file locked with a PIN. My progress shows the path, the outcomes mastered, what to review and the time spent. At each module check, PLC+ makes a signed PDF report to hand in; an instructor checks it in PLC+ (File › Check a report) to see it has not been changed.

OFFLINE AND PRIVATE
PLC+ works without an internet connection, has no accounts and no advertising, and sends nothing anywhere. Progress leaves the computer only as files you save yourself.
```

**Product features** [up to 20]

```
Nine modules: electrical basics to sequencing and troubleshooting
Simulated machines in place of real PLC hardware
Every lesson has a working example on a machine like the exercise's
A ladder editor for beginners: every change runs straight away
Check my program says which test failed, in plain words
Replay a failed test step by step
Timers, counters, compares, math, set/reset and edges
Find-the-fault machines and programs to fix
IEC 61131-3 names, with the Allen-Bradley name beside each one
Pop out a machine and its program into a window of their own
My progress: outcomes mastered, what to review, time on task
Many learners on one computer, each with a PIN-locked progress file
Signed PDF progress reports to hand in; instructors can check them
Works offline; sends nothing
```

**Search terms** [up to 7] — few people will search while the listing is hidden, but they carry over to the public release

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
First release, for the PLC instructors reviewing the course.
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
☐ Tag the commit: `git tag v1.0.0` and push the tag.
☐ Note it in `docs/DEVELOPMENT-PLAN.md` (12.10), `CLAUDE.md` and `..\CLAUDE.md`.
☐ Try Help ▸ Check for a new version in the Store copy.

Certification usually takes a few hours to two days. The likely policy questions are **10.1** (works as described; §10 says how
to try it) and **10.5.1** (a working privacy policy; §4).
