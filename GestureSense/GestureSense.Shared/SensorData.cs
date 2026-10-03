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
