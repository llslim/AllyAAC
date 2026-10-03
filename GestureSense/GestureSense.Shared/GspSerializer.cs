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

        public static byte[] SerializeImu6Flat(uint timestamp, SensorData data, byte clientRef = 0x01)
        {
            byte[] packet = new byte[6 + (6 * 4)]; // 6-byte header + 24 bytes payload
            
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
            
            return packet;
        }
    }
}
