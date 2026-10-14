# Product Requirements Document (PRD)
# Camera Facial Expression & Head Gesture Recognition Engine: Spastic Cranial Tone & Tremor Considerations

* **Document Version**: 1.0.0  
* **Date**: October 9, 2026  
* **Status**: Approved Clinical Architecture Specification  
* **Target Platforms**: AllyAACUI, AlphaAACUI (ScoobyUI), BetaAACUI (HerbieUI) — Web/PWA, Windows Desktop, Android  
* **Primary Sensing**: Monocular Front RGB Camera (MediaPipe 468/478 Face Mesh with Blendshapes @ 30–60 FPS)  
* **Target User Population**: Communicators with Quadriplegia, High Spinal Cord Injury (C1–C4), Late-Stage ALS, Severe Athetoid Dyskinetic Cerebral Palsy with zero functional limb movement  

---

## 1. Executive Summary & Clinical Context

When upper-extremity limb movement is completely lost due to cervical spinal cord injury, late-stage ALS, or severe athetoid spasticity, **cranial and facial motor pathways** frequently remain intact or exhibit the highest voluntary control.

However, standard face-tracking systems fail individuals with neurological impairments due to:
1. **Involuntary Facial Grimacing & Oromandibular Dystonia**: Spasms pull the mouth, jaw, and brow involuntarily during effortful speech or breathing.
2. **Head Instability & Asymmetric Tonic Neck Reflexes (ATNR)**: Involuntary neck spasticity causes sudden head whipping or tilted resting postures against wheelchair headrests.
3. **Ptosis & Involuntary Blepharospasm**: Drooping eyelids or reflexive eyelid clenching that confuses traditional blink-detection algorithms.
4. **Fatigue & Variable Cranial Muscle Tone**: Smile or brow movements weaken after 10–15 minutes of continuous communication.

### Objective
This PRD specifies the **Spasticity-Aware Camera Facial Expression & Head Gesture Engine**. By tracking 478 3D facial landmarks and 52 standardized FACS blendshapes via MediaPipe Face Mesh, it isolates voluntary cranial micro-gestures while aggressively filtering out dystonic grimaces, breathing shifts, and head instability.

---

## 2. Facial Landmark Topology & Blendshape Mapping

```
+----------------------------------------------------------------------------------------------------+
|                             FACIAL MESH & VOLUNTARY BLENDSHAPE CHANNELS                            |
+----------------------------------------------------------------------------------------------------+
|                                                                                                    |
|                                       [Brows: 66, 296]                                             |
|                                     Brow Inner Up / Frown                                          |
|                                                                                                    |
|                 [Left Eye: 33, 133]                         [Right Eye: 362, 263]                  |
|                 Blink / Wink Left                           Blink / Wink Right                     |
|                                      [Nose Tip: 1]                                                 |
|                                   Head Pitch, Yaw, Roll                                            |
|                                                                                                    |
|                                 [Mouth: 61, 291, 13, 14]                                           |
|                            Mouth Open / Smile / Cheek Puff / Pucker                                |
|                                                                                                    |
|                                      [Jaw Base: 152]                                               |
|                                         Jaw Thrust                                                 |
+----------------------------------------------------------------------------------------------------+
```

### 52 Facial Blendshape Channels Evaluated
MediaPipe Face Landmarker produces normalized blendshape coefficients $\in [0.0, 1.0]$. The AAC engine evaluates 7 primary voluntary triggers:
* `eyeBlinkLeft` & `eyeBlinkRight`: Independent left/right eye wink discrimination.
* `jawOpen`: Deliberate mouth opening / drop.
* `mouthSmileLeft` & `mouthSmileRight`: Bilateral or unilateral smile activation.
* `browInnerUp`: Surprise / brow flash.
* `mouthPucker`: Kiss / whistle lip protrusion.
* `headPitch`, `headYaw`, `headRoll`: 3-DoF head nod, turn, and tilt extracted via Perspective-n-Point (PnP) from face mesh bounding anchors.

---

## 3. Cranial Spasticity, Grimace & Tremor Mitigation Pipeline

```
+----------------------------------------------------------------------------------------------------+
|                              CRANIAL SPASM & HEAD TREMOR PIPELINE                                  |
+----------------------------------------------------------------------------------------------------+
| [1. Raw 30 FPS Camera Stream] -> [MediaPipe 478 3D Landmarks & 52 Blendshapes]                    |
|                                                                                                    |
| [2. Head Pose Stabilization & Baseline Re-Centering]                                               |
|     • Decouples Facial Expressions from Head Movement: Facial blendshapes are evaluated relative    |
|       to the user's localized rigid head pose, remaining stable even if head tilts 30°             |
|     • Dynamic Resting Neutral Calibrator: Continually tracks 5-second moving average of the resting|
|       face, accommodating asymmetric resting facial tone (e.g. post-stroke hemiparesis)           |
|                                                                                                    |
| [3. Oromandibular Dystonia & Grimace Rejection]                                                    |
|     • Dystonic Grimace Filter: Calculates cross-channel muscle co-activation:                      |
|       $$\Gamma_{\text{grimace}} = \text{jawOpen} \times \text{browDown} \times \text{mouthStretch}$$ |
|     • When multi-muscle grimacing spikes (> 0.65), all AAC triggers are temporarily suppressed    |
|                                                                                                    |
| [4. Micro-Motion Expansion & Relative Range Normalization]                                         |
|     • Expands minimal voluntary muscle contractions: a 15% brow raise is normalized to 100% trigger |
|                                                                                                    |
| [5. Intentional Dwell Gating & Cooldown]                                                           |
|     • Dwell Time: $350\text{--}500\text{ ms}$ required to distinguish deliberate winks from normal|
|       corneal reflex blinks (normal blinks last $100\text{--}150\text{ ms}$)                        |
|     • Refractory Lockout: $600\text{ ms}$ to prevent double selection                              |
+----------------------------------------------------------------------------------------------------+
```

---

## 4. Facial & Head Gesture Action Mapping

| Cranial Trigger | FACS Blendshape / Pose Metric | Minimum Hold Duration | Mapped AAC Action | Clinical Rationale |
| :--- | :--- | :--- | :--- | :--- |
| **Deliberate Wink (Left)** | `eyeBlinkLeft > 0.70` && `eyeBlinkRight < 0.25` | $350\text{ ms}$ | ⌫ Delete Word | Single-switch scanning reverse / backspace |
| **Deliberate Wink (Right)**| `eyeBlinkRight > 0.70` && `eyeBlinkLeft < 0.25` | $350\text{ ms}$ | 👉 Accept Prediction | Rapid predictive text selection |
| **Mouth Open / Jaw Drop** | `jawOpen > 0.45` | $450\text{ ms}$ | 🔊 Speak Active Message | High-reliability trigger for communicators with ATNR |
| **Brow Flash (Raise)** | `browInnerUp > 0.50` | $400\text{ ms}$ | 🆘 Emergency Alert / Quick Help | Retained in high-spinal cord injury |
| **Cheek Puff / Lip Pucker**| `mouthPucker > 0.55` | $500\text{ ms}$ | 🗑️ Clear Message | Distinct from ambient breathing patterns |
| **Head Nod (Pitch Down)** | Head Pitch angle $> +15^\circ$ | $300\text{ ms}$ | ➕ Insert "Yes" | Natural affirmative response |
| **Head Shake (Yaw Sweep)** | Head Yaw reversal $\Delta \theta > 25^\circ$ | $450\text{ ms}$ | ➕ Insert "No" | Natural negative response |
| **Head Tilt (Roll Left/Right)**| Head Roll angle $> \pm 18^\circ$ | $400\text{ ms}$ | 🔄 Next / Prev Board | Accessible navigation for wheelchair headrest mounts |

---

## 5. Privacy, Lighting & Battery Considerations

1. **100% On-Device WebAssembly Processing**: Zero facial images or biometrics ever hit local disk or cloud servers. Fully compliant with HIPAA and GDPR biometric privacy laws.
2. **Infrared & Low-Light Fallback**: Supports standard USB Webcams with automatic gamma compensation for evening/bedroom communication without harsh screen glares.
3. **Adaptive CPU Throttling**: If device battery drops below $20\%$, video processing automatically throttles to 20 FPS with minimal impact on 400ms dwell detection.
