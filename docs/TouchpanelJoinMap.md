# Touchpanel Direct Join Map

All join numbers used directly on touchpanel devices (NOT SmartObject-relative, NOT EISC joins).

Last updated: 2026-06-10

---

## Digital / Boolean Joins

| Join | Function | File | Notes |
|------|----------|------|-------|
| 1 | Home button | TouchpanelUI.SigChange.cs | Page navigation |
| 2 | TSR-310 home button | TouchpanelUI.SigChange.cs | Sets page to room subsystem list |
| 6 | TSR-310 volume up | TouchpanelUI.SigChange.cs | |
| 7 | TSR-310 volume down | TouchpanelUI.SigChange.cs | |
| 8 | TSR-310 mute | TouchpanelUI.SigChange.cs | |
| 11 | Home page indicator (fb) | TouchpanelUI.PageFlips.cs | Output |
| 12 | Room page indicator (fb) | TouchpanelUI.PageFlips.cs | Output |
| 14 | Home button handler | TouchpanelUI.SigChange.cs | |
| 15 | Room button | TouchpanelUI.SigChange.cs | |
| 16 | Room list button | TouchpanelUI.SigChange.cs | |
| 20 | Music zones notification (fb) | TouchpanelUI.ButtonFeedback.cs | "X zones playing" |
| 21 | Media player menu toggle / Music sharing | TouchpanelUI.SigChange.cs | |
| 22 | Close media player menu | TouchpanelUI.SigChange.cs | |
| 31 | TSR-310 mic/voice button | TouchpanelUI.SigChange.cs | |
| 49 | Project name display enable (fb) | TouchpanelUI.ButtonFeedback.cs | Output |
| 50 | Room list subpage (fb) | TouchpanelUI.PageFlips.cs | Output |
| 51 | Room list subpage no floors (fb) | TouchpanelUI.PageFlips.cs | Output |
| 53 | Toggle button (fb) | TouchpanelUI.PageFlips.cs | Page state output |
| 55 | Toggle button (fb) | TouchpanelUI.PageFlips.cs | Page state output |
| 60 | Lift menu / Sleep command | TouchpanelUI.SigChange.cs | |
| 70 | Lift "Go with Off" toggle | TouchpanelUI.SigChange.cs | |
| 91 | Whole house zone list (fb) | TouchpanelUI.PageFlips.cs | Output |
| 94 | Whole house zone list with floors (fb) | TouchpanelUI.PageFlips.cs | Output |
| 100 | Zone close / page navigation | TouchpanelUI.SigChange.cs | |
| 141 | Video subpage scenario 1 (fb) | TouchpanelUI.PageFlips.cs | Output |
| 142 | Video subpage scenario 2 (fb) | TouchpanelUI.PageFlips.cs | Output |
| 149 | Video source OFF button | TouchpanelUI.SigChange.cs | |
| 150 | Music source OFF button | TouchpanelUI.SigChange.cs | |
| 154 | Video volume up | TouchpanelUI.SigChange.cs | |
| 155 | Video volume down | TouchpanelUI.SigChange.cs | |
| 156 | Video mute | TouchpanelUI.SigChange.cs | |
| 160 | Sleep button | TouchpanelUI.SigChange.cs | |
| 161-165 | Sleep timer buttons (1-5) | TouchpanelUI.SigChange.cs | |
| 166 | Sleep timer disable/clear | TouchpanelUI.SigChange.cs | |
| 180 | Format button | TouchpanelUI.SigChange.cs | |
| 200-350 | Subsystem routing (calculated) | TouchpanelUI.SigChange.cs | `(subsystemIndex * offset) + join` |
| 351 | Display selection panel toggle | TouchpanelUI.SigChange.cs | |
| 352-361 | Display selection buttons (10) | TouchpanelUI.SigChange.cs | |
| 357 | TSR-310 "More" channel button | TouchpanelUI.SigChange.cs | Overloaded with display 6 |
| 358 | TSR-310 "More" visibility (fb) | TouchpanelUI.ButtonFeedback.cs | Output |
| 361-366 | Channel button visibility (fb) | TouchpanelUI.ButtonFeedback.cs | Output |
| 500-510 | TSR-310 video source buttons | TouchpanelUI.SigChange.cs | |
| 510 | TSR-310 more video sources | TouchpanelUI.SigChange.cs | |
| 530-540 | TSR-310 audio source buttons | TouchpanelUI.SigChange.cs | |
| 600-700 | Subsystem buttons (calculated) | TouchpanelUI.SigChange.cs | `args.Sig.Number - 600` = subsystem index |
| 750-799 | Security buttons | TouchpanelUI.SigChange.cs | |
| 998 | Sharing menu state toggle | TouchpanelUI.SigChange.cs | |
| 999 | Sharing menu state toggle | TouchpanelUI.SigChange.cs | |
| 1000 | Music off indicator (fb) | TouchpanelUI.ButtonFeedback.cs | Output |
| 1001 | Audio source sharing enabled (fb) | TouchpanelUI.ButtonFeedback.cs | Output |
| 1002 | Sharing button feedback (fb) | TouchpanelUI.ButtonFeedback.cs | Output |
| 1007 | Main volume up | TouchpanelUI.SigChange.cs | |
| 1008 | Main volume down | TouchpanelUI.SigChange.cs | |
| 1009 | Music mute feedback (fb) | TouchpanelUI.ButtonFeedback.cs | Output |
| 1011-1030 | Music subpage navigation (fb) | TouchpanelUI.PageFlips.cs | Output, music page flips |
| 1021 | Close media player menu on home (fb) | TouchpanelUI.PageFlips.cs | Output |
| **1100-4000** | **UNUSED** | | **Available for new features** |
| 29000 | TSR-310 voice/speech recognition | TouchpanelUI.SigChange.cs | Serial input, routed to EISC |

---

## Analog / UShort Joins

| Join | Function | File | Notes |
|------|----------|------|-------|
| 1 | Subsystem video subpage routing | TouchpanelUI.SigChange.cs | |
| 2 | Volume slider / Music volume fb | TouchpanelUI.SigChange.cs | Input + Output |
| 3 | Room zone count (fb) | TouchpanelUI.ButtonFeedback.cs | Output |
| 4 | Floor scenario count (fb) | TouchpanelUI.ButtonFeedback.cs | Output |
| 200+ | Subsystem control routing (calculated) | TouchpanelUI.SigChange.cs | Per-TP offset |
| 251-256 | Channel analog mode | TouchpanelUI.SigChange.cs | 6 channels |
| 500+ | DM output selection feedback | TouchpanelUI.SigChange.cs | Calculated |
| 1500 | Music sharing group slot index | TouchpanelUI.MusicSharing.cs | Staging for menu |
| 1501 | Music sharing source change slot | TouchpanelUI.MusicSharing.cs | Staging for menu |
| **1100-1499** | **UNUSED** | | **Available for new features** |

---

## Serial / String Joins

| Join | Function | File | Notes |
|------|----------|------|-------|
| 2 | Voice/Mic command data | TouchpanelUI.SigChange.cs | TSR-310 via join 29000 |
| 3 | Music source name / Off text (fb) | TouchpanelUI.ButtonFeedback.cs | Output |
| 5 | Image URL for room display (fb) | TouchpanelUI.ButtonFeedback.cs | Output |
| 6 | Project name (fb) | TouchpanelUI.ButtonFeedback.cs | Output |
| 11-60 | Room names, subsystem names (fb) | TouchpanelUI.SmartObjects.cs | Output, via SmartObjects |
| 200+ | Security zone names (fb) | TouchpanelUI.SmartObjects.cs | Output |
| 300+ | Subsystem button names (fb) | TouchpanelUI.Subscriptions.cs | Output, calculated |
| 400+ | Quick action preset names (fb) | TouchpanelUI.SmartObjects.cs | Output |
| 751-753 | Security button names (fb) | TouchpanelUI.SmartObjects.cs | Output |
| **1100-4000** | **UNUSED** | | **Available for new features** |

---

## SmartObject IDs

SmartObject joins are relative to the SmartObject, not absolute touchpanel joins.
Listed here for reference only.

| SO ID | Function | File |
|-------|----------|------|
| 1 | Media player | TouchpanelUI.SmartObjects.cs |
| 3 | Floor list | TouchpanelUI.SmartObjects.cs |
| 4 | Room list | TouchpanelUI.SmartObjects.cs |
| 5 | Video source list | TouchpanelUI.SmartObjects.cs |
| 6 | Audio source list | TouchpanelUI.SmartObjects.cs |
| 7 | Music sharing zone list | TouchpanelUI.MusicSharing.cs |
| 8 | Light button list | TouchpanelUI.SmartObjects.cs |
| 9 | Music floor list | TouchpanelUI.SmartObjects.cs |
| 10 | Whole house zone list | TouchpanelUI.SmartObjects.cs |
| 15 | Quick action presets | TouchpanelUI.SmartObjects.cs |
| 19 | Shade controls | TouchpanelUI.SmartObjects.cs |
| 21 | Security zone list | TouchpanelUI.SmartObjects.cs |
| 22 | Security partition states | TouchpanelUI.SmartObjects.cs |
| 26 | DVR tabs | TouchpanelUI.SmartObjects.cs |
| 35 | Home page music zones | TouchpanelUI.SmartObjects.cs |

---

## CrComLib Contract (Ch5_Sample_Contract.cse2j)

**DO NOT EDIT this file directly.** It is generated by the Crestron contract editor software.
If the contract needs changes, ask the user to modify it in the contract editor and regenerate.
The contract maps named signals to internal identifiers — those identifiers are not join numbers.

---

## EISC Connections (for reference)

These are NOT touchpanel joins -- they are inter-program EISC signal numbers.

| EISC | IPID | Purpose | Signal Range |
|------|------|---------|--------------|
| subsystemControlEISC | 0x9D | Subsystem routing TP 1-20 | Per-TP: (TP#-1)*200 + join |
| subsystemControlEISC2 | 0x9E | Subsystem routing TP 21+ | Offset from EISC1 |
| Lighting EISC | 0xB3 | Lighting4Series room control | Slot-based: slot*50 (digital), slot*25 (analog), slot*30 (serial) |
| VOLUMEEISC | varies | Media player / volume | Per-TP calculated |

---

## TSR-310 Lighting Join Allocation (NEW)

Starting at join **1101** for all types to stay clear of existing usage.
Implemented in `LightingScenario2Control.cs` (constants prefixed `TSR_`).
Handled in `TouchpanelUI.SigChange.cs` (`HandleBooleanSigChange`).

### Digital Joins

| Join | Function | Direction | EISC Mapping |
|------|----------|-----------|--------------|
| 1101-1110 | Scene select (press) / Scene active (fb) | Input + Output | slot*50 + 1-10 |
| 1111-1120 | House scene recall (press) | Input | saveCommand analog 501+slot, value 201+idx |

### Analog Joins

| Join | Function | Direction | EISC Mapping |
|------|----------|-----------|--------------|
| 1101 | Number of scenes | Output (fb) | slot*25 + 2 |
| 1102 | Number of house scenes | Output (fb) | global analog 521 |

### Serial Joins

| Join | Function | Direction | EISC Mapping |
|------|----------|-----------|--------------|
| 1101-1110 | Scene names | Output (fb) | slot*30 + 1-10 |
| 1111-1120 | House scene names | Output (fb) | global serials 601-610 |
| 1121 | Room name | Output (fb) | Reserved (not yet wired) |
