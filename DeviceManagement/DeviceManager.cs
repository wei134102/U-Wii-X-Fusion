using System;
using System.Collections.Generic;
using System.Linq;
using U_Wii_X_Fusion.DeviceManagement.Devices;
using U_Wii_X_Fusion.DeviceManagement.Interfaces;

namespace U_Wii_X_Fusion.DeviceManagement
{
    public class DeviceManager
    {
        private readonly List<IDevice> _devices;

        public event EventHandler<DeviceStatusChangedEventArgs> DeviceStatusChanged;

        public DeviceManager()
        {
            _devices = new List<IDevice>();
            // 初始化一些模拟设备
            InitializeMockDevices();
        }

        private void InitializeMockDevices()
        {
            // 添加一些模拟设备用于测试
            _devices.Add(new WiiDevice("wii-1", "Wii Console"));
            _devices.Add(new WiiUDevice("wiiu-1", "Wii U Console"));
            _devices.Add(new Xbox360Device("xbox360-1", "Xbox 360 Console"));

            // 订阅设备状态变化事件
            foreach (var device in _devices)
            {
                device.StatusChanged += Device_StatusChanged;
            }
        }

        public List<IDevice> GetAllDevices()
        {
            return _devices;
        }

        public List<IDevice> GetDevicesByPlatform(string platform)
        {
            return _devices.Where(d => d.Platform == platform).ToList();
        }

        public IDevice GetDevice(string deviceId)
        {
            return _devices.Find(d => d.DeviceId == deviceId);
        }

        public void ConnectDevice(string deviceId)
        {
            var device = GetDevice(deviceId);
            if (device != null)
            {
                device.Connect();
            }
        }

        public void DisconnectDevice(string deviceId)
        {
            var device = GetDevice(deviceId);
            if (device != null)
            {
                device.Disconnect();
            }
        }

        public void RefreshDevices()
        {
            foreach (var device in _devices)
            {
                device.RefreshStatus();
            }
        }

        private void Device_StatusChanged(object sender, DeviceStatusChangedEventArgs e)
        {
            // 转发设备状态变化事件
            DeviceStatusChanged?.Invoke(this, e);
        }
    }
}
