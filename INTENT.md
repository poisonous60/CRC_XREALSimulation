# RadVis Intent (glasses side)

RadVis is an augmented-reality application for XREAL Air 2 Ultra glasses. It marks where a radioactive source sits in a lab room and changes that mark's appearance with the live readings of the room's radiation detectors. The full glossary is `CONTEXT.md`.

## 1. Purpose

This document records what the owner wants RadVis to do: the terms, the decisions already made, and the decisions still open. It has to be complete enough that someone can write the requirements and design specification (`SPEC.md`) from it without asking the owner, and later write an implementation from that specification alone. The specification has two later uses: judging each part of the existing code as required, unneeded, or missing, and deciding future changes to the Source Marker's appearance against a fixed reference.

## 2. Scope

The physical scene: a lab room; one radioactive source on a table; four radiation detectors placed around the room, each reporting its count rate to a laptop; a wearer with XREAL Air 2 Ultra glasses (below: the glasses) and the phone-sized compute unit that drives them.

The application shows the wearer one floating mark at the source's position. The mark stays at that spot while the wearer walks around, and its appearance follows the detectors' live readings. What the glasses show also changes with the wearer's distance to the mark, because the wearer must not come close to the source. The wearer does nothing but put on the glasses and look. A lab member (the Operator) prepares everything beforehand: connects the application, fixes the mark's position once, and repeats whatever a restart needs before handing the glasses over. After a restart the saved mark comes back to the same spot.

In scope: everything shown through the glasses. 
Out of scope: the detectors, their link to the laptop, and the laptop server.

## 3. Terms

| Term | Meaning |
|---|---|
| Detector | One of the four physical radiation-measuring devices in the room. Each carries a QR sticker |
| Detector ID | The identifier printed in a Detector's QR sticker; the server labels that Detector's reading with it. In this lab it is a number |
| CPS | Counts per second, the single number a Detector reports; higher means more radiation |
| Measurement Server | The program on the lab laptop that collects the four readings and sends them to the glasses over Wi-Fi |
| Readings | The CPS values the Measurement Server sends, one per reporting Detector, labeled by Detector ID; they carry no positions. One delivery is a reading update |
| Source | The radioactive material on the table. Nothing is attached to it |
| Source Marker | The one visual mark the glasses draw at the Source's position; its shape, size, opacity, and color mapping are TBD-4 |
| Operator | The lab member who prepares the application before a wearer puts on the glasses: connects it, fixes the Source Marker, and repeats any per-launch preparation |
| Wearer | The person wearing the glasses and looking at the room. Does nothing else |
| Placement | The Operator setting the Source Marker's position at the Source. Its procedure is TBD-11 |
| Wall reference QR | A QR code fixed to a wall that lets the glasses align saved positions after a restart; used by one restore option (TBD-3) |
| Beam Pro | The XREAL compute unit: an Android device that runs the application, drives the glasses, takes the Operator's input, and carries a rear camera |
| Placement error | The maximum allowed distance between the center of the Source Marker and the Source reference point. Value: TBD-8 |
| Source reference point | The agreed physical point on the Source used when measuring placement error. Value: TBD-8 |
| Distance mapping | The rule that assigns what the glasses show to each range of distance from the wearer to the Source Marker. Value: TBD-9 |
| Developer diagnostics | Developer-only test or diagnostic code, UI, overlays, or logs |

## 4. Product overview

### 4.1 Perspective

```
Detector x4  --(Bluetooth, lab's)-->  Measurement Server on laptop
                                             |
                                    Wi-Fi, periodic readings
                                             v
Beam Pro (application, Operator input, camera)  <-->  glasses (display, tracking)
```

The application has three external interfaces: the Measurement Server (readings in), the Beam Pro (Operator input and camera), and the glasses (display and tracking). Nothing flows back to the server.

### 4.2 Wanted

1. Receive the Detectors' readings from the Measurement Server.
2. Let the Operator fix the Source Marker at the Source (Placement).
3. Keep exactly one Source Marker; a new Placement moves it, never adds another.
4. Show the Source Marker fixed in the room, with an appearance that follows the readings.
5. Bring the Source Marker back to the same spot after a restart, within the placement error.
6. Change what the glasses show with the wearer's distance to the Source Marker.

Every setup step is the Operator's, done before the glasses are handed over. The wearer only puts on the glasses and looks.

### 4.3 Not wanted

- Any action required of the wearer.
- Detectors drawn in the room. The wearer never sees a Detector; readings only affect the Source Marker.
- The Source Marker's position derived from readings. Position comes only from the Operator's Placement.
- Per-Detector readings on the Source Marker.
- A second Source.
- Audio or haptics.
- Developer diagnostics in device builds.
- A safety disclaimer. The application exists to make radiation visible, so the documents do not call it something that must not be relied on.

Photo and video capture from the glasses, and moving the Source Marker with the Beam Pro used as a pointer, exist in the current build. Keeping or dropping them is TBD-5 and TBD-6.

### 4.4 Constraints

- Hardware: the glasses driven by a Beam Pro.
- Software: Unity, Android build.
- Baseline: the existing build, made by a predecessor, is the starting point. This version changes only what the lab requested: the source-estimation code is removed, the Source Marker marks the Source position set by Placement, its appearance follows the readings, and what the glasses show changes with the wearer's distance to it. Everything else in the build stays until a decision in section 5 says otherwise. Where the build differs from a requirement, the requirement stands, and the implementation document lists the difference as a gap to close.

### 4.5 Users

One Operator: a lab member who knows where the Source is and can follow a short procedure. One wearer at a time, and anyone can be one. They do nothing and need no training.

### 4.6 Assumptions

- A1. Four Detectors, anywhere in the room, each carrying a QR sticker.
- A2. One Source, on a table, fixed for the session. Nothing is attached to it.
- A3. The Measurement Server runs on a laptop on the same Wi-Fi and sends readings periodically. Its interface has to be confirmed with the lab (TBD-7).

## 5. Open decisions

Open decisions are not requirements. Only a made decision becomes one. The specification lists the options considered and the working design for each. Until a decision is made, the current build's value is the working value.

| Id | Decision |
|---|---|
| TBD-1 | Level rule: how the four readings become one level |
| TBD-2 | Stale or absent readings: when readings stop counting as fresh, and what the Source Marker shows while there are no fresh readings or no connection |
| TBD-3 | Restore method and procedure |
| TBD-4 | Appearance mapping: the Source Marker's shape, size, opacity, and the appearance state for each level |
| TBD-5 | Photo and video capture: keep or drop |
| TBD-6 | Moving the Source Marker with the Beam Pro as a pointer: keep or drop |
| TBD-7 | Real server rate and message format |
| TBD-8 | Placement error tolerance and the Source reference point |
| TBD-9 | Distance mapping: the distance ranges and what the glasses show in each |
| TBD-10 | User interface: how the Operator connects, scans, confirms, and cancels; what, if anything, the glasses draw head-locked |
| TBD-11 | Placement procedure: how the Operator starts, aims, confirms, and cancels fixing the Source Marker |

## 6. Rules for the specification written from this document

- Requirements, then design. The requirements say what the application does. The design records the decisions on what the wearer sees and what the Operator does: the Source Marker's appearance, the distance representation, the user interface, the restore and Placement procedures, and their values. The connection method, tracking and surface-detection mechanisms, and data-parsing rules stay out, in the implementation document.
- One requirement per line, one unique identifier each.
- Exclusions from 4.3 are written as statements of scope ("does not"), not as `shall not` requirements.
- Numbers only in the design section; no file, class, or method names; no status or history; no version line.
- Where a requirement depends on an open decision, it names the TBD; the design section records the options considered and the current build's value as the working design, and flags what still waits on the lab or the owner.
