using System;
using System.Windows;
using System.Windows.Controls;

namespace AgingTest.Views
{
    public partial class LoadSettingWindow : Window
    {
        /// <summary>弹窗输出结果</summary>
        public LoadParamResult? Result { get; private set; }
        /// <summary>点击的通道编号</summary>
        public int ClickChNo { get; set; }

        /// <summary>传入旧配置，用于打开弹窗回填</summary>
        public LoadParamResult? OldConfig { get; set; }

        public LoadSettingWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (OldConfig == null) return;

            var cbbMode = this.FindName("cbbMode") as ComboBox;
            var txtVomCtrl = this.FindName("txtVom") as TextBox;
            var txtLoadCtrl = this.FindName("txtLoadValue") as TextBox;
            var txtExtraCtrl = this.FindName("txtExtraParam") as TextBox;

            if (cbbMode != null)
            {
                cbbMode.SelectedItem = OldConfig.ModeText;
            }
            if (txtVomCtrl != null)
            {
                txtVomCtrl.Text = OldConfig.VonVolt.ToString();
            }
            if (txtLoadCtrl != null)
            {
                txtLoadCtrl.Text = OldConfig.LoadValue.ToString();
            }
            if (txtExtraCtrl != null)
            {
                txtExtraCtrl.Text = OldConfig.ExtraParam.ToString();
            }
        }

        private void BtnSingleCh_Click(object sender, RoutedEventArgs e)
        {
            if (!ParseParam(out var param))
                return;

            var cbb = this.FindName("cbbMode") as ComboBox;
            string modeText = cbb?.SelectedItem?.ToString() ?? "CC 恒流_慢速";

            Result = new LoadParamResult
            {
                IsSingleChannel = true,
                ChNo = ClickChNo,
                ModeText = modeText,
                VonVolt = param.VonVolt,
                LoadValue = param.LoadValue,
                ExtraParam = param.ExtraParam
            };
            this.DialogResult = true;
            this.Close();
        }

        private void BtnAllCh_Click(object sender, RoutedEventArgs e)
        {
            if (!ParseParam(out var param))
                return;

            var cbb = this.FindName("cbbMode") as ComboBox;
            string modeText = cbb?.SelectedItem?.ToString() ?? "CC 恒流_慢速";

            Result = new LoadParamResult
            {
                IsSingleChannel = false,
                ChNo = ClickChNo,
                ModeText = modeText,
                VonVolt = param.VonVolt,
                LoadValue = param.LoadValue,
                ExtraParam = param.ExtraParam
            };
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        /// <summary>参数解析校验，新增附加参数读取</summary>
        private bool ParseParam(out (double VonVolt, double LoadValue, byte ExtraParam) res)
        {
            res = (0, 0, 0);
            var txtVomCtrl = this.FindName("txtVom") as TextBox;
            var txtLoadCtrl = this.FindName("txtLoadValue") as TextBox;
            var txtExtraCtrl = this.FindName("txtExtraParam") as TextBox;

            if (txtVomCtrl == null || txtLoadCtrl == null || txtExtraCtrl == null)
            {
                MessageBox.Show("界面控件加载失败！");
                return false;
            }

            if (!double.TryParse(txtVomCtrl.Text, out double vom))
            {
                MessageBox.Show("VOM点(V) 数值格式错误！");
                return false;
            }
            if (!double.TryParse(txtLoadCtrl.Text, out double loadVal))
            {
                MessageBox.Show("负载值 数值格式错误！");
                return false;
            }
            if (!byte.TryParse(txtExtraCtrl.Text, out byte extraVal))
            {
                MessageBox.Show("附加参数必须是0~255整数！");
                return false;
            }
            res = (vom, loadVal, extraVal);
            return true;
        }
    }

    /// <summary>弹窗返回参数实体，新增ExtraParam字段</summary>
    public class LoadParamResult
    {
        public bool IsSingleChannel { get; set; }
        public int ChNo { get; set; }
        public string ModeText { get; set; } = "";
        public double VonVolt { get; set; }
        public double LoadValue { get; set; }
        public byte ExtraParam { get; set; }
    }
}
