# GestureSense: Technical Deep-Dive & Gemini Developer Guide

This document describes the binary serialization specifications, BLE GATT configuration, and native sensor integration mapping required to build **GestureSense** watchOS and WearOS emulation targets.

---

## 1. GSP Binary Serialization Specification

The GATT SensorData Protocol (GSP) streams binary packets via BLE GATT Notifications. The `GestureSense.Shared` library handles this serialization in native C# to ensure consistent, allocation-free byte generation.

### A. Data Notification Packet (Result Type = 0x02)
When telemetry is streaming, notifications are sent with a 6-byte header followed by the binary sensor payload.

| Byte Offset | Data Type | Field Name | Description |
| :--- | :--- | :--- | :--- |
| **0** | `byte` | `ResultType` | Fixed at `0x02` (indicates telemetry/subscription data). |
| **1** | `byte` | `ClientRef` | Matches the reference ID provided in the subscription request (typically `0x01`). |
| **2 - 5** | `uint32` | `Timestamp` | Little-endian millisecond timestamp from the sensor start. |
| **6+** | `byte[]` | `Payload` | Sensor readings (IMU6 or IMU9). |

### B. Payload Structures

#### 1. Flat Interleaved Format (IMU9)
If the payload is a flat interleaved stream, it consists of 9 consecutive single-precision floats (`float`, 4 bytes each) in little-endian format:

| Float Offset | Bytes | Name | Description |
| :--- | :--- | :--- | :--- |
| **0** | 0 - 3 | `AccX` | Accelerometer X ($m/s^2$) |
| **1** | 4 - 7 | `AccY` | Accelerometer Y ($m/s^2$) |
| **2** | 8 - 11 | `AccZ` | Accelerometer Z ($m/s^2$) |
| **3** | 12 - 15 | `GyroX` | Gyroscope X ($rad/s$) |
| **4** | 16 - 19 | `GyroY` | Gyroscope Y ($rad/s$) |
| **5** | 20 - 23 | `GyroZ` | Gyroscope Z ($rad/s$) |
| **6** | 24 - 27 | `MagX` | Magnetometer X ($\mu T$ or normalized) |
| **7** | 28 - 31 | `MagY` | Magnetometer Y ($\mu T$ or normalized) |
| **8** | 32 - 35 | `MagZ` | Magnetometer Z ($\mu T$ or normalized) |

#### 2. Structured Array Format
If the sensor reports multiple samples per packet, it uses array headers:
*   `[AccCount] + [AccX, AccY, AccZ...] + [GyroCount] + [GyroX, GyroY, GyroZ...] + [MagCount] + [MagX, MagY, MagZ...]`
*   Each count is a single byte (e.g. `0x01` if sending one sample).

---

## 2. Shared C# Implementation Scaffolding

The shared logic is placed under `GestureSense.Shared`. Here are the core classes:

### `SensorDataModel.cs`
```csharp
namespace GestureSense.Shared
{
    public class SensorData
    {
        public uint Timestamp { get; set; }
        public float AccX { get; set; }
        public float AccY { get; set; }
        public float AccZ { get; set; }
        public float GyroX { get; set; }
        public float GyroY { get; set; }
        public float GyroZ { get; set; }
        public float MagX { get; set; }
        public float MagY { get; set; }
        public float MagZ { get; set; }
    }
}
```

### `GspSerializer.cs`
```csharp
using System;

namespace GestureSense.Shared
{
    public static class GspSerializer
    {
        public static byte[] SerializeImu9Flat(uint timestamp, SensorData data, byte clientRef = 0x01)
        {
            byte[] packet = new byte[6 + (9 * 4)]; // 6-byte header + 36 bytes payload
            
            // Header
            packet[0] = 0x02; // Data Notification
            packet[1] = clientRef;
            
            // Timestamp (uint32, Little Endian)
            BitConverter.TryWriteBytes(new Span<byte>(packet, 2, 4), timestamp);
            
            // Floats (Little Endian)
            BitConverter.TryWriteBytes(new Span<byte>(packet, 6, 4), data.AccX);
            BitConverter.TryWriteBytes(new Span<byte>(packet, 10, 4), data.AccY);
            BitConverter.TryWriteBytes(new Span<byte>(packet, 14, 4), data.AccZ);
            
            BitConverter.TryWriteBytes(new Span<byte>(packet, 18, 4), data.GyroX);
            BitConverter.TryWriteBytes(new Span<byte>(packet, 22, 4), data.GyroY);
            BitConverter.TryWriteBytes(new Span<byte>(packet, 26, 4), data.GyroZ);
            
            BitConverter.TryWriteBytes(new Span<byte>(packet, 30, 4), data.MagX);
            BitConverter.TryWriteBytes(new Span<byte>(packet, 34, 4), data.MagY);
            BitConverter.TryWriteBytes(new Span<byte>(packet, 38, 4), data.MagZ);
            
            return packet;
        }
    }
}
```

---

## 3. watchOS (Apple Watch) Native Integration (Swift/C#)

On iOS/watchOS, CoreBluetooth and CoreMotion handle GATT services and sensor feeds.

### BLE GATT Peripheral Scaffolding
Using `CBPeripheralManager` to configure and advertise the GSP service:

```swift
import CoreBluetooth
import CoreMotion

class GspPeripheralManager: NSObject, CBPeripheralManagerDelegate {
    private var peripheralManager: CBPeripheralManager!
    private var gspService: CBMutableService!
    private var writeChar: CBMutableCharacteristic!
    private var notifyChar: CBMutableCharacteristic!
    private var subscribedCentrals = [CBCentral]()
    
    override init() {
        super.init()
        peripheralManager = CBPeripheralManager(delegate: self, queue: nil)
    }
    
    func peripheralManagerDidUpdateState(_ peripheral: CBPeripheralManager) {
        if peripheral.state == .poweredOn {
            let serviceUUID = CBUUID(string: "34802252-7185-4d5d-b431-630e7050e8f0")
            let writeUUID = CBUUID(string: "34800001-7185-4d5d-b431-630e7050e8f0")
            let notifyUUID = CBUUID(string: "34800002-7185-4d5d-b431-630e7050e8f0")
            
            writeChar = CBMutableCharacteristic(type: writeUUID, properties: [.write, .writeWithoutResponse], value: nil, permissions: [.writeable])
            notifyChar = CBMutableCharacteristic(type: notifyUUID, properties: [.notify], value: nil, permissions: [.readable])
            
            gspService = CBMutableService(type: serviceUUID, primary: true)
            gspService.characteristics = [writeChar, notifyChar]
            
            peripheralManager.add(gspService)
            
            // Start advertising (Keep watch app in foreground / active workout)
            peripheralManager.startAdvertising([CBAdvertisementDataServiceUUIDsKey: [serviceUUID], CBAdvertisementDataLocalNameKey: "Movesense Watch"])
        }
    }
    
    func peripheralManager(_ peripheral: CBPeripheralManager, central: CBCentral, didSubscribeTo characteristic: CBCharacteristic) {
        if characteristic.uuid == notifyChar.uuid {
            subscribedCentrals.append(central)
            // Start CoreMotion sensor updates at 50Hz/52Hz
        }
    }
}
```

---

## 4. WearOS (Android Watch) Native Integration (Java/C#)

On WearOS, `BluetoothGattServer` and `SensorEventListener` handle service advertisement and hardware logging.

### BLE GATT Server Scaffolding
Using `BluetoothManager` to start the advertisement:

```java
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothGatt;
import android.bluetooth.BluetoothGattCharacteristic;
import android.bluetooth.BluetoothGattServer;
import android.bluetooth.BluetoothGattServerCallback;
import android.bluetooth.BluetoothGattService;
import android.bluetooth.BluetoothManager;
import android.bluetooth.le.AdvertiseCallback;
import android.bluetooth.le.AdvertiseData;
import android.bluetooth.le.AdvertiseSettings;
import android.bluetooth.le.BluetoothLeAdvertiser;
import android.content.Context;
import android.os.ParcelUuid;
import java.util.UUID;

public class GspGattServer {
    private BluetoothGattServer gattServer;
    private BluetoothGattCharacteristic notifyChar;
    private BluetoothLeAdvertiser advertiser;
    
    public void startServer(Context context) {
        BluetoothManager manager = (BluetoothManager) context.getSystemService(Context.BLUETOOTH_SERVICE);
        gattServer = manager.openGattServer(context, new BluetoothGattServerCallback() {
            @Override
            public void onCharacteristicWriteRequest(BluetoothDevice device, int requestId, BluetoothGattCharacteristic characteristic, boolean preparedWrite, boolean responseNeeded, int offset, byte[] value) {
                super.onCharacteristicWriteRequest(device, requestId, characteristic, preparedWrite, responseNeeded, offset, value);
                // Parse GSP Subscribe/Unsubscribe command (e.g. value[0] == 0x01)
                if (responseNeeded) {
                    gattServer.sendResponse(device, requestId, BluetoothGatt.GATT_SUCCESS, offset, null);
                }
                // Toggle internal Android SensorManager listeners
            }
        });
        
        UUID serviceUuid = UUID.fromString("34802252-7185-4d5d-b431-630e7050e8f0");
        UUID writeUuid = UUID.fromString("34800001-7185-4d5d-b431-630e7050e8f0");
        UUID notifyUuid = UUID.fromString("34800002-7185-4d5d-b431-630e7050e8f0");
        
        BluetoothGattService service = new BluetoothGattService(serviceUuid, BluetoothGattService.SERVICE_TYPE_PRIMARY);
        
        BluetoothGattCharacteristic writeChar = new BluetoothGattCharacteristic(writeUuid, BluetoothGattCharacteristic.PROPERTY_WRITE | BluetoothGattCharacteristic.PROPERTY_WRITE_NO_RESPONSE, BluetoothGattCharacteristic.PERMISSION_WRITE);
        notifyChar = new BluetoothGattCharacteristic(notifyUuid, BluetoothGattCharacteristic.PROPERTY_NOTIFY, BluetoothGattCharacteristic.PERMISSION_READ);
        
        service.addCharacteristic(writeChar);
        service.addCharacteristic(notifyChar);
        gattServer.addService(service);
        
        // Start LE Advertising
        advertiser = manager.getAdapter().getBluetoothLeAdvertiser();
        AdvertiseSettings settings = new AdvertiseSettings.Builder()
                .setAdvertiseMode(AdvertiseSettings.ADVERTISE_MODE_LOW_LATENCY)
                .setTxPowerLevel(AdvertiseSettings.ADVERTISE_TX_POWER_HIGH)
                .setConnectable(true)
                .build();
        AdvertiseData data = new AdvertiseData.Builder()
                .setIncludeDeviceName(true)
                .addServiceUuid(new ParcelUuid(serviceUuid))
                .build();
        
        advertiser.startAdvertising(settings, data, new AdvertiseCallback() {
            @Override
            public void onStartSuccess(AdvertiseSettings settingsInEffect) {
                super.onStartSuccess(settingsInEffect);
            }
        });
    }
}
```
