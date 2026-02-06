using System;

namespace U_Wii_X_Fusion.DeviceManagement.Interfaces
{
    public interface IDevice
    {
        string DeviceId { get; }
        string Name { get; }
        string Platform { get; }
        string Status { get; }
        bool IsConnected { get; }

        void Connect();
        void Disconnect();
        void RefreshStatus();
        event EventHandler<DeviceStatusChangedEventArgs> StatusChanged;
    }

    public class DeviceStatusChangedEventArgs : EventArgs
    {
        public string DeviceId { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
    }
}
