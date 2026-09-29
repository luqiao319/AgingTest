using System;
using System.Runtime.InteropServices;

namespace AgingTest.NetCan
{
    /// <summary>
    /// ZLG zlgcan.dll 原生API导入，NetCAN-800H网口CAN
    /// </summary>
    public static class ZlgNativeApi
    {
        public delegate void ZCAN_CallBack(uint devIndex, uint chn, ref ZCAN_ReceiveFrame frame, IntPtr pUser);

        /// <summary>打开网口CAN设备，ip为NetCAN800H的IP地址</summary>
        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ZCAN_OpenDevice(string ip, uint devType, uint reserved);

        /// <summary>搜索局域网内NetCAN网口设备（用于IP配置工具）</summary>
        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ZCAN_SearchNetCanDevice(IntPtr pDevInfo, ref uint count);

        /// <summary>修改网口设备IP、子网掩码、网关，传入MAC定位设备</summary>
        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ZCAN_SetNetConfig(string mac, string ip, string mask, string gateway, string password);

        /// <summary>设置CAN通道波特率，支持CAN FD</summary>
        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ZCAN_SetBaudRate(IntPtr devHandle, uint chn, ref ZCAN_BaudConfig baudCfg);

        /// <summary>批量发送CAN报文</summary>
        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ZCAN_Transmit(IntPtr devHandle, uint chn, ref ZCAN_TransmitFrame frame, uint count);

        /// <summary>关闭设备，释放句柄资源</summary>
        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ZCAN_CloseDevice(IntPtr devHandle);

        /// <summary>波特率配置结构体</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ZCAN_BaudConfig
        {
            public uint baud;         //仲裁段波特
            public uint dataBaud;     //FD数据段波特
            public byte isCanFd;      //1=开启CAN FD，0=CAN2.0
            public byte termResistor; //1=开启120Ω终端电阻
            public byte listenOnly;   //1=只听模式
            public byte reserved;
        }

        /// <summary>发送报文结构体</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ZCAN_TransmitFrame
        {
            public uint id;
            public byte len;
            public byte isFd;
            public byte isRemote;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] data;
        }

        /// <summary>接收报文结构体</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ZCAN_ReceiveFrame
        {
            public uint id;
            public byte len;
            public byte isFd;
            public byte isRemote;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] data;
            public ulong timestamp;
        }

        /// <summary>搜索返回网口设备信息结构体</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct ZCAN_NetDeviceInfo
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string mac;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string ip;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string mask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string gateway;
        }
    }
}
