using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AgingTest.Views
{
    public partial class HardwareControlWindow : Window
    {
        public HardwareControlWindow()
        {
            InitializeComponent();
            // 默认加载电子负载页面
            ccMain.Content = new UC_LoadDevice();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string menuName = btn.Content.ToString();
                HeaderText.Text = menuName;

                switch (menuName)
                {
                    case "多通道电子负载":
                        ccMain.Content = new UC_LoadDevice();
                        break;
                    case "CAN卡-ZLG-800H":
                        ccMain.Content = new UC_CanZlg800H();
                        break;
                    case "交换机":
                        MessageBox.Show("交换机页面暂未实现", "提示", MessageBoxButton.OK);
                        break;
                    case "高精度程控电源":
                        MessageBox.Show("高精度程控电源页面暂未实现", "提示", MessageBoxButton.OK);
                        break;
                    case "大电流程控电源":
                        MessageBox.Show("大电流程控电源页面暂未实现", "提示", MessageBoxButton.OK);
                        break;
                    case "程控电源框架":
                        MessageBox.Show("程控电源框架页面暂未实现", "提示", MessageBoxButton.OK);
                        break;
                    case "电阻模拟卡":
                        MessageBox.Show("电阻模拟卡页面暂未实现", "提示", MessageBoxButton.OK);
                        break;
                    case "继电器板":
                        MessageBox.Show("继电器板页面暂未实现", "提示", MessageBoxButton.OK);
                        break;
                    case "LIN卡":
                        MessageBox.Show("LIN卡页面暂未实现", "提示", MessageBoxButton.OK);
                        break;
                    case "扫码枪":
                        MessageBox.Show("扫码枪页面暂未实现", "提示", MessageBoxButton.OK);
                        break;
                }
                // 左侧菜单按钮高亮
                var parentPanel = btn.Parent as StackPanel;
                if (parentPanel != null)
                {
                    foreach (var child in parentPanel.Children)
                    {
                        if (child is Button menuBtn)
                        {
                            menuBtn.Background = Brushes.Transparent;
                        }
                    }
                }
                btn.Background = new SolidColorBrush(Color.FromRgb(216, 228, 240));
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}
