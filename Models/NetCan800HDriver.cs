using System;

namespace AgingTest.NetCan
{
    /// <summary>
    /// NetCAN-800H 网口CAN通信封装类
    /// 负责：打开通道、收发CAN/CANFD报文、关闭释放资源
    /// </summary>
    public class NetCan800HDriver
    {
        #region 实例属性
        public string IpAddress { get; private set; }
        public int DevIndex { get; private set; }
        public int CanChannel { get; private set; }
        public bool IsCanFd { get; private set; }
        public uint BaudArbitration { get; private set; }
        public uint BaudData { get; private set; }
        public bool TermResistorOn { get; private set; }
        public bool ListenOnly { get; private set; }
        public bool IsOpened { get; private set; }

        //收到报文回调事件
        public Action<NetCanFrame> OnFrameReceived;

        //设备句柄
        private IntPtr _devHandle = IntPtr.Zero;
        //NetCAN800H设备类型编号，ZLG官方定义：7
        private const uint DevType = 7;
        #endregion

        /// <summary>构造函数，仅保存参数，不打开设备</summary>
        public NetCan800HDriver(string ip, int devIndex, int canChannel, bool isCanFd, uint baudArb, uint baudData, bool termResistor, bool listenOnly)
        {
            IpAddress = ip;
            DevIndex = devIndex;
            CanChannel = canChannel;
            IsCanFd = isCanFd;
            BaudArbitration = baudArb;
            BaudData = baudData;
            TermResistorOn = termResistor;
            ListenOnly = listenOnly;
        }

        /// <summary>打开设备通道，建立网络连接</summary>
        public bool Open()
        {
            if (IsOpened) return true;
            try
            {
                _devHandle = ZlgNativeApi.ZCAN_OpenDevice(IpAddress, DevType, 0);
                if (_devHandle == IntPtr.Zero)
                {
                    return false;
                }

                //波特率配置
                ZlgNativeApi.ZCAN_BaudConfig baudCfg = new ZlgNativeApi.ZCAN_BaudConfig
                {
                    baud = BaudArbitration,
                    dataBaud = BaudData,
                    isCanFd = (byte)(IsCanFd ? 1 : 0),
                    termResistor = (byte)(TermResistorOn ? 1 : 0),
                    listenOnly = (byte)(ListenOnly ? 1 : 0),
                    reserved = 0
                };

                int ret = ZlgNativeApi.ZCAN_SetBaudRate(_devHandle, (uint)CanChannel, ref baudCfg);
                if (ret != 0)
                {
                    ZlgNativeApi.ZCAN_CloseDevice(_devHandle);
                    _devHandle = IntPtr.Zero;
                    return false;
                }

                IsOpened = true;
                return true;
            }
            catch
            {
                IsOpened = false;
                return false;
            }
        }

        /// <summary>发送CAN/CANFD报文</summary>
        public bool SendFrame(uint canId, byte[] data, bool isFd = false)
        {
            if (!IsOpened || _devHandle == IntPtr.Zero)
                return false;

            try
            {
                ZlgNativeApi.ZCAN_TransmitFrame txFrame = new ZlgNativeApi.ZCAN_TransmitFrame
                {
                    id = canId,
                    len = (byte)data.Length,
                    isFd = (byte)(isFd ? 1 : 0),
                    isRemote = 0,
                    data = new byte[64]
                };
                Array.Copy(data, txFrame.data, data.Length);

                int ret = ZlgNativeApi.ZCAN_Transmit(_devHandle, (uint)CanChannel, ref txFrame, 1);
                return ret >= 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>关闭设备，释放资源</summary>
        public void Close()
        {
            if (!IsOpened || _devHandle == IntPtr.Zero) return;
            try
            {
                ZlgNativeApi.ZCAN_CloseDevice(_devHandle);
            }
            catch
            {
            }
            _devHandle = IntPtr.Zero;
            IsOpened = false;
        }
    }

    /// <summary>CAN报文对外实体</summary>
    public class NetCanFrame
    {
        public uint CanId { get; set; }
        public byte[] Data { get; set; }
        public bool IsCanFd { get; set; }
        public bool IsRemoteFrame { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
