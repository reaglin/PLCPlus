# PLC+ on a lab computer (plan 12.4)

For the person who looks after a lab or classroom computer that several students share. PLC+ comes from the Microsoft Store
(free); it needs no administrator rights to run, and no network once installed.

## Where the learners' files are

Every account on the computer shares one folder: **`C:\ProgramData\PLCPlus\Learners`**. Each learner has one file there
(`<id>.plcprogress`), locked with their PIN. PLC+ makes the folder the first time it runs and lets every account on the
computer change files in it (by default Windows lets only the account that made a file under ProgramData change it). The
program's start screen shows the folder, and **Open the learners' folder** opens it.

The Store's package does not move this folder: an MSIX program's writes are redirected only for the user's own AppData, and
PLC+ keeps nothing there. So a learner who signs in as a different Windows account on the same computer still finds their name
on the start screen. (The computer's settings — where the learners' files are kept, and when PLC+ locks itself — sit beside the folder, in
`C:\ProgramData\PLCPlus\settings.json`. **Settings…** on the start screen can point PLC+ at another folder, such as a network
drive every lab computer shares.)

**Computers that are wiped or reimaged** (Deep Freeze, a nightly restore, a shared image): the folder goes with everything
else. Students must back up (Learner ▸ Back up my progress, to a USB drive or a cloud folder) — PLC+ asks them to, and the
start screen shows when each learner last did. On the next computer, **Add learner from file** brings the progress back; if
the learner is already there, the two copies are combined and nothing is lost. To keep the files between restores instead,
exclude `C:\ProgramData\PLCPlus` from the restore.

## Installing it

**One account at a time (simplest).** Sign in as the account the students use, open the PLC+ Store link, press **Get**. A
second account on the same computer installs in a few seconds: Windows keeps one copy of the files and registers it for each
account.

**Every account at once (an administrator, with winget).** In a terminal run as administrator:

```powershell
winget install --id 9NK6K23SSR27 --source msstore --scope machine --accept-package-agreements --accept-source-agreements
```

`9NK6K23SSR27` is PLC+'s Store ID (the end of its Store link, `apps.microsoft.com/detail/9NK6K23SSR27`). If the Store does not allow a
machine-wide install, winget says so; then install it once per account, as above, or through Intune ("Microsoft Store app
(new)", assigned to the device).

**Updates** come from the Store on their own. Help ▸ Check for a new version (or the start screen's button) asks the Store at
once. An update keeps every learner's progress: the files are not in the program's folder.

## Removing it

Uninstalling PLC+ (Settings ▸ Apps) leaves `C:\ProgramData\PLCPlus` in place, so no learner's progress is lost by mistake.
Delete that folder too when the learners no longer need it — after they have their backups.

## Hand-test for Ron (12.4)

On a computer with two Windows accounts, A and B, and PLC+ installed from the Store link:

1. **Shared login.** In account A, make two learners (X and Y, different PINs). Close PLC+. Open it again: both are listed; each
   opens only with its own PIN.
2. **Separate logins.** Sign in as B and open PLC+ (install it there if the Store asks). X and Y are listed. Open X with its
   PIN, finish one reading item, close. Sign back in as A: X shows the item complete. (This is the check that the folder is shared
   and B could change the file A made.)
3. **The folder.** On the start screen, Open the learners' folder: it is `C:\ProgramData\PLCPlus\Learners`, with two files.
4. **An update** (when 1.0.1 exists): install it; X and Y are still there.
