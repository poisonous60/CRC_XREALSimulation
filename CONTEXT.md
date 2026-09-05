# RadVis (CRC_XREALSimulation)

An XREAL AR glasses app that marks where a radiation source sits in the room and colors that mark by live readings from a small set of physical detectors. This context is everything the wearer sees through the glasses; the detectors, their Bluetooth link, and the laptop server belong to the lab. Terms below are the project's language; implementation details belong elsewhere.

## Language

### Hardware and data

**Detector**:
One of four physical radiation-measuring devices placed anywhere in the room, each carrying a QR sticker.
_Avoid_: sensor, block, unit

**Detector ID**:
The text encoded in a Detector's QR sticker; the only identity the app ever receives for that Detector. In this lab the text is a number.
_Avoid_: device name, key

**Measurement Server**:
The web server running on the laptop that the Detectors report to over Bluetooth; XREAL connects to it by IP.
_Avoid_: backend, web server (alone), 측정기 서버

**Readings**:
The CPS values the Measurement Server sends, one per reporting Detector, labeled by Detector ID, with no position information. One delivery is a reading update.
_Avoid_: snapshot, packet, frame

**CPS**:
Counts per second, the single number a Detector reports; higher means stronger radiation.
_Avoid_: dose, value, level

### Glasses side

**Operator**:
The lab member who prepares the application before a wearer puts on the glasses: connects it, does the Placement, and repeats any per-launch preparation. The wearer only puts on the glasses and looks.
_Avoid_: user (ambiguous), admin

**Source**:
The radioactive material in the room; the Operator marks its position through Placement, and it is the only thing the glasses draw in the room.
_Avoid_: detector (a Detector is never drawn), target, hotspot

**Source Marker**:
The sphere drawn at the Source's placed position, colored by CPS.
_Avoid_: point (점), dot, detector sphere, marker (alone)

**Wall reference QR**:
The QR fixed to a wall that lets the glasses align saved positions after a restart.
_Avoid_: room origin, calibration QR, anchor QR

**Source QR**:
The QR code scanned to start a Placement. In this lab it is any Detector's sticker; nothing is attached to the Source itself, and the sticker is only the trigger.
_Avoid_: source sticker, source tag

**Placement**:
The act, done by the Operator, of fixing a point in the room by scanning a Source QR, aiming the glasses' center gaze at the Source's surface, and confirming.
_Avoid_: registration, pinning, scanning (scanning is only the first step)

## Relationships

- A **Measurement Server** serves exactly one room and up to four **Detectors**
- A reading update carries one **CPS** per **Detector ID**; it never carries a position
- A **Detector** is never drawn on the glasses; it exists only as a **Detector ID** inside the **Readings**
- A **Source** gets its position only from **Placement** on the glasses; the **Measurement Server** never knows it
- A **Source QR** only starts a **Placement**; its text is not used and it carries no position; the position comes from where the Operator aims
- A room has exactly one **Source** (working assumption as of 2026-09-04; the lab said one for now, more may come later)
- A **Source Marker** shows exactly one **Source**; its color is the maximum **CPS** across the four **Detectors**
- A **Wall reference QR** is registered once per app launch; **Placements** made afterwards are stored relative to it
- A **Source Marker** is shown only after the **Wall reference QR** has been registered in that run; a restart always begins with the wall QR

## Example dialogue

> **Dev:** "When new **Readings** arrive, do we move the **Source Marker**?"
> **Domain expert:** "No. The **Readings** only carry **CPS** per **Detector ID**. The **Source**'s position comes from **Placement**, and only the glasses know it."
> **Dev:** "Then where do the four **Detectors** show up?"
> **Domain expert:** "They don't. The wearer never sees a **Detector**; their readings only color the **Source Marker**."
> **Dev:** "So if the laptop restarts, positions survive?"
> **Domain expert:** "Positions were never on the laptop. If the *glasses* restart, you scan the **Wall reference QR** again and the saved **Placement** comes back."

## Flagged ambiguities

- "점" (point) in the user's overview: resolved 2026-09-04 as the **Source Marker**. A **Detector** is treated as if it did not exist on the glasses. Class and UI names in code still say "Detector"; read them as **Source Marker** until renamed.
- Source count: one per room is an assumption, not a fixed rule. If the lab later brings several **Sources**, "maximum **CPS** across all **Detectors**" no longer tells them apart and a per-Source rule is needed.
- "웹서버" / "측정 서버" / "서버": all mean **Measurement Server**. The Bluetooth hop between **Detectors** and the laptop is user-observed only; nothing in the repository can confirm it.
- The lab deck's "25 block-type detectors, RS-485, 4-level LED" description was a wrong slide extraction and was discarded on 2026-09-04. It is not this project's setup.
