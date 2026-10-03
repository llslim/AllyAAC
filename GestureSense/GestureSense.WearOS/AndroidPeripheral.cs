using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using GestureSense.Shared;
using Java.Util;

namespace GestureSense.WearOS
{
    public class AndroidPeripheral : BluetoothGattServerCallback
    {
        private BluetoothGattServer? _gattServer;
        private BluetoothGattCharacteristic? _notifyChar;
        private BluetoothLeAdvertiser? _advertiser;

        public void StartServer(Context context)
        {
            var bluetoothManager = (BluetoothManager?)context.GetSystemService(Context.BluetoothService);
            if (bluetoothManager == null) return;

            _gattServer = bluetoothManager.OpenGattServer(context, this);
            if (_gattServer == null) return;

            var serviceUuid = UUID.FromString("34802252-7185-4d5d-b431-630e7050e8f0");
            var writeUuid = UUID.FromString("34800001-7185-4d5d-b431-630e7050e8f0");
            var notifyUuid = UUID.FromString("34800002-7185-4d5d-b431-630e7050e8f0");

            var service = new BluetoothGattService(serviceUuid, GattServiceType.Primary);

            var writeChar = new BluetoothGattCharacteristic(writeUuid, GattProperty.Write | GattProperty.WriteNoResponse, GattPermission.Write);
            _notifyChar = new BluetoothGattCharacteristic(notifyUuid, GattProperty.Notify, GattPermission.Read);

            service.AddCharacteristic(writeChar);
            service.AddCharacteristic(_notifyChar);

            _gattServer.AddService(service);

            _advertiser = bluetoothManager.Adapter?.BluetoothLeAdvertiser;
            if (_advertiser == null) return;

            var settings = new AdvertiseSettings.Builder()
                .SetAdvertiseMode(AdvertiseMode.LowLatency)
                .SetTxPowerLevel(AdvertiseTxPower.High)
                .SetConnectable(true)
                .Build();

            var data = new AdvertiseData.Builder()
                .SetIncludeDeviceName(true)
                .AddServiceUuid(new ParcelUuid(serviceUuid))
                .Build();

            _advertiser.StartAdvertising(settings, data, new MyAdvertiseCallback());
        }

        public override void OnCharacteristicWriteRequest(BluetoothDevice? device, int requestId, BluetoothGattCharacteristic? characteristic, bool preparedWrite, bool responseNeeded, int offset, byte[]? value)
        {
            base.OnCharacteristicWriteRequest(device, requestId, characteristic, preparedWrite, responseNeeded, offset, value);

            if (responseNeeded && _gattServer != null && device != null)
            {
                _gattServer.SendResponse(device, requestId, GattStatus.Success, offset, null);
            }

            if (value != null && value.Length > 0)
            {
                // Parse GSP Subscribe (0x01) / Unsubscribe (0x02) command
                byte command = value[0];
                if (command == 0x01)
                {
                    // Start reading Android SensorManager telemetry at 52Hz
                }
                else if (command == 0x02)
                {
                    // Stop reading telemetry
                }
            }
        }

        private class MyAdvertiseCallback : AdvertiseCallback
        {
            public override void OnStartSuccess(AdvertiseSettings? settingsInEffect)
            {
                base.OnStartSuccess(settingsInEffect);
            }

            public override void OnStartFailure(AdvertiseFailure errorCode)
            {
                base.OnStartFailure(errorCode);
            }
        }
    }
}
