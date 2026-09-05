# RadVis Implementation (current build)

This document records how the current build is put together: the platform and libraries, the runtime architecture, how the glasses talk to the Measurement Server, how QR scanning, placement, persistence, and restore work, the build settings, the references and examples the code follows, and the gaps against `SPEC.md`. It is written so that someone can answer "how is it implemented" from it and rebuild the same approach from it, `SPEC.md`, and the references in section 9. What the wearer sees and what the Operator does are in `SPEC.md` section 3 and are not repeated here.

## 1. Platform and libraries

| Item | Version | Role in the build |
|---|---|---|
| Unity | 6000.0.72f1, Built-in Render Pipeline, Linear color space | engine; Android build with IL2CPP, ARM64, minimum API 29, OpenGL ES 3 |
| XREAL XR Plugin (`com.xreal.xr`) | 3.1.0, installed from a local archive rather than a package registry | XR loader for the glasses: 6DoF head tracking (`TrackingType.MODE_6DOF`), single-pass instanced stereo, input source Controller (the Beam Pro), the virtual controller prefab, RGB camera capture |
| AR Foundation (`com.unity.xr.arfoundation`) | 6.0.8 | `ARPlaneManager` for detected surfaces, `ARAnchorManager` and `ARAnchor` for spatial anchors, the XR Origin |
| XR Interaction Toolkit (`com.unity.xr.interaction.toolkit`) | 3.0.11 | the controller ray used to point at a placed sphere |
| Input System (`com.unity.inputsystem`) | 1.19.0 | one `InputAction` bound to `<XREALController>/TriggerButton`, which is the Beam Pro center pad |
| XR Hands (`com.unity.xr.hands`) | 1.7.3 | installed with its sample; no script uses it |
| NativeWebSocket (`com.endel.nativewebsocket`) | git, `upm` branch | WebSocket client to the Measurement Server |
| Newtonsoft Json (`com.unity.nuget.newtonsoft-json`) | 3.2.2 (Json.NET 13.0.2) | parsing reading updates with `JObject` |
| ZXing.Net | `Assets/Plugins/zxing.dll` | decoding QR codes from camera frames |
| Unity UI (`com.unity.ugui`) and TextMesh Pro | 2.0.0 | the Beam Pro panel and the glasses HUD |
| unity-cli connector (`com.youngwoocho02.unity-cli-connector`) | git | editor automation during development only |

Imported samples: XREAL XR Plugin "AR Features", "Camera Features", "Interaction Basics"; XR Interaction Toolkit 3.0.10 and 3.0.11; XR Hands 1.7.3. The app scene `Assets/Scenes/HelloMR.unity` started from the XREAL sample scene of that name. The other scenes (`EditorTest`, `EditorTestXR`, `FalloffDemo`, `SampleScene`, `ShaderCompare`) are editor experiments and are not in the build.

The sphere material uses `Assets/Resources/RadVisDetectorTransparent.shader`, a Built-in pipeline `CGPROGRAM` transparent shader that the marker manager forces on (`forceDedicatedTransparentShader`); without it the code falls back to the Standard shader. Earlier shader attempts sit under `Assets/ShaderHistory/`, which git ignores.

## 2. Architecture

Fourteen scripts under `Assets/Scripts/`, all `MonoBehaviour`s. Where each lives: `RadiationReceiver`, `QRScanner`, `DetectorWorldMarkerManager`, `DetectorCoordinateDatabase`, `DetectorSpatialAnchorManager`, `ARDetectorHud`, `HeadLockedCanvas`, `XREALCaptureManager`, and `UnityMainThreadDispatcher` are components in `HelloMR.unity`; `BeamProControllerBridge` and `ControllerPanelEditorLayout` sit on the virtual controller prefab of section 7; `RoomCoordinateSystem` and `DetectorControllerInteractor` are added at runtime by `DetectorWorldMarkerManager` when missing, which also adds the HUD and the database if they are absent; `FlyCameraRig` is only in the editor scenes.

| Script | Responsibility |
|---|---|
| `RadiationReceiver` | WebSocket connection, reading-update parsing, freshness, reconnect, saved server IP |
| `QRScanner` | Beam Pro camera preview and ZXing decoding; fires the scan events |
| `RoomCoordinateSystem` | the `ROOM_ORIGIN` wall frame: preview, confirmation, gravity-aligned frame, invalidation on resume |
| `DetectorWorldMarkerManager` | placement preview, placed spheres, color by level, falloff shells, visibility gates, restore from the database, controller reposition |
| `DetectorCoordinateDatabase` | the JSON save file, one record per sphere |
| `DetectorSpatialAnchorManager` | spatial-anchor layer on top of the database |
| `ARDetectorHud` | head-locked HUD: status, rows, distances, gaze highlight, edge indicators |
| `HeadLockedCanvas` | keeps a world-space canvas in front of the camera |
| `BeamProControllerBridge` | Beam Pro panel: buttons, labels, workflow guide, detector list, capture buttons |
| `ControllerPanelEditorLayout` | editor-only relayout of the panel after a screen-size change |
| `DetectorControllerInteractor` | controller ray plus center pad to move a placed sphere |
| `XREALCaptureManager` | photo and video capture through the XREAL camera API |
| `UnityMainThreadDispatcher` | queue for callbacks that arrive off the main thread |
| `FlyCameraRig` | editor free camera |

Managers find each other through `static event`s, subscribed in `OnEnable` and released in `OnDisable`, plus Inspector references on the bridge and the marker manager. The static events:

- `QRScanner.OnQRDetectedDetailed(text, image center, pixel size)` and `QRScanner.OnScanStarted` go to `RoomCoordinateSystem` and `DetectorWorldMarkerManager`. The two listeners split on the text: `ROOM_ORIGIN` goes to the room frame, any other text starts a Placement of the one sphere keyed `sourceMarkerKey`. The panel's Add Source button starts the same Placement through `TryBeginSourcePlacement` without the scanner.
- `RadiationReceiver.OnRadiationDataReceived` and `OnServerConnectionChanged` go to `DetectorWorldMarkerManager`.
- `RadiationReceiver.OnServerStatusChanged`, `OnRadiationDataReceived`, and `OnRadiationDataFreshnessChanged` go to `ARDetectorHud`.
- All five receiver events (`OnServerStatusChanged`, `OnDisplayTextChanged`, `OnServerConnectionChanged`, `OnRadiationDataFreshnessChanged`, `OnRadiationDataReceived`), `RoomCoordinateSystem.RoomStatusChanged`, and `XREALCaptureManager.OnCaptureStateChanged` go to `BeamProControllerBridge` for its labels.
- The bridge calls the receiver, scanner, room system, marker manager, and capture manager directly when a panel button is tapped.

Instance events and callbacks outside that list: `DetectorSpatialAnchorManager.AnchorSaved`, `AnchorLoaded`, `AnchorSaveFailed`, and `AnchorLoadFailed` go to `DetectorWorldMarkerManager`; the XREAL virtual controller's `pointerDown` and `pointerUp` go to `DetectorControllerInteractor`; Unity UI Button and input-field listeners go to the bridge and the scanner; NativeWebSocket's open, message, error, and close callbacks go to the receiver.

```
Measurement Server --WebSocket--> RadiationReceiver --events--> DetectorWorldMarkerManager --> spheres, shells
                                                    \---------> ARDetectorHud (rows, edge indicators)
                                                     \--------> BeamProControllerBridge (panel text)
Beam Pro camera --> QRScanner --OnQRDetectedDetailed--> RoomCoordinateSystem (ROOM_ORIGIN)
                                                    \-> DetectorWorldMarkerManager (any other text)
XR Origin (XREAL loader, ARPlaneManager, ARAnchorManager) --> gaze ray, planes, anchors
DetectorCoordinateDatabase <--> DetectorWorldMarkerManager, DetectorSpatialAnchorManager
Beam Pro panel (XREALVirtualController_custom prefab) --> BeamProControllerBridge --> every manager
```

## 3. Server communication

- Transport: one WebSocket client from NativeWebSocket at `ws://<ip>:5002`. The IP comes from the panel's address field; the scene default is `192.168.0.60`; the last IP used is kept in `PlayerPrefs` under `ServerIP` and reconnected on the next launch.
- Message: the server pushes JSON text. The receiver parses it with `JObject.Parse` and reads `deviceDataDictionary` as a map of Detector ID to CPS:

```json
{ "deviceDataDictionary": { "DET_01": 5.0, "DET_02": 120.0, "DET_03": 400.0 } }
```

  Keys are trimmed and compared without regard to letter case. Values that are negative or not finite stay in the map for the HUD, which shows them as `--`, but are ignored when the marker level is computed and when readings are saved.
- Threading: NativeWebSocket callbacks are queued to the main thread through `UnityMainThreadDispatcher`, and `DispatchMessageQueue()` runs in `Update`.
- Freshness: a reading update counts as fresh for 5 s (`maximumDataSilenceSeconds`); the receiver raises `OnRadiationDataFreshnessChanged` when that flips. The marker manager applies the same 5 s window again (`maximumRadiationSnapshotAgeSeconds`).
- Reconnect: at launch and on return from the background, the saved server is reconnected after a 0.35 s delay; after a failed attempt or a drop, retries start at 2 s and double up to 15 s; each attempt times out after 10 s.
- After a manual connect the QR camera starts by itself 0.3 s later when `startQrCameraAfterConnect` is on; `HelloMR` has it off. A restored connection at launch never starts it. The scene has `openKeyboardOnStart` off.
- Nothing is sent to the server after the handshake.
- Test server: `mock_radvis_server.py`, a Python `websockets` script kept outside the repository, port 5002, pushes the same three-detector reading update once per second and has a self-test mode. The real server's rate and format are TBD-7.

## 4. QR scanning

`QRScanner` asks for the Android camera permission (`UnityEngine.Android.Permission`), opens the Beam Pro's rear camera as a `WebCamTexture` (1280 x 720 at 30 fps), shows it on a `RawImage`, and every 0.25 s copies the frame with `GetPixels32` and decodes it with ZXing's `BarcodeReader`. On the first result it stops the camera, hides the preview, and fires `OnQRDetected(text)` and `OnQRDetectedDetailed(text, center, size)`. Scanned texts are remembered so the same sticker is treated as a rescan. The QR gives no 3D pose; it only chooses what the next Place fixes. The camera in use is the Beam Pro's, not the glasses' RGB camera.

## 5. Tracking, surfaces, and placement

### Where the glasses think they are

The glasses have no room model. Two side cameras track feature points frame to frame and fuse that motion with the IMU, which yields position and rotation relative to where the app started. The origin is the head pose at app start and differs every launch; it may also move after a background resume. Detected surfaces are planes fitted to those points, not a scan of the room.

### How the position is chosen

While a Placement or the room step is pending, each frame a ray from the glasses camera (`placementOrigin`) is intersected with every `ARPlane` of the `ARPlaneManager` (detection mode horizontal and vertical); the nearest hit between 0.15 m and 10 m wins. For `ROOM_ORIGIN` only planes whose normal has an absolute dot product with world up of at most 0.30 count (about 17 degrees from vertical), and the viewer must stand at least 8 cm from the wall horizontally so the wall's facing side is unambiguous. A Placement takes any plane. Without a hit there is no preview. If plane placement is switched off the preview sits 2 m ahead. Place freezes the point. The preview is a gray sphere: 0.75 gray at alpha 0.35 for a Placement, 14 cm light gray at alpha 0.30 for the room step. The error comes from aim, plane fit, and tracking drift since app start.

### How the room frame is built

At "Place Room" the gaze hit point on the wall becomes the origin. Up is world up (gravity). Forward is the wall normal projected onto the horizontal plane and flipped to point into the room, toward the Operator; right is the cross product, and the frame is `Quaternion.LookRotation(forward, up)`. Floor and ceiling codes are rejected by the vertical filter above. A yaw error of the frame moves a sphere at distance d by d * sin(yaw); at 3 m, 2 degrees is about 10 cm.

### How the sphere is drawn

`GameObject.CreatePrimitive(PrimitiveType.Sphere)` scaled to 20 cm, with a material on the dedicated transparent shader at alpha 0.18, colored by the thresholds in `SPEC.md` 3.4 (`hiddenMaxCps` 2, `greenMaxCps` 10, `dangerThresholdCps` 350) from one number per reading update, the maximum over valid entries, with no hysteresis and no smoothing. Falloff shells are further sphere primitives around it: for each level boundary, the radius at which the current CPS would fall to that boundary by the inverse-square law from a reference distance of 8 cm, out to 5 m, at most 3 shells, alpha 0.012. The per-sphere label is off in the scene; the HUD carries the text instead. Four private properties of `MeshFilter`, `MeshRenderer`, `SphereCollider`, and `BoxCollider` in the marker manager keep those types out of engine code stripping, which the Unity manual requires for `CreatePrimitive` in a stripped build.

### When the sphere is allowed to show

For a placed sphere, three gates are all required: server connected, a reading update younger than the 5 s window with at least one valid entry, and, while the room requirement setting is on, room frame set this launch. In `HelloMR` that setting is off, so the third gate always passes. The preview sphere skips all three. Failing a gate hides the placed sphere.

### Starting a Placement again

Every Add Source tap, and every sticker scan in the wall QR flow, targets the one sphere keyed `sourceMarkerKey` ("SOURCE" by default); the sticker text is never a key. Starting a Placement while the sphere is placed snapshots its pose so Cancel restores it (`updateExistingMarkerOnRescan`), without smoothing. On start, records in the save file whose id is not that key are deleted, so a device that still holds an older file keyed by sticker text does not bring extra spheres back.

## 6. Persistence and restore

### The save file

`DetectorCoordinateDatabase` writes `detector_coordinates.json`, pretty-printed, into `Application.persistentDataPath` after every change; CPS updates are batched to at most one write per second. One record per sphere key holds the Unity-world pose of the session that placed it, the room-relative pose, QR scan metadata, the last CPS, and the spatial-anchor GUID when one was saved.

### Three restore layers

1. Session world pose. Valid only inside the launch that made it. Loaded at start only when `loadSavedCoordinatesOnStart` is on and anchors are off, never for a record that has a room pose; in `HelloMR` the load is off.
2. Room-relative pose. Positions are saved relative to the room frame and re-created at the next "Place Room". This is the layer the build relies on.
3. Spatial anchors. In `HelloMR` this layer is off: `useSpatialAnchors` is 0 on the marker manager and `loadAnchorsOnStart` is 0 on the anchor manager, so no anchor is created or loaded; `createSpatialAnchorOnQr` and `parentMarkerToAnchor` are still 1 and do nothing without them. With it on and the room frame set, no anchor is created: `FinalizeSpatialBinding` saves the room-relative pose and returns before the anchor code. With it on and no room frame, a Placement creates one: an `ARAnchor` through `ARAnchorManager`, saved after 0.5 s through the XREAL anchor persistence (up to 3 attempts, 2 s apart), the old anchor erased on rescan. On start, after a 1 s delay and up to 8 s waiting for the anchor subsystem, every record with a saved anchor GUID is loaded, and the code follows the XREAL Anchors sample by waiting up to 30 s for a loaded anchor to report tracking before using it. A loaded anchor whose record also has a room pose is ignored. The XREAL documentation says to look around before saving so the anchor has enough feature points; the build does not enforce a duration.

Background resume: `RoomCoordinateSystem` drops the frame and destroys every placed sphere when the app returns from the background (`invalidateCalibrationOnApplicationResume`), whether or not a frame existed, so the Operator places the Source Marker again, or scans `ROOM_ORIGIN` again with the room requirement on.

`HelloMR` runs with the room requirement off: `requireRoomCalibrationBeforeDetectorPlacement` 0, `useSpatialAnchors` 0, and `loadSavedCoordinatesOnStart` 0 on the marker manager, `loadAnchorsOnStart` 0 on the anchor manager, `startQrCameraAfterConnect` 0 on the receiver. Add Source works without `ROOM_ORIGIN`, the room gate passes, nothing is restored at start, no anchor is created, no camera opens after connecting, and each launch begins with a Placement (SPEC 3.3). The save file is still written at each Place. Turning the room requirement back on restores the wall QR flow of the paragraphs above: the sphere is saved room-relative and returns at "Place Room"; that flow also needs a clean coordinate file on the device.

## 7. Panel, HUD, pointer, capture

### Beam Pro panel

The panel is the XREAL virtual controller prefab, customized as `Assets/Prefabs/XREAL/XREALVirtualController_custom.prefab` and set as the virtual controller in the XREAL settings, so it appears on the Beam Pro's screen. Its Unity UI elements (a TextMesh Pro input field, texts, three workflow Buttons: Scan, Place, Cancel, and the Record and photo Buttons) are wired to `BeamProControllerBridge`, which owns the button labels, the workflow guide (a capitalized next step with an instruction line), the detector list text, and the enable states. The first button is Add Source: with no wall QR needed it connects if needed and calls the marker manager's `TryBeginSourcePlacement`, so no camera opens; only while the room requirement setting is on and the room is not yet registered does it start the scanner, and then it needs the scanner present and no capture in progress. Place needs a valid preview. Cancel is enabled during a scan or a pending placement. The first button is disabled while the marker manager is missing, and Add Source stays available while the server is still connecting or a capture is running, since it needs neither. There is no Connect button: the first button connects first when there is no connection, and finishing the address field's edit also connects. `ControllerPanelEditorLayout` stands in for the XREAL layout script in the editor only.

### HUD

`ARDetectorHud` builds a world-space canvas at runtime, 1.5 m in front of the camera, anchored at viewport (0.94, 0.06), 720 x 430 px, font size 27, at most 9 rows; `HeadLockedCanvas` keeps it in front of the camera. Rows show Detector ID, CPS, and the glasses-to-sphere distance; a sphere within 8 degrees of the view center highlights its row; when a sphere is off screen a text indicator sits 5 % in from the nearest edge in one of four directions, font size 28.

### Pointer move

`DetectorControllerInteractor` uses the XR Interaction Toolkit ray of the right controller (the Beam Pro) and an `InputAction` on `<XREALController>/TriggerButton`, the center pad, with fallbacks for a generic XR controller and the editor's simulated controller. A placed, visible sphere within 10 m and 1.5 degrees of the ray (hit radius 1.25 times the sphere) highlights by blending 20 % toward white; the pad moves it. The feature is on through the script default of `enableControllerDetectorReposition`, which the scene does not override, and the interactor is added at runtime when missing.

### Capture

`XREALCaptureManager` uses `XREALVideoCapture` and `XREALPhotoCapture` from `Unity.XR.XREAL` with the RGB camera, blend mode (camera plus virtual content), single side, no audio, black background. Because the QR scanner and the capture share the camera, capture first releases the `WebCamTexture` and waits 0.25 s. The panel's Start Record / Stop Record button and photo button call it. Video is recorded to `Application.persistentDataPath/RadVis_<date>_<time>.mp4`; a photo is written with `File.WriteAllBytes` to `Application.persistentDataPath/XrealShots/RadVis_<date>_<time>.jpg`; both are then inserted into the Android gallery; a photo whose insert throws is still reported as saved, with a warning in the log.

## 8. Build settings and checklist

Project: product name `Hello_IDCLab`, bundle version 0.2, Android minimum API 29, ARM64, IL2CPP, OpenGL ES 3, Linear color space. `Assets/Plugins/Android/AndroidManifest.xml` adds CAMERA, INTERNET, and RECORD_AUDIO; the XREAL settings add VIBRATION, RECORD_AUDIO, and CAMERA and turn on multi-resume and auto logcat. XR: the XREAL loader only (`Assets/XR/Loaders/XREALXRLoader.asset`, settings in `Assets/XR/Settings/XREALSettings.asset`). Build Profile Android; scene list `HelloMR` only.

Three variants for the device day; the two partial ones are planned as scene copies. Server-only is a component removal; placement-only also needs a code change:

| Build | Settings | Notes |
|---|---|---|
| placement-only | leave the receiver enabled, run without a server; turn off "hide markers until server connected" and "hide markers without fresh radiation data" on the marker manager | the Scan button needs the receiver present; a failed connection attempt logs an error but does not block scanning or placing. With no reading the placed sphere's center is not drawn (section 5, "When the sphere is allowed to show"), so this variant shows the gray preview, the HUD row, and the edge indicator, but no sphere after Place, until a "no readings" appearance exists |
| server-only | remove the marker manager component from the scene copy | the HUD and the anchor manager share its object and stay on. With the component missing the first button is disabled and the guide reads "APP SETUP NOT READY"; the QR camera never opens because "start QR camera after connect" is off. Disabling the component instead of removing it is not enough: the bridge still finds a disabled component, and Add Source would start a preview that never updates |
| full | nothing | the scene as committed |

Before every build:

- The XREAL package is referenced as a local archive outside the repository; building on another machine needs that archive at the same location or a copy inside the repo.
- Build Profile: Android. Scene list: `HelloMR` only.
- Laptop firewall allows inbound 5002. Beam Pro and laptop on the same Wi-Fi.
- No QR is needed while the room requirement setting is off. With it on, print the `ROOM_ORIGIN` QR and tape it to a vertical wall at eye height.
- The table top under the source must be a surface the glasses can detect, and the wall carrying the QR too when that setting is on.

## 9. References and examples

XREAL SDK 3.1.0 documentation (the site's version selector says 3.1.0, the same as the package in the repo):

- Overview: https://docs.xreal.com/
- Getting Started with XREAL SDK: https://docs.xreal.com/Getting%20Started%20with%20XREAL%20SDK
- Plane Detection: https://docs.xreal.com/Plane%20Detection
- Spatial Anchor: https://docs.xreal.com/Spatial%20Anchor/intro and Anchors: https://docs.xreal.com/Spatial%20Anchor/Anchors (the look-around advice before saving; `SaveAnchor`, `LoadAllAnchors`)
- Camera: https://docs.xreal.com/Camera/Camera and Access RGB Camera: https://docs.xreal.com/Camera/Access%20RGB%20Camera (`XREALVideoCapture`, blend modes)
- Input and Interactions: https://docs.xreal.com/category/input-and-interactions
- Migrating from NRSDK to XREAL SDK: https://docs.xreal.com/MigratingFromNRSDKToXREALSDK/intro (why the build uses AR Foundation and the XR Interaction Toolkit rather than NRSDK APIs)
- API reference: https://developer.xreal.com/reference/nrsdk/overview/

Unity packages:

- AR Foundation 6.0.8: https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.0/manual/index.html
- XR Interaction Toolkit 3.0.11: https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.0/manual/index.html
- Input System 1.19.0: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/index.html
- Newtonsoft Json 3.2.2: https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html

Libraries:

- NativeWebSocket: https://github.com/endel/NativeWebSocket
- ZXing.Net: https://github.com/micjahn/ZXing.Net

Examples the code follows or that can be run:

- XREAL samples imported under `Assets/Samples/XREAL XR Plugin/`: AR Features (plane detection and anchors; the anchor code follows the Anchors sample's load-then-wait-for-tracking pattern), Camera Features (capture), Interaction Basics (controller ray).
- XR Interaction Toolkit 3.0.10 and XR Hands 1.7.3 samples: imported, not used by the scripts.
- `mock_radvis_server.py` (section 3) for editor runs without the lab server.
- Editor scenes `EditorTest`, `EditorTestXR` (simulated XR rig), `FalloffDemo` (shells), `ShaderCompare` (shader variants).

## 10. Gaps against SPEC.md

- F-07, 3.8: nothing shown depends on the wearer's distance; the wearer-to-sphere distance is already computed for the HUD row.
- 3.3, 3.10: the Add Source flow has not run on the device; the table plane under the source is the surface it depends on.
- 3.7: no code measures the placement error.
