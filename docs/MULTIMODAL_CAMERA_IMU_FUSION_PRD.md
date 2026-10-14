# Product Requirements Document (PRD)
# Multimodal Sensor Fusion: Dual Camera Hand Vision & Wearable 9-DoF IMU Engine

* **Document Version**: 2.0.0  
* **Date**: October 9, 2026  
* **Status**: Approved Implementation Architecture  
* **Target Platforms**: AllyAACUI (Canonical), AlphaAACUI, BetaAACUI — Web/PWA, Windows Desktop (WPF/WebView2), Android  
* **Sensor Streams**:
  1. Optical RGB Camera (MediaPipe 21 3D Hand Landmarks @ 30 FPS)
  2. Wearable 9-DoF BLE Inertial Measurement Unit (Movesense 2.0 @ 50–52 Hz Accel/Gyro/Inclinometry)  
* **Target Audience**: Communicators with complex neuromuscular profiles where unimodal sensing yields unacceptable error rates  

---

## 1. Executive Summary & Why Unimodal Sensing Fails

Single-modality sensing inevitably encounters physiological and environmental blind spots in clinical AAC:

```
+----------------------------------------------------------------------------------------------------+
|                               UNIMODAL SENSING FAILURE MODES                                       |
+--------------------------------------------------+-------------------------------------------------+
| CAMERA ONLY BLIND SPOTS                          | IMU WEARABLE ONLY BLIND SPOTS                   |
+--------------------------------------------------+-------------------------------------------------+
| • Severe Hand Occlusion (blanket, wheelchair)    | • Baseline Drift & Gravitational Ambiguity      |
| • Lighting Dropouts (dim room, bright sunlight)  | • Inability to track finger flexion/finger tips |
| • Out of Camera Field-of-View (FOV)              | • Vulnerable to ambient wheelchair vibration    |
| • False Triggers from background people passing  | • Cannot distinguish intentional wave from slap |
+--------------------------------------------------+-------------------------------------------------+
```

### The Solution: Multimodal Cross-Attention Fusion
By fusing **spatial pose coordinates** from computer vision with **high-frequency inertial dynamics** from the wearable IMU, AllyAAC achieves **$> 98\%$ false-positive rejection** while allowing micro-kinetic triggers that neither sensor could confirm alone.

---

## 2. Multimodal Fusion Architecture

```
+----------------------------------------------------------------------------------------------------+
|                             DUAL STREAM TIMEBASE & FUSION TOPOLOGY                                 |
+----------------------------------------------------------------------------------------------------+
|                                                                                                    |
|  [STREAM A: CAMERA VISION]                                     [STREAM B: WEARABLE IMU]            |
|  • WebRTC MediaStream (30 FPS)                                 • Movesense BLE GATT (50-52 Hz)     |
|  • MediaPipe 21 3D Hand Landmarks                              • 3-Axis Accel + 3-Axis Gyro        |
|  • Normalization: D_palm distance                              • Pitch, Roll, Shock Magnitude      |
|              |                                                               |                     |
|              +-------------------------------+-------------------------------+                     |
|                                              |                                                     |
|                                              v                                                     |
|                           [UNIFIED HARDWARE TIMEBASE & SYNCHRONIZER]                               |
|                           • High-Resolution Epoch: performance.now()                               |
|                           • Linear Hermite Spline Resampling to 50 Hz Grid                          |
|                                              |                                                     |
|                                              v                                                     |
|                           [MULTI-TIER CROSS-MODAL FUSION ENGINE]                                   |
|                                              |                                                     |
|       +--------------------------------------+--------------------------------------+              |
|       |                                                                             |              |
|       v                                                                             v              |
|  [EARLY KINEMATIC GATING (Tier 0/1)]                           [DEEP CROSS-ATTENTION (Tier 3)]     |
|  • Fast logical veto:                                          • Dual-stream 1D Transformer        |
|    - Vision sees "Pinch" BUT IMU sees 0 accel                  • Query: Vision spatial features    |
|      -> Suppressed (Static resting hand)                       • Key/Value: IMU dynamic frequency  |
|    - IMU sees Shock Spike BUT Vision sees no hand              • ONNX Runtime Web (WebGPU/WASM)    |
|      -> Suppressed (Wheelchair bump)                           • Confidence score $\ge 0.90$       |
|                                              |                                                     |
|                                              +--------------------------------------+              |
|                                              v                                                     |
|                             [CONFIRMED DISPATCH & REFRACTORY HUD]                                  |
|                             • Dispatches handleGestureTrigger(gesture)                             |
|                             • Ingests aligned session clip to Gesture Studio Dataset                |
+----------------------------------------------------------------------------------------------------+
```

---

## 3. Mathematical Timebase Synchronization

Because the camera operates at $\sim 30\text{ FPS}$ ($33.3\text{ ms}$ intervals) and the Movesense IMU streams at $50\text{--}52\text{ Hz}$ ($19.2\text{--}20.0\text{ ms}$ intervals), raw frames cannot be directly combined.

### Unified Resampling Grid
Both streams are aligned onto a synchronized $50\text{ Hz}$ interpolation grid ($\Delta t = 20\text{ ms}$):
$$\hat{\mathbf{V}}(t) = (1 - \alpha) \mathbf{V}(t_k) + \alpha \mathbf{V}(t_{k+1}), \quad \alpha = \frac{t - t_k}{t_{k+1} - t_k}$$
where $\mathbf{V}$ represents the 21 3D hand keypoints and $\mathbf{I}(t) = [a_x, a_y, a_z, \omega_x, \omega_y, \omega_z]$ represents the 6-DoF inertial packet.

---

## 4. Multi-Modal Verification Matrix

| Gesture Token | Vision Requirement (Spatial) | IMU Requirement (Inertial) | Cross-Modal Verification Rule |
| :--- | :--- | :--- | :--- |
| **`knock`** | Fist pose steady in camera ($> 0.85$ conf) | Acceleration impulse $\|\mathbf{a}\| > 14.0\text{ m/s}^2$ | **IMU impulse confirmed only if hand is visible in workspace.** |
| **`pinch`** | Thumb/index contact ($< 0.28 D_{\text{palm}}$) | Steady gravitational equilibrium ($\pm 0.15\text{G}$) | **Suppresses pinch if arm is swinging wildly.** |
| **`swipe_right`**| Horizontal trajectory $\Delta x > +0.6 D_{\text{palm}}$ | Gyroscopic roll impulse $\omega_z > 80^\circ/\text{s}$ | **Dual-confirmed lateral stroke; immune to head movement.** |
| **`wave`** | Lateral hand bounding oscillation | Periodic angular velocity reversal ($1.0\text{--}2.5\text{ Hz}$) | **Filters ambient camera noise by verifying physical wrist motion.**|
| **`shake`** | Hand pitch angle rapid inversion | Roll angular rate reversal across 3 half-waves | **Requires simultaneous optical and inertial agreement.** |

---

## 5. Graceful Degradation & Single-Sensor Fallback

In real-world use, sensor dropouts will occur (e.g. Movesense battery dies, or communicator moves out of camera sight):

```
+------------------------------------------------------------------------------------+
|                             SENSOR HEALTH MONITOR & FALLBACK                       |
|                                                                                    |
| [HEALTH CHECK: CONTINUOUS 1000ms WINDOW]                                           |
| • IMU Packet Loss Rate > 15% OR BLE Disconnected?                                  |
|   --> Degrades smoothly to "Vision Unimodal Mode" (Higher dwell times enforced)     |
| • Camera Low Lux (< 10 lux) OR Face/Hand occluded?                                 |
|   --> Degrades smoothly to "IMU Unimodal Mode" (SW-RKPIC state machine active)     |
| • Both streams healthy?                                                            |
|   --> Activates full "Multimodal Synergy Tier" with instant, sub-50ms firing      |
+------------------------------------------------------------------------------------+
```
