using BLE;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace ArduinoTempMonitor
{
    public class SensorSystem : ViewModel
    {
        public double Temperature { get; private set; } = double.NaN;
        public double Humidity { get; private set; } = double.NaN;

        public string FriendlyTemperature => $"{Temperature:N2}C";
        public string FriendlyHumidity => $"{Humidity:N2}%";

        public bool IsConnected { get; private set; } = false;

        public const string SensorName = "DSD TECH";
        public const string ServiceUuidPrefix = "0000ffe0";
        public const string CharacteristicUuidPrefix = "0000ffe1";

        BLESystem BLE;

        GattCharacteristic characteristic = null;
        bool fail = false;

        string lastReadBuffer = "";
        public async Task FindAndConnectAsync()
        {
            fail = false;
            BLE = new BLESystem();
            BLE.OnWatcherAdd += BLE_OnWatcherAdd;
            BLE.OnRead += BLE_OnRead;
            while (characteristic == null && !fail)
            {
                await Task.Delay(500);
            }
        }

        private void BLE_OnRead(object sender, string e)
        {
            lock (lastReadBuffer)
            {
                lastReadBuffer += e;
                Parse();
            }
        }

        void Parse()
        {
            var commaIndex = lastReadBuffer.IndexOf("|"); 
            if (commaIndex < 0) return;
            Parse(lastReadBuffer.Substring(0, commaIndex));
            lastReadBuffer = lastReadBuffer.Substring(commaIndex + 1, lastReadBuffer.Length - 1 - commaIndex);
        }

        void Parse(string chunk)
        {
            try
            {
                if (chunk.Contains("T:") && chunk.Contains("H:"))
                {
                    chunk = chunk
                        .Replace("T:", ",")
                        .Replace("H:", "");
                    var split = chunk.Split(',');
                    Temperature = double.Parse(split[0]);
                    Humidity = double.Parse(split[1]);
                    RaisePropertyChanged(nameof(Temperature));
                    RaisePropertyChanged(nameof(Humidity));
                    RaisePropertyChanged(nameof(FriendlyTemperature));
                    RaisePropertyChanged(nameof(FriendlyHumidity));
                }
            }
            catch { }
        }

        private async void BLE_OnWatcherAdd(object sender, DiscoveredDevice e)
        {
            if (e.Name == SensorName)
            {
                BLE.StopWatching();

                var services = await BLE.QueryServicesAsync(e);
                foreach (var s in services)
                    Debug.WriteLine(s.Uuid);
                var service = services?.FirstOrDefault(x => x.Uuid.ToString().ToLower().StartsWith(ServiceUuidPrefix));
                if (service == null)
                {
                    fail = true;
                    return;
                }

                var characteristics = await BLE.QueryCharacteristicsAsync(service);
                if (characteristics == null)
                {
                    fail = true;
                    return;
                }

                var target = characteristics.FirstOrDefault(x => x.Uuid.ToString().ToLower().StartsWith(CharacteristicUuidPrefix));
                if (target == null)
                {
                    fail = true;
                    return;
                }

                characteristic = target;

                var _ = Task.Run(() => BLE.ReadAsync(characteristic));
            }
        }
    }
}
