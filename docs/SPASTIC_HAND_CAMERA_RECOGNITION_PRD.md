# Product Requirements Document (PRD)
# Camera Optical Hand Recognition Engine: Spasticity, Involuntary Tone & Tremor Considerations

* **Document Version**: 2.1.0  
* **Date**: October 9, 2026  
* **Status**: Approved Clinical Architecture Specification  
* **Target Platforms**: AllyAACUI, AlphaAACUI (ScoobyUI), BetaAACUI (HerbieUI) — Web/PWA, Windows Desktop, Android  
* **Primary Sensing**: Monocular RGB Camera (30–60 FPS, WebRTC / MediaPipe 3D Hand Landmarker)  
* **Target User Population**: Communicators with Dyskinetic / Spastic Cerebral Palsy, Dystonia, Parkinsonian Action Tremor, Brainstem Stroke, and Motor Neuron Conditions  

---

## 1. Executive Summary & Clinical Context

Traditional optical hand tracking models (such as vanilla MediaPipe or OpenCV contours) operate on assumptions of **normotypical biomechanics**: continuous trajectory smoothness, predictable finger joint extension angles, and low-frequency transition jitter. 

For communicators with neuromuscular conditions, these assumptions fail catastrophically:
1. **Hypertonicity & Spastic Clenching**: Sudden involuntary flexor spasms collapse fingers into a clenched fist without user intent.
2. **Athetoid Dystonia & Chorea**: Continuous, involuntary writhing movements displace the hand across the camera frame, triggering continuous false gestures.
3. **High-Frequency Action Tremors (4–12 Hz)**: Oscillations destabilize keypoint detection, causing rapid state flipping and false double-triggers.
4. **Restricted Active Range of Motion (ROM)**: Individuals with severe contractures cannot fully extend digits; a "point" or "pinch" may exhibit only 15–20% of normotypical joint displacement.

### Objective
This PRD specifies the **Spasticity-Aware Camera Hand Recognition Pipeline**. It incorporates adaptive dynamic filtering, biomechanical normalization, clinical dwell gating, and explicit negative-margin spasm rejection to enable robust AAC selection.

---

## 2. Anatomical Keypoints & Biomechanical Normalization

```
+----------------------------------------------------------------------------------------------------+
|                                MEDIAPIPE 21 3D HAND TOPOLOGY (NORMALIZED)                          |
+----------------------------------------------------------------------------------------------------+
|                                                                                                    |
|            [4] Tip           [8] Tip         [12] Tip        [16] Tip        [20] Tip              |
|              \                 |                |               |               |                  |
|              [3] IP          [7] DIP         [11] DIP        [15] DIP        [19] DIP              |
|                \               |                |               |               |                  |
|                [2] MP        [6] PIP         [10] PIP        [14] PIP        [18] PIP              |
|                  \             |                |               |               |                  |
|                  [1] CMC     [5] MCP         [9] MCP         [13] MCP        [17] MCP              |
|                    \           \                |               /               /                  |
|                     +-----------+---------------+---------------+--------------+                   |
|                                                 |                                                  |
|                                               [0] Wrist Base                                       |
|                                                                                                    |
+----------------------------------------------------------------------------------------------------+
```

### Scale & Distance Invariance
All coordinate measurements are normalized relative to the user's anatomical palm dimension $D_{\text{palm}}$ rather than raw pixel coordinates:
$$D_{\text{palm}} = \|\mathbf{p}_0 - \mathbf{p}_9\|_2$$
$$\hat{\mathbf{p}}_i = \frac{\mathbf{p}_i - \mathbf{p}_0}{D_{\text{palm}}}$$

This guarantees that recognition remains identical whether the communicator sits 40 cm or 150 cm from the camera.

---

## 3. Clinical Spasticity, Tremor & Tone Mitigation Architecture

```
+----------------------------------------------------------------------------------------------------+
|                              SPASTICITY-AWARE VISION PROCESSING PIPELINE                           |
+----------------------------------------------------------------------------------------------------+
| [1. Raw 30 FPS Camera Frame] -> [MediaPipe 21 3D Landmarks]                                        |
|                                                                                                    |
| [2. Adaptive Dual-Rate 1€ (One Euro) Filter]                                                       |
|     • Low velocity: Heavy cutoff ($f_c = 0.8\text{ Hz}$) to completely eliminate 4-12 Hz tremors    |
|     • High velocity: Dynamic cutoff ($f_c = 15\text{ Hz}$) to eliminate perception lag             |
|                                                                                                    |
| [3. Spasm Shock Gate (Displacement Velocity Thresholding)]                                         |
|     • Computes mean joint acceleration: $\mathbf{a}_{\text{mean}}(t) = \frac{d^2}{dt^2}\sum \|\hat{\mathbf{p}}_i\|$ |
|     • If $\mathbf{a}_{\text{mean}} > \tau_{\text{spasm}}$: Locks pipeline into REFRACTORY state     |
|       (prevents violent involuntary twitches from triggering speech)                               |
|                                                                                                    |
| [4. Personalized Relative ROM (Range of Motion) Re-Mapping]                                        |
|     • Calibrates user's personal minimum and maximum extension thresholds ($[E_{\min}, E_{\max}]$) |
|     • Allows a 20° finger lift to register as a full intentional "Point" or "Select"               |
|                                                                                                    |
| [5. Temporal Dwell & Confirmation HUD]                                                             |
|     • Requires target pose maintenance for $T_{\text{dwell}} = 350\text{--}650\text{ ms}$          |
|     • Circular glowing countdown ring on Floating Camera HUD confirms intent                       |
|                                                                                                    |
| [6. Dispatcher & Refractory Cooldown]                                                              |
|     • Fires handleGestureTrigger(gesture) -> Locks recognition for $600\text{ ms}$                 |
+----------------------------------------------------------------------------------------------------+
```

### 3.1 The Adaptive 1€ (One Euro) Tremor Filter
To eliminate Parkinsonian and cerebral palsy tremors without making the interface feel sluggish, keypoints pass through a speed-adaptive low-pass filter:
$$f_c = f_{c,\min} + \beta \cdot \|\dot{\hat{\mathbf{p}}}_i\|$$
where $f_{c,\min} = 0.7\text{ Hz}$ and speed coefficient $\beta = 0.005$. At rest, jitter is suppressed down to sub-pixel precision; during intentional movement, lag drops to $< 18\text{ ms}$.

### 3.2 Involuntary Tone Spasm Detection
An involuntary flexor spasm is characterized by an abrupt, high-acceleration collapse across all 5 digits simultaneously. 
* When the closure velocity across all distal joints exceeds $\tau_{\text{spasm}} = 2.8 \times D_{\text{palm}}/\text{s}$, the event is tagged as `INVOLUNTARY_SPASM`.
* The system suppresses the output, pulses a soft amber HUD indicator, and enters a 750 ms relaxation recovery phase.

---

## 4. Preset Gestures Adapted for Motor Impairments

| Gesture Token | Normotypical Definition | Spasticity-Adapted Formulation | Clinical AAC Action |
| :--- | :--- | :--- | :--- |
| **`pinch`** | Thumb tip touches index tip ($< 0.15 D_{\text{palm}}$) | Euclidean proximity $\le 0.32 D_{\text{palm}}$ + Dwell 400ms | 🔊 Speak Active Message |
| **`open_palm`** | All 5 fingers extended ($> 0.85 D_{\text{palm}}$) | Palm orientation planar + at least 3 digits $> 0.50 D_{\text{palm}}$ | ✋ Stop Speech Output |
| **`point`** | Index extended, others curled | Index extension $> 1.4 \times$ mean of other digits | 👉 Accept Word Prediction |
| **`thumbs_up`** | Fist + thumb extended vertical | Vertical thumb vector $\mathbf{v}_4 - \mathbf{v}_2 > 0$ with wrist steady | ➕ Insert "Yes" |
| **`thumbs_down`**| Fist + thumb extended downward | Inverted thumb vector $\mathbf{v}_4 - \mathbf{v}_2 < 0$ | ➕ Insert "No" |
| **`wave`** | High-frequency lateral wrist oscillation | Low-frequency forearm sway ($0.8\text{--}2.5\text{ Hz}$) | ➕ Insert "Hello!" |
| **`swipe_left`** | Rapid leftward hand swipe | Sustained lateral translation $\Delta x < -0.6 D_{\text{palm}}$ | ⌫ Delete Word |
| **`swipe_right`**| Rapid rightward hand swipe | Sustained lateral translation $\Delta x > +0.6 D_{\text{palm}}$ | 🔄 Next Page / Board |
| **`shake`** | Quick roll oscillation | Angular velocity reversal across 3 consecutive half-waves | 🗑️ Clear Message |

---

## 5. UI/UX & Feedback Governance

1. **Floating Accessible HUD**:
   * Displays 21-point wireframe skeleton colored by confidence (Green = Intentional, Amber = Tremor Filter Active, Red = Spasm Suppressed).
   * Circular dwell arc fills smoothly as the user holds a gesture, providing continuous visual confirmation.
2. **Audio Earcons**:
   * Distinct, non-startling auditory tones when a gesture locks in, preventing startle reflexes common in spastic cerebral palsy.
3. **Emergency Disarm**:
   * Caregivers can instantly disable camera recognition via global keyboard shortcut (`Esc` or `Space`) or a high-contrast disarm button.
