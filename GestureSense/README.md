# GestureSense: Smartwatch Movesense Emulation

GestureSense is a cross-platform project designed to emulate a **Movesense BLE sensor** using **Apple Watch (watchOS)** and **Android Smartwatches (WearOS)**. By mimicking the GATT SensorData Protocol (GSP), these wearable devices can act as drop-in replacements for Movesense hardware, streaming high-frequency IMU data directly to the **AllyAAC** Windows desktop application.

---

## Project Structure

*   **`GestureSense.Shared/`**: A shared .NET 8 / MAUI class library containing core telemetry data models, GSP packet serialization logic, and command parser definitions.
*   **`GestureSense.WatchOS/`**: The watchOS target project focusing on iOS/watchOS CoreMotion integration and CoreBluetooth GATT server hosting.
*   **`GestureSense.WearOS/`**: The WearOS target project focusing on Android SensorManager and BluetoothGattServer hosting.

---

## BLE Peripheral Role & GSP Protocol Emulation

Smartwatches run a local BLE GATT server advertising the standard **Movesense GATT SensorData Protocol (GSP)** service and characteristics:

*   **Service UUID**: `34802252-7185-4d5d-b431-630e7050e8f0`
*   **Write (Command) Characteristic UUID**: `34800001-7185-4d5d-b431-630e7050e8f0` (Properties: `Write`, `WriteWithoutResponse`)
*   **Notify (Data) Characteristic UUID**: `34800002-7185-4d5d-b431-630e7050e8f0` (Properties: `Notify`)

### GSP Communication Flow

```
+-------------+                +-------------+
| AllyAAC |                | Smartwatch  |
|  (Central)  |                | (Peripheral)|
+-------------+                +-------------+
       |                              |
       |----- Connect via BLE ------->| (Starts GATT Server)
       |<---- Discover Services ------|
       |                              |
       |--- Enable CCCD Notify ------>| (Enables notifications)
       |                              |
       |-- Write: Subscribe Command ->| (Payload: 0x01 + Ref + "/Meas/IMU9/52")
       |                              |
       |                              | [Reads Internal Sensors at 52Hz]
       |                              | [Serializes to GSP Binary Packets]
       |<-- Stream: Notification -----| (Payload: 0x02 + Ref + Float Stream)
```

---

## Platform Issues & Workarounds

### 1. ECG (Electrocardiogram)
*   **The Issue**: Both Apple and Google block third-party developers from accessing raw, real-time ECG sensor feeds due to medical regulations.
    *   **watchOS**: Apple Watch only allows manual, user-initiated 30-second recordings in the native ECG app. Access via HealthKit (`HKElectrocardiogram`) is strictly read-only after completion.
    *   **WearOS**: Standard WearOS health APIs do not expose raw ECG waveforms. Access is locked behind manufacturer-specific SDKs (e.g., Samsung Health Sensor SDK) which require corporate partnerships.
*   **Workarounds**:
    1.  **Fallback to Heart Rate (BPM/PPG)**: Mimic ECG endpoints by streaming heart rate frequency metrics or optical PPG heart rate signals, which are widely available in real-time on both platforms.
    2.  **Mock ECG Streaming**: For simulation and UI validation, stream synthetic ECG waveforms (e.g. generating a standard PQRST waveform) at 250Hz.
    3.  **Historical Syncing**: For analytical app components, read recorded ECG data post-hoc via HealthKit or Health Connect and export to CSV.

### 2. Magnetometer (Compass)
*   **The Issue**: 
    *   **watchOS**: Although Apple Watch Series 5+ contains a compass, raw magnetometer data access is restricted in standard watchOS CoreMotion APIs.
    *   **WearOS**: Many budget and fitness-focused WearOS watches omit the physical magnetometer hardware entirely.
*   **Workarounds**:
    1.  **Zero-Fill Channel**: If a magnetometer is missing or restricted, the GSP packet parser is kept happy by transmitting zero values (`0.0f`) on the `MagX`, `MagY`, and `MagZ` channels.
    2.  **Sensor Fusion (Orientation)**: Estimate magnetic heading changes by fusing accelerometer and gyroscope data (using Madgwick or Mahony filters) and project dummy magnetometer fields to emulate rotation.

### 3. Background BLE Advertising (Keep-Alive)
*   **The Issue**: Power management on watches suspends custom BLE GATT servers and stops advertising when the screen turns off.
*   **Workarounds**:
    *   **watchOS**: Launch an active **Workout Session** (`HKWorkoutSession`) to keep the watchOS app active in the foreground and maintain BLE operation.
    *   **WearOS**: Start a **Foreground Service** with a permanent notification and request a Wake Lock (`PowerManager.WakeLock`) to prevent CPU suspension.

---

## Shared Code Architecture (.NET MAUI)

To maximize code sharing, we use a **Shared Class Library**:
1.  **Models**: `SensorDataModel.cs` represents Acceleration, Angular Velocity, and Magnetic Field channels.
2.  **Serializers**: `GspSerializer.cs` packages float arrays into little-endian byte streams using both structured array formats and flat interleaved streams.
3.  **Protocol Handlers**: `GspCommandHandler.cs` processes incoming write requests (Subscribe/Unsubscribe/Hello) and triggers telemetry capture.
