# Product Requirements Document (PRD)
# AllyAAC: Frontend Proof-of-Concept (PoC)
## Modern File System Sync, Research Opt-In UX & Mock Fine-Tuning Pipeline

* **Document Version**: 1.0.0  
* **Date**: October 9, 2026  
* **Author**: Frontend / UX Lead  
* **Status**: Ready for Implementation  
* **Target Target**: AllyAACUI (`D:\projects\aac\AllyAAC\AllyAACUI`) & AllyAAC-PWA  
* **Dependencies / Cost**: **$0.00 (100% Free Toolstack / No Cloud Infrastructure Required)**  
* **Companion Documents**:
  - [Modern File System & Cloud Fine-Tuning Overview PRD](file:///d:/projects/aac/AllyAAC/AllyAACUI/docs/MODERN_FS_CLOUD_FINETUNING_PRD.md)
  - [AWS Cloud Fine-Tuning Backend PRD (For Syed)](file:///d:/projects/aac/AllyAAC/AllyAACUI/docs/AWS_CLOUD_FINETUNING_BACKEND_PRD.md)
  - [Gesture Studio PRD](file:///d:/projects/aac/AllyAAC/AllyAACUI/docs/GESTURE_STUDIO_PRD.md)

---

## 1. Executive Summary & Objective

This document defines the **Frontend Proof-of-Concept (PoC)** for:
1. **Modern File System Access (`showDirectoryPicker` / `showSaveFilePicker`)**: Enabling communicators and clinicians to mount their OS-synced Google Drive, OneDrive, or iCloud folders directly in AllyAAC, supporting one-click in-place saves and auto-backup of `.gesturepkg` files.
2. **Informed Consent & Research Opt-In UX**: Accessible, plain-language modal dialogs allowing communicators to voluntarily contribute anonymized gesture telemetry to the research team.
3. **Simulated Fine-Tuning Pipeline (Zero AWS Cost)**: A self-contained, browser-side simulation engine (or optional free Google Colab / local mock worker) that mimics the complete asynchronous model training lifecycle—from bundle creation and upload to progress tracking, download, and ONNX WebGPU runtime ingestion.

### PoC Goals:
* **Zero Backend Blocker:** Unblock all frontend UI/UX development and user testing immediately while Syed works on the AWS infrastructure.
* **Realistic User Validation:** Allow clinicians and communicators to experience the full end-to-end workflow (connecting cloud folders, requesting model optimization, and testing enhanced models).
* **Drop-in Backend Compatibility:** Design the client networking layer so swapping the mock engine with Syed's live AWS API Gateway requires changing only a single configuration flag (`USE_MOCK_CLOUD_BACKEND: false`).

---

## 2. Architecture & Free Tooling Stack

```
+----------------------------------------------------------------------------------------------------+
|                                    FRONTEND PoC ARCHITECTURE                                       |
+----------------------------------------------------------------------------------------------------+

  [ Communicator / Clinician UI ]
        |
        +---> [ Modern File System Manager ] 
        |       - window.showDirectoryPicker() / showSaveFilePicker()
        |       - IndexedDB handle persistence (idb-keyval / native IDB)
        |       - Direct file sync to local Google Drive / OneDrive / iCloud directory
        |
        +---> [ Research Opt-In & Sanitization Engine ]
        |       - Accessible informed consent modal (WCAG 2.2 AAA compliant)
        |       - Client-side PII scrubbing (Generates pseudonymous subject UUID)
        |       - In-memory `.gesturepkg` bundle builder
        |
        +---> [ Cloud Exchange Client Adapter ]
                |
                +---> [ Mode A: Local Mock Cloud Engine (Default / $0 Cost) ]
                |       - Simulates S3 upload & DynamoDB job state machine
                |       - Emits realistic progress (0% -> 25% -> 60% -> 100%)
                |       - Delivers sample INT8 ONNX model (`model_enhanced_mock.onnx`)
                |
                +---> [ Mode B: Optional Free Google Colab / ngrok Worker ($0 Cost) ]
                |       - Runs real PyTorch Triplet training on Free T4 GPU
                |       - Returns real fine-tuned INT8 ONNX model via Cloudflare Tunnel
                |
                +---> [ Mode C: Production AWS Backend (Syed's System) ]
                        - Active when AWS infrastructure is deployed
```

### Free Tooling Selection:
| Capability | PoC Free Tool / API | Cost | Justification |
| :--- | :--- | :--- | :--- |
| **Directory Storage** | Native W3C File System Access API | $0 | Built directly into Chrome, Edge, and Chromium browsers. Seamlessly accesses mounted Google Drive and OneDrive folders. |
| **Handle Persistence** | Browser IndexedDB | $0 | Standard browser storage; keeps directory handles active across reloads and tab closures. |
| **API / S3 Simulation** | In-Browser Mock Service Adapter (`mock_cloud_service.js`) | $0 | Simulates presigned S3 uploads, asynchronous delays, and job status polling without any server setup. |
| **Real GPU Training (Optional)** | Google Colab (Free T4 GPU) or Kaggle (30h/wk) | $0 | Optional path to train real PyTorch models on user data without AWS credits. |
| **Tunneling (Optional)** | Cloudflare Tunnel (`cloudflared`) or ngrok free | $0 | Securely exposes local or Colab endpoints over HTTPS to the PWA. |
| **Model Execution** | ONNX Runtime Web (`ort-web` v1.19+ via CDN) | $0 | Runs INT8 quantized models in WebGPU or WASM directly in the client browser. |

---

## 3. User Experience & Interaction Design

### 3.1 Gesture Studio Header Toolbar Enhancements

The existing toolbar in `Gesture Studio` is updated with modern storage indicators and the research optimization action:

```
+----------------------------------------------------------------------------------------------------+
| [AllyAAC Gesture Studio]   [📁 Folder: OneDrive/AAC/Gestures 🟢]   [Model: Tier 2B (Enhanced 94%)] |
|                                                                                                    |
| [Pair Movesense] [Live Preview] | [📂 Connect Cloud Folder] [💾 Save] [🔬 Request Research AI Boost]|
+----------------------------------------------------------------------------------------------------+
```

1. **Folder Status Pill (`#fs-status-pill`)**:
   * **Disconnected (Default)**: Gray badge `📁 No Folder Connected (Download Mode)`.
   * **Connected**: Green badge `📁 [Folder Name] (In-Place Auto-Sync)`.
2. **"Connect Cloud Folder" Button**:
   * Triggers the native folder picker dialog.
3. **"Save" Button**:
   * If a folder is connected: Writes directly in-place to `allyaac_gestures.gesturepkg` with a subtle pulse animation.
   * If not connected: Triggers standard browser file download.
4. **"Request Research AI Boost" Button**:
   * Displays a prominent science badge (`🔬 Research AI Boost`).
   * Launches the multi-step opt-in wizard.

---

### 3.2 Modal 1: Accessible Research Consent Dialog

Designed with high-contrast accessibility (WCAG 2.2 AAA), large touch targets ($\ge 48\text{px}$), and plain language:

```
+------------------------------------------------------------------------------------+
| 🔬 Accelerate Your Gesture Accuracy with Research AI (AWS Cloud Hub)              |
+------------------------------------------------------------------------------------+
|                                                                                    |
| Our research team has high-performance GPU cloud servers to train advanced deep     |
| learning models tailored to your exact motor movement patterns.                    |
|                                                                                    |
| [ ] I agree to contribute my anonymized gesture data to help train my custom model  |
|                                                                                    |
| Select Data to Include:                                                            |
|   [x] Motion Sensor Data (Inertial movements only)                                 |
|   [x] Hand / Body Keypoint Angles (Mathematical coordinates only - NO photos/video)|
|   [ ] Camera Video Clips (Optional: helps researchers spot false activations)      |
|                                                                                    |
| Privacy & Safety Guarantee:                                                        |
|   • All names and identifiers are permanently stripped before sending.             |
|   • Your data is assigned a random anonymous research code.                        |
|   • You can revert back to your original gesture settings at any time.             |
|                                                                                    |
| [ Cancel ]                                         [ Proceed to Optimization -> ]  |
+------------------------------------------------------------------------------------+
```

---

### 3.3 Modal 2: Live Fine-Tuning Progress & Retargeting HUD

When the user initiates optimization, this modal provides real-time transparency into the asynchronous job:

```
+------------------------------------------------------------------------------------+
| ⚙️ Optimizing Your Custom Gesture Model...                                         |
+------------------------------------------------------------------------------------+
|                                                                                    |
| Target Architecture: Tier 2B: Triplet Metric Deep Hypersphere                      |
| Status: Training Micro-Motion Spasm Rejection (Epoch 28/50)                        |
|                                                                                    |
| Progress:                                                                          |
| [====================================>                  ] 65%                      |
| Estimated time remaining: ~45 seconds                                              |
|                                                                                    |
| [x] Notify me when ready (You may continue using communication boards)             |
|                                                                                    |
|                                                                     [ Run in Background ]
+------------------------------------------------------------------------------------+
```

---

### 3.4 Modal 3: Model Ready & Side-by-Side Comparison

When training completes, the user is presented with a clear scorecard showing their improvement before applying the model:

```
+------------------------------------------------------------------------------------+
| 🎉 Your Enhanced Model is Ready!                                                   |
+------------------------------------------------------------------------------------+
|                                                                                    |
| Performance Comparison:                                                            |
|                                                                                    |
| Metric                    Standard (Baseline)       Enhanced (Cloud Model)         |
| ------------------------  --------------------      ----------------------         |
| Gesture Accuracy (F1)     71.8%                     94.2% (+22.4% gain)  🚀        |
| Involuntary Spasm Filter  Moderate                  98.5% Spasm Immunity ⭐        |
| Model Footprint           ~130 params               3.2 MB (Quantized INT8)        |
| On-Device Latency         12 ms                     18 ms (WebGPU Accelerated)     |
|                                                                                    |
| [ Keep Current Model ]                              [ Apply Enhanced Model Now ]   |
+------------------------------------------------------------------------------------+
```

---

## 4. Technical Specification & Implementation Plan

### 4.1 Component Architecture

The PoC will introduce three clean, decoupled JavaScript modules in `AllyAACUI`:

```
AllyAAC/AllyAACUI/
├── js/
│   ├── storage/
│   │   ├── file_system_manager.js   # W3C File System Access API & IndexedDB store
│   │   └── data_sanitizer.js        # Anonymizer & PII scrubbing engine
│   ├── cloud/
│   │   ├── cloud_client.js          # Unified Cloud API adapter interface
│   │   └── mock_cloud_service.js    # $0 In-browser simulation engine
│   └── models/
│       └── mock_tier2b_model.onnx   # Lightweight dummy ONNX model for WebGPU loading
```

---

### 4.2 Module 1: Modern File System Manager (`file_system_manager.js`)

```javascript
/**
 * Modern File System Access API Manager for AllyAAC
 * Handles directory connection, handle persistence, and direct in-place saving.
 */
class FileSystemManager {
  constructor() {
    this.directoryHandle = null;
    this.dbName = 'allyaac_storage_db';
    this.storeName = 'handles';
    this.currentFileName = 'allyaac_gestures.gesturepkg';
  }

  isSupported() {
    return 'showDirectoryPicker' in window && 'showSaveFilePicker' in window;
  }

  async init() {
    if (!this.isSupported()) return false;
    // Restore saved directory handle from IndexedDB
    this.directoryHandle = await this._loadHandleFromIDB('active_gesture_dir');
    if (this.directoryHandle) {
      // Verify permission
      const status = await this.directoryHandle.queryPermission({ mode: 'readwrite' });
      if (status !== 'granted') {
        const req = await this.directoryHandle.requestPermission({ mode: 'readwrite' });
        if (req !== 'granted') this.directoryHandle = null;
      }
    }
    return !!this.directoryHandle;
  }

  async connectDirectory() {
    if (!this.isSupported()) {
      alert('Your browser does not support the File System Access API. Standard downloads will be used.');
      return null;
    }
    try {
      this.directoryHandle = await window.showDirectoryPicker({
        id: 'allyaac_gesture_sync_folder',
        mode: 'readwrite'
      });
      await this._saveHandleToIDB('active_gesture_dir', this.directoryHandle);
      return this.directoryHandle.name;
    } catch (err) {
      if (err.name !== 'AbortError') console.error('Directory selection failed:', err);
      return null;
    }
  }

  async savePackageInPlace(packageData) {
    const jsonStr = JSON.stringify(packageData, null, 2);

    if (this.directoryHandle) {
      try {
        const fileHandle = await this.directoryHandle.getFileHandle(this.currentFileName, { create: true });
        const writable = await fileHandle.createWritable();
        await writable.write(jsonStr);
        await writable.close();
        return { success: true, method: 'in_place', path: `${this.directoryHandle.name}/${this.currentFileName}` };
      } catch (err) {
        console.warn('In-place save failed, falling back to picker:', err);
      }
    }

    // Fallback: showSaveFilePicker
    if ('showSaveFilePicker' in window) {
      try {
        const handle = await window.showSaveFilePicker({
          suggestedName: this.currentFileName,
          types: [{ description: 'AllyAAC Gesture Package', accept: { 'application/json': ['.gesturepkg', '.json'] } }]
        });
        const writable = await handle.createWritable();
        await writable.write(jsonStr);
        await writable.close();
        return { success: true, method: 'save_picker', path: handle.name };
      } catch (err) {
        if (err.name === 'AbortError') return { cancelled: true };
      }
    }

    // Ultimate fallback: traditional download anchor
    this._downloadAnchorFallback(jsonStr);
    return { success: true, method: 'download_anchor' };
  }

  _downloadAnchorFallback(jsonStr) {
    const blob = new Blob([jsonStr], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `allyaac_gestures_${Date.now()}.gesturepkg`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  }

  // --- IndexedDB Handle Storage Helpers ---
  async _openDB() {
    return new Promise((resolve, reject) => {
      const req = indexedDB.open(this.dbName, 1);
      req.onupgradeneeded = () => req.result.createObjectStore(this.storeName);
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
  }

  async _saveHandleToIDB(key, handle) {
    const db = await this._openDB();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(this.storeName, 'readwrite');
      tx.objectStore(this.storeName).put(handle, key);
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  }

  async _loadHandleFromIDB(key) {
    try {
      const db = await this._openDB();
      return new Promise((resolve) => {
        const tx = db.transaction(this.storeName, 'readonly');
        const req = tx.objectStore(this.storeName).get(key);
        req.onsuccess = () => resolve(req.result || null);
        req.onerror = () => resolve(null);
      });
    } catch {
      return null;
    }
  }
}
```

---

### 4.3 Module 2: Data Sanitizer & Anonymizer (`data_sanitizer.js`)

```javascript
/**
 * Client-Side PII Scrubbing and Salted Anonymization
 */
class DataSanitizer {
  static getOrCreateSubjectUuid() {
    let uuid = localStorage.getItem('allyaac_research_subject_uuid');
    if (!uuid) {
      uuid = 'sub_anon_' + crypto.randomUUID();
      localStorage.setItem('allyaac_research_subject_uuid', uuid);
    }
    return uuid;
  }

  static sanitizeTelemetryBundle(rawPackage, consentOptions) {
    const sanitized = {
      packageId: 'pkg_' + crypto.randomUUID(),
      subjectUuid: this.getOrCreateSubjectUuid(),
      sanitizedAt: new Date().toISOString(),
      consent: {
        consented: true,
        imuIncluded: consentOptions.includeImu,
        landmarksIncluded: consentOptions.includeLandmarks,
        videoIncluded: consentOptions.includeVideo
      },
      thresholds: { ...rawPackage.thresholds },
      templates: (rawPackage.templates || []).map((t, idx) => ({
        id: `class_${idx + 1}`,
        actionHint: t.action ? t.action.split(':')[0] : 'custom',
        featureVector: t.featureVector || [],
        imuSamplesCount: t.imuSamplesCount || 0
      }))
    };

    // Strip any raw text labels or identifying caregiver notes
    return sanitized;
  }
}
```

---

### 4.4 Module 3: Mock Cloud Fine-Tuning Service (`mock_cloud_service.js`)

This module simulates the entire AWS asynchronous lifecycle for **$0**, complete with simulated realistic delays, progress notifications, and delivery of a mock INT8 ONNX package:

```javascript
/**
 * $0 Free Mock Cloud Fine-Tuning Service
 * Simulates API Gateway, S3, SQS, GPU training delays, and ONNX delivery.
 */
class MockCloudService {
  constructor() {
    this.activeJob = null;
  }

  async requestTraining(payload) {
    // Simulate network delay
    await new Promise(r => setTimeout(r, 600));

    const jobId = 'mock_job_' + Date.now();
    this.activeJob = {
      jobId,
      subjectUuid: payload.subjectUuid,
      targetTier: payload.targetTier || 'tier_2b_triplet',
      status: 'QUEUED',
      progressPct: 0,
      createdAt: Date.now()
    };

    return {
      success: true,
      jobId,
      uploadUrl: `https://mock-s3.local/uploads/${jobId}.bundle`,
      expiresInSeconds: 1800
    };
  }

  async uploadBundle(uploadUrl, sanitizedBundle) {
    // Simulate uploading data bundle to S3
    await new Promise(r => setTimeout(r, 1200));
    if (this.activeJob) {
      this.activeJob.status = 'TRAINING';
      this.activeJob.progressPct = 10;
    }
    return { success: true };
  }

  async pollJobStatus(jobId, onProgress) {
    return new Promise((resolve) => {
      let currentPct = 10;
      const interval = setInterval(() => {
        currentPct += Math.floor(Math.random() * 15) + 10;
        if (currentPct >= 100) {
          currentPct = 100;
          clearInterval(interval);
          if (this.activeJob) this.activeJob.status = 'COMPLETED';

          if (onProgress) onProgress({ status: 'COMPLETED', progressPct: 100 });

          resolve({
            jobId,
            status: 'COMPLETED',
            progressPct: 100,
            downloadUrl: 'mock://models/model_enhanced_mock.onnx',
            metrics: {
              f1Score: 0.942,
              baselineAccuracy: 0.718,
              improvementPct: 22.4,
              spasmRejectionRate: 0.985,
              quantizedSizeBytes: 3240192,
              meanInferenceLatencyMs: 18.2
            }
          });
        } else {
          if (this.activeJob) this.activeJob.progressPct = currentPct;
          if (onProgress) onProgress({ status: 'TRAINING', progressPct: currentPct });
        }
      }, 1500); // 1.5s steps = ~10-12s total simulated training run
    });
  }
}
```

---

## 5. Phased PoC Implementation Checklist

```
+----------------------------------------------------------------------------------------------------+
|                                    PoC DEVELOPMENT SPRINTS                                         |
|                                                                                                    |
|  [SPRINT 1: MODERN FILE SYSTEM]       [SPRINT 2: CONSENT & MOCK CLOUD] [SPRINT 3: ONNX & BENCHMARK]|
|  - Implement FileSystemManager.js     - Implement DataSanitizer.js     - Add Mock ONNX Model       |
|  - Connect/Save Toolbar Buttons       - Build Accessible Consent Modal - Integrate ort-web (CDN)   |
|  - Test with local OneDrive/GDrive    - Implement MockCloudService.js  - Build Model Scorecard Modal|
|  - Fallback Download Verification     - Wire Async Progress HUD        - Test Rollback Action      |
+----------------------------------------------------------------------------------------------------+
```

### Sprint 1: Modern File System Integration (Days 1–2)
- [ ] Add `js/storage/file_system_manager.js` to `AllyAACUI`.
- [ ] Add `📁 Connect Folder` and `💾 Save` buttons to Gesture Studio header in `AllyAACUI/index.html`.
- [ ] Test mounting a local OneDrive / Google Drive folder on Windows.
- [ ] Verify that editing thresholds and clicking `💾 Save` writes directly to disk without browser download prompts.
- [ ] Verify fallback behavior on Safari or Firefox.

### Sprint 2: Consent Modal & Mock Cloud Service (Days 3–4)
- [ ] Add `js/storage/data_sanitizer.js` and verify PII removal.
- [ ] Build the high-contrast Research Opt-In Consent Modal (`#research-consent-modal`).
- [ ] Build the real-time Training Progress HUD (`#training-progress-hud`).
- [ ] Add `js/cloud/mock_cloud_service.js` to simulate asynchronous job states and progress updates.

### Sprint 3: ONNX Web Runtime Integration & Evaluation Scorecard (Days 5–6)
- [ ] Build the "Model Ready" Scorecard Modal (`#model-ready-modal`) comparing Baseline vs. Enhanced F1 metrics.
- [ ] Include a pre-compiled dummy ONNX model (`model_enhanced_mock.onnx`) in the workspace.
- [ ] Integrate ONNX Runtime Web (`ort-web`) to load the model into memory and display active status on the HUD.
- [ ] Implement one-click "Rollback / Discard Cloud Model" functionality.

---

## 6. Verification & Demonstration Plan

| Step | User Action | Expected PoC Behavior | Verified ($0 Tool) |
| :--- | :--- | :--- | :--- |
| **1** | Click `Connect Cloud Folder` | Native OS picker opens; user selects `OneDrive/AAC/Gestures`. Badge turns green: `📁 OneDrive/AAC/Gestures (In-Place Auto-Sync)`. | W3C File System Access API |
| **2** | Calibrate gesture & click `Save` | Writes to `OneDrive/AAC/Gestures/allyaac_gestures.gesturepkg` with toast: `Saved in-place to OneDrive`. File instantly syncs via desktop OneDrive. | Local OS Cloud Sync Engine |
| **3** | Click `🔬 Request Research AI Boost` | High-contrast consent modal appears explaining research pipeline and privacy guarantees. | Pure Frontend HTML/CSS |
| **4** | Check consent & click `Proceed` | DataSanitizer scrubs PII, assigns `sub_anon_...`, launches background training HUD. | Client-side Cryptography |
| **5** | Observe Progress HUD | Progress bar increments through simulated epochs (10% → 35% → 70% → 100%) with estimated time remaining. | In-browser Mock Service |
| **6** | Model Ready Modal | Displays F1 scorecard (+22.4% accuracy boost, 98.5% spasm rejection). | Accessible Tailwind Modal |
| **7** | Click `Apply Enhanced Model` | Model loads into ONNX WebGPU runtime; status pill displays `Model: Tier 2B (Enhanced 94%)`. | `ort-web` Runtime |
| **8** | Click `Rollback Model` | Instantly reverts back to local Tier 1 (LCSS) matcher without reload. | Local State Machine |
