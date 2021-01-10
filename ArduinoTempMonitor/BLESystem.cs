using BLE;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;

namespace ArduinoTempMonitor
{
    public class BLESystem : ViewModel
    {
        DeviceWatcher deviceWatcher;
        BluetoothLEDevice currentDevice;
        IReadOnlyList<GattDeviceService> services;
        IReadOnlyList<GattCharacteristic> characteristics;

        bool _isDiscovering = false;
        public bool IsDiscovering
        {
            get => _isDiscovering;
            private set => SetProperty(ref _isDiscovering, value);
        }

        bool _isReading = false;
        public bool IsReading
        {
            get => _isReading;
            private set => SetProperty(ref _isReading, value);
        }

        bool _isConnected = false;
        public bool IsConnected
        {
            get => _isConnected;
            private set => SetProperty(ref _isConnected, value);
        }

        public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; private set; } = new ObservableCollection<DiscoveredDevice>();

        public event EventHandler<string> OnLog;
        public event EventHandler<string> OnRead;
        public event EventHandler<IList<DiscoveredDevice>> OnWatcherStop;
        public event EventHandler<DiscoveredDevice> OnWatcherUpdate;
        public event EventHandler<DiscoveredDevice> OnWatcherRemove;
        public event EventHandler<IList<DiscoveredDevice>> OnWatcherEnumerationComplete;
        public event EventHandler<DiscoveredDevice> OnWatcherAdd;
        //public event EventHandler<IList<DiscoveredDevice>> OnDevicesUpdate;

        public BLESystem()
        {
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };

            // BT_Code: Example showing paired and non-paired in a single query.
            string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

            deviceWatcher =
                    DeviceInformation.CreateWatcher(
                        aqsAllBluetoothLEDevices,
                        requestedProperties,
                        DeviceInformationKind.AssociationEndpoint);

            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;

            deviceWatcher.Start();
        }

        void Log(string s)
        {
            Debug.WriteLine(s);
            OnLog?.Invoke(this, s);
        }

        public void StopWatching()
        {
            deviceWatcher.Stop();
        }

        private void DeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {
            Log("DeviceWatcher Stopped");
            OnWatcherStop?.Invoke(this, null);
        }

        private void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            Log("DeviceWatcher Enumeration Completed");
            OnWatcherEnumerationComplete?.Invoke(this, DiscoveredDevices);
        }

        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            Log("DeviceWatcher Device Added");
            var device = new DiscoveredDevice(args);
            DiscoveredDevices.Add(device);
            OnWatcherAdd?.Invoke(this, device);
        }

        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            Log("DeviceWatcher Device Updated");
            var device = DiscoveredDevices.FirstOrDefault(x => x.Id == args.Id);
            if (device != null)
            {
                device.Update(args);
                OnWatcherUpdate?.Invoke(this, device);
            }
        }

        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            Log("DeviceWatcher Device Removed");

            var device = DiscoveredDevices.FirstOrDefault(x => x.Id == args.Id);
            if (device != null)
            {
                DiscoveredDevices.Remove(device);
                OnWatcherRemove?.Invoke(this, device);
            }
        }

        public async Task WriteAsync(GattCharacteristic characteristic, string command)
        {
            var writeBuffer = CryptographicBuffer.ConvertStringToBinary(command,
                BinaryStringEncoding.Utf8);

            try
            {
                // BT_Code: Writes the value from the buffer to the characteristic.
                var result = await characteristic.WriteValueWithResultAsync(writeBuffer);

                if (result.Status == GattCommunicationStatus.Success)
                {
                    Log($"Write to characteristic successful for: {command}");
                    return;
                }
                else
                {
                    Log($"Write to characteristic fail for: {command} -- {result.Status}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Log($"Error writing characteristic: {ex}");
            }
        }

        public async Task ReadAsync(GattCharacteristic characteristic)
        {
            if (IsReading) return;
            IsReading = true;
            try
            {
                while (IsReading)
                {
                    var result = await characteristic.ReadValueAsync();
                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        byte[] data = new byte[result.Value.Length];
                        Windows.Storage.Streams.DataReader.FromBuffer(
                            result.Value).ReadBytes(data);
                        var text = Encoding.UTF8.GetString(data, 0, data.Length);
                        OnRead?.Invoke(this, text);
                    }
                    else
                    {
                        var text = result.Status.ToString();
                        Log(text);
                    }
                }
            }
            finally
            {
                IsReading = false;
            }
        }

        public void StopReading()
        {
            IsReading = false;
        }

        public async Task<IReadOnlyList<GattDeviceService>> QueryServicesAsync(DiscoveredDevice d)
        {
            return await QueryServicesAsync(d.Id);
        }

        public async Task<IReadOnlyList<GattDeviceService>> QueryServicesAsync(string id)
        {
            var device = await BluetoothLEDevice.FromIdAsync(id);
            if (device == null)
            {
                Log($"Device ID={id} not found. Will not connect.");
                IsConnected = false;
                return null;
            }

            currentDevice = device;
            services = (await currentDevice.GetGattServicesAsync())?.Services;
            if (services != null)
                IsConnected = true;
            return services;
        }

        public async Task<IReadOnlyList<GattCharacteristic>> QueryCharacteristicsAsync(GattDeviceService service)
        {
            if (service == null) return null;
            var characteristics = await service.GetCharacteristicsAsync();
            return this.characteristics = characteristics?.Characteristics;
        }
    }
}
