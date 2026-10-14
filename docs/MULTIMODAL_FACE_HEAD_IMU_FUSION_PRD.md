# Product Requirements Document (PRD)
# Multimodal Cranial Fusion: Dual Camera Face/Head Tracking & Head-Mounted 9-DoF IMU Engine

* **Document Version**: 1.0.0  
* **Date**: October 9, 2026  
* **Status**: Approved Implementation Architecture  
* **Target Platforms**: AllyAACUI, AlphaAACUI, BetaAACUI — Web/PWA, Windows Desktop, Android  
* **Dual Sensing Architecture**:
  1. Optical RGB Front Camera (MediaPipe Face Mesh 478 3D Landmarks & Blendshapes @ 30 FPS)
  2. Head-Mounted Wearable 9-DoF IMU (Movesense 2.0 Headband / Glasses Frame Mount @ 50–52 Hz)  
* **Target User Population**: Communicators with Quadriplegia, Severe Cerebral Palsy, ALS, and Wheelchair Users Navigating Rough Terrain  

---

## 1. Executive Summary & Problem Definition

Tracking head gestures and facial expressions using a front-facing camera mounted to a wheelchair or bed tray encounters severe environmental distortions in real-world everyday life:

```
+----------------------------------------------------------------------------------------------------+
|                         CHALLENGES OF WHEELCHAIR-MOUNTED HEAD TRACKING                             |
+----------------------------------------------------------------------------------------------------+
| 1. Wheelchair Motion Artifacts: When a powered wheelchair drives over bumps, ramps, or thresholds,  |
|    the entire camera and user bounce relative to each other, causing massive optical false nods.   |
| 2. Angular Ambiguity: Camera perspective distortion creates false facial yaw/roll when the user    |
|    leans forward or shifts their torso posture against headrests.                                  |
| 3. High-Speed Tremor vs. Low-Latency Intent: A user may have a 6 Hz resting head tremor that makes |
|    optical dwell impossible without aggressive, lag-inducing low-pass filtering.                   |
+----------------------------------------------------------------------------------------------------+
```

### The Solution: Multimodal Cranial Fusion
By fusing an optical camera facing the communicator's face with a lightweight, head-mounted **Movesense 2.0 BLE sensor** (clipped to an eyeglass temple, headband, or baseball cap), the system achieves:
* **True Inertial Angular Decoupling**: Direct, drift-free gyroscopic orientation ($\pm 0.1^\circ$) verified against optical face landmarks.
* **Ambient Wheelchair Shock Subtraction**: Eliminates false activations from bumpy wheelchair driving.
* **Instant Confirmation of Facial Triggers**: Requires optical confirmation (e.g. Jaw Drop or Wink) alongside stable inertial stillness.

---

## 2. Hardware Mounts & Sensor Topology

```
+----------------------------------------------------------------------------------------------------+
|                               HARDWARE SETUP & STREAM TOPOLOGY                                     |
+----------------------------------------------------------------------------------------------------+
|                                                                                                    |
|   [FRONT CAMERA (Tablet / Kiosk Mount)]             [HEAD-MOUNTED MOVESENSE 2.0 BLE]               |
|   • Captures 30 FPS RGB Stream                      • Clips to glasses frame, headband, or cap     |
|   • MediaPipe Face Mesh:                            • 6-DoF Inertial:                              |
|     - Eye winks, smile, jaw open, cheek puff          - 50 Hz Linear Acceleration (Ax, Ay, Az)     |
|     - Optical head pose (Pitch, Yaw, Roll)            - 50 Hz Gyroscope (Gx, Gy, Gz)              |
|              |                                                               |                     |
|              +-------------------------------+-------------------------------+                     |
|                                              |                                                     |
|                                              v                                                     |
|                         [UNIFIED 50 Hz TIMEBASE SYNCHRONIZER]                                      |
|                         • Spline interpolation aligning video frames to IMU ticks                  |
|                                              |                                                     |
|                                              v                                                     |
|                         [CRANIAL CROSS-MODAL FUSION ENGINE]                                        |
|                                              |                                                     |
|       +--------------------------------------+--------------------------------------+              |
|       |                                                                             |              |
|       v                                                                             v              |
|  [DYNAMIC VEHICULAR SHOCK CANCELLER]                          [OPTICAL-INERTIAL CO-VERIFIER]       |
|  • If IMU detects chassis shock impulse                       • High-speed head gesture confirmed: |
|    without intentional neck muscle torque:                      - IMU gyroscopic angular rate      |
|    --> Discards optical frame motion as vehicular vibration     - Camera face mesh angle rotation  |
|                                                               • Micro-facial trigger confirmed:    |
|                                                                 - Optical blendshape active        |
|                                                                 - IMU indicates head is at rest    |
|                                              |                                                     |
|                                              +--------------------------------------+              |
|                                              v                                                     |
|                         [ACCESSIBLE AAC DISPATCHER & RECENTRING HUD]                               |
|                         • Dispatches handleGestureTrigger(gesture)                                 |
+----------------------------------------------------------------------------------------------------+
```

---

## 3. Vehicular Shock Cancellation & Inertial Subtraction

When a communicator is riding in a wheelchair or vehicle, both the camera mount and the communicator experience external translational accelerations $\mathbf{a}_{\text{vehicle}}(t)$:

$$\mathbf{a}_{\text{total}}(t) = \mathbf{a}_{\text{head\_voluntary}}(t) + \mathbf{a}_{\text{vehicle}}(t)$$

### Subtraction Algorithm
1. High-frequency translational shocks ($\|\mathbf{a}\| > 18\text{ m/s}^2$ with rise time $< 40\text{ ms}$) lack the biomechanical signature of voluntary human neck flexion (human neck pitch acceleration rarely exceeds $600^\circ/\text{s}^2$ with a smooth onset).
2. The fusion engine flags high-frequency shock spikes as `VEHICULAR_JOLT` and locks optical head tracking for $350\text{ ms}$, preventing false selections whenever the wheelchair crosses a doorway saddle or street curb.

---

## 4. Optical-Inertial Co-Verification Rules

| Cranial Trigger | Camera Requirement (Facial Mesh) | Head-Mounted IMU Requirement | Clinical Co-Verification Rule |
| :--- | :--- | :--- | :--- |
| **Intentional Head Nod ("Yes")** | Optical Face Pitch rotation $> +12^\circ$ | Gyroscopic Pitch rate $\omega_y > 45^\circ/\text{s}$ | **Dual-confirmed nod; rejects false nods caused by torso slouching.** |
| **Intentional Head Shake ("No")** | Optical Face Yaw reversal $> 20^\circ$ | Gyroscopic Yaw rate $\omega_z > 50^\circ/\text{s}$ with reversal | **Requires continuous rotational momentum; rejects jerky tremors.** |
| **Deliberate Wink (Left / Right)**| Unilateral eye closure $> 0.75$, other eye $< 0.20$ | Head stability: $\|\boldsymbol{\omega}\| < 10^\circ/\text{s}$ | **Suppresses wink selection if user's head is actively whipping or turning.** |
| **Jaw Drop / Mouth Open** | `jawOpen > 0.45` sustained for $400\text{ ms}$ | Head pitch stationary within $\pm 4^\circ$ | **Disregards mouth opening caused by yawning or head extension.** |
| **Cheek Puff / Pucker** | `mouthPucker > 0.50` | Accelerometer quiet ($\le 1.05\text{G}$) | **High-reliability discrete selection trigger for high-level SCI.** |

---

## 5. Clinical Safety, Fatigue Management & Battery Optimization

1. **Automatic Fatigue Drift Tracking**:
   * As communicators with ALS or spinal muscular atrophy fatigue, neck range of motion decreases by up to $40\%$ over an hour.
   * The fusion engine maintains an adaptive moving baseline of active range of motion ($ROM_{\text{active}}$), dynamically lowering required nod angles from $15^\circ$ down to $8^\circ$ as session fatigue is detected.
2. **One-Tap Emergency Re-Centering**:
   * Communicators or caregivers can re-center their baseline head posture instantly via a floating target button, spacebar tap, or a 3-second sustained double-wink.
3. **Power Consumption**:
   * The Movesense sensor draws only $\sim 2.2\text{ mA}$ over Bluetooth Low Energy, providing $> 120\text{ hours}$ of continuous operation on a single CR2025 coin cell battery.
