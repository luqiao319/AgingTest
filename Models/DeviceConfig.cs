using AgingTest.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AgingTest.Views
{
    /// <summary>单台设备参数配置</summary>
    public class DeviceConfig
    {
        public int DevId { get; set; }
        public string LoadParam { get; set; } = "0";
        public string ExtraParam { get; set; } = "";
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 9600;
        public byte SlaveAddr { get; set; } = 0x01;
    }

        

    /// <summary>全局4台设备配置集合，用于JSON序列化</summary>
    public class GlobalDeviceConfig
    {
        public List<DeviceConfig> Devices { get; set; } = new();
    }

    /// <summary>JSON读写工具</summary>
    public static class ConfigJsonHelper
    {
        private const string ConfigFileName = "DeviceConfig.json";
        public static void Save(GlobalDeviceConfig cfg)
        {
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFileName, json);
        }
        public static GlobalDeviceConfig Load()
        {
            if (!File.Exists(ConfigFileName))
            {
                // 初始化4台设备默认参数
                var def = new GlobalDeviceConfig();
                for (int i = 1; i <= 4; i++)
                {
                    def.Devices.Add(new DeviceConfig { DevId = i });
                }
                Save(def);
                return def;
            }
            var txt = File.ReadAllText(ConfigFileName);
            return JsonSerializer.Deserialize<GlobalDeviceConfig>(txt)!;
        }
    }
}
