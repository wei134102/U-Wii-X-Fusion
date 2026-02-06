namespace U_Wii_X_Fusion.DeviceManagement.Models
{
    public class DeviceInfo
    {
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public string Platform { get; set; }
        public string Status { get; set; }
        public string ConnectionType { get; set; }
        public string IpAddress { get; set; }
        public string UsbPort { get; set; }
        public long FreeSpace { get; set; }
        public long TotalSpace { get; set; }
    }
}
