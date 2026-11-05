---
title: Contacts
---

# Contacts

| Symbol | Name | Passes power when | Allen-Bradley |
|---|---|---|---|
| `-| |-` | Normally open | its tag is TRUE | XIC |
| `-|/|-` | Normally closed | its tag is FALSE | XIO |
| `-|P|-` | Rising edge | for one scan, when its tag turns TRUE | XIC + ONS |
| `-|N|-` | Falling edge | for one scan, when its tag turns FALSE | XIO + ONS |

A contact **reads** a tag; it never changes it. In PLC+ the editor keys are O (open), C (closed), P and N.
