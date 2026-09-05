# RadVis Requirements and Design (glasses side)

This document says what the RadVis glasses application does and gives the design that does it. Section 2 holds the requirements, one per line with its own identifier. Section 3 holds the design: for each open decision (TBD-n) the options considered and the working design, taken from the build unless marked as proposed.

## 1. Introduction

### 1.1 Purpose

This document is the reference for two later uses: judging each part of the existing code as required, unneeded, or missing, and deciding future changes to the Source Marker's appearance against a fixed reference. Someone can write an implementation from it alone.

### 1.2 Scope

RadVis is an augmented-reality application for XREAL Air 2 Ultra glasses. It marks where a radioactive source sits in a lab room and changes that mark's appearance with the live readings of the room's radiation detectors.

In scope: everything shown through the glasses.
Out of scope: the detectors, their link to the laptop, and the laptop server.

Photo and video capture from the glasses (TBD-5) and moving the Source Marker with the Beam Pro used as a pointer (TBD-6) are open decisions; section 3 keeps them as they are in the build.

### 1.3 Product perspective

The application has three external interfaces: the Measurement Server (readings in), the Beam Pro (Operator input and camera), and the glasses (display and tracking). Nothing flows back to the server.

### 1.4 Users

One Operator: a lab member who knows where the Source is and can follow a short procedure. The Operator wears the glasses during setup, then hands them over. One wearer at a time, and anyone can be one. They do nothing and need no training.

### 1.5 Assumptions

- A1. Four Detectors, anywhere in the room, each carrying a QR sticker.
- A2. One Source, on a table, fixed for the session. Nothing is attached to it.
- A3. The Measurement Server runs on a laptop on the same Wi-Fi and sends readings periodically. Its interface has to be confirmed with the lab (TBD-7).

### 1.6 Definitions

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

A restart is the application being started again.

## 2. Requirements

### 2.1 Readings

- F-01. The application receives the Detectors' readings from the Measurement Server (interface TBD-7).

### 2.2 Placement

- F-02. The Operator sets the Source Marker's position at the Source (Placement, procedure TBD-11).
- F-03. There is at most one Source Marker; setting its position again moves the same marker and never adds another.

### 2.3 Display

- F-04. The Source Marker stays at its position in the room while the wearer moves.
- F-05. The Source Marker's appearance follows the latest readings, as long as they are fresh (TBD-2), through the level rule (TBD-1) and the appearance mapping (TBD-4).
- F-06. While the glasses have no fresh readings, the Source Marker shows what TBD-2 assigns to that condition.
- F-07. What the glasses show changes with the wearer's distance to the Source Marker, following the distance mapping (TBD-9).

### 2.4 Restore

- F-08. After a restart and the Operator's per-launch preparation (TBD-3), the Source Marker is back at the last placed position, within the placement error (TBD-8).

### 2.5 Quality

- Q-01. Whenever the Source Marker has a position, the position it indicates is within the placement error (TBD-8) of the Source reference point.

### 2.6 Constraints

- C-01. The application runs on the Beam Pro driving the XREAL Air 2 Ultra glasses.
- C-02. The application is a Unity project delivered as an Android build.
- C-03. The implementation starts from the existing build and changes only four things: the source-estimation code is removed; the Source Marker marks the Source position set by Placement; its appearance follows the readings; what the glasses show changes with the wearer's distance. Everything else stays until an open decision in section 3 says otherwise; where the build differs from a requirement, the requirement stands.

### 2.7 Exclusions

- X-01. The application does not require any action from the wearer.
- X-02. The application does not draw Detectors; readings affect only the Source Marker.
- X-03. The application does not derive the Source Marker's position from readings; position comes only from Placement.
- X-04. The application does not show per-Detector readings on the Source Marker.
- X-05. The application does not support a second Source.
- X-06. The application does not use audio.
- X-07. The application does not use haptics.
- X-08. Device builds do not include developer diagnostics.

## 3. Design

One subsection per open decision of the intent, TBD-1 to TBD-11. Each names the options considered and records the working design: what the build does, or, where the build has nothing, a proposal marked as such. Section 4 lists what still waits on the lab or the owner.

### 3.1 Level rule (TBD-1)

Options: the maximum of the four readings, or a rule the lab provides.

The level is the highest CPS among the valid entries of the latest reading update.

### 3.2 Fresh and missing readings (TBD-2)

Options: how many seconds a reading update stays fresh; then hide the Source Marker, or keep its last state with a stale look.

A reading update stays fresh for 5 s after it arrives. While there is no fresh reading update, including while there is no connection to the Measurement Server, the Source Marker is hidden. There is no separate stale look.

### 3.3 Returning the saved Source Marker after a restart (TBD-3)

Options: register a wall reference QR each launch; rely on the glasses' saved spatial anchors, which try to re-find a position after a restart; or repeat Placement each launch.

The method is a wall reference QR registered once per launch. The Source Marker's position is saved relative to it and re-created when it is registered again.

Every launch, the Operator, wearing the glasses with the Beam Pro in hand, does this:

1. Tap Scan and hold the rear camera to the wall QR until it reads.
2. Aim the glasses' center at that QR. A gray preview appears where the gaze meets the wall; only a near-vertical surface counts as the wall.
3. Tap Place. The saved Source Marker returns at its position.

Until step 3 is done, the Source Marker stays hidden and no Placement can start.

### 3.4 Source Marker appearance (TBD-4)

Options: keep the current mapping, or redesign it from reference examples from other AR projects, still to be compiled.

The Source Marker is one sphere, 20 cm across, opacity 0.18, in one flat color with no label; its center is the position it indicates. Color by level: hidden at 2 CPS and below; green above 2 up to 10; yellow above 10 up to 350; red above 350. The thresholds are plain comparisons with no smoothing. Around the sphere, up to three fainter concentric shells (opacity 0.012, out to 5 m) mark where the green and yellow levels would fall off with distance from the sphere; they are fixed in the room around the sphere.

### 3.5 Photo and video capture (TBD-5)

Options: keep or drop.

The application keeps photo and video capture as the build has it: a Start Record / Stop Record button and a photo button on the Beam Pro panel record the camera view blended with the virtual content, without audio, to the Beam Pro's storage and its gallery.

### 3.6 Moving the Source Marker with the Beam Pro as a pointer (TBD-6)

Options: keep or drop.

The application keeps pointer moving as the build has it: the Operator aims the Beam Pro's controller ray at the placed Source Marker, which highlights, and uses the center pad to move it with the ray.

### 3.7 Placement error and Source reference point (TBD-8)

Options: a distance in centimeters and a marked point on the Source, agreed with the lab.

The placement error is 10 cm, measured to the Source's center, which is the Source reference point.

### 3.8 Distance representation (TBD-9), proposed

Options: ranges in meters agreed with the lab; a full-screen effect that strengthens as the wearer comes close and shows the direction to the Source Marker, or a change of the Source Marker itself.

The ranges of distance from the wearer to the Source Marker are:

- Beyond 2 m: nothing beyond the Source Marker itself.
- 2 m down to 1 m: a faint glow at the edge of the view on the side facing the Source Marker, growing stronger as the distance shrinks.
- Under 1 m: a strong glow along the whole edge of the view, strongest on the side facing the Source Marker.

The glow takes the Source Marker's level color, or white while there is no current level. It follows the Source Marker's position and works whether or not the Source Marker itself is shown.

### 3.9 User interface (TBD-10)

Options: to be designed.

The Beam Pro panel, used by the Operator, has an address field for the laptop's address and three workflow buttons: Scan, Place, and Cancel. There is no separate Connect button; finishing the address entry or tapping Scan connects when there is no connection. Button labels change with the step. Scan reads "Connect & Scan", "Scan Room QR", "Add Detector", or "Scanning...". Place reads "Place Room" or "Place Detector". Cancel reads "Cancel Scan" or "Cancel Place". Below the buttons a workflow guide names the next step in capitals with a one-line instruction under it: "CONNECT SERVER", "SCAN ROOM QR", "AIM AT THE ROOM QR", "TAP PLACE ROOM", "SCAN DETECTOR QR", "AIM AT THE DETECTOR POSITION", "TAP PLACE DETECTOR", plus a few states for waiting on the server, capture, and setup errors. The panel also carries the Start Record / Stop Record button and the photo button of 3.5, and a text list of the reporting Detector IDs with their CPS.

The glasses draw text rows head-locked 1.5 m ahead, headed DETECTOR / CPS / DISTANCE: one row per reporting Detector ID with its CPS, plus the wearer-to-Source Marker distance, and the row of a sphere near the view center is highlighted. When the Source Marker is out of view, a text indicator sits at the edge of the view (5 % in from the edge, in one of four directions) pointing toward it.

### 3.10 Placement procedure (TBD-11)

Options: to be designed.

After 3.3 is complete, the Operator, wearing the glasses with the Beam Pro in hand, does this:

1. Tap Scan and hold the rear camera to any Detector sticker until it reads.
2. Aim the glasses' center at the Source. A gray preview sphere follows the gaze along the nearest detected surface, the table top in the lab; where no surface is detected there is no preview.
3. Tap Place. The Source Marker is fixed there and saved.

Cancel during the scan or while the preview is shown keeps the previous position. Cancel with no scan or preview active removes the most recently placed Source Marker and its saved position. Repeating the steps moves the one Source Marker. A sticker scanned before 3.3 is complete is ignored.

### 3.11 Server rate and message format (TBD-7)

Options: confirm with the lab.

The working value is the test server's: one reading update per second carrying every reporting Detector's CPS labeled by Detector ID.

## 4. Areas of concern

- The values in 3.1, 3.4, and 3.7 are the build's and not yet agreed with the lab.
- 3.8 is proposed here; the build has none of it and the lab has not seen it. Open are the two range limits, the glow itself, and whether it stays active while the Source Marker is hidden for lack of readings.
- The shells around the sphere in 3.4 may stay or go.
- 3.5 and 3.6 may each stay or go.
- 3.7 is an acceptance value; the build does not measure it.
- The Cancel behavior in 3.10 that removes a placed Source Marker may stay or go.
- The rows in 3.9 show each Detector ID with its CPS, while X-02 says readings affect only the Source Marker. The rows may stay or go.
- The labels in 3.9 say "Detector" ("Add Detector", "Place Detector", the DETECTOR column) for what is the Source Marker. They may stay or be renamed.
- The real server's rate and format (3.11) wait on the lab.
