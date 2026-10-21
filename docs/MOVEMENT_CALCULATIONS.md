# AllyAAC IMU Movement Calculation & Telemetry Physics Specification

* **Document**: `MOVEMENT_CALCULATIONS.md`
* **Version**: 1.1.0
* **Date**: October 20, 2026
* **Status**: Implemented & Production Deployed (`v3.1.8`)
* **Target Platforms**: Web/PWA, Windows Desktop (WPF/WebView2), Android Kiosk
* **Applies To**: Gesture Studio Workbench, Inclinometer Gauges, 6-Channel Waveform Canvas & Scrubber, Settings Diagnostic Inspector (`Alt + D`), Annotator Scrubber

---

## 1. Overview & Mathematical Foundations

In motor-accessible AAC design, individuals with motor speech disorders, quadriplegic cerebral palsy, ALS, spinal cord injuries, or muscular dystrophies express intentional intent through subtle, micro-kinetic motor gestures. 

To turn physical movement into reliable, low-latency, and spasm-resilient AAC activations, AllyAAC ingests 9 Degrees-of-Freedom (9-DoF) telemetry at **50 Hz (20 ms interval)** from wearable inertial sensors (such as the Movesense Medical/Flash BLE pod or internal device accelerometers).

This document details:
1. Physical coordinate systems and the 3 orthogonal acceleration axes ($A_x, A_y, A_z$).
2. Inclinometer trigonometry (Pitch, Roll).
3. Dynamic Lateral Sway, RMS Postural Sway, and 2D Sway Path Length computation.
4. Total Magnitude ($|M|$) and Dynamic Surge Shockwave ($|M| - 9.8$).
5. Sensor collection pipelines, calibration, and synchronization.
6. The interactive instruments and tools built to visualize and manipulate this data.

---

## 2. Sensor Coordinate Frame & The 3 Orthogonal Axes

The wearable inertial measurement unit measures **linear acceleration** in meters per second squared ($\text{m/s}^2$) and **angular velocity** in degrees per second ($\text{deg/s}$) relative to the sensor body frame:

```
                          ▲ +Z (Heave - Outward from skin/face)
                          │
                          │   ▲ +X (Surge - Along forearm to fingers)
                          │  /
                          │ /
                          │/
                          +──────────────► +Y (Sway - Lateral across wrist)
```

### The Three Spatial Axes:

| Axis | App Trace Color | Biomechanical Term | Direction on Forearm | Physical Movements & AAC Intent |
| :--- | :--- | :--- | :--- | :--- |
| **$A_x$** | 🌸 **Pink** (`#F38BA8`) | **Surge (Longitudinal)** | Along the longitudinal axis of the forearm toward the fingertips. | Reaching forward, pulling back, forearm elevation/depression (Pitch tilt). |
| **$A_y$** | 🍏 **Green** (`#A6E3A1`) | **Sway (Lateral)** | Across the wrist plane from radial to ulnar side. | Waving hand side-to-side, lateral swipes, involuntary lateral tremors, forearm pronation/supination (Roll rotation). |
| **$A_z$** | 💧 **Blue** (`#89B4FA`) | **Heave (Vertical / Normal)** | Perpendicular to the sensor face outward from the skin. | Lifting or dropping arm, knocking downward against a table/wheelchair tray, tap shocks. |

---

## 3. Gravitational Field & Inclinometry Calculations

At complete rest, the accelerometer continues to register the constant downward acceleration of Earth's gravity vector:
$$\vec{g} \approx 9.80665\,\text{m/s}^2 \quad (\approx 1.0\text{G})$$

Because Earth's gravity vector always points toward the center of the planet, the distribution of gravity across $A_x, A_y, A_z$ provides an absolute gravitational inclinometer.

### 3.1 Pitch Angle ($\theta$) — Forearm Elevation
Pitch measures the elevation or downward depression of the forearm along the longitudinal axis ($A_x$):

$$\text{Pitch} = \text{atan2}\left(A_x, \sqrt{A_y^2 + A_z^2}\right) \times \left(\frac{180^\circ}{\pi}\right)$$

* **Resting Horizontal**: $A_x = 0 \implies \text{Pitch} = 0^\circ$
* **Forearm Tilted Upward**: $A_x > 0 \implies \text{Pitch} \in (0^\circ, +90^\circ]$
* **Forearm Tilted Downward**: $A_x < 0 \implies \text{Pitch} \in [-90^\circ, 0^\circ)$

### 3.2 Roll Angle ($\phi$) — Pronation / Supination
Roll measures rotation around the longitudinal axis (twisting the wrist outward or inward):

$$\text{Roll} = \text{atan2}\left(A_y, A_z\right) \times \left(\frac{180^\circ}{\pi}\right)$$

* **Neutral Palm-Down**: $A_y = 0,\, A_z \approx 9.8 \implies \text{Roll} = 0^\circ$
* **Supination (Wrist rotated right)**: $A_y > 0 \implies \text{Roll} > 0^\circ$
* **Pronation (Wrist rotated left)**: $A_y < 0 \implies \text{Roll} < 0^\circ$

---

## 4. Lateral Sway Computation (Clinical Biomechanics)

A major question in clinical posturography and upper-limb biomechanics is: **Can we calculate Sway?**

**Yes.** In clinical posturography, "Sway" refers to voluntary or involuntary displacement away from postural equilibrium. AllyAAC implements three distinct formulations depending on whether the objective is gesture triggering, spasm rejection, or clinical stability assessment:

### 4.1 Instantaneous 3D Gravity-Compensated Lateral Sway ($\text{Sway}_{\text{dynamic}}$)
When the forearm is tilted in 3D space (both Pitch $\theta$ and Roll $\phi$), static Earth gravity projects onto the lateral $A_y$ axis according to:
$$A_{y,\text{gravity}} = g \cdot \cos(\theta) \cdot \sin(\phi)$$

To isolate **true voluntary lateral movement** from static 3D wrist/forearm tilt:
$$\text{Sway}_{\text{dynamic}}(t) = A_y(t) - 9.80665 \cdot \cos\left(\theta \cdot \frac{\pi}{180}\right) \cdot \sin\left(\phi \cdot \frac{\pi}{180}\right)$$

* **Resting at any 3D angle**: $\text{Sway}_{\text{dynamic}} \approx 0.0\,\text{m/s}^2$.
* **Lateral Swipe / Wave**: Creates an immediate non-zero excursion ($\pm 2 \text{ to } \pm 15\,\text{m/s}^2$).
* **Instrument Representation**: Drives the **`SWAY` needle** on the Inclinometer Gauges (`#dial-sway-needle`) and Channel 3 of the 6-Channel Waveform Canvas.

### 4.2 RMS Postural Sway (Root Mean Square)
Used to evaluate involuntary motor tremors, ataxia, or ambient wheelchair motor noise over a sliding time window of $N$ samples (e.g., $N=100$ samples = 2.0 seconds at 50 Hz):

$$\text{RMS}_{\text{Sway}} = \sqrt{\frac{1}{N}\sum_{i=1}^{N}\left(A_{y,i} - \mu_y\right)^2}$$

Where $\mu_y$ is the mean lateral acceleration across the window:
$$\mu_y = \frac{1}{N}\sum_{i=1}^{N} A_{y,i}$$

* **Low RMS ($< 0.5\,\text{m/s}^2$)**: High motor stability, quiet baseline.
* **High RMS ($> 2.5\,\text{m/s}^2$)**: High spasticity or ambient wheelchair vibration; triggers the **Tier 2B Triplet Metric Spasm Rejection Filter**.

### 4.3 2D Sway Path Length (Center of Acceleration Trajectory)
Measures the total cumulative distance traversed by the hand in the horizontal plane ($A_x$ vs $A_y$):

$$\text{Path Length} = \sum_{i=1}^{N-1} \sqrt{(A_{x,i+1} - A_{x,i})^2 + (A_{y,i+1} - A_{y,i})^2}$$

This metric provides clinicians with a quantifiable index of motor control fatigue over successive communication trials.

---

## 5. Magnitude, Dynamic Shockwave & 3D Gravity-Compensated Heave

### 5.1 Total Acceleration Magnitude ($|M|$)
The scalar Euclidean norm across all 3 orthogonal axes:

$$|M| = \sqrt{A_x^2 + A_y^2 + A_z^2}$$

* At rest in **any** arbitrary 3D orientation, $|M| = 9.80665\,\text{m/s}^2 \ (1.0\text{G})$.

### 5.2 Dynamic Shockwave Magnitude ($||M| - 9.8|$)
By taking the absolute deviation from the $1.0\text{G}$ gravitational bias, we capture both compressive impact spikes and unloading/free-fall shockwaves symmetrically:

$$\text{Shock}_{|M|} = \left| |M| - 9.80\,\text{m/s}^2 \right|$$

* **Resting Baseline**: $\text{Shock}_{|M|} \approx 0.0\,\text{m/s}^2$.
* **Desk Tap / Surface Knock**: Sharp positive spike ($+10 \text{ to } +25\,\text{m/s}^2$).
* **6-Channel Waveform / Scrubber Trace**: Rendered as Channel 4, the 🍑 **Peach curve** (`#FAB387`).

### 5.3 3D Gravity-Compensated Vertical Heave ($\text{Heave}_{\text{dynamic}}$)
When the forearm is pitched ($\theta$) or rolled ($\phi$), the normal $Z$-axis component of static gravity decreases as $g_z = g \cdot \cos(\theta) \cdot \cos(\phi)$. Subtracting a fixed $9.8\,\text{m/s}^2$ would falsely report up to $-9.8\,\text{m/s}^2$ of downward heave when simply tilting the wrist at rest. Therefore, AllyAAC computes true 3D gravity-compensated dynamic heave:

$$\text{Heave}_{\text{dynamic}}(t) = A_z(t) - 9.80665 \cdot \cos\left(\theta \cdot \frac{\pi}{180}\right) \cdot \cos\left(\phi \cdot \frac{\pi}{180}\right)$$

* **Resting at any 3D tilt angle**: $\text{Heave}_{\text{dynamic}} \approx 0.0\,\text{m/s}^2$.
* **6-Channel Waveform / Scrubber Trace**: Rendered as Channel 6, the 🩵 **Cyan curve** (`#89DCEB`).

---

## 6. 3D Reach & Travel Distance Mathematics

Using the kinematic arm-chain model pinned at the shoulder joint, the user's linear reach distance from their calibrated **Rest Position** is decomposed into 6 directional Cartesian channels (3 antagonistic pairs) plus a composite 3D Euclidean spatial vector:

```
                          Overhead (Pitch = +85°)
                                 \  O
                                  \ |  ▲
                             Arm   \|  │
                            Length  │  │ Upward Reach (Δz_up)
                             (L)    │  │
                                    ▼  ▼
                     Rest Position (Pitch_rest, Roll_rest)
                                    ├──────────────► Forward Extension (Δx_fwd)
                                    ├──────────────► Backward Retraction (Δx_back)
                                    ├──────────────► Left Lateral Reach (Δy_left)
                                    ├──────────────► Right Lateral Reach (Δy_right)
                                    ▼
                          Downward Drop (Δz_down)
```

### 6.1 Automatic Camera Arm-Length Calibration ($L$)
The scalar radius $L$ (shoulder-to-wrist distance) is measured using the live webcam feed via MediaPipe keypoints:
* **Optical Scale Reference**: The physical diameter of the Movesense sensor pod ($36.6\text{ mm}$) provides an absolute fiducial:
  $$S = \frac{36.6\text{ mm}}{\text{Pod Diameter in Pixels}}$$
* **Arm Length**:
  $$L = \text{PixelDistance}(\text{Shoulder} \to \text{Wrist}) \times S \quad (\text{Default fallback: } 65\text{ cm} \approx 25.6\text{ in})$$

### 6.2 Upward Elevation Reach ($\Delta z_{\text{up}}$)
The vertical height gained above the calibrated rest position:
$$\Delta z = L \times \left(\sin(\text{Pitch}) - \sin(\text{Pitch}_{\text{rest}})\right), \quad \Delta z_{\text{up}} = \max(0, \Delta z)$$

* At rest position ($\text{Pitch} = \text{Pitch}_{\text{rest}}$): $\Delta z_{\text{up}} = 0.0\text{ cm}$.
* At full vertical stretch ($\text{Pitch} \approx +85^\circ$): $\Delta z_{\text{up}} \approx 0.95 \text{ to } 1.25\text{ m}$ ($37 \text{ to } 49\text{ in}$).

### 6.3 Downward Drop Reach ($\Delta z_{\text{down}}$)
The vertical displacement dropping below the calibrated rest position (e.g. reaching toward floor or footrest):
$$\Delta z_{\text{down}} = \begin{cases} |\Delta z|, & \text{if } \Delta z < 0 \\ 0, & \text{otherwise} \end{cases}$$

### 6.4 Forward Extension ($\Delta x_{\text{fwd}}$) & Backward Retraction ($\Delta x_{\text{back}}$)
When calibrated on a flat table or wheelchair tray ($\theta_{\text{rest}} = 0^\circ$), a purely cosine difference $\cos(\theta) - \cos(0^\circ)$ would always be $\le 0$. To ensure responsive bidirectional reach across **all** baseline angles, AllyAAC combines horizontal projection, longitudinal dynamic surge ($A_{x,\text{dyn}} = A_x - g\sin\theta$), and signed pitch arc:

$$\Delta x = L \times (\cos\theta - \cos\theta_{\text{rest}}) + A_{x,\text{dyn}} \times 1.85 + L \times \sin(\theta - \theta_{\text{rest}}) \times 0.35$$
$$\Delta x_{\text{fwd}} = \max(0, \Delta x), \qquad \Delta x_{\text{back}} = \max(0, -\Delta x)$$

* Quantifies intentional forward reaches toward touchscreens/switches ($\Delta x_{\text{fwd}}$) and posterior retractions toward the torso ($\Delta x_{\text{back}}$).

### 6.6 Left Lateral Reach ($\Delta y_{\text{left}}$)
The lateral sweeping motion directed toward the communicator's left side:
$$\Delta y = L \times \left(\sin(\text{Roll}) - \sin(\text{Roll}_{\text{rest}})\right), \quad \Delta y_{\text{left}} = \begin{cases} |\Delta y|, & \text{if } \Delta y < 0 \\ 0, & \text{otherwise} \end{cases}$$

### 6.7 Right Lateral Reach ($\Delta y_{\text{right}}$)
The lateral sweeping motion directed toward the communicator's right side:
$$\Delta y_{\text{right}} = \begin{cases} \Delta y, & \text{if } \Delta y > 0 \\ 0, & \text{otherwise} \end{cases}$$

### 6.8 3D Euclidean Spatial Vector Reach ($\Delta D_{\text{3D}}$)
The net straight-line 3D Euclidean displacement from the calibrated rest position across all three dimensions:
$$\Delta D_{\text{3D}} = \sqrt{\Delta z^2 + \Delta x^2 + \Delta y^2}$$

This provides a single continuous metric quantifying the communicator's voluntary motor workspace volume.

---

## 7. Sensor Collection & Processing Pipeline

```
[Movesense 2.0 BLE Sensor] (50Hz Accel/Gyro)
                │
                ▼ Web Bluetooth API (GATT Characteristic Notification)
[decodeMovesensePacket(dataView)]
  ├── Ax, Ay, Az (m/s²)
  ├── Gx, Gy, Gz (deg/s)
  └── Timestamp (BLE clock)
                │
                ▼ processSensorTelemetry()
  ├── Compute Magnitude: |M| = √(Ax² + Ay² + Az²)
  ├── Compute Pitch: atan2(Ax, √(Ay² + Az²))
  ├── Compute Roll: atan2(Ay, Az)
  ├── Compute Dynamic Sway: Ay - 9.8·sin(Roll)
  └── Low-Pass / High-Pass Smoothing Filters
                │
        ┌───────┴────────────────────────┐
        ▼                                ▼
[Visual Instruments]             [Detection Engines]
 • Inclinometer Needles           • Tier 0: Kinematic State Machine
   (Pitch, Roll, Sway, Mag)       • Tier 1: DTW + LCSS Matcher
 • Live Oscilloscope              • Tier 2A: Masked MAE
 • Frame HUD Badges               • Tier 2B: Triplet Metric Spasm Filter
                                  • Tier 3: Multimodal Vision+IMU Attention
```

---

## 8. Interactive Tools Associated with IMU Data

AllyAAC provides four integrated tools for working with these telemetry streams:

### 1. 6-Channel Kinematic Waveform Canvas & Scrubber (`#studio-waveform-canvas`)
* **Role**: Primary real-time kinematic signal monitor and interactive buffer timeline scrubber.
* **Temporal Window**: Rolling 120-sample buffer (~2.4 seconds at 50 Hz).
* **Calibrated Time Ticks**: Shows relative time divisions: `-2.4s`, `-1.8s`, `-1.2s`, `-0.6s`, `0.0s (Now)`.
* **The 6 Kinematic Channels**:
  1. **Pitch ($\theta$)** (Blue `#89B4FA`): Forearm elevation angle ($-90^\circ$ to $+90^\circ$).
  2. **Roll ($\phi$)** (Purple `#CBA6F7`): Forearm supination/pronation rotation ($-90^\circ$ to $+90^\circ$).
  3. **Sway** (Green `#A6E3A1`): Gravity-compensated dynamic lateral acceleration ($\pm 10\,\text{m/s}^2$).
  4. **Magnitude ($|M|$)** (Peach `#FAB387`): Dynamic surge shockwave ($|M| - 9.80\,\text{m/s}^2$, $0$ to $15\,\text{m/s}^2$).
  5. **Surge ($A_x$)** (Pink `#F38BA8`): Anterior-posterior longitudinal linear force ($\pm 20\,\text{m/s}^2$).
  6. **Heave ($A_z$)** (Cyan `#89DCEB`): Vertical linear dynamic acceleration ($A_z - 9.80\,\text{m/s}^2$, $\pm 20\,\text{m/s}^2$).
* **Dual-Axis Scaling Architecture**:
  * **Left Y-Axis (Angles)**: Calibrated from $-90^\circ$ to $+90^\circ$ with ticks at $+90^\circ, +45^\circ, 0^\circ, -45^\circ, -90^\circ$.
  * **Right Y-Axis (Acceleration)**: Calibrated from $-20\,\text{m/s}^2$ to $+20\,\text{m/s}^2$ with ticks at $+20, +10, 0, -10, -20\,\text{m/s}^2$.
* **Interactive Waveform Scrubber**:
  * **Dual Operating Modes**:
    * `🔴 Live Stream`: Real-time streaming 50 Hz rolling telemetry window.
    * `🎞️ Scrubber`: Frozen interactive timeline allowing frame-by-frame inspection.
  * **Interactive Scrubber Controls**:
    * Play / Pause toggle with animated transport loop.
    * Frame-stepping buttons: `⏮ -1F` (-20 ms) and `⏭ +1F` (+20 ms).
    * Configurable playback speeds (`1.0x`, `0.5x`, `0.25x`).
    * Loop playback toggle (`🔁 Loop`).
    * Scrub slider (`#studio-wf-slider`) + click-and-drag scrubbing directly on the waveform canvas.
    * Glowing cursor line (`#studio-wf-playhead`) with accent marker dots highlighting exact instantaneous channel states.
    * Interactive channel filter pills (`Pitch`, `Roll`, `Sway`, `|M| Shock`, `Ax`, `Az`) with live telemetry badges and toggleable visibility.

### 2. Inclinometer Gauges (`#inclinometer-dial`)
* **Role**: Static orientation and dynamic sway monitor.
* **Gauges**:
  1. **Pitch Needle (`#dial-pitch-needle`, Blue)**: $\pm 90^\circ$ forearm elevation.
  2. **Roll Needle (`#dial-roll-needle`, Purple)**: $\pm 90^\circ$ forearm rotation.
  3. **Sway Needle (`#dial-sway-needle`, Green)**: $\pm 90^\circ$ dynamic lateral deviation ($\pm 10\,\text{m/s}^2$).
  4. **Mag Readout (`#studio-mag-val`)**: Instantaneous scalar acceleration.

### 3. Synchronized Timeline Scrubber & Annotator (`#scrubber-timeline-canvas`)
* **Role**: Frame-accurate post-recording segmentation and labeling.
* **Features**:
  * Displays the entire captured session from $t=0.00\text{s}$ to $t_{\text{end}}$ with unified 6-channel traces.
  * Calibrated absolute time ticks ($0.00\text{s}, 0.50\text{s}, 1.00\text{s}, \dots$).
  * Glowing synchronized playhead (`#scrubber-sync-playhead`) with floating time badge (`#scrubber-playhead-stamp`).
  * In/Out boundary markers (`Set [ In`, `Set Out ]`) for cropping gesture templates.
  * Instantaneous telemetry readout badge displaying $A_x, A_y, A_z, |M|$, Pitch, Roll, and Sway for the exact frame under the playhead.

### 4. Gesture Studio Help Workbench (`#studio-pane-help`)
* Accessible directly inside the web and desktop apps via the **`❓ Help`** sub-tab in Gesture Studio.
* Provides clinicians, caregivers, and developers with on-device reference cards for physical axes, posturography math, and trigger tuning.

### 5. Settings Menu 6-Point Diagnostic Checklist & Real-Time Telemetry Inspector (`#tab-pane-diagnostics`)
* **Access Paths**:
  * **Settings Modal (`⚙️`) → `🩺 Diagnostics` Tab** (`openSettingsModal('diagnostics')`)
  * **Settings Modal (`⚙️`) → `Hardware` Tab** → **`Run Diagnostic Checklist & Inspector 🩺`**
  * **Global Keyboard Shortcut**: **`Alt + D`**
  * **Gesture Studio Quick-Launch**: **`🩺 Inspect`** button in the 6-Channel Waveform Canvas header and inside the **Diagnostics** sub-view.
* **Live Telemetry Monitor Strip**:
  * Displays real-time stream source (`Movesense-BLE`, `WebView2-Host`, `50Hz-Sim`, `50Hz-Diagnostic-Sweep`), measured sample rate ($\text{Hz}$), and waveform mode (`LIVE` vs `SCRUB`).
  * Live 6-channel readout cards (`1. PITCH`, `2. ROLL`, `3. SWAY`, `4. |M| SHOCK`, `5. SURGE Ax`, `6. HEAVE Az`) and 6-direction 3D Reach strip (`⬆ UP`, `⬇ DOWN`, `➡ FWD`, `⬅ BACK`, `◀ LEFT`, `▶ RIGHT`).
* **The 6 Automated Diagnostic Checks**:
  1. **Check #1 — 50 Hz Real-Time Telemetry Clock & Source Pipe**: Verifies active streaming frequency ($\ge 18\text{ Hz}$), frame delta ($\Delta t\text{ ms}$), and 120-sample ring buffer health.
  2. **Check #2 — Raw Accelerometer Vector & Unit Scale ($G$ vs $\text{m/s}^2$)**: Validates Movesense GSP `0x02` multi-sample packet unpacking (`structured-array` at byte offset 7 vs `flat-float` at byte offset 6), automatic $G \rightarrow \text{m/s}^2$ scale normalization, raw $|M|$ bounds, and `NaN` frame guard counters.
  3. **Check #3 — Inclinometry Angles (`Pitch` $\theta$ & `Roll` $\phi$ Trigonometry)**: Verifies finite 3D $\text{atan2}$ angles within $[-90^\circ, +90^\circ]$ and inspects tare baseline offsets ($\theta_0, \phi_0$).
  4. **Check #4 — Gravity-Compensated Dynamic Channels (`Sway`, `|M| Shock`, `Surge Ax`, `Heave Az`)**: Confirms 3D static gravity projection removal ($g_y = g\cos\theta\sin\phi$, $g_z = g\cos\theta\cos\phi$) and symmetric shockwave calculation.
  5. **Check #5 — 6-Direction 3D Spatial Reach Solver (`Up`, `Down`, `Fwd`, `Back`, `Left`, `Right`)**: Validates finite excursion calculations (cm) and peak 3D Euclidean workspace vector tracking.
  6. **Check #6 — 6-Channel Waveform Canvas, Scrubber Mode & Live Badge Sync**: Detects if the waveform canvas is frozen in `'scrub'` mode or if any of the 6 channel pills are muted.
* **Interactive Remediation & Verification Controls**:
  * **`▶ Run Inspection`**: Executes all 6 checks on demand and updates pass/warn/fail badges.
  * **`⚡ Test 6-Ch Sweep`**: Injects a 3.0-second (150-tick) 50 Hz multi-frequency orthogonal wave exercising all 6 waveform channels and all 6 spatial reach directions in real time.
  * **`🔧 Auto-Fix & Tare`**: Automatically restores `'live'` waveform mode if stuck in `'scrub'`, enables all 6 channel filters, clears `NaN` drop counters, tares the 3D reach baseline, and launches a verification sweep if idle.
  * **`📋 Copy Report`**: Copies a structured Markdown diagnostic report to the system clipboard.

---

## 9. Summary Formula Reference Card

$$\begin{aligned}
\text{Magnitude } |M| &= \sqrt{A_x^2 + A_y^2 + A_z^2} \\[6pt]
\text{Dynamic Shock } |M|_{\text{shock}} &= \left||M| - 9.80\,\text{m/s}^2\right| \\[6pt]
\text{Pitch } \theta &= \text{atan2}\left(A_x, \sqrt{A_y^2 + A_z^2}\right) \times \frac{180^\circ}{\pi} \\[6pt]
\text{Roll } \phi &= \text{atan2}(A_y, A_z) \times \frac{180^\circ}{\pi} \\[6pt]
\text{Dynamic Sway} &= A_y - 9.80665 \cdot \cos\left(\theta \cdot \frac{\pi}{180}\right) \cdot \sin\left(\phi \cdot \frac{\pi}{180}\right) \\[6pt]
\text{Dynamic Heave} &= A_z - 9.80665 \cdot \cos\left(\theta \cdot \frac{\pi}{180}\right) \cdot \cos\left(\phi \cdot \frac{\pi}{180}\right) \\[6pt]
\text{RMS Sway} &= \sqrt{\frac{1}{N}\sum_{i=1}^N (A_{y,i} - \mu_y)^2}
\end{aligned}$$
