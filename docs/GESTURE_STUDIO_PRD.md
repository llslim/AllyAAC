# Product Requirements Document (PRD)
# AllyAACUI & Gesture Studio: Unified Gesture Algorithm Engine & Clinical Training Suite

* **Document Version**: 1.0.0  
* **Date**: October 3, 2026  
* **Status**: Approved for Implementation / Architecture Specification  
* **Primary Systems**: AllyAACUI (Canonical Multi-Modal Multiplexing AAC), AllyAAC (`D:\projects\windows\AllyAAC`), and Gesture Studio  
* **Target Platforms**: Web/PWA, Windows Desktop (WPF/Edge WebView2), Android Kiosk  
* **Underlying Research**: Imran & Syed Empirical Evaluation (`model_data_result_future-imran_and_syed-en-US.vtt`)

---

## 1. Executive Summary & Vision

Augmentative and Alternative Communication (AAC) systems must empower communicators with diverse motor profiles—from individuals with total absence of speech and severe spasticity (e.g., dyskinetic cerebral palsy) to those with progressive neurodegenerative conditions (ALS, muscular dystrophy) or brainstem stroke.

**AllyAACUI** (and its desktop counterpart **AllyAAC**) serves as the canonical multi-modal multiplexing AAC application. While traditional AAC systems rely on single-switch scanning or gaze tracking alone, AllyAACUI multiplexes continuous physiological streams:
1. **Wearable Inertial Sensing (Movesense 6-DoF IMU)** at 50 Hz.
2. **Optical Computer Vision (Google MediaPipe 3D Hand Landmarks)** at 30 fps.
3. **Adaptive AAC Action Dispatching** to low-latency speech synthesis, message composition, and home automation.

To make this ecosystem viable in real-world clinical and educational environments, this PRD specifies the **Gesture Studio**—a comprehensive authoring, recording, pseudo-labeling, training, and benchmarking suite. Gesture Studio provides speech-language pathologists (SLPs), occupational therapists (OTs), researchers, communicators, and caregivers with a unified graphical workbench to create, calibrate, evaluate, and package personalized gestures across **six distinct algorithmic tiers**.

```
+----------------------------------------------------------------------------------------------------+
|                                    GESTURE STUDIO WORKBENCH                                        |
|                                                                                                    |
|  [Clinician / SLP / OT]         [Communicator / User]          [Support / Caregiver]   [Researcher]|
|             |                             |                              |                  |      |
|             +-----------------------------+------------------------------+------------------+      |
|                                           |                                                        |
|                                           v                                                        |
|  +-----------------------------------------------------------------------------------------------+ |
|  | SENSOR HUB & SYNCHRONIZED STREAM RECORDER                                                     | |
|  | - Movesense 6-DoF BLE (50 Hz Accel/Gyro)    - Camera / MediaPipe 3D Hand Keypoints (30 fps)   | |
|  | - Real-time Data Validation, Drop-frame Interpolation, Inclinometry (Pitch/Roll) Preview       | |
|  +-----------------------------------------------------------------------------------------------+ |
|                                           |                                                        |
|                                           v                                                        |
|  +-----------------------------------------------------------------------------------------------+ |
|  | AUTOMATED PSEUDO-LABELING & ANNOTATION SCRUBBER                                               | |
|  | - MediaPipe Mean Keypoint Displacement Velocity Thresholding (Active Gesture vs Rest Idle)     | |
|  | - Interactive Waveform Scrubber: Trim, Split, Re-label, Discard Noise Spasms                    | |
|  +-----------------------------------------------------------------------------------------------+ |
|                                           |                                                        |
|                                           v                                                        |
|  +-----------------------------------------------------------------------------------------------+ |
|  | UNIFIED MULTI-TIER ALGORITHM ENGINE                                                           | |
|  |                                                                                               | |
|  | [Tier 0: SW-RKPIC]     [Tier 1: LCSS]          [Tier 2A: Masked MAE]  [Tier 2B: Triplet Metric] | |
|  | Kinematic Rules        K-Means Clustering      1D-CNN Transformer     Contrastive Hypersphere | |
|  | <0.1ms, 0 params       ~130 params, <15ms      >500k params, Micro    >500k params, Spasm-Rej | |
|  |                                                                                               | |
|  | [Tier 2C: InfoNCE]     [Tier 3: Multimodal Fusion]                                            | |
|  | Autoregressive         Vision + IMU Cross-Attention                                           | |
|  | Long Trajectories      >890k params, Max Accuracy                                             | |
|  +-----------------------------------------------------------------------------------------------+ |
|                                           |                                                        |
|                                           v                                                        |
|  +-----------------------------------------------------------------------------------------------+ |
|  | DIAGNOSTIC BENCHMARK & EXPORT SUITE                                                           | |
|  | - F1 Score, Confusion Matrix, Latency Meter (<200ms verification), False-Positive Stress Test | |
|  | - Output: Open Universal Gesture Package (`.gesturepkg`) for AllyAACUI, ScoobyUI, HerbieUI      | |
|  +-----------------------------------------------------------------------------------------------+ |
+----------------------------------------------------------------------------------------------------+
```

---

## 2. Theoretical Formulation of All Gesture Algorithms

Gesture Studio and AllyAACUI incorporate six mathematical paradigms, each solving distinct clinical and technical constraints:

```
                                      GESTURE ALGORITHM TAXONOMY
                                                  |
           +--------------------------------------+--------------------------------------+
           |                                                                             |
           v                                                                             v
   DETERMINISTIC / SYMBOLIC                                                   DEEP LEARNING TRANSFORMERS
           |                                                                             |
     +-----+-----+                                                    +------------------+------------------+
     |           |                                                    |                  |                  |
     v           v                                                    v                  v                  v
 [Tier 0]    [Tier 1]                                             [Tier 2A]          [Tier 2B]          [Tier 2C]
 SW-RKPIC    LCSS Matcher                                         Masked MAE         Triplet Metric     InfoNCE CPC
 Kinematic   K-Means (20)                                         Micro-motion       Spasm-Rejection    Continuous
 Rules       Symbolic DP                                          Reconstruct        Contrastive        Predictive
                                                                                         |
                                                                                         v
                                                                                     [Tier 3]
                                                                                Multimodal Fusion
                                                                                Cross-Attention (IMU+Vision)
```

### 2.1 Tier 0: SW-RKPIC (Sliding-Window Rule-Based Kinematic Peak & Inclinometry Classifier)
* **Design Philosophy**: Zero-training, deterministic, white-box physical angle & shock state machine.
* **Sensors**: 3-axis Accelerometer ($a_x, a_y, a_z$) at $26\text{--}50\text{ Hz}$.
* **Sliding Buffer**: Circular FIFO buffer $\mathcal{B}$ with $N = 24\text{ samples}$ ($\sim 920\text{ ms}$).
* **Kinematic Metrics**:
  - Total Euclidean Magnitude: $M(t) = \sqrt{a_x(t)^2 + a_y(t)^2 + a_z(t)^2}$ (Nominal 1G Earth gravity $\approx 9.81\text{ m/s}^2$).
  - Pitch (Forearm elevation): $\theta(t) = \text{atan2}\left(a_x(t), \sqrt{a_y(t)^2 + a_z(t)^2}\right) \times \frac{180}{\pi}$.
  - Roll (Pronation/Supination): $\phi(t) = \text{atan2}(a_y(t), a_z(t)) \times \frac{180}{\pi}$.
* **Dual-Regime Execution**:
  1. *Regime A (High-Pass Dynamic Impulse)*:
     - Peak Shock: If $M(t) > 21.0\text{ m/s}^2$ and $M(t - 2\Delta t) < 14.0\text{ m/s}^2 \implies \mathbf{Knock}$.
     - Zero-Crossing Oscillations: If sign reversals on $a_y \ge 3$ within window $\implies \mathbf{Shake}$.
     - Lateral Surges: If $a_y < -13.8\text{ m/s}^2 \implies \mathbf{Swipe\ Left}$; if $a_y > +13.8\text{ m/s}^2 \implies \mathbf{Swipe\ Right}$.
  2. *Regime B (Low-Pass Quiescent Inclinometry)*:
     - Gated strictly by gravitational equilibrium: $|M(t) - 9.81| < 1.4\text{ m/s}^2$ sustained across $K \ge 7$ consecutive samples.
     - $\theta > +38^\circ \implies \mathbf{Point}$; $\theta < -38^\circ \implies \mathbf{Thumbs\ Down}$.
     - $\phi \in [65^\circ, 125^\circ] \implies \mathbf{Thumbs\ Up}$.
     - $|\theta| < 16^\circ \text{ and } |\phi| < 22^\circ \implies \mathbf{Open\ Palm}$.
* **Compute Footprint**: $< 0.1\text{ ms}$, 0 trainable parameters, 100% auditable by clinicians.

---

### 2.2 Tier 1: Baseline Sliding-Window K-Means + LCSS
* **Design Philosophy**: Rapid on-device user-defined gesture template recorder without neural network training.
* **Sensors**: 6-axis IMU (3-axis Accel + 3-axis Gyro) at 50 Hz.
* **Complexity**: $\sim 130$ parameters (20 6D centroids + template sequences).
* **Algorithmic Pipeline**:
  1. *Clustering*: Runs $K$-Means clustering ($K = 20$) over ambient calibration recordings to define symbolic codebook $\mathcal{C} = \{c_1, \dots, c_{20}\} \subset \mathbb{R}^6$.
  2. *Symbolic Tokenization*: Continuous 6D sensor vectors are quantized to discrete integer sequences:
     $$s_t = \arg\min_{k \in \{1,\dots,20\}} \|x_t - c_k\|_2$$
  3. *LCSS Dynamic Programming*: Given candidate sliding window $\mathcal{S} = [s_1, \dots, s_L]$ and stored template $\mathcal{T} = [t_1, \dots, t_M]$:
     $$\text{LCSS}(i, j) = \begin{cases} 
     0 & \text{if } i=0 \text{ or } j=0 \\
     1 + \text{LCSS}(i-1, j-1) & \text{if } |\text{index}(s_i) - \text{index}(t_j)| \le \delta \\
     \max(\text{LCSS}(i-1, j), \text{LCSS}(i, j-1)) & \text{otherwise}
     \end{cases}$$
  4. *Normalized Similarity*:
     $$\text{Sim}(\mathcal{S}, \mathcal{T}) = \frac{\text{LCSS}(\mathcal{S}, \mathcal{T})}{\min(|\mathcal{S}|, |\mathcal{T}|)}$$
* **Performance Profile**: $5\text{--}15\text{ ms}$ latency, allows adding custom gestures in seconds. However, accuracy tops out at $\sim 60\%$ F1 and degrades if vocabulary exceeds 3 classes.

---

### 2.3 Tier 2A: Masked Autoencoding (MAE) Reconstruction Transformer
* **Design Philosophy**: Deep representation learning for communicators with restricted movement amplitudes, subtle tremors, or fine-grained wrist/finger micro-motions.
* **Complexity**: $> 500,000$ parameters.
* **Architecture**:
  - Input: $X \in \mathbb{R}^{50 \times 6}$ ($1.0\text{ s}$ window at $50\text{ Hz}$).
  - 1D Convolutional Feature Extractor: Kernel size 5, stride 1, channel projection to $d_{\text{model}} = 128$.
  - Transformer Encoders: 4 layers, 4 attention heads, feedforward dimension 512, sinusoidal positional encodings.
* **Self-Supervised Pre-Training**:
  - Mask out random contiguous temporal patches ($30\%\text{--}50\%$ of time steps).
  - 1D Transpose Convolution reconstruction head minimizes Mean Squared Error (MSE):
    $$\mathcal{L}_{\text{MSE}} = \frac{1}{|M|} \sum_{t \in M} \|x_t - \hat{x}_t\|_2^2$$
* **Supervised Fine-Tuning**:
  - Replace transpose conv head with Global Average Pooling (GAP) and an $N$-class Linear projection with Cross-Entropy Loss.

---

### 2.4 Tier 2B: Metric Contrastive Triplet Transformer (Involuntary Spasm Rejection)
* **Design Philosophy**: High-precision discrimination for communicators with cerebral palsy, dystonia, or severe tremors who suffer from frequent false triggers.
* **Complexity**: $> 500,000$ parameters.
* **Metric Hypersphere Projection**:
  - Transformer encoder maps input window $X$ to normalized embedding $z = \frac{f_\theta(X)}{\|f_\theta(X)\|_2} \in S^{127}$.
* **Triplet Margin Objective**:
  $$\mathcal{L}_{\text{triplet}} = \max\left(0, \mathcal{D}(f_\theta(a), f_\theta(p)) - \mathcal{D}(f_\theta(a), f_\theta(n)) + \alpha\right)$$
  - **Anchor ($a$)**: Labeled intentional gesture.
  - **Positive ($p$)**: Same gesture with temporal jitter or slight sensor noise.
  - **Negative ($n$)**: Baseline ambient limb repositioning, spastic tremor, or visually similar intentional gesture (e.g., *"Point"* vs *"Look at that"*).
  - Distance metric: Cosine distance $\mathcal{D}(u, v) = 1 - (u \cdot v)$, margin $\alpha = 0.3$.
* **Inference**: Computes cosine similarity against class centroid prototypes $\bar{z}_g$. If $\max_g \cos(z, \bar{z}_g) < \tau_{\text{threshold}}$, the window is discarded as non-communicative ambient noise.

---

### 2.5 Tier 2C: Autoregressive Future-Value Predictive Transformer (InfoNCE)
* **Design Philosophy**: Optimized for extended, multi-phase, fluid gesture trajectories (e.g., multi-stroke directional pointing, continuous waving).
* **Complexity**: $> 560,000$ parameters.
* **Contrastive Predictive Coding (CPC)**:
  - Temporal encoder compresses past sensor steps into context representation $c_t = g_{\text{AR}}(z_{\le t})$.
  - Network predicts future latent representations $z_{t+k}$ across steps $k \in \{1, 2, 4, 8\}$ using InfoNCE loss:
    $$\mathcal{L}_{k} = -\sum_t \log \frac{\exp(z_{t+k}^T W_k c_t / \tau)}{\exp(z_{t+k}^T W_k c_t / \tau) + \sum_{j \in \mathcal{N}_t} \exp(z_j^T W_k c_t / \tau)}$$
* **Benefit**: Encodes temporal trajectory momentum and long-range continuity, preventing premature gesture triggering mid-motion.

---

### 2.6 Tier 3: Multimodal Cross-Attention Transformer (Vision + IMU Fusion)
* **Design Philosophy**: Gold-standard accuracy for stationary kiosk and wheelchair mounts equipped with both an active camera and a wrist-worn IMU.
* **Complexity**: $\sim 890,000$ parameters.
* **Dual Stream Fusion**:
  - Stream A: 6-axis Movesense IMU ($50\text{ Hz}$, $50\text{ steps}$).
  - Stream B: 21 3D MediaPipe Hand Landmarks ($30\text{ fps}$, $30\text{ steps}$, 63 coordinates).
* **Cross-Attention Mechanism**:
  $$\text{CrossAttn}(Q_{\text{IMU}}, K_{\text{VIS}}, V_{\text{VIS}}) = \text{softmax}\left(\frac{Q_{\text{IMU}} K_{\text{VIS}}^T}{\sqrt{d_k}}\right) V_{\text{VIS}}$$
  Cross-validates internal physical wrist kinetic acceleration against external optical finger/hand posture.
* **Automated Pseudo-Labeling Engine**:
  - Eliminates manual video annotation by calculating instantaneous mean landmark displacement:
    $$\Delta d(t) = \frac{1}{21} \sum_{i=1}^{21} \|\mathbf{p}_i(t) - \mathbf{p}_i(t-1)\|_2$$
  - When $\Delta d(t) > \tau_{\text{motion}}$, automatically boundaries the clip as an active candidate gesture; when $\Delta d(t) \le \tau_{\text{motion}}$, marks as resting baseline.

---

## 3. Comprehensive Algorithm Benchmark & Selection Matrix

| Evaluation Dimension | **Tier 0: SW-RKPIC** | **Tier 1: LCSS Matcher** | **Tier 2A: Masked MAE** | **Tier 2B: Triplet Metric** | **Tier 2C: InfoNCE CPC** | **Tier 3: Multimodal Cross-Attn** |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Model Nature** | Deterministic Rules | Symbolic DP Matcher | Deep Self-Attention | Deep Metric Distance | Autoregressive Attention | Dual-Stream Attention |
| **Trainable Params** | 0 | $\sim 130$ | $> 520,000$ | $> 520,000$ | $> 560,000$ | $\sim 890,000$ |
| **Edge Execution** | Pure JS CPU math | Typed Arrays DP | ONNX WebGPU / WASM | ONNX WebGPU / WASM | ONNX WebGPU / WASM | ONNX WebGPU + MediaPipe |
| **Inference Latency**| $< 0.1\text{ ms}$ | $5\text{--}15\text{ ms}$ | $15\text{--}40\text{ ms}$ | $15\text{--}40\text{ ms}$ | $20\text{--}45\text{ ms}$ | $35\text{--}60\text{ ms}$ |
| **Input Signals** | 3-DoF Accel (26Hz) | 6-DoF IMU (50Hz) | 6-DoF IMU (50Hz) | 6-DoF IMU (50Hz) | 6-DoF IMU (50Hz) | 6-DoF IMU + 21 Hand Landmarks |
| **Training Time** | None (Zero-shot) | $< 5\text{ seconds}$ | ~2h GPU Pre-train | ~2h GPU Pre-train | ~2h GPU Pre-train | ~3h GPU Multimodal Pre-train |
| **Mean F1 Score** | $0.95$ (on 7 presets) | $0.589$ (Pilot benchmark)| $\ge 0.85$ ($0.871$ mean)| $\ge 0.85$ ($0.871$ mean)| $\ge 0.85$ ($0.871$ mean)| $> 0.90$ (Cross-validated) |
| **Spasm Immunity** | Static 1G Equilibrium | ⚠️ Low (False matches) | Moderate | ⭐ Superior (Negative margin)| Moderate | ⭐ Superior (Dual verification)|
| **Best Clinical Fit**| System Navigation | Rapid Caregiver Setup | Micro-tremors / Small ROM| Spasticity, CP, Dystonia | Fluid / Multi-stroke Gestures| Mounted Kiosks / Wheelchairs |

---

## 4. Gesture Studio Functional Specification

Gesture Studio is structured into four specialized personas:

```
+----------------------------------------------------------------------------------------------------+
|                                    GESTURE STUDIO USER WORKSPACES                                   |
|                                                                                                    |
|  [1. CLINICIAN / THERAPIST]    [2. COMMUNICATOR / USER]   [3. CAREGIVER / SUPPORT] [4. RESEARCHER] |
|  - Joint Range of Motion (ROM) - Big Accessible Target UI  - Simple Green/Red Status- Raw Sensor Loss  |
|  - Angle & Shock Thresholds    - Audio Prompts & Visuals  - One-Tap Calibration    - Confusion Matrix  |
|  - Spasm Rejection Tolerance   - Fatigue Break Reminders  - Battery & BLE Check    - ONNX Quantization |
+----------------------------------------------------------------------------------------------------+
```

### 4.1 Sensor Hub & Synchronized Telemetry Recorder
* **BLE Device Manager**: Direct Web Bluetooth pairing with Movesense Medical/Active sensors, displaying real-time battery status, packet loss rate, and 50 Hz streaming health.
* **Camera / MediaPipe Preview**: Canvas overlay visualizing the 21 3D skeleton keypoints, tracking confidence, and FPS counter.
* **Synchronized Session Recorder**: Streams incoming 6-DoF IMU vectors and 21 3D keypoint arrays into a synchronized high-resolution time-stamped array buffer.

### 4.2 Automated Pseudo-Labeling & Annotation Scrubber
* **Automatic Gesture Boundary Detection**: Uses the MediaPipe displacement metric to automatically highlight candidate active segments with bounding boxes on the session timeline.
* **Interactive Waveform Scrubber**:
  - Clinicians can drag boundary handles, split merged gestures, delete accidental arm adjustments, and label clips (e.g. `"Washroom"`, `"Get Out of Way"`, `"Pain"`).
  - Mark specific clips as `"Spasm/Noise Negative"` to feed the Triplet Loss trainer.

### 4.3 Training & Model Optimization Engine
* **Instant On-Device Trainer (Tiers 0 & 1)**: Computes 20-cluster K-Means centroids and extracts LCSS prototype sequences directly in browser JavaScript in $< 5\text{ seconds}$.
* **Cloud GPU Dispatcher (Tiers 2 & 3)**:
  - Bundles calibrated session data into a secure JSON/HDF5 archive.
  - Dispatches training jobs to local or cloud PyTorch instances (NVIDIA RTX 6000 Ada GPU).
  - Automatically executes self-supervised pre-training (Masked MAE / Triplet / InfoNCE) followed by few-shot supervised fine-tuning.
  - Returns a quantized INT8 ONNX package (`model_personalized.onnx`, $< 4\text{ MB}$).

### 4.4 Diagnostic & Benchmarking Sandbox
* **Live Playback Testing**: Replays recorded sessions against any of the six model tiers, displaying real-time prediction probabilities, false activations, and missed triggers.
* **Clinical Performance Scorecard**:
  - Displays Per-Class F1 Score, Precision, Recall, and Confusion Matrix.
  - Reports average inference latency and hardware memory footprint.
  - **False-Positive Stress Test Score**: Evaluates model resistance against a standardized 10-minute continuous baseline of typing, wheelchair driving, and eating.

---

## 5. Technical Architecture & File Layout

Gesture Studio and AllyAACUI will be structured as modular components within the ecosystem:

```
src/
├── js/
│   ├── movesense.js             # 50 Hz BLE IMU Acquisition & Buffer
│   ├── vision.js                # MediaPipe 3D Landmark Extraction & Pseudo-labeling
│   ├── gesture_engine.js        # Runtime Dispatcher & ONNX WebGPU Pipeline
│   ├── sw_rkpic.js              # Pure JS Tier 0 Kinematic State Machine
│   ├── lcss_matcher.js          # Tier 1 K-Means & LCSS Sequence Matcher
│   ├── gesture_studio/
│   │   ├── studio_main.js       # Studio Lifecycle, State, & Mode Management
│   │   ├── recorder.js          # Synchronized Sensor Data Logger
│   │   ├── annotator.js         # Waveform Scrubber & Displacement Detector
│   │   ├── trainer_client.js    # Cloud GPU Training Bridge & ONNX Importer
│   │   └── diagnostics.js       # Confusion Matrix & F1 Benchmark Visualizer
├── html/
│   ├── gesture_studio_modal.html# Full-Screen Studio Modal & Drawer Layouts
│   └── hud.html                 # Real-time Gesture Probability & Dwell HUD
docs/
└── GESTURE_STUDIO_MUXLYUI_PRD.md # This Specification
```

---

## 6. Implementation Phased Roadmap

### Phase 1: Studio Telemetry Hub & Tier 0/1 Matcher (Weeks 1–3)
- Deliver unified sensor hub recording synchronized 50 Hz Movesense IMU and MediaPipe keypoints.
- Implement pure-JS Tier 0 (SW-RKPIC) and Tier 1 (K-Means + LCSS) in `src/js/`.
- Provide timeline scrubber and manual gesture snippet export.

### Phase 2: MediaPipe Auto-Annotation & ONNX WebGPU Runtime (Weeks 4–6)
- Integrate automatic displacement-based pseudo-labeling in `annotator.js`.
- Integrate ONNX Runtime Web (`ort-web`) with WebGPU and WASM-SIMD fallbacks into ScoobyUI and AllyAACUI.
- Build live diagnostic sandbox with real-time probability HUD and dwell triggers.

### Phase 3: Cloud GPU Pipeline & Deep Transformer Tiers (Weeks 7–9)
- Release standalone PyTorch training worker for Tier 2A (Masked MAE), Tier 2B (Triplet), and Tier 2C (InfoNCE).
- Implement automated INT8 quantization and packaging.
- Deploy one-click cloud train & download bridge.

### Phase 4: Multimodal Cross-Attention & Clinical Trials (Weeks 10–12)
- Implement Tier 3 (Multimodal Vision + IMU Cross-Attention) in ONNX Runtime.
- Conduct clinical validation with AAC communicators and occupational therapists.
- Benchmark false-positive rejection rates during ambient wheelchair activities.
