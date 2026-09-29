using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AgingTest.Models;

namespace AgingTest
{
    /// <summary>
    /// 奇瑞C1068 老化柜测试上位机 - 主界面
    /// 当前版本：界面功能 + 扫码绑定 + 测试流程模拟（设备通讯预留，后续接入）
    /// </summary>
    public partial class MainWindow : Window
    {
        // 工位布局：6层 × 8列 = 48工位
        private const int LAYERS = 6;
        private const int COLUMNS = 8;

        // TODO(条码规则): 工位码与产品码的区分规则待客户确认后调整（当前默认 工位码="POS"+编号）
        private const string StationBarcodePrefix = "POS";

        private readonly List<WorkStation> _stations = new();
        private readonly Dictionary<WorkStation, StationCard> _cards = new();
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _testTimer;
        private readonly Random _rnd = new();
        private WorkStation? _pendingStation;   // 已扫描工位码、等待产品码的工位
        private readonly UserInfo _currentUser;

        public MainWindow(UserInfo user)
        {
            InitializeComponent();
            _currentUser = user;
            BuildStationGrid();
            RefreshStats();
            ApplyPermission(user.Role);

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) => TxtClock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _clockTimer.Start();

            _testTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _testTimer.Tick += TestTimer_Tick;

            ScanInput.Focus();
            AddLog($"系统就绪（当前用户：{user.RoleName}），请先扫描【工位码】绑定工位（先扫工位码，再扫产品码）");
        }

        // ==================== 权限管理（三级权限） ====================

        /// <summary>按角色应用界面权限：作业员 &lt; 技术员 &lt; 工程师</summary>
        private void ApplyPermission(UserRole role)
        {
            // 作业员不可修改测试/机种参数，技术员及以上可修改
            bool canEditParams = role != UserRole.Operator;
            foreach (var tb in new[] { TxtAgingTemp, TxtAgingMinutes, TxtPollingMs, TxtRTSeconds })
            {
                tb.IsReadOnly = !canEditParams;
                tb.Background = canEditParams ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xF0, 0xF2, 0xF5));
                tb.ToolTip = canEditParams ? "可修改" : "仅技术员及以上可修改";
            }

            // 用户管理入口：仅工程师可见
            BtnUserManage.Visibility = role == UserRole.Engineer ? Visibility.Visible : Visibility.Collapsed;

            TxtUser.Text = $"当前用户：{_currentUser.RoleName}（{_currentUser.DisplayName}）";
            TxtUser.ToolTip = $"账号：{_currentUser.UserName}";
        }

        /// <summary>打开用户管理窗口（仅工程师入口可见）</summary>
        private void BtnUserManage_Click(object sender, RoutedEventArgs e)
        {
            var win = new Views.UserManageWindow(_currentUser) { Owner = this };
            win.ShowDialog();
            AddLog("[用户管理] 用户管理窗口已关闭");
        }

        /// <summary>退出登录：重新弹出登录窗口，成功后切换到新用户</summary>
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var login = new Views.LoginWindow();
            if (login.ShowDialog() == true && login.LoggedUser != null)
            {
                var newMain = new MainWindow(login.LoggedUser);
                Application.Current.MainWindow = newMain;
                newMain.Show();
                Close();   // 关闭旧主窗口（MainWindow 已指向新窗口，不会触发退出）
            }
        }

        // ==================== 工位网格构建 ====================

        /// <summary>点击工位卡片，打开该工位的数据监控详情窗口（非模态，可同时打开多个）</summary>
        private void OpenDetail(WorkStation ws)
        {
            var win = new Views.DataMonitorWindow(ws) { Owner = this };
            win.Show();
        }

        private void BuildStationGrid()
        {
            var grid = new Grid();
            for (int c = 0; c < COLUMNS; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int r = 0; r < LAYERS; r++)
                grid.RowDefinitions.Add(new RowDefinition());

            for (int layer = 1; layer <= LAYERS; layer++)
            {
                for (int col = 1; col <= COLUMNS; col++)
                {
                    var ws = new WorkStation(layer, col);
                    _stations.Add(ws);

                    var card = CreateStationCard(ws);
                    Grid.SetRow(card.Root, layer - 1);
                    Grid.SetColumn(card.Root, col - 1);
                    grid.Children.Add(card.Root);
                    _cards[ws] = card;
                    // 点击工位卡片 → 打开数据监控详情窗口
                    card.Root.Cursor = Cursors.Hand;
                    card.Root.MouseLeftButtonUp += (_, _) => OpenDetail(ws);
                    UpdateCard(ws);
                }
            }
            StationGridHost.Content = grid;
        }

        private StationCard CreateStationCard(WorkStation ws)
        {
            var root = new Border
            {
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                ToolTip = $"工位 {ws.Code}（{ws.Barcode}）"
            };

            var panel = new StackPanel();

            var txtCode = new TextBlock
            {
                Text = ws.Code,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x38, 0x64)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var txtState = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            var txtProduct = new TextBlock
            {
                FontSize = 10,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
                ToolTip = "产品二维码"
            };
            var txtData = new TextBlock
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };

            panel.Children.Add(txtCode);
            panel.Children.Add(txtState);
            panel.Children.Add(txtProduct);
            panel.Children.Add(txtData);
            root.Child = panel;

            return new StationCard(root, txtState, txtProduct, txtData);
        }

        private void UpdateCard(WorkStation ws)
        {
            var card = _cards[ws];
            var root = card.Root;

            (Brush bg, Brush border, string stateText, Brush stateColor) = ws.State switch
            {
                WorkStationState.Idle => ((Brush)FindResource("BrushIdle"), (Brush)FindResource("BorderIdle"), "空闲", Brushes.Gray),
                WorkStationState.Bound => ((Brush)FindResource("BrushBound"), (Brush)FindResource("BorderBound"), "已绑定", (Brush)FindResource("BorderBound")),
                WorkStationState.RTAging => ((Brush)FindResource("BrushRT"), (Brush)FindResource("BorderRT"), "常温测试", (Brush)FindResource("BorderRT")),
                WorkStationState.Aging => ((Brush)FindResource("BrushAging"), (Brush)FindResource("BorderAging"), "老化测试", (Brush)FindResource("BorderAging")),
                WorkStationState.Passed => ((Brush)FindResource("BrushPass"), (Brush)FindResource("BorderPass"), "通过", (Brush)FindResource("BorderPass")),
                WorkStationState.Failed => ((Brush)FindResource("BrushFail"), (Brush)FindResource("BorderFail"), "NG", (Brush)FindResource("BorderFail")),
                _ => (Brushes.White, Brushes.Gray, "未知", Brushes.Gray)
            };

            root.Background = bg;
            root.BorderBrush = border;
            // 已绑定/测试中/异常 加粗边框高亮区分
            root.BorderThickness = ws.State switch
            {
                WorkStationState.Idle or WorkStationState.Passed => new Thickness(1),
                _ => new Thickness(2.5)
            };

            card.StateText.Text = stateText;
            card.StateText.Foreground = stateColor;
            card.ProductText.Text = string.IsNullOrEmpty(ws.ProductCode) ? "未绑定" : ws.ProductCode;
            card.ProductText.Foreground = string.IsNullOrEmpty(ws.ProductCode) ? Brushes.Gray : Brushes.DarkSlateGray;

            card.DataText.Text = ws.State switch
            {
                WorkStationState.Aging =>
                    $"V:{ws.CellVoltage:F2}V  T:{ws.CellTemp:F1}℃  I:{ws.Current:F1}A\n" +
                    $"{ws.AgingStepText} {ws.AgingStepRemainSec}s  剩 {ws.RemainTime:hh\\:mm\\:ss}",
                WorkStationState.RTAging =>
                    $"V:{ws.CellVoltage:F2}V  T:{ws.CellTemp:F1}℃  I:{ws.Current:F1}A\n" +
                    $"内:{ws.InnerVoltage:F2}V 外:{ws.OuterVoltage:F2}V  剩 {ws.RemainTime:hh\\:mm\\:ss}",
                WorkStationState.Passed => $"PASS\n{(ws.TestStartTime.HasValue ? ws.TestStartTime.Value.ToString("HH:mm:ss") : "")}",
                WorkStationState.Failed => $"NG  {ws.FaultInfo}",
                WorkStationState.Bound => ws.BindTime.HasValue ? $"绑定 {ws.BindTime.Value:HH:mm:ss}" : "",
                _ => ""
            };
        }

        // ==================== 扫码绑定 ====================

        private void ScanInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var text = ScanInput.Text;
                ScanInput.Clear();
                if (!string.IsNullOrWhiteSpace(text))
                    HandleScan(text.Trim());
                e.Handled = true;
            }
        }

        private void HandleScan(string raw)
        {
            AddLog($"[扫码] {raw}");

            // 1) 尝试识别为工位码
            var pos = TryParseStationBarcode(raw);
            if (pos.HasValue)
            {
                var ws = _stations.FirstOrDefault(s => s.Layer == pos.Value.Layer && s.Column == pos.Value.Column);
                if (ws == null)
                {
                    Hint($"工位码 {raw} 无效（应在 POS1-1 ~ POS6-8 范围）");
                    _pendingStation = null;
                    return;
                }

                if (ws.State != WorkStationState.Idle)
                {
                    Hint($"工位 {ws.Code} 当前为【{StateName(ws.State)}】状态，空闲工位才可绑定");
                    _pendingStation = null;
                    return;
                }

                _pendingStation = ws;
                TxtPendingStation.Text = $"待绑定工位：{ws.Code}（{ws.Barcode}）→ 请扫描产品码";
                Hint($"已识别工位码 {ws.Barcode}，请扫描产品码");
                return;
            }

            // 2) 否则按产品码处理
            if (_pendingStation == null)
            {
                Hint("请先扫描【工位码】");
                return;
            }

            // TODO(条码规则): 产品码格式校验规则待客户确认（当前仅做非空/最小长度校验）
            if (raw.Length < 4)
            {
                Hint($"产品码 {raw} 长度异常，请检查扫码枪");
                return;
            }

            var target = _pendingStation;
            _pendingStation = null;

            target.ProductCode = raw;
            target.BindTime = DateTime.Now;
            target.State = WorkStationState.Bound;
            target.FaultInfo = "";

            TxtPendingStation.Text = "";
            Hint($"工位 {target.Code} 绑定产品 {raw} 完成");
            AddLog($"[绑定] 工位 {target.Code}（{target.Barcode}）← 产品 {raw}，时间 {target.BindTime:HH:mm:ss}");

            UpdateCard(target);
            RefreshStats();
        }

        /// <summary>
        /// 工位码解析。默认规则：POS + 编号，支持 "POS1-1"（层-列）或 "POS0108"（LLCC 两位层两位列）。
        /// </summary>
        private (int Layer, int Column)? TryParseStationBarcode(string raw)
        {
            var t = raw.Trim().ToUpperInvariant().Replace(" ", "");
            if (!t.StartsWith(StationBarcodePrefix, StringComparison.Ordinal))
                return null;

            var body = t.Substring(StationBarcodePrefix.Length);
            var m1 = Regex.Match(body, @"^(\d+)[-](\d+)$");
            if (m1.Success)
            {
                int l = int.Parse(m1.Groups[1].Value);
                int c = int.Parse(m1.Groups[2].Value);
                return (l >= 1 && l <= LAYERS && c >= 1 && c <= COLUMNS) ? (l, c) : null;
            }
            var m2 = Regex.Match(body, @"^(\d{2})(\d{2})$");
            if (m2.Success)
            {
                int l = int.Parse(m2.Groups[1].Value);
                int c = int.Parse(m2.Groups[2].Value);
                return (l >= 1 && l <= LAYERS && c >= 1 && c <= COLUMNS) ? (l, c) : null;
            }
            return null;
        }

        // ==================== 测试控制 ====================

        private void BtnRT_Click(object sender, RoutedEventArgs e)
        {
            var targets = _stations.Where(s => s.State == WorkStationState.Bound).ToList();
            if (targets.Count == 0)
            {
                Hint("没有已绑定的空闲工位，请先扫码绑定");
                return;
            }
            if (!int.TryParse(TxtRTSeconds.Text, out var sec) || sec < 5)
                sec = 60;

            foreach (var ws in targets)
            {
                ws.State = WorkStationState.RTAging;
                ws.TestType = "常温测试";
                ws.TestStartTime = DateTime.Now;
                ws.TestDuration = TimeSpan.FromSeconds(sec);
                ws.RemainTime = ws.TestDuration;
                ws.FaultInfo = "";
                ws.MosClosed = true;   // 带载条件：MOS 闭合
                UpdateCard(ws);
            }
            if (!_testTimer.IsEnabled) _testTimer.Start();
            AddLog($"[测试] 常温测试启动：{targets.Count} 个工位，时长 {sec} 秒");
            SetStatus($"常温测试进行中：{targets.Count} 个工位");
            RefreshStats();
            ScanInput.Focus();
        }

        private void BtnAging_Click(object sender, RoutedEventArgs e)
        {
            var targets = _stations.Where(s => s.State == WorkStationState.Bound).ToList();
            if (targets.Count == 0)
            {
                Hint("没有已绑定的空闲工位，请先扫码绑定");
                return;
            }
            if (!double.TryParse(TxtAgingTemp.Text, out var temp) || temp < 0)
                temp = 80;
            if (!int.TryParse(TxtAgingMinutes.Text, out var minutes) || minutes < 1)
                minutes = 120;

            foreach (var ws in targets)
            {
                ws.State = WorkStationState.Aging;
                ws.TestType = "老化测试";
                ws.TestStartTime = DateTime.Now;
                ws.TestDuration = TimeSpan.FromMinutes(minutes);
                ws.RemainTime = ws.TestDuration;
                ws.FaultInfo = "";
                ws.DtcInfo = "无";
                ws.MosClosed = true;   // 带载条件：MOS 闭合
                // 老化流程：从《BMS老化测试项》第1步 BMS上电 开始
                ws.AgingStep = AgingStep.PowerOn;
                ws.AgingStepRemainSec = 10;
                UpdateCard(ws);
            }
            if (!_testTimer.IsEnabled) _testTimer.Start();
            AddLog($"[测试] 老化测试启动：{targets.Count} 个工位，温度 {temp:F0}℃，时长 {minutes} 分钟（流程：上电→带载→休眠→静置→唤醒循环）");
            SetStatus($"老化测试进行中：{targets.Count} 个工位，目标温度 {temp:F0}℃");
            RefreshStats();
            ScanInput.Focus();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            var running = _stations.Where(s => s.State is WorkStationState.RTAging or WorkStationState.Aging).ToList();
            if (running.Count == 0)
            {
                Hint("当前没有正在进行的测试");
                return;
            }

            foreach (var ws in running)
            {
                AddLog($"[停止] 工位 {ws.Code} {ws.TestType} 已手动停止（未完成）");
                ws.State = WorkStationState.Bound;
                ws.TestType = "";
                ws.TestStartTime = null;
                ws.TestDuration = null;
                ws.RemainTime = null;
                ws.MosClosed = false;
                ws.AgingStepRemainSec = 0;
                UpdateCard(ws);
            }
            if (!_stations.Any(s => s.State is WorkStationState.RTAging or WorkStationState.Aging))
                _testTimer.Stop();
            SetStatus($"已停止 {running.Count} 个工位的测试");
            Hint($"已停止 {running.Count} 个工位的测试");
            RefreshStats();
            ScanInput.Focus();
        }

        private void TestTimer_Tick(object? sender, EventArgs e)
        {
            bool anyRunning = false;
            foreach (var ws in _stations)
            {
                if (ws.State is not (WorkStationState.RTAging or WorkStationState.Aging))
                    continue;

                anyRunning = true;

                // 老化测试按《BMS老化测试项》步骤循环驱动；常温测试走简单模拟
                if (ws.State == WorkStationState.Aging)
                    AgingTick(ws);
                else
                    RTAgingTick(ws);

                ws.RemainTime = ws.RemainTime - TimeSpan.FromSeconds(1);
                if (ws.RemainTime <= TimeSpan.Zero)
                {
                    ws.RemainTime = TimeSpan.Zero;
                    // ---- 完成判定（模拟：95% PASS，5% NG 演示报警）----
                    bool pass = _rnd.NextDouble() < 0.95;
                    ws.State = pass ? WorkStationState.Passed : WorkStationState.Failed;
                    if (!pass)
                    {
                        ws.FaultInfo = "模拟异常（电压超限）";
                        ws.DtcInfo = "P0560";
                    }
                    AddLog($"[完成] 工位 {ws.Code} {ws.TestType} 结束：{(pass ? "PASS" : "NG")}");
                    if (!pass) Hint($"工位 {ws.Code} 测试 NG，请检查！");
                }
                UpdateCard(ws);
            }

            if (!anyRunning)
            {
                _testTimer.Stop();
                SetStatus("全部测试已完成，等待下一轮");
            }
            RefreshStats();
        }

        // ==================== 老化流程（按《BMS老化测试项》步骤循环） ====================

        /// <summary>
        /// 老化步骤时序（序号1~12 循环，13=重复1-12）：
        /// 上电 → 带载20min → 一级休眠 → 静置1min → 唤醒 → 带载20min → 二级休眠 → 静置1min → 唤醒 → 带载20min → 下电 → 静置1min → 回上电
        /// </summary>
        private static (AgingStep step, int seconds) NextAgingStep(AgingStep s) => s switch
        {
            AgingStep.PowerOn => (AgingStep.Load1, 1200),    // 1→2 带载20min（20×60s）
            AgingStep.Load1 => (AgingStep.Sleep1, 10),       // 2→3 一级休眠
            AgingStep.Sleep1 => (AgingStep.Rest1, 60),       // 3→4 静置1min
            AgingStep.Rest1 => (AgingStep.Wake1, 10),        // 4→5 唤醒（网络管理报文）
            AgingStep.Wake1 => (AgingStep.Load2, 1200),      // 5→6 带载20min
            AgingStep.Load2 => (AgingStep.Sleep2, 10),       // 6→7 二级休眠
            AgingStep.Sleep2 => (AgingStep.Rest2, 60),       // 7→8 静置1min
            AgingStep.Rest2 => (AgingStep.Wake2, 10),        // 8→9 唤醒（低电平唤醒线拉低）
            AgingStep.Wake2 => (AgingStep.Load3, 1200),      // 9→10 带载20min
            AgingStep.Load3 => (AgingStep.PowerOff, 10),     // 10→11 BMS下电
            AgingStep.PowerOff => (AgingStep.Rest3, 60),     // 11→12 静置1min
            AgingStep.Rest3 => (AgingStep.PowerOn, 10),      // 12→1 重复序号1-12
            _ => (AgingStep.PowerOn, 10)
        };

        /// <summary>老化工位每秒推进：步骤计时 + 恒温恒载数据模拟</summary>
        private void AgingTick(WorkStation ws)
        {
            // 1) 步骤计时推进
            ws.AgingStepRemainSec--;
            if (ws.AgingStepRemainSec <= 0)
            {
                var next = NextAgingStep(ws.AgingStep);
                ws.AgingStep = next.step;
                ws.AgingStepRemainSec = next.seconds;
                AddLog($"[老化] 工位 {ws.Code} → {ws.AgingStepText}（{next.seconds}s）");
            }

            // 2) 按当前步骤驱动 MOS / 电流 / 休眠状态（恒载）
            switch (ws.AgingStep)
            {
                case AgingStep.PowerOn:   // 上电：低电平唤醒线拉低、发网络管理报文 → 可读电池信息
                    ws.Current = 0.02 + _rnd.NextDouble() * 0.03;
                    ws.MosClosed = false;
                    ws.SleepMode = 0;
                    break;
                case AgingStep.Load1 or AgingStep.Load2 or AgingStep.Load3:   // 带载：P2带载5A + OUT1-OUT4各1A ≈ 9A
                    ws.Current = 8.5 + _rnd.NextDouble() * 1.0;
                    ws.MosClosed = true;
                    ws.SleepMode = 0;
                    break;
                case AgingStep.Sleep1:    // 一级休眠：停止电流、发休眠指令（MOS保持闭合，不强制断开）
                    ws.Current = 0;
                    ws.SleepMode = 1;
                    break;
                case AgingStep.Sleep2:    // 二级休眠：停止电流、强制断开MOS及HSD输出
                    ws.Current = 0;
                    ws.MosClosed = false;
                    ws.SleepMode = 2;
                    break;
                case AgingStep.Rest1 or AgingStep.Rest2 or AgingStep.Rest3:   // 静置
                    ws.Current = 0;
                    ws.MosClosed = false;
                    ws.SleepMode = 0;
                    break;
                case AgingStep.Wake1 or AgingStep.Wake2:   // 唤醒：可正确读取电池信息
                    ws.Current = 0.02 + _rnd.NextDouble() * 0.03;
                    ws.MosClosed = false;
                    ws.SleepMode = 0;
                    break;
                case AgingStep.PowerOff:  // BMS下电：停止电流输出、给BMS下电
                    ws.Current = 0;
                    ws.MosClosed = false;
                    ws.SleepMode = 0;
                    break;
            }

            // 3) 恒温恒载监控数据（单体电压/电池电流/MOS温度/内外总压/DTC/MOS状态/故障）
            ws.CellVoltage = ws.Current > 1
                ? 3.2 + _rnd.NextDouble() * 0.3                       // 带载：电压略降
                : 3.2 + _rnd.NextDouble() * 0.8;                      // 空载/休眠：3.2~4.0V
            ws.CellTemp = 80 + (_rnd.NextDouble() - 0.5);             // 老化恒温 80±0.5℃
            ws.MosTemp = ws.MosClosed
                ? ws.CellTemp + 6 + _rnd.NextDouble() * 4             // MOS闭合带载温升
                : ws.CellTemp;
            ws.InnerVoltage = 12.0 + (_rnd.NextDouble() - 0.5) * 0.5; // 内总压 ≈12V
            ws.OuterVoltage = ws.MosClosed
                ? ws.InnerVoltage + (_rnd.NextDouble() - 0.5) * 0.1   // MOS闭合：内外导通
                : 0;                                                  // MOS断开：内外隔离，外压≈0
            ws.DtcInfo = "无";
            ws.FaultInfo = "";
        }

        /// <summary>常温测试每秒模拟（简单恒温25℃带载）</summary>
        private void RTAgingTick(WorkStation ws)
        {
            ws.CellVoltage = 3.2 + _rnd.NextDouble() * 1.0;
            ws.CellTemp = 25 + (_rnd.NextDouble() - 0.5) * 2;
            ws.InnerVoltage = 12.0 + (_rnd.NextDouble() - 0.5) * 1.0;
            ws.OuterVoltage = ws.InnerVoltage + (_rnd.NextDouble() - 0.5) * 0.2;
            ws.Current = _rnd.NextDouble() * 8.0;
            ws.DtcInfo = "无";
            ws.FaultInfo = "";
        }

        // ==================== UI 辅助 ====================

        private void Hint(string msg) => TxtScanHint.Text = msg;

        private void SetStatus(string msg) => TxtStatus.Text = msg;

        internal void AddLog(string msg)
        {
            LogList.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {msg}");
            while (LogList.Items.Count > 200)
                LogList.Items.RemoveAt(LogList.Items.Count - 1);
        }

        private void RefreshStats()
        {
            TxtStatBound.Text = $"已绑定 {_stations.Count(s => s.State == WorkStationState.Bound)}";
            TxtStatRT.Text = $"常温 {_stations.Count(s => s.State == WorkStationState.RTAging)}";
            TxtStatAging.Text = $"老化 {_stations.Count(s => s.State == WorkStationState.Aging)}";
            TxtStatPass.Text = $"通过 {_stations.Count(s => s.State == WorkStationState.Passed)}";
            TxtStatFail.Text = $"NG {_stations.Count(s => s.State == WorkStationState.Failed)}";
        }

        private static string StateName(WorkStationState s) => s switch
        {
            WorkStationState.Idle => "空闲",
            WorkStationState.Bound => "已绑定",
            WorkStationState.RTAging => "常温测试中",
            WorkStationState.Aging => "老化测试中",
            WorkStationState.Passed => "通过",
            WorkStationState.Failed => "NG",
            _ => "未知"
        };

        /// <summary>点击非输入框区域时，将焦点还给扫码输入框（模拟扫码枪连续扫码场景）</summary>
        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is TextBox)
                return;
            if (!ScanInput.IsKeyboardFocusWithin)
                ScanInput.Focus();
        }

        /// <summary>工位卡片 UI 元素集合</summary>
        private class StationCard
        {
            public Border Root { get; }
            public TextBlock StateText { get; }
            public TextBlock ProductText { get; }
            public TextBlock DataText { get; }

            public StationCard(Border root, TextBlock stateText, TextBlock productText, TextBlock dataText)
            {
                Root = root;
                StateText = stateText;
                ProductText = productText;
                DataText = dataText;
            }
        }
    }
}
