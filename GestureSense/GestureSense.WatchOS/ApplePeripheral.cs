using CoreBluetooth;
using Foundation;
using GestureSense.Shared;

namespace GestureSense.WatchOS
{
    public class ApplePeripheral : CBPeripheralManagerDelegate
    {
        private CBPeripheralManager _manager;
        private CBMutableService? _gspService;
        private CBMutableCharacteristic? _writeChar;
        private CBMutableCharacteristic? _notifyChar;

        public ApplePeripheral()
        {
            _manager = new CBPeripheralManager(this, null);
        }

        public override void StateUpdated(CBPeripheralManager peripheral)
        {
            if (peripheral.State == CBPeripheralManagerState.PoweredOn)
            {
                var serviceUuid = CBUUID.FromString("34802252-7185-4d5d-b431-630e7050e8f0");
                var writeUuid = CBUUID.FromString("34800001-7185-4d5d-b431-630e7050e8f0");
                var notifyUuid = CBUUID.FromString("34800002-7185-4d5d-b431-630e7050e8f0");

                _writeChar = new CBMutableCharacteristic(writeUuid, CBCharacteristicProperties.Write | CBCharacteristicProperties.WriteWithoutResponse, null, CBAttributePermissions.Writeable);
                _notifyChar = new CBMutableCharacteristic(notifyUuid, CBCharacteristicProperties.Notify, null, CBAttributePermissions.Readable);

                _gspService = new CBMutableService(serviceUuid, true)
                {
                    Characteristics = new CBCharacteristic[] { _writeChar, _notifyChar }
                };

                _manager.AddService(_gspService);

                var options = new StartAdvertisingOptions
                {
                    LocalName = "Movesense Watch",
                    ServicesUUIDs = new CBUUID[] { serviceUuid }
                };
                _manager.StartAdvertising(options);
            }
        }

        public override void CharacteristicSubscribed(CBPeripheralManager peripheral, CBCentral central, CBCharacteristic characteristic)
        {
            if (_notifyChar != null && characteristic.UUID == _notifyChar.UUID)
            {
                // Start streaming IMU/telemetry data from CoreMotion using GspSerializer
            }
        }

        public override void CharacteristicUnsubscribed(CBPeripheralManager peripheral, CBCentral central, CBCharacteristic characteristic)
        {
            if (_notifyChar != null && characteristic.UUID == _notifyChar.UUID)
            {
                // Stop streaming data
            }
        }
    }
}
