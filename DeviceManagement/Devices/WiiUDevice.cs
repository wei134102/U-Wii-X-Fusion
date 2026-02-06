using System;
using U_Wii_X_Fusion.DeviceManagement.Interfaces;

namespace U_Wii_X_Fusion.DeviceManagement.Devices
{
    public class WiiUDevice : IDevice
    {
        public string DeviceId { get; private set; }
        public string Name { get; private set; }
        public string Platform { get; private set; }
        public string Status { get; private set; }
        public bool IsConnected { get; private set; }

        public event EventHandler<DeviceStatusChangedEventArgs> StatusChanged;

        public WiiUDevice(string deviceId, string name)
        {
            DeviceId = deviceId;
            Name = name;
            Platform = "Wii U";
            Status = "未连接";
            IsConnected = false;
        }

        public void Connect()
        {
            if (IsConnected)
                return;

            string oldStatus = Status;
            Status = "连接中...";
            OnStatusChanged(oldStatus, Status);

            // 模拟连接过程
            System.Threading.Thread.Sleep(1000);

            Status = "已连接";
            IsConnected = true;
            OnStatusChanged(oldStatus, Status);
        }

        public void Disconnect()
        {
            if (!IsConnected)
                return;

            string oldStatus = Status;
            Status = "断开中...";
            OnStatusChanged(oldStatus, Status);

            // 模拟断开过程
            System.Threading.Thread.Sleep(500);

            Status = "未连接";
            IsConnected = false;
            OnStatusChanged(oldStatus, Status);
        }

        public void RefreshStatus()
        {
            // 模拟刷新状态
            if (IsConnected)
            {
                Status = "已连接";
            }
            else
            {
                Status = "未连接";
            }
        }

        protected virtual void OnStatusChanged(string oldStatus, string newStatus)
        {
            StatusChanged?.Invoke(this, new DeviceStatusChangedEventArgs
            {
                DeviceId = DeviceId,
                OldStatus = oldStatus,
                NewStatus = newStatus
            });
        }
    }
}
