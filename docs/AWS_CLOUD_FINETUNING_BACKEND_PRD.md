# Product Requirements Document (PRD) & Architecture Blueprint
# AllyAAC Cloud Fine-Tuning Hub: Serverless AWS Backend & ML Pipeline

* **Document Version**: 1.0.0  
* **Date**: October 9, 2026  
* **Author / Frontend Lead**: Kevin (Frontend / UX Architecture)  
* **Target Backend & ML Lead**: Syed (Cloud Infrastructure & ML Research)  
* **Target Cloud Ecosystem**: Amazon Web Services (AWS) — Utilizing Research Grant Credits  
* **Companion Specifications**: 
  - [Frontend Modern FS & Research Opt-In PRD](file:///d:/projects/aac/AllyAAC/AllyAACUI/docs/MODERN_FS_CLOUD_FINETUNING_PRD.md)
  - [Gesture Studio PRD (6-Tier Algorithm Engine)](file:///d:/projects/aac/AllyAAC/AllyAACUI/docs/GESTURE_STUDIO_PRD.md)
  - [Video-to-IMU Telemetry Sync Specification](file:///d:/projects/aac/AllyAAC/AllyAACUI/docs/VIDEO_IMU_SYNC.md)

---

## 1. Executive Summary & Objective

AllyAAC is a multi-modal Augmentative and Alternative Communication (AAC) platform providing real-time gestural access for communicators with motor impairments (cerebral palsy, dystonia, brainstem stroke, ALS).

While deterministic rules (Tier 0: SW-RKPIC) and symbolic sequence matchers (Tier 1: LCSS) run client-side in the browser, users with complex involuntary tremors, tone fluctuations, and spasm noise require deep transformer models:
* **Tier 2A**: 1D-CNN Transformer Masked Autoencoder (Micro-motion reconstruction)
* **Tier 2B**: Triplet Metric Deep Hypersphere (Spasm vs. Intent contrastive separation)
* **Tier 2C**: InfoNCE Contrastive Predictive Coding (Continuous trajectory forecasting)
* **Tier 3**: Multimodal Cross-Attention (50 Hz Movesense 6-DoF IMU + 30 fps MediaPipe 3D Hand Landmarks)

### Syed's Deliverable:
Build and operate the **AWS Serverless Ingestion & Asynchronous GPU Fine-Tuning Pipeline**. This system accepts anonymized telemetry bundles (`.gesturepkg` + synchronized session telemetry) from the frontend, manages training queues, executes few-shot fine-tuning on GPU instances, quantizes the resulting weights to INT8 ONNX, and exposes a secure retrieval mechanism for the frontend client.

---

## 2. End-to-End System Architecture

```
+----------------------------------------------------------------------------------------------------------------------------------------+
|                                                      AWS CLOUD ARCHITECTURE OVERVIEW                                                   |
+----------------------------------------------------------------------------------------------------------------------------------------+

  [ AllyAAC Frontend Client ]
              |
              | 1. POST /api/v1/training/request
              v
   +----------------------+        2. Generate S3 Presigned URL
   |  Amazon API Gateway  | -------------------------------------> [ AWS Lambda: JobInitiator ]
   |     (HTTP API v2)    | <-------------------------------------             |
   +----------------------+        3. Return JobID & Presigned PUT URL         | 4. PutItem (Status: AWAITING_UPLOAD)
              |                                                                v
              |                                                     +---------------------+
              | 5. Direct S3 Upload (Telemetry Bundle)              |  Amazon DynamoDB    |
              v                                                     |  (Jobs Table)       |
   +------------------------------------+                           +---------------------+
   |      Amazon S3 Staging Bucket      |                                      ^
   |   (s3://allyaac-telemetry-staging) |                                      |
   +------------------------------------+                                      |
              |                                                                |
              | 6. S3 ObjectCreated Event (or Client Confirmation POST)        |
              v                                                                |
   +------------------------------------+                                      |
   |      AWS Lambda: JobDispatcher     | -------------------------------------+ Update Status: QUEUED
   +------------------------------------+
              |
              | 7. Enqueue Training Payload
              v
   +------------------------------------+
   |          Amazon SQS Queue          |
   |      (allyaac-training-queue)      |
   +------------------------------------+
              |
              | 8. Polled / Event-Triggered (Long Polling)
              v
   +----------------------------------------------------------------------------------------------------+
   |                            GPU TRAINING & MODEL COMPILATION WORKER                                 |
   |                                                                                                    |
   |  Option A (Cost-Optimized): EC2 Spot Worker (`g5.xlarge` / `g6.xlarge` NVIDIA A10G / L4)           |
   |  Option B (Managed): Amazon SageMaker Training Job                                                 |
   |                                                                                                    |
   |  Pipeline Steps:                                                                                   |
   |  a. Download Telemetry Bundle & Extract Negative Spasm Samples vs. Positive Gestures               |
   |  b. Load Foundation Checkpoint (`foundation_imu_v2.pt` or `multimodal_backbone.pt`)                |
   |  c. Execute Few-Shot Fine-Tuning (Triplet Loss Margin $m=0.3$, Epochs: 25-50, AdamW)              |
   |  d. Compute Validation Metrics (Per-class F1, Spasm False-Positive Resistance)                     |
   |  e. Export & Quantize: PyTorch -> ONNX Graph -> INT8 Dynamic Quantization (`< 3.8 MB`)             |
   |  f. Upload `model_enhanced.onnx` & `eval_metrics.json` to S3 Models Bucket                        |
   +----------------------------------------------------------------------------------------------------+
              |
              | 9. Update Job Status: COMPLETED, Metrics, S3 Model Key
              v
   +------------------------------------+
   |   Amazon DynamoDB (Jobs Table)     |
   +------------------------------------+
              ^
              | 10. GET /api/v1/training/status?jobId=...
              |     Returns: { status: "COMPLETED", metrics: {...}, downloadUrl: "https://..." }
   +----------------------+
   |  Amazon API Gateway  | <---- [ AWS Lambda: StatusChecker ]
   +----------------------+
              ^
              | 11. Client fetches model directly into WebGPU Runtime
   [ AllyAAC Frontend Client ]
```

---

## 3. Detailed Component Specifications (For Syed)

### 3.1 Data Ingestion & Storage Layer

1. **Amazon S3 Buckets**:
   * **`s3://allyaac-raw-telemetry-staging/`**:
     - Dedicated bucket for raw client uploads (`.gesturepkg`, `.json`, optional `.webm`).
     - **Security**: SSE-KMS encryption, Block All Public Access enabled, TLS 1.3 enforcement.
     - **Lifecycle Policy**: Expiration rule purging objects after **30 days** (ensures minimal storage footprint and GDPR/IRB compliance).
   * **`s3://allyaac-compiled-models/`**:
     - Retains trained and quantized `.onnx` artifacts and diagnostic evaluation summaries.
     - Organized as: `models/{subjectUuid}/{jobId}/model_enhanced_int8.onnx`.

2. **Amazon DynamoDB (`AllyAAC_TrainingJobs`)**:
   * **Primary Key**: `jobId` (String, UUIDv4)
   * **GSI 1 (Global Secondary Index)**: `subjectUuid-createdAt-index` (Allows listing user job histories)
   * **Schema Attributes**:
     ```json
     {
       "jobId": "job_9831_c4d2",
       "subjectUuid": "sub_anon_87e2f140-5a39-4d62-9e2c-bb1456a8d011",
       "status": "QUEUED | RUNNING | COMPLETED | FAILED",
       "targetTier": "tier_2b_triplet | tier_2c_infonce | tier_3_multimodal",
       "clientVersion": "3.1.0",
       "rawUploadS3Key": "uploads/job_9831_c4d2.gesturepkg",
       "modelOutputS3Key": "models/sub_.../job_9831_c4d2/model_int8.onnx",
       "evalMetrics": {
         "f1Score": 0.942,
         "latencyEstimateMs": 18.5,
         "falsePositiveRejectionRate": 0.981
       },
       "errorMessage": null,
       "createdAt": 1728519600,
       "updatedAt": 1728519920,
       "ttl": 1731111600
     }
     ```

### 3.2 API Gateway & Serverless Endpoints

| Method | Endpoint | Lambda Handler | Description |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/training/request` | `JobInitiator` | Validates schema, registers job in DynamoDB, issues pre-signed S3 upload URL. |
| `POST` | `/api/v1/training/confirm` | `JobDispatcher` | Confirms upload completion, inspects file header, pushes job to SQS. |
| `GET` | `/api/v1/training/status` | `StatusChecker` | Returns current progress (%), worker state, and pre-signed download URL when ready. |
| `DELETE`| `/api/v1/subject/{id}` | `SubjectRevoker` | Right-to-be-forgotten endpoint: purges all S3 artifacts and DB entries for a subject UUID. |

### 3.3 Training & Quantization Worker (ML Pipeline)

1. **Base Foundation Weights**:
   * The worker maintains pre-trained base models (trained on public/synthetic kinematic datasets) for:
     - 6-DoF IMU 1D ResNet-Transformer backbone (128-dimensional embedding space).
     - Vision Hand Landmark GCN (Graph Convolutional Network) / MLP encoder.
2. **Fine-Tuning Objective (Tier 2B Triplet Loss)**:
   * **Anchor ($a$)**: Calibrated template demonstration for gesture $k$.
   * **Positive ($p$)**: Additional trials of gesture $k$ by the communicator.
   * **Negative ($n$)**: Labeled spasm episodes, involuntary dystonic jerks, and resting baseline noise.
   * **Loss Function**:
     $$\mathcal{L}_{\text{triplet}} = \max(0, \|\mathbf{f}(a) - \mathbf{f}(p)\|_2^2 - \|\mathbf{f}(a) - \mathbf{f}(n)\|_2^2 + \alpha)$$
     where margin $\alpha = 0.3$.
3. **Quantization Pipeline (Export to ONNX)**:
   ```python
   import torch
   import onnx
   from onnxruntime.quantization import quantize_dynamic, QuantType

   # 1. Export PyTorch to FP32 ONNX
   torch.onnx.export(
       model,
       dummy_input,
       "model_fp32.onnx",
       input_names=["imu_input"],
       output_names=["embedding"],
       dynamic_axes={"imu_input": {0: "batch", 1: "time"}},
       opset_version=17
   )

   # 2. Dynamic INT8 Quantization (WebGPU / WASM Target)
   quantize_dynamic(
       model_input="model_fp32.onnx",
       model_output="model_enhanced_int8.onnx",
       weight_type=QuantType.QInt8
   )
   ```
   * **Constraint**: Final file size **MUST BE $< 4.0\text{ MB}$** to ensure instant download over mobile connections.

---

## 4. Frontend & Backend Contract Interface

### Request Payload (`POST /api/v1/training/request`)
```json
{
  "subjectUuid": "sub_anon_87e2f140-5a39-4d62-9e2c-bb1456a8d011",
  "clientVersion": "AllyAAC-3.1.0",
  "targetTier": "tier_2b_triplet",
  "sampleCount": 42,
  "negativeSpasmCount": 14,
  "bundleSizeBytes": 1284500
}
```

### Response Payload (`200 OK`)
```json
{
  "jobId": "job_9831_c4d2",
  "uploadUrl": "https://allyaac-raw-telemetry-staging.s3.amazonaws.com/uploads/job_9831_c4d2.gesturepkg?AWSAccessKeyId=...",
  "uploadHeaders": {
    "x-amz-server-side-encryption": "AES256"
  },
  "expiresInSeconds": 1800
}
```

### Status Payload (`GET /api/v1/training/status?jobId=job_9831_c4d2`)
```json
{
  "jobId": "job_9831_c4d2",
  "status": "COMPLETED",
  "progressPct": 100,
  "downloadUrl": "https://allyaac-compiled-models.s3.amazonaws.com/models/.../model_enhanced_int8.onnx?AWSAccessKeyId=...",
  "metrics": {
    "f1Score": 0.942,
    "baselineAccuracy": 0.718,
    "improvementPct": 31.2,
    "spasmRejectionRate": 0.985,
    "quantizedSizeBytes": 3280140
  }
}
```

---

## 5. Free Tools for Frontend Proof-of-Concept (Zero AWS Cost)

Before Syed deploys the production AWS backend, you (the UX/Frontend dev) can completely prototype, test, and demonstrate the entire fine-tuning and modern file system loop for **$0** using standard free developer tooling:

```
+------------------------------------------------------------------------------------+
|               FRONTEND ZERO-COST PROOF-OF-CONCEPT (POC) MATRIX                     |
+------------------------------------------------------------------------------------+
| COMPONENT                  | RECOMMENDED FREE TOOL            | SETUP TIME         |
| -------------------------- | -------------------------------- | ------------------ |
| Modern File System Access  | Native Web API (`showDirectory`) | 0 min (Built-in)   |
| Local Mock Cloud S3 Server | MinIO Community / Mock Service Worker| 10 mins        |
| Serverless API Mock        | Mock Service Worker (MSW) or Vite| 5 mins             |
| Real Cloud GPU Training    | Google Colab (Free T4 GPU)       | 15 mins            |
| Free Tunnel / Webhook Sync | Cloudflare Tunnel / ngrok        | 2 mins             |
| ONNX Web Runtime           | `onnxruntime-web` (WebGPU / WASM)| 5 mins (CDN)       |
+------------------------------------------------------------------------------------+
```

### 5.1 Step-by-Step Free PoC Strategy

1. **Free Local Mock API (`MockServiceWorker` or simple Vite API route)**:
   * Intercept `POST /api/v1/training/request` and `GET /api/v1/training/status`.
   * Simulate upload latency and progress percentage increments (`QUEUED` -> `25%` -> `75%` -> `COMPLETED`).
   * Return a static pre-compiled dummy ONNX model (`model_test.onnx`) to verify that the UI seamlessly downloads, parses, and loads the model into the WebGPU runtime.

2. **Free Real Cloud GPU Training via Google Colab / Kaggle Notebooks**:
   * Use **Google Colab (Free Tier - 15GB T4 GPU)** or **Kaggle (30 hrs/week free GPU)**.
   * Write a small Python script in Colab that accepts an exported `.gesturepkg`, fits a PyTorch Triplet embedding model, quantizes it with ONNX Runtime, and outputs `model.onnx`.
   * Expose the Colab notebook as a live webhook endpoint using **Cloudflare Tunnel (`cloudflared`)** or **ngrok**.
   * Your frontend can make real API calls to your free Colab instance without spending a single AWS credit.

3. **Free Local Storage & Cloud Sync**:
   * Use the native **File System Access API** in Chrome / Edge.
   * Point the folder picker to your computer's local `C:\Users\<user>\OneDrive\AllyAAC\` or `G:\My Drive\AllyAAC\` directory.
   * Test the automatic in-place sync without building any cloud backend.

---

## 6. Syed's Task Checklist & Milestones

- [ ] **Milestone 1: Infrastructure as Code (Terraform / AWS CDK)**
  - [ ] S3 Staging & Model buckets with SSE-KMS and 30-day lifecycle rules.
  - [ ] DynamoDB `AllyAAC_TrainingJobs` table with TTL.
  - [ ] API Gateway HTTP API v2 routes and IAM execution roles.
- [ ] **Milestone 2: Serverless Lambda Handlers**
  - [ ] `JobInitiator`: Generates presigned S3 PUT URLs.
  - [ ] `JobDispatcher`: Validates bundle checksum and pushes message to SQS.
  - [ ] `StatusChecker`: Queries DynamoDB and generates presigned GET download URLs.
- [ ] **Milestone 3: PyTorch GPU Training Pipeline**
  - [ ] Setup EC2 Spot instance (`g5.xlarge`) or SageMaker Training Job container.
  - [ ] Implement Triplet Loss fine-tuning script with spasm negative mining.
  - [ ] Automated Dynamic INT8 ONNX export and verification script.
- [ ] **Milestone 4: Security & Compliance**
  - [ ] Implement user deletion / Right-to-be-Forgotten endpoint (`DELETE /api/v1/subject/{id}`).
  - [ ] Verify that no PII or unanonymized metadata is written to S3 or CloudWatch logs.
