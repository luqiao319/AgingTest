using Common.SerialportHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AgingTest.Views
{
    public partial class UC_LoadDevice : UserControl
    {
        private readonly List<GjdaDeviceItem> _devList = new();
        private GlobalDeviceConfig _globalCfg;
        private readonly DispatcherTimer[] _devTimers = new DispatcherTimer[5];
        public ObservableCollection<string> ComPortList { get; set; } = new();
        public ObservableCollection<int> BaudList { get; set; } = new()
        {
            9600,19200,38400,57600,115200
        };

        public UC_LoadDevice()
        {
            InitializeComponent();
            _globalCfg = LoadConfigFromJsonFile();
            InitDevices();
            LoadConfigToUI();
            RefreshComList();
            for (int i = 1; i <= 4; i++)
            {
                _devTimers[i] = new DispatcherTimer();
                _devTimers[i].Interval = TimeSpan.FromSeconds(5);
                int devId = i;
                _devTimers[i].Tick += (s, e) => AutoReadData(devId);
            }
        }

        #region Json 读写
        private void SaveConfigToJsonFile()
        {
            var opt = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_globalCfg, opt);
            File.WriteAllText("gjda_config.json", json);
        }
        private GlobalDeviceConfig LoadConfigFromJsonFile()
        {
            if (!File.Exists("gjda_config.json"))
            {
                var newCfg = new GlobalDeviceConfig();
                for (int i = 1; i <= 4; i++)
                {
                    newCfg.Devices.Add(new DeviceConfig
                    {
                        DevId = i,
                        ComPort = "COM1",
                        BaudRate = 9600,
                        SlaveAddr = 1,
                        Channels = new List<ChannelConfig>()
                    });
                }
                return newCfg;
            }
            string jsonTxt = File.ReadAllText("gjda_config.json");
            return JsonSerializer.Deserialize<GlobalDeviceConfig>(jsonTxt) ?? new GlobalDeviceConfig();
        }
        private void SaveDeviceSerialConfig(int devId, string com, int baud, byte slaveAddr)
        {
            var devCfg = _globalCfg.Devices.FirstOrDefault(d => d.DevId == devId);
            if (devCfg == null)
            {
                devCfg = new DeviceConfig { DevId = devId };
                _globalCfg.Devices.Add(devCfg);
            }
            devCfg.ComPort = com;
            devCfg.BaudRate = baud;
            devCfg.SlaveAddr = slaveAddr;
            SaveConfigToJsonFile();
        }
        private void SaveDeviceChannelConfig(int devId, List<ChannelConfig> chList)
        {
            var devCfg = _globalCfg.Devices.FirstOrDefault(d => d.DevId == devId);
            if (devCfg == null)
            {
                devCfg = new DeviceConfig { DevId = devId };
                _globalCfg.Devices.Add(devCfg);
            }
            devCfg.Channels = chList;
            SaveConfigToJsonFile();
        }
        #endregion

        private void InitDevices()
        {
            _devList.Clear();
            foreach (var cfg in _globalCfg.Devices)
            {
                _devList.Add(new GjdaDeviceItem
                {
                    DevId = cfg.DevId,
                    PortName = cfg.ComPort,
                    BaudRate = cfg.BaudRate,
                    SlaveAddr = cfg.SlaveAddr,
                    IsConnected = false
                });
            }
        }

        private int GetDeviceIdBySender(object sender)
        {
            if (sender is Button btn && btn.Name.Length >= 6)
            {
                var idChar = btn.Name.Substring(6, 1);
                if (int.TryParse(idChar, out int devId))
                    return devId;
            }
            return -1;
        }

        private void RefreshComList()
        {
            ComPortList.Clear();
            var realPorts = SerialPort.GetPortNames();
            foreach (var p in realPorts)
                ComPortList.Add(p);
            cbbCom1.ItemsSource = ComPortList;
            cbbCom2.ItemsSource = ComPortList;
            cbbCom3.ItemsSource = ComPortList;
            cbbCom4.ItemsSource = ComPortList;

            cbbBaud1.ItemsSource = BaudList;
            cbbBaud2.ItemsSource = BaudList;
            cbbBaud3.ItemsSource = BaudList;
            cbbBaud4.ItemsSource = BaudList;
        }

        #region 设备连接断开
        private void BtnDevConnect_Click(object sender, RoutedEventArgs e)
        {
            int devId = GetDeviceIdBySender(sender);
            if (devId < 1)
            {
                MessageBox.Show("获取设备编号失败！");
                return;
            }
            string comPort = GetSelectedCom(devId);
            int baudRate = GetSelectedBaud(devId);
            TextBox txtSlave = FindName($"TxtDev{devId}_SlaveAddr") as TextBox;
            if (txtSlave == null)
            {
                MessageBox.Show("找不到从站地址输入框");
                return;
            }
            if (!byte.TryParse(txtSlave.Text, out byte slaveAddr))
            {
                MessageBox.Show("从站地址必须是数字！");
                return;
            }
            var dev = GetDevById(devId);
            var tbStatus = GetDevStatusTb(devId);
            if (dev.IsConnected)
            {
                MessageBox.Show($"设备{devId}已经连接");
                return;
            }
            try
            {
                dev.DeviceHandle = new GJDA10032();
                dev.PortName = comPort;
                dev.SlaveAddr = slaveAddr;
                dev.BaudRate = baudRate;
                dev.DeviceHandle.Open(dev.PortName, dev.BaudRate, dev.SlaveAddr);
                dev.DeviceHandle.DeviceAddress = dev.SlaveAddr;
                bool connOk = dev.DeviceHandle.CheckConnect();
                if (!connOk)
                {
                    dev.DeviceHandle.Close();
                    dev.DeviceHandle = null;
                    MessageBox.Show($"设备{devId}通讯检测失败！检查串口、波特率、485接线、从站地址");
                    return;
                }
                dev.IsConnected = true;
                if (tbStatus != null)
                {
                    tbStatus.Text = $"设备状态：已连接，地址:{dev.SlaveAddr}";
                    tbStatus.Foreground = Brushes.Green;
                }
                _devTimers[devId].Start();
                SaveDeviceSerialConfig(devId, comPort, baudRate, slaveAddr);
                MessageBox.Show($"设备{devId}连接成功，参数已保存，开始自动采集");
            }
            catch (Exception ex)
            {
                dev.DeviceHandle?.Close();
                dev.DeviceHandle = null;
                MessageBox.Show($"设备{devId}连接异常：{ex.Message}");
            }
        }
        private void BtnDevDisconnect_Click(object sender, RoutedEventArgs e)
        {
            int devId = GetDeviceIdBySender(sender);
            if (devId < 1) return;
            var dev = GetDevById(devId);
            var tbStatus = GetDevStatusTb(devId);
            if (!dev.IsConnected)
            {
                MessageBox.Show($"设备{devId}未连接");
                return;
            }
            try
            {
                dev.DeviceHandle?.Close();
            }
            catch { }
            dev.DeviceHandle = null;
            dev.IsConnected = false;
            _devTimers[devId].Stop();
            if (tbStatus != null)
            {
                tbStatus.Text = "设备状态：未连接";
                tbStatus.Foreground = Brushes.Red;
            }
            MessageBox.Show($"设备{devId}已断开，停止自动采集");
        }
        #endregion

        #region 辅助读取UI控件值
        private string GetSelectedCom(int devId)
        {
            return devId switch
            {
                1 => cbbCom1.SelectedItem?.ToString(),
                2 => cbbCom2.SelectedItem?.ToString(),
                3 => cbbCom3.SelectedItem?.ToString(),
                4 => cbbCom4.SelectedItem?.ToString(),
                _ => null
            };
        }
        private int GetSelectedBaud(int devId)
        {
            var cbb = devId switch
            {
                1 => cbbBaud1,
                2 => cbbBaud2,
                3 => cbbBaud3,
                4 => cbbBaud4,
                _ => null
            };
            if (cbb != null && int.TryParse(cbb.SelectedItem?.ToString(), out int b))
                return b;
            return 9600;
        }
        #endregion

        #region 辅助获取对象
        private GjdaDeviceItem GetDevById(int devId)
        {
            if (devId < 1 || devId > _devList.Count)
                throw new Exception("设备编号错误");
            return _devList[devId - 1];
        }
        private TextBlock GetDevStatusTb(int devId)
        {
            return this.FindName($"TbDev{devId}Status") as TextBlock;
        }
        private WrapPanel GetDevWrapPanel(int devId)
        {
            return this.FindName($"WrapDev{devId}Data") as WrapPanel;
        }
        #endregion

        private void LoadConfigToUI()
        {
            foreach (var devCfg in _globalCfg.Devices)
            {
                switch (devCfg.DevId)
                {
                    case 1:
                        cbbCom1.SelectedItem = devCfg.ComPort;
                        cbbBaud1.SelectedItem = devCfg.BaudRate;
                        ((TextBox)FindName("TxtDev1_SlaveAddr")).Text = devCfg.SlaveAddr.ToString();
                        break;
                    case 2:
                        cbbCom2.SelectedItem = devCfg.ComPort;
                        cbbBaud2.SelectedItem = devCfg.BaudRate;
                        ((TextBox)FindName("TxtDev2_SlaveAddr")).Text = devCfg.SlaveAddr.ToString();
                        break;
                    case 3:
                        cbbCom3.SelectedItem = devCfg.ComPort;
                        cbbBaud3.SelectedItem = devCfg.BaudRate;
                        ((TextBox)FindName("TxtDev3_SlaveAddr")).Text = devCfg.SlaveAddr.ToString();
                        break;
                    case 4:
                        cbbCom4.SelectedItem = devCfg.ComPort;
                        cbbBaud4.SelectedItem = devCfg.BaudRate;
                        ((TextBox)FindName("TxtDev4_SlaveAddr")).Text = devCfg.SlaveAddr.ToString();
                        break;
                }
            }
        }

        private void AutoReadData(int devId)
        {
            var dev = GetDevById(devId);
            var wrapPanel = GetDevWrapPanel(devId);
            if (wrapPanel == null || !dev.IsConnected || dev.DeviceHandle == null)
            {
                _devTimers[devId].Stop();
                return;
            }
            try
            {
                var res = dev.DeviceHandle.ReadAll32Channel();
                wrapPanel.Children.Clear();
                foreach (var ch in res.Channels)
                {
                    var tile = new Border { Style = this.FindResource("DataTile") as Style };
                    tile.Tag = new Tuple<int, int>(devId, ch.ChNo);
                    tile.MouseDown += Tile_MouseDown;
                    var sp = new StackPanel();
                    sp.Children.Add(new TextBlock { Text = $"CH{ch.ChNo}", FontSize = 11 });
                    sp.Children.Add(new TextBlock { Text = $"U:{ch.Voltage:F2} V", FontSize = 12 });
                    sp.Children.Add(new TextBlock { Text = $"I:{ch.Current:F3} A", FontSize = 12 });
                    tile.Child = sp;
                    wrapPanel.Children.Add(tile);
                }
            }
            catch (Exception ex)
            {
                _devTimers[devId].Stop();
                dev.IsConnected = false;
                dev.DeviceHandle?.Close();
                dev.DeviceHandle = null;
                var tbStatus = GetDevStatusTb(devId);
                if (tbStatus != null)
                {
                    tbStatus.Text = "设备状态：通信异常";
                    tbStatus.Foreground = Brushes.Red;
                }
                MessageBox.Show($"设备{devId}自动采集失败：{ex.Message}\n已停止自动刷新");
            }
        }

        private void BtnTestAddr_Click(object sender, RoutedEventArgs e)
        {
            int devId = GetDeviceIdBySender(sender);
            if (devId < 1)
            {
                MessageBox.Show("获取设备编号失败，无法执行操作！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dev = GetDevById(devId);
            string comPort = GetSelectedCom(devId);
            int baud = GetSelectedBaud(devId);
            if (string.IsNullOrWhiteSpace(comPort))
            {
                MessageBox.Show("请先选择串口！");
                return;
            }
            if (!byte.TryParse(((TextBox)FindName($"TxtDev{devId}_SlaveAddr")).Text, out byte slaveAddr))
            {
                MessageBox.Show("从站地址格式错误！");
                return;
            }
            var gjda = new GJDA10032();
            try
            {
                gjda.Open(comPort, baud, slaveAddr);
                gjda.DeviceAddress = slaveAddr;
                bool ok = gjda.CheckConnect();
                if (ok)
                {
                    MessageBox.Show("✅ 当前串口、波特率、从站地址通信正常！");
                }
                else
                {
                    MessageBox.Show("❌ 设备无应答，请检查接线与参数");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"通信异常：{ex.Message}");
            }
            finally
            {
                gjda.Close();
            }
        }

        private void BtnWriteParam_Click(object sender, RoutedEventArgs e)
        {
            int devId = GetDeviceIdBySender(sender);
            if (devId < 1) return;
            var dev = GetDevById(devId);
            if (!dev.IsConnected || dev.DeviceHandle == null)
            {
                MessageBox.Show($"设备{devId}未连接，请先连接");
                return;
            }
            var txtLoad = this.FindName($"TxtDev{devId}LoadVal") as TextBox;
            var txtExtra = this.FindName($"TxtDev{devId}ExtraVal") as TextBox;
            string loadVal = txtLoad?.Text ?? "0";
            string extraVal = txtExtra?.Text ?? "0";
            try
            {
                double current = double.TryParse(loadVal, out double d) ? d : 0.5;
                byte extra = byte.TryParse(extraVal, out byte exVal) ? exVal : (byte)0;
                var list16 = new List<GJDA10032.ChSetParam>();
                for (int i = 0; i < 16; i++)
                {
                    list16.Add(new GJDA10032.ChSetParam
                    {
                        Mode = 0,
                        VonVolt = 12.0,
                        SetValue = current,
                        ExtraParam = extra
                    });
                }
                var list17_32 = new List<GJDA10032.ChSetParam>();
                for (int i = 0; i < 16; i++)
                {
                    list17_32.Add(new GJDA10032.ChSetParam
                    {
                        Mode = 0,
                        VonVolt = 12.0,
                        SetValue = current,
                        ExtraParam = extra
                    });
                }
                dev.DeviceHandle.SetBatch1To16Channel(list16, false);
                dev.DeviceHandle.SetBatch17To32Channel(list17_32, false);
                MessageBox.Show($"设备{devId}参数下发成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设备{devId}下发参数失败：{ex.Message}");
            }
        }

        private void BtnReadData_Click(object sender, RoutedEventArgs e)
        {
            int devId = GetDeviceIdBySender(sender);
            if (devId < 1) return;
            var dev = GetDevById(devId);
            var wrapPanel = GetDevWrapPanel(devId);
            if (wrapPanel == null) return;
            if (!dev.IsConnected || dev.DeviceHandle == null)
            {
                MessageBox.Show($"设备{devId}未连接，请先连接");
                return;
            }
            try
            {
                var res = dev.DeviceHandle.ReadAll32Channel();
                wrapPanel.Children.Clear();
                foreach (var ch in res.Channels)
                {
                    var tile = new Border { Style = this.FindResource("DataTile") as Style };
                    tile.Tag = new Tuple<int, int>(devId, ch.ChNo);
                    tile.MouseDown += Tile_MouseDown;
                    var sp = new StackPanel();
                    sp.Children.Add(new TextBlock { Text = $"CH{ch.ChNo}", FontSize = 11 });
                    sp.Children.Add(new TextBlock { Text = $"U:{ch.Voltage:F2} V", FontSize = 12 });
                    sp.Children.Add(new TextBlock { Text = $"I:{ch.Current:F3} A", FontSize = 12 });
                    tile.Child = sp;
                    wrapPanel.Children.Add(tile);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设备{devId}读取实时数据失败：{ex.Message}");
            }
        }

        private void Tile_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border bd && bd.Tag is Tuple<int, int> tagData)
            {
                int devId = tagData.Item1;
                int chNo = tagData.Item2;
                var dev = GetDevById(devId);
                var devCfg = _globalCfg.Devices.First(d => d.DevId == devId);
                var savedCh = devCfg.Channels.FirstOrDefault(c => c.ChNo == chNo);
                var win = new LoadSettingWindow();
                win.ClickChNo = chNo;
                if (savedCh != null)
                {
                    win.OldConfig = new LoadParamResult
                    {
                        ChNo = chNo,
                        ModeText = savedCh.Mode switch
                        {
                            0 => "CC 恒流_慢速",
                            1 => "CV 恒压",
                            2 => "CP 恒功率",
                            3 => "CR 恒电阻",
                            _ => "CC 恒流_慢速"
                        },
                        VonVolt = savedCh.VonVolt,
                        LoadValue = savedCh.SetValue,
                        ExtraParam = savedCh.ExtraParam
                    };
                }
                var dialogRes = win.ShowDialog();
                if (dialogRes == true && win.Result != null)
                {
                    var para = win.Result;
                    byte mode = para.ModeText switch
                    {
                        "CC 恒流_慢速" => 0,
                        "CV 恒压" => 1,
                        "CP 恒功率" => 2,
                        "CR 恒电阻" => 3,
                        _ => 0
                    };
                    if (para.IsSingleChannel)
                    {
                        var chParam = new GJDA10032.ChSetParam
                        {
                            Mode = mode,
                            VonVolt = para.VonVolt,
                            SetValue = para.LoadValue,
                            ExtraParam = para.ExtraParam
                        };
                        dev.DeviceHandle.SetSingleChannel(chNo, chParam, false);
                        var existCh = devCfg.Channels.FirstOrDefault(c => c.ChNo == chNo);
                        if (existCh != null)
                        {
                            existCh.Mode = mode;
                            existCh.VonVolt = para.VonVolt;
                            existCh.SetValue = para.LoadValue;
                            existCh.ExtraParam = para.ExtraParam;
                        }
                        else
                        {
                            devCfg.Channels.Add(new ChannelConfig
                            {
                                ChNo = chNo,
                                Mode = mode,
                                VonVolt = para.VonVolt,
                                SetValue = para.LoadValue,
                                ExtraParam = para.ExtraParam
                            });
                        }
                    }
                    else
                    {
                        var chParam = new GJDA10032.ChSetParam
                        {
                            Mode = mode,
                            VonVolt = para.VonVolt,
                            SetValue = para.LoadValue,
                            ExtraParam = para.ExtraParam
                        };
                        for (int ch = 1; ch <= 32; ch++)
                        {
                            dev.DeviceHandle.SetSingleChannel(ch, chParam, false);
                        }
                        devCfg.Channels.Clear();
                        for (int ch = 1; ch <= 32; ch++)
                        {
                            devCfg.Channels.Add(new ChannelConfig
                            {
                                ChNo = ch,
                                Mode = mode,
                                VonVolt = para.VonVolt,
                                SetValue = para.LoadValue,
                                ExtraParam = para.ExtraParam
                            });
                        }
                    }
                    SaveConfigToJsonFile();
                    RenderDeviceChannels(devId);
                }
            }
        }

        private void RenderDeviceChannels(int devId)
        {
            var wrapPanel = GetDevWrapPanel(devId);
            if (wrapPanel == null) return;
            wrapPanel.Children.Clear();
            var devConfig = _globalCfg.Devices.FirstOrDefault(d => d.DevId == devId);
            if (devConfig == null) return;
            for (int ch = 1; ch <= 32; ch++)
            {
                var savedCh = devConfig.Channels.FirstOrDefault(c => c.ChNo == ch);
                byte mode = savedCh?.Mode ?? 0;
                double vonVolt = savedCh?.VonVolt ?? 0;
                double setVal = savedCh?.SetValue ?? 0;
                byte extra = savedCh?.ExtraParam ?? 0;
                var border = new Border
                {
                    Style = FindResource("DataTile") as Style,
                    Tag = new Tuple<int, int>(devId, ch)
                };
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = $"CH{ch}" });
                sp.Children.Add(new TextBlock { Text = $"模式:{mode}" });
                sp.Children.Add(new TextBlock { Text = $"电压:{vonVolt:F2}" });
                sp.Children.Add(new TextBlock { Text = $"设定:{setVal:F2}" });
                sp.Children.Add(new TextBlock { Text = $"附加:{extra}" });
                border.Child = sp;
                border.MouseDown += Tile_MouseDown;
                wrapPanel.Children.Add(border);
            }
        }

        private void BtnAllChannelOn_Click(object sender, RoutedEventArgs e)
        {
            int devId = GetDeviceIdBySender(sender);
            if (devId < 1) return;
            var dev = GetDevById(devId);
            if (!dev.IsConnected || dev.DeviceHandle == null)
            {
                MessageBox.Show($"设备{devId}未连接，请先连接");
                return;
            }
            try
            {
                var currList = Enumerable.Repeat(0.5d, 32).ToList();
                dev.DeviceHandle.SetAllChannelCcSlowCurrent(currList);
                MessageBox.Show($"设备{devId}全部通道开启拉载(0.5A)");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"开启失败：{ex.Message}");
            }
        }

        private void BtnAllChannelOff_Click(object sender, RoutedEventArgs e)
        {
            int devId = GetDeviceIdBySender(sender);
            if (devId < 1) return;
            var dev = GetDevById(devId);
            if (!dev.IsConnected || dev.DeviceHandle == null)
            {
                MessageBox.Show($"设备{devId}未连接，请先连接");
                return;
            }
            try
            {
                var currList = Enumerable.Repeat(0.0d, 32).ToList();
                dev.DeviceHandle.SetAllChannelCcSlowCurrent(currList);
                MessageBox.Show($"设备{devId}全部通道关闭拉载(0A)");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"关闭失败：{ex.Message}");
            }
        }


        // 页面切换离开时触发
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            DisconnectAllDevice();
        }

        /// <summary>
        /// 全部4台电子负载统一断开
        /// </summary>
        /// <summary>
        /// 全部4台电子负载统一断开
        /// </summary>
        private void DisconnectAllDevice()
        {
            //设备1
            if (_devList[0] != null && _devList[0].IsConnected)
            {
                _devList[0].DeviceHandle?.Close(); // 调用驱动实例关闭串口
                _devList[0].IsConnected = false;
                TbDev1Status.Text = "设备状态：未连接";
                TbDev1Status.Foreground = Brushes.Red;
            }
            //设备2
            if (_devList[1] != null && _devList[1].IsConnected)
            {
                _devList[1].DeviceHandle?.Close();
                _devList[1].IsConnected = false;
                TbDev2Status.Text = "设备状态：未连接";
                TbDev2Status.Foreground = Brushes.Red;
            }
            //设备3
            if (_devList[2] != null && _devList[2].IsConnected)
            {
                _devList[2].DeviceHandle?.Close();
                _devList[2].IsConnected = false;
                TbDev3Status.Text = "设备状态：未连接";
                TbDev3Status.Foreground = Brushes.Red;
            }
            //设备4
            if (_devList[3] != null && _devList[3].IsConnected)
            {
                _devList[3].DeviceHandle?.Close();
                _devList[3].IsConnected = false;
                TbDev4Status.Text = "设备状态：未连接";
                TbDev4Status.Foreground = Brushes.Red;
            }
        }



        //protected override void OnUnloaded(RoutedEventArgs e)
        //{
        //    for (int i = 1; i <= 4; i++)
        //    {
        //        _devTimers[i]?.Stop();
        //    }
        //    foreach (var d in _devList)
        //    {
        //        try
        //        {
        //            d.DeviceHandle?.Close();
        //        }
        //        catch { }
        //        d.DeviceHandle = null;
        //        d.IsConnected = false;
        //    }
        //    base.OnUnloaded(e);
        //}

        #region 实体类
        public class GjdaDeviceItem
        {
            public int DevId { get; set; }
            public string? PortName { get; set; }
            public int BaudRate { get; set; }
            public byte SlaveAddr { get; set; }
            public bool IsConnected { get; set; }
            public GJDA10032? DeviceHandle { get; set; }
        }
        public class ChannelConfig
        {
            public int ChNo { get; set; }
            public byte Mode { get; set; }
            public double VonVolt { get; set; }
            public double SetValue { get; set; }
            public byte ExtraParam { get; set; }
        }
        public class DeviceConfig
        {
            public int DevId { get; set; }
            public string ComPort { get; set; } = "COM1";
            public int BaudRate { get; set; } = 9600;
            public byte SlaveAddr { get; set; } = 1;
            public List<ChannelConfig> Channels { get; set; } = new();
        }
        public class GlobalDeviceConfig
        {
            public List<DeviceConfig> Devices { get; set; } = new();
        }
        #endregion
    }
}
