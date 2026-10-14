# AllyAAC Video-to-IMU Synchronization Technical Specification

* **Document Version**: 1.1.0  
* **Date**: October 9, 2026  
* **Status**: Implemented & Production Deployed (`v3.0.4`)  
* **Target Platforms**: Web/PWA, Windows Desktop (WPF/WebView2), Android Kiosk  
* **Live Deployment**: [https://lab.llslim.com/allyaac/](https://lab.llslim.com/allyaac/)  

---

## 1. Overview & Problem Definition

In motor-accessible AAC design, communicators with severe physical impairments (e.g. quadriplegic cerebral palsy, ALS, muscular dystrophy) execute subtle, micro-kinetic gestures such as wrist twitches, finger pinches, or directional forearm tilts.

A critical limitation in clinical evaluation has been the lack of **visual ground-truth verification**:
- An accelerometer spike on an oscilloscope indicates that motion occurred, but clinicians cannot easily verify *what physical movement caused it* without synchronized video footage.
- Involuntary spasticity, ambient wheelchair vibrations, or motor spasms produce acceleration spikes that resemble deliberate gestures.

The **Video-to-IMU Synchronization Engine** ([`src/js/video_sync.js`](file:///D:/projects/aac/AllyAAC/AllyAACUI/src/js/video_sync.js)) solves this by linking high-frequency 9-DoF IMU data directly to physical video frames recorded from the device's camera.

---

## 2. System Architecture

```
+-----------------------------------------------------------------------------------------+
|                                    CAPTURE PHASE                                        |
+------------------------------------+----------------------------------------------------+
|       CAMERA VIDEO FEED            |               9-DoF IMU SENSOR STREAM              |
| • WebRTC MediaStream (30 FPS)      | • Movesense 2.0 BLE (50-52 Hz) / Optical Vision /   |
| • MediaRecorder API (.webm)        |   Kinematic Simulator (50 Hz)                      |
|                                    | • Ax, Ay, Az, |M|, Pitch, Roll                     |
+------------------------------------+----------------------------------------------------+
                                     |
                                     v
+-----------------------------------------------------------------------------------------+
|                   HIGH-RESOLUTION SYNCHRONIZATION TIMEBASE                              |
|           • Unified epoch: performance.now()                                            |
|           • Frame alignment: t_ms = now - startTime, videoSec = t_ms / 1000             |
+-----------------------------------------------------------------------------------------+
                                     |
                                     v
+-----------------------------------------------------------------------------------------+
|                   TIMELINE SCRUBBER & ANNOTATOR WORKBENCH                               |
| • Video Player Viewport (#scrubber-sync-video)                                          |
| • Multi-Channel Waveform Track (#scrubber-timeline-canvas)                              |
| • Bidirectional Scrubbing: Dragging timeline immediately updates video.currentTime      |
| • Glowing Synchronized Playhead (#scrubber-sync-playhead) glides during playback        |
| • Sub-millisecond Frame-Stepping: ⏮ -1F / ⏭ +1F at 33ms intervals                       |
+-----------------------------------------------------------------------------------------+
                                     |
                                     v
+-----------------------------------------------------------------------------------------+
|                         MULTI-MODAL DATASET EXPORT                                      |
| • allyaac_synced_telemetry_[ts].json  (JSON telemetry packet series)                    |
| • allyaac_synced_video_[ts].webm      (Synchronized camera video clip)                  |
+-----------------------------------------------------------------------------------------+
```

---

## 3. Data Model & Timestamp Mathematics

Each synchronized sample in `videoSyncState.activeSession.imuSamples` records:

```typescript
interface SyncedTelemetrySample {
  t_ms: number;       // Elapsed milliseconds from recording start
  videoSec: number;   // Corresponding video playback time in seconds (t_ms / 1000)
  ax: number;         // Surge acceleration (m/s²)
  ay: number;         // Sway acceleration (m/s²)
  az: number;         // Heave acceleration (m/s²)
  mag: number;        // Total acceleration magnitude: sqrt(ax² + ay² + az²)
  pitch: number;      // Gravitational pitch inclination (-90° to +90°)
  roll: number;       // Gravitational roll inclination (-180° to +180°)
  landmarks?: any[];  // Optional 21-keypoint optical hand landmarks
}
```

### Gravitational Inclinometry Equations:
$$\theta_{\text{pitch}} = \text{atan2}\left(a_x, \sqrt{a_y^2 + a_z^2}\right) \times \frac{180}{\pi}$$
$$\phi_{\text{roll}} = \text{atan2}\left(a_y, a_z\right) \times \frac{180}{\pi}$$

---

## 4. UI Controls & Interactions

### 1. Recording Phase
- **`📹 Sync Video` Checkbox:** Located on the studio recording bar; toggles simultaneous video capture with IMU stream.
- **Recording Status Pill (`#sync-recording-indicator`):** Displays live recording status, duration elapsed, and accumulated IMU points.
- **Camera Feed Toggle (`📹 Cam`):** In the Hand Pose Keypoints instrument card, toggles live frontal camera feed preview.

### 2. Scrubber & Annotator Phase
- **Waveform Seeking:** Clicking or dragging anywhere across `#scrubber-track-container` computes:
  $$\text{ratio} = \frac{x_{\text{click}} - x_{\text{left}}}{\text{width}}, \quad t_{\text{target}} = \text{ratio} \times \text{duration}$$
  and immediately seeks `video.currentTime = targetSec`.
- **Telemetry HUD:** Automatically binary-searches `imuSamples` for the sample closest to `targetSec` and updates on-screen values (`Ax`, `Ay`, `Az`, `|M|`, `Pitch`, `Roll`).
- **Frame Stepping:**
  - `stepSyncVideo(-1)`: Moves back 1 frame ($-33\text{ ms}$).
  - `stepSyncVideo(1)`: Moves forward 1 frame ($+33\text{ ms}$).
- **Looping & Speed Control:**
  - `toggleSyncVideoLoop()`: Constrains playback to the candidate In/Out segment.
  - `toggleSyncPlaybackSpeed()`: Toggles playback between normal (`1.0x`) and half-speed (`0.5x`).
- **Boundary Trimming:**
  - `setCandidateBoundaryStart()`: Sets In-Point (`[ In`) to current video playhead.
  - `setCandidateBoundaryEnd()`: Sets Out-Point (`Out ]`) to current video playhead.

### 3. Dataset Export
Clicking **📥 Export Synced Dataset** calls `exportSyncedDataset()`:
- Downloads `allyaac_synced_telemetry_[timestamp].json` containing complete metadata and sample arrays.
- Downloads `allyaac_synced_video_[timestamp].webm` for machine learning training and verification.

---

## 5. Fallback & Cross-Platform Support

| Environment | Video Capture Strategy | Playback Renderer |
| :--- | :--- | :--- |
| **PWA / Desktop Browser with Webcam** | `getUserMedia` + `MediaRecorder` (`video/webm`) | Native `<video id="scrubber-sync-video">` |
| **Offline / Test Automation / No Camera** | Kinetic Avatar Engine | Canvas `<canvas id="scrubber-synthetic-video-canvas">` |
| **Windows Desktop Host (WPF)** | Edge WebView2 Media Stream + BLE GATT | Native WebView2 Video Surface |
| **Android WebView Kiosk** | Android CameraX + WebRTC | Hardware Accelerated WebView Video |

---

## 6. Verification & Test Suite

The implementation is verified by two test suites in `AllyAACUI/tests/`:
1. **`tests/run_tests.js` (60 passed, 0 failed):** Validates DTW mathematical invariance, modular `@include` compilation, and verifies definitions for all 13 Video Sync button handlers.
2. **`tests/test_runtime.js`:** Evaluates DOM event dispatching, canvas drawing, video scrubbing, stepping, and dataset export in a mocked DOM environment.

---

## 7. Related Specifications

* **Movement Calculations & Telemetry Physics**: [`docs/MOVEMENT_CALCULATIONS.md`](file:///D:/projects/aac/AllyAAC/AllyAACUI/docs/MOVEMENT_CALCULATIONS.md) — Comprehensive guide to $A_x, A_y, A_z$ spatial axes, Inclinometer trigonometry, lateral dynamic sway, and shockwave filtering.
* **Gesture Studio PRD**: [`docs/GESTURE_STUDIO_PRD.md`](file:///D:/projects/aac/AllyAAC/AllyAACUI/docs/GESTURE_STUDIO_PRD.md) — Clinical sensor workbench architecture and multi-tier gesture classification specifications.

