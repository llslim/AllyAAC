# Product Requirements Document (PRD) - AllyAAC-Windows

* **Target System**: **`AllyAAC-Windows`** (`D:\projects\aac\AllyAAC\AllyAAC-Windows`)
* **Role**: Native Windows Desktop Host (WPF, C#, Microsoft Edge WebView2, Native WinRT BLE GATT)
* **Canonical UI Source of Truth**: **`AllyAACUI`** (`D:\projects\aac\AllyAAC\AllyAACUI`)
* **Shared Code Engine**: **`AllyAAC.Shared`** (.NET MAUI / .NET 8 Cross-Platform Library)

---

## 1. Product Objective
Develop the native Windows desktop host application for the **AllyAAC** gesture-enabled multi-modal AAC platform. Integrates wearable 9-DoF IMU sensors (Movesense BLE) via native Windows WinRT Bluetooth APIs, camera feeds via MediaPipe hand tracking, and embeds the canonical **AllyAACUI** web presentation layer (Gesture Studio, Activity Boards, and Simplified Message Display) inside a high-performance, low-latency WebView2 container.

---

## 2. Architecture & Ecosystem Roles

The AllyAAC family is organized under `D:\projects\aac\AllyAAC`:
- **`AllyAACUI`**: Canonical source of truth for the web UI, compiled to `index.html`.
- **`AllyAAC-Windows`**: Native C# Windows host consuming `AllyAAC.Shared` and hosting `AllyAACUI`.
- **`AllyAAC-Android`**: Native Android host with CameraX, system TTS, and kiosk mode.
- **`AllyAAC-PWA`**: Offline Progressive Web App target with Service Worker.

```
D:\projects\aac\AllyAAC\AllyAAC-Windows\
├── AllyAAC.Shared\                      # Cross-Platform .NET MAUI Class Library (net8.0)
│   ├── AllyAAC.Shared.csproj            # Multi-targeted for Windows, Android, and Core
│   ├── Models/
│   │   ├── GestureTemplate.cs           # Serialized template (Points, Threshold, UseGyro, Action)
│   │   ├── SensorSample.cs              # Standardized 6D sensor frame (ax, ay, az, gx, gy, gz)
│   │   ├── OasisGesturePayload.cs       # OASIS standard JSON event schema
│   │   └── ScoobyMessage.cs             # Two-way WebView host bridge IPC contract
│   └── Services/
│       ├── DtwMatcher.cs                # Multi-dimensional Z-score normalized DTW algorithm
│       ├── GestureCatalogService.cs     # Sliding window search & template storage
│       ├── MovesenseProtocol.cs         # Binary parser for Movesense MDS / GSP packets
│       └── AacStorageService.cs         # Cross-platform JSON persistence
│
└── AllyAAC.Windows\                     # Windows Desktop Host Application (net8.0-windows)
    ├── AllyAAC.Windows.csproj           # References AllyAAC.Shared & Microsoft.Web.WebView2
    ├── MainWindow.xaml                  # Fluent Windows UI with dockable WebView2 container
    ├── MainWindow.xaml.cs               # Two-way C# <-> JS IPC bridge
    └── Services/
        ├── BluetoothService.cs          # Native Windows 10/11 WinRT Bluetooth LE GATT
        ├── CameraService.cs             # Local MediaPipe webcam worker bridge
        └── TtsSpeechService.cs          # Native SAPI / System.Speech audio output
```

---

## 3. Technology Research & Implementation Specifics

### A. Wearable Sensors & BLE APIs (Movesense)
* **Movesense Device Protocol**: MDS / GSP (GATT SensorData Protocol).
* **Windows BLE API**: Native Windows Runtime (WinRT) BLE APIs (`Windows.Devices.Bluetooth`) allow direct GATT service communication on Windows desktop with zero third-party drivers.
* **GATT Service Mapping**:
  * MDS Service UUID: `34802252-7185-4d5d-b431-630e7050e8f0`
  * Command Characteristic: `34800001-7185-4d5d-b431-630e7050e8f0`
  * Data Characteristic: `34800002-7185-4d5d-b431-630e7050e8f0`
* **Sampling Rate**: 50 Hz linear acceleration + angular gyroscope rate.

### B. Dynamic Time Warping (DTW) & OASIS Classification
* **Continuous Trajectory Matching**: Implements `DtwMatcher` with per-channel Z-score normalization:
  $$z_{t,d} = \frac{x_{t,d} - \mu_d}{\sigma_d}$$
* **Path-Length Normalization**: Length-invariant distance $D = dp[n,m] / (n + m)$.
* **Dynamic Window Scaling**: Matches candidate executions between $80\%$ and $120\%$ of template length ($w \in [0.8L, 1.2L]$).
* **OASIS Standard Confidence**:
  $$\text{Confidence} = \max\left(0, \min\left(1, 1 - \frac{D}{\tau_{\text{threshold}}}\right)\right)$$

### C. WebView2 Host Bridge & Presentation
* Hosts `AllyAAC.Windows/allyaac_ui.html` synchronized from canonical `AllyAACUI`.
* Two-way IPC bridge:
  - C# $\rightarrow$ JS: `window.chrome.webview.postMessage({ type: 'SENSOR_TELEMETRY', ... })` and `{ type: 'GESTURE_DETECTED', ... }`.
  - JS $\rightarrow$ C#: `window.chrome.webview.postMessage({ type: 'SPEAK', text: '...' })`.

---

## 4. Phased Implementation Roadmap

1. **Phase 1: .NET MAUI Shared Library (`AllyAAC.Shared`)**: Implement models, `DtwMatcher`, `GestureCatalogService`, and `MovesenseProtocol`.
2. **Phase 2: WinRT BLE Service**: Connect directly to Movesense 9-DoF sensor over native Windows Bluetooth LE.
3. **Phase 3: WebView2 Host & UI Sync**: Synchronize canonical `AllyAACUI` into `allyaac_ui.html` and bridge hardware events.
4. **Phase 4: SAPI & System.Speech Fallback**: Native TTS speech dispatching.
5. **Phase 5: Packaging & Installer**: WiX Setup MSI package for standalone clinic deployment.
