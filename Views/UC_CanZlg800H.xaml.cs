using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AgingTest.NetCan; // 引入NetCan命名空间
using System.Windows;


namespace AgingTest.Views
{
    public partial class UC_CanZlg800H : UserControl
    {
        // 保存12个CAN卡实例
        private readonly List<CanCardItem> _canCardList = new();
        private readonly string _jsonConfigPath = Path.Combine(Environment.CurrentDirectory, "CanCardConfig.json");
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        public UC_CanZlg800H()
        {
            InitializeComponent();
            Create12CanCards();
            LoadConfigFromJson(); //页面打开加载JSON回显
        }

        /// <summary>动态创建1~12号CAN卡UI卡片</summary>
        /// <summary>动态创建1~12号CAN卡UI卡片</summary>
        private void Create12CanCards()
        {
            // ========== 先循环生成全部12个CAN卡片（移除原来顶部的按钮） ==========
            for (int i = 1; i <= 12; i++)
            {
                // 默认IP初始化：CAN1=192.168.0.10
                var defaultIp = NetCan800HNetConfig.GetPresetIpByDevNo(i);
                var card = new CanCardItem { CanIndex = i, IpAddress = defaultIp };
                _canCardList.Add(card);
                // 卡片容器
                var groupBox = new GroupBox
                {
                    Header = $"CAN{i} - ZLG800H",
                    Width = 420,
                    Margin = new Thickness(8),
                    Tag = i
                };
                var stackMain = new StackPanel
                {
                    Margin = new Thickness(8)
                };
                // ========== 行1：IP地址 ==========
                var rowIp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                rowIp.Children.Add(new TextBlock { Text = "IP地址：", Width = 70, VerticalAlignment = VerticalAlignment.Center });
                var txtIp = new TextBox { Width = 140, Text = defaultIp, Tag = i };
                rowIp.Children.Add(txtIp);
                stackMain.Children.Add(rowIp);
                // ========== 行2：设备编号 + CAN通道 ==========
                var row1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                row1.Children.Add(new TextBlock { Text = "设备编号：", Width = 70, VerticalAlignment = VerticalAlignment.Center });
                var txtDevNo = new TextBox { Width = 80, Text = "0", Tag = i };
                row1.Children.Add(txtDevNo);
                row1.Children.Add(new TextBlock { Text = "CAN通道：", Width = 65, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
                var cbbCh = new ComboBox { Width = 80, Tag = i };
                cbbCh.Items.Add("CH0");
                cbbCh.Items.Add("CH1");
                cbbCh.SelectedIndex = 0;
                row1.Children.Add(cbbCh);
                stackMain.Children.Add(row1);
                // ========== 行3：协议类型 + 仲裁波特率 ==========
                var row2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                row2.Children.Add(new TextBlock { Text = "协议：", Width = 70, VerticalAlignment = VerticalAlignment.Center });
                var cbbProto = new ComboBox { Width = 80, Tag = i };
                cbbProto.Items.Add("CAN2.0");
                cbbProto.Items.Add("CAN FD");
                cbbProto.SelectedIndex = 0;
                row2.Children.Add(cbbProto);
                row2.Children.Add(new TextBlock { Text = "仲裁波特率：", Width = 80, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
                var cbbBaudArb = new ComboBox { Width = 80, Tag = i };
                cbbBaudArb.Items.Add("125K");
                cbbBaudArb.Items.Add("250K");
                cbbBaudArb.Items.Add("500K");
                cbbBaudArb.Items.Add("1M");
                cbbBaudArb.SelectedIndex = 2;
                row2.Children.Add(cbbBaudArb);
                stackMain.Children.Add(row2);
                // ========== 行4：FD数据段波特率 ==========
                var row3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                row3.Children.Add(new TextBlock { Text = "FD波特率：", Width = 70, VerticalAlignment = VerticalAlignment.Center });
                var cbbBaudData = new ComboBox { Width = 80, Tag = i, IsEnabled = false };
                cbbBaudData.Items.Add("2M");
                cbbBaudData.Items.Add("4M");
                cbbBaudData.Items.Add("8M");
                cbbBaudData.SelectedIndex = 0;
                row3.Children.Add(cbbBaudData);
                cbbProto.SelectionChanged += (s, e) =>
                {
                    if (s is ComboBox cb)
                    {
                        cbbBaudData.IsEnabled = cb.SelectedItem.ToString() == "CAN FD";
                    }
                };
                stackMain.Children.Add(row3);
                // ========== 行5：终端电阻 + 只听模式 ==========
                var row4 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                var chkTerm = new CheckBox { Content = "启用120Ω终端", IsChecked = true, Tag = i, Margin = new Thickness(0, 0, 10, 0) };
                var chkListenOnly = new CheckBox { Content = "只听模式", IsChecked = false, Tag = i };
                row4.Children.Add(chkTerm);
                row4.Children.Add(chkListenOnly);
                stackMain.Children.Add(row4);
                // ========== 行6：连接断开按钮 ==========
                var row5 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                var btnConnect = new Button
                {
                    Content = "连接",
                    Width = 120,
                    Background = new SolidColorBrush(Color.FromRgb(40, 167, 69)),
                    Foreground = Brushes.White,
                    Tag = i
                };
                btnConnect.Click += BtnCanConnect_Click;
                var btnDisconnect = new Button
                {
                    Content = "断开",
                    Width = 120,
                    Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                    Foreground = Brushes.White,
                    Margin = new Thickness(8, 0, 0, 0),
                    Tag = i
                };
                btnDisconnect.Click += BtnCanDisconnect_Click;
                row5.Children.Add(btnConnect);
                row5.Children.Add(btnDisconnect);
                stackMain.Children.Add(row5);
                // ========== 报文接收日志 ==========
                var txtLog = new TextBox
                {
                    Height = 100,
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Tag = i
                };
                stackMain.Children.Add(new TextBlock { Text = "接收报文日志：", Margin = new Thickness(0, 4, 0, 2) });
                stackMain.Children.Add(txtLog);
                groupBox.Content = stackMain;
                WrapCanCards.Children.Add(groupBox);
            }
        }

        /// <summary>【新增】批量搜索设备并分配IP 按钮点击事件</summary>
        private async void BtnBatchResetIp_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            btn.IsEnabled = false;
            try
            {
                MessageBox.Show("开始广播搜索局域网NetCAN800H设备，请等待...\n程序需要管理员权限！", "提示");
                //1.搜索所有在线设备
                var devList = NetCan800HNetConfig.SearchDevices();
                if (devList.Count == 0)
                {
                    MessageBox.Show("未搜索到任何NetCAN800H设备！\n检查网线、同网段、供电，管理员权限运行程序", "错误");
                    return;
                }
                if (devList.Count > 12)
                {
                    MessageBox.Show($"搜索到{devList.Count}台设备，最多只处理前12台！");
                }

                //2.循环依次设置IP，设备1→192.168.0.10，设备2→11...
                int successCount = 0;
                for (int idx = 0; idx < devList.Count && idx < 12; idx++)
                {
                    int canNo = idx + 1;
                    var devInfo = devList[idx];
                    string targetIp = NetCan800HNetConfig.GetPresetIpByDevNo(canNo);
                    bool setOk = NetCan800HNetConfig.SetPresetIpByDevNo(devInfo.Mac, canNo, "88888");
                    if (setOk)
                    {
                        successCount++;
                        // 更新内存卡片
                        var card = _canCardList[canNo - 1];
                        card.IpAddress = targetIp;
                        // 回填UI
                        var gb = FindGroupBoxByCardIndex(canNo);
                        if (gb != null)
                        {
                            FillCardUiValue(gb, card);
                        }
                        //保存到JSON
                        SaveSingleCardConfig(card);
                        AppendLog(canNo, $"✅MAC:{devInfo.Mac} 已设置IP:{targetIp}");
                    }
                    else
                    {
                        AppendLog(canNo, $"❌MAC:{devInfo.Mac} IP设置失败，请确认密码88888");
                    }
                    await Task.Delay(1200); //每台设备修改IP后等待网口重启
                }
                MessageBox.Show($"批量IP分配完成！成功{successCount}/{devList.Count}台\n设备网口重启，请等待2~3秒后再连接CAN", "完成");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"批量设置IP异常：{ex.Message}", "异常");
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        private void BtnCanConnect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int canId)
            {
                var groupBox = FindGroupBoxByCardIndex(canId);
                if (groupBox == null) return;
                var cardConfig = ReadCardUiValue(groupBox);
                var card = _canCardList[canId - 1];
                // ========== 这里是CAN连接逻辑，连接成功后执行保存 ==========
                MessageBox.Show($"准备连接 CAN{canId} ZLG800H（网口）", "CAN操作");
                //bool connectOk = card.CanHandle.OpenNet(cardConfig.IpAddress, cardConfig.DevIndex, cardConfig.CanChannel, ...);
                bool connectOk = true; // 临时占位，后续替换成真实驱动返回值
                if (connectOk)
                {
                    card.IsConnected = true;
                    // ✅ 连接成功，自动保存当前卡片配置到JSON
                    SaveSingleCardConfig(cardConfig);
                    AppendLog(canId, "✅ 连接成功，配置已自动保存");
                }
                else
                {
                    AppendLog(canId, "❌ 连接失败");
                }
            }
        }
        private void BtnCanDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int canId)
            {
                var card = _canCardList[canId - 1];
                MessageBox.Show($"准备断开 CAN{canId} ZLG800H", "CAN操作");
                //card.CanHandle?.Close();
                card.IsConnected = false;
                AppendLog(canId, "已断开");
            }
        }
        #region JSON 读写
        /// <summary>连接成功：更新单台设备配置并写入JSON</summary>
        private void SaveSingleCardConfig(CanCardItem updatedCard)
        {
            try
            {
                List<CanCardItem> allList;
                if (File.Exists(_jsonConfigPath))
                {
                    string json = File.ReadAllText(_jsonConfigPath);
                    allList = JsonSerializer.Deserialize<List<CanCardItem>>(json, _jsonOptions) ?? new List<CanCardItem>();
                }
                else
                {
                    allList = new List<CanCardItem>();
                }
                // 查找同编号，覆盖；不存在新增
                var exist = allList.Find(x => x.CanIndex == updatedCard.CanIndex);
                if (exist != null)
                {
                    exist.IpAddress = updatedCard.IpAddress;
                    exist.DevIndex = updatedCard.DevIndex;
                    exist.CanChannel = updatedCard.CanChannel;
                    exist.IsCanFd = updatedCard.IsCanFd;
                    exist.BaudArbitration = updatedCard.BaudArbitration;
                    exist.BaudData = updatedCard.BaudData;
                    exist.TermResistorOn = updatedCard.TermResistorOn;
                    exist.ListenOnly = updatedCard.ListenOnly;
                }
                else
                {
                    allList.Add(updatedCard);
                }
                // 不保存IsConnected（运行状态，不需要持久化）
                foreach (var item in allList)
                    item.IsConnected = false;
                string outJson = JsonSerializer.Serialize(allList, _jsonOptions);
                File.WriteAllText(_jsonConfigPath, outJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"自动保存配置失败：{ex.Message}", "警告");
            }
        }
        /// <summary>页面加载：读取JSON，回填所有卡片UI</summary>
        private void LoadConfigFromJson()
        {
            if (!File.Exists(_jsonConfigPath)) return;
            try
            {
                string json = File.ReadAllText(_jsonConfigPath);
                var savedList = JsonSerializer.Deserialize<List<CanCardItem>>(json, _jsonOptions);
                if (savedList == null) return;
                foreach (var savedItem in savedList)
                {
                    var gb = FindGroupBoxByCardIndex(savedItem.CanIndex);
                    if (gb == null) continue;
                    FillCardUiValue(gb, savedItem);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载CAN配置失败：{ex.Message}", "警告");
            }
        }
        #endregion
        #region UI控件读取/回填工具方法
        /// <summary>从GroupBox读取卡片所有UI值，封装CanCardItem</summary>
        private CanCardItem ReadCardUiValue(GroupBox gb)
        {
            var item = new CanCardItem();
            if (gb.Tag is int canIdx)
                item.CanIndex = canIdx;
            if (gb.Content is StackPanel panel)
            {
                foreach (var row in panel.Children)
                {
                    if (row is StackPanel spRow)
                    {
                        foreach (var c in spRow.Children)
                        {
                            if (c is TextBox tb)
                            {
                                if (tb.Name == "" && tb.Tag is int)
                                {
                                    //IP文本框
                                    if (spRow.Children[0] is TextBlock tbk && tbk.Text == "IP地址：")
                                    {
                                        item.IpAddress = tb.Text;
                                    }
                                    //设备编号
                                    if (spRow.Children[0] is TextBlock tbk2 && tbk2.Text == "设备编号：")
                                    {
                                        int.TryParse(tb.Text, out int devNo);
                                        item.DevIndex = devNo;
                                    }
                                }
                            }
                            else if (c is ComboBox cbb)
                            {
                                var tbHeader = spRow.Children[0] as TextBlock;
                                if (tbHeader != null)
                                {
                                    if (tbHeader.Text == "CAN通道：")
                                    {
                                        item.CanChannel = cbb.SelectedIndex;
                                    }
                                    if (tbHeader.Text == "协议：")
                                    {
                                        item.IsCanFd = cbb.SelectedItem.ToString() == "CAN FD";
                                    }
                                    if (tbHeader.Text == "仲裁波特率：")
                                    {
                                        item.BaudArbitration = GetBaudValue(cbb.SelectedItem.ToString());
                                    }
                                    if (tbHeader.Text == "FD数据波特率：")
                                    {
                                        item.BaudData = GetBaudValue(cbb.SelectedItem.ToString());
                                    }
                                }
                            }
                            else if (c is CheckBox chk)
                            {
                                if (chk.Content.ToString().Contains("120Ω终端"))
                                    item.TermResistorOn = chk.IsChecked == true;
                                if (chk.Content.ToString().Contains("只听模式"))
                                    item.ListenOnly = chk.IsChecked == true;
                            }
                        }
                    }
                }
            }
            return item;
        }
        /// <summary>把CanCardItem回填到UI控件</summary>
        private void FillCardUiValue(GroupBox gb, CanCardItem item)
        {
            if (gb.Content is not StackPanel panel) return;
            foreach (var row in panel.Children)
            {
                if (row is StackPanel spRow)
                {
                    foreach (var c in spRow.Children)
                    {
                        if (c is TextBox tb)
                        {
                            var tbHeader = spRow.Children[0] as TextBlock;
                            if (tbHeader?.Text == "IP地址：")
                                tb.Text = item.IpAddress;
                            if (tbHeader?.Text == "设备编号：")
                                tb.Text = item.DevIndex.ToString();
                        }
                        else if (c is ComboBox cbb)
                        {
                            var tbHeader = spRow.Children[0] as TextBlock;
                            if (tbHeader?.Text == "CAN通道：")
                                cbb.SelectedIndex = item.CanChannel;
                            if (tbHeader?.Text == "协议：")
                                cbb.SelectedItem = item.IsCanFd ? "CAN FD" : "CAN2.0";
                            if (tbHeader?.Text == "仲裁波特率：")
                                cbb.SelectedItem = GetBaudText(item.BaudArbitration);
                            if (tbHeader?.Text == "FD数据波特率：")
                                cbb.SelectedItem = GetBaudText(item.BaudData);
                        }
                        else if (c is CheckBox chk)
                        {
                            if (chk.Content.ToString().Contains("120Ω终端"))
                                chk.IsChecked = item.TermResistorOn;
                            if (chk.Content.ToString().Contains("只听模式"))
                                chk.IsChecked = item.ListenOnly;
                        }
                    }
                }
            }
        }
        /// <summary>根据卡片编号找到GroupBox</summary>
        private GroupBox? FindGroupBoxByCardIndex(int canIndex)
        {
            foreach (var child in WrapCanCards.Children)
            {
                if (child is GroupBox gb && gb.Tag is int tag && tag == canIndex)
                {
                    return gb;
                }
            }
            return null;
        }
        /// <summary>波特率文本转数字常量</summary>
        private uint GetBaudValue(string text)
        {
            return text switch
            {
                "125K" => 125000,
                "250K" => 250000,
                "500K" => 500000,
                "1M" => 1000000,
                "2M" => 2000000,
                "4M" => 4000000,
                "8M" => 8000000,
                _ => 500000
            };
        }
        /// <summary>波特率数值转下拉显示文本</summary>
        private string GetBaudText(uint baud)
        {
            return baud switch
            {
                125000 => "125K",
                250000 => "250K",
                500000 => "500K",
                1000000 => "1M",
                2000000 => "2M",
                4000000 => "4M",
                8000000 => "8M",
                _ => "500K"
            };
        }
        #endregion
        /// <summary>追加日志，线程安全</summary>
        private void AppendLog(int cardIndex, string msg)
        {
            Dispatcher.Invoke(() =>
            {
                var groupBox = FindGroupBoxByCardIndex(cardIndex);
                if (groupBox?.Content is StackPanel panel)
                {
                    foreach (var c in panel.Children)
                    {
                        if (c is TextBox tb && tb.Tag is int tagId && tagId == cardIndex)
                        {
                            tb.AppendText($"{DateTime.Now:HH:mm:ss} {msg}{Environment.NewLine}");
                            tb.ScrollToEnd();
                            break;
                        }
                    }
                }
            });
        }
    }
    /// <summary>CAN卡实体，网口NetCAN800H，保存每个CAN卡配置</summary>
    public class CanCardItem
    {
        public int CanIndex { get; set; }
        public bool IsConnected { get; set; }
        public string IpAddress { get; set; } = "192.168.0.100";
        public int DevIndex { get; set; }
        public int CanChannel { get; set; }        //0=CH0，1=CH1
        public bool IsCanFd { get; set; }
        public uint BaudArbitration { get; set; }
        public uint BaudData { get; set; }
        public bool TermResistorOn { get; set; }
        public bool ListenOnly { get; set; }
        //public ZlgCan800H? CanHandle { get; set; }
    }
}
