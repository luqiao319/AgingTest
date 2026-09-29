using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AgingTest.NetCan
{
    /// <summary>
    /// NetCAN800H 网络参数配置工具类
    /// 功能：广播搜索局域网设备、修改IP、掩码、网关、恢复出厂IP
    /// 【独立，不需要打开CAN通信通道】
    /// </summary>
    public static class NetCan800HNetConfig
    {
        /// <summary>广播搜索局域网所有NetCAN800H设备</summary>
        public static List<NetCanDeviceInfo> SearchDevices()
        {
            var list = new List<NetCanDeviceInfo>();
            uint devCount = 12; //最多搜索10台设备
            int structSize = Marshal.SizeOf<ZlgNativeApi.ZCAN_NetDeviceInfo>();
            IntPtr buffer = Marshal.AllocHGlobal(structSize * (int)devCount);

            try
            {
                int ret = ZlgNativeApi.ZCAN_SearchNetCanDevice(buffer, ref devCount);
                if (ret < 0) return list;

                for (int i = 0; i < devCount; i++)
                {
                    IntPtr pItem = buffer + i * structSize;
                    var info = Marshal.PtrToStructure<ZlgNativeApi.ZCAN_NetDeviceInfo>(pItem);
                    list.Add(new NetCanDeviceInfo
                    {
                        Mac = info.mac,
                        CurrentIp = info.ip,
                        SubnetMask = info.mask,
                        Gateway = info.gateway
                    });
                }
            }
            catch
            {
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            return list;
        }

        /// <summary>
        /// 设置设备新IP、子网掩码、网关（MAC定位设备，需要设备配置密码）
        /// </summary>
        /// <param name="mac">设备MAC地址</param>
        /// <param name="newIp">新IP</param>
        /// <param name="subnetMask">子网掩码</param>
        /// <param name="gateway">网关</param>
        /// <param name="password">设备配置密码（800H默认88888）</param>
        /// <returns>成功true，失败false</returns>
        public static bool SetDeviceIp(string mac, string newIp, string subnetMask, string gateway, string password)
        {
            try
            {
                // ZCAN_SetNetConfig 追加密码参数
                int ret = ZlgNativeApi.ZCAN_SetNetConfig(mac, newIp, subnetMask, gateway, password);
                return ret == 0;
            }
            catch
            {
                return false;
            }
        }

        ///// <summary>一键恢复NetCAN800H出厂IP（ZLG默认：192.168.0.100）</summary>
        //public static bool ResetToFactoryIp(string mac)
        //{
        //    return SetDeviceIp(mac, "192.168.0.100", "255.255.255.0", "192.168.0.1");
        //}

        /// <summary>
        /// 根据设备序号获取预设IP（12台：1~12号设备对应IP从10开始）
        /// 设备编号1 → 192.168.0.10
        /// 设备编号12 → 192.168.0.21
        /// </summary>
        /// <param name="devNo">设备序号 1~12</param>
        /// <returns>ip字符串</returns>
        public static string GetPresetIpByDevNo(int devNo)
        {
            if (devNo < 1 || devNo > 12)
                throw new ArgumentOutOfRangeException(nameof(devNo), "设备序号范围只能是1~12");
            var lastSegment = 10 + devNo - 1;
            return $"192.168.0.{lastSegment}";
        }

        /// <summary>
        /// 一键设置指定编号设备到预设IP（1号=10，12号=21）
        /// </summary>
        /// <param name="mac">设备MAC</param>
        /// <param name="devNo">设备序号1~12</param>
        /// <summary>
        /// 一键设置指定编号设备到预设IP（1号=192.168.0.10，12号=192.168.0.21）
        /// </summary>
        /// <param name="mac">设备MAC</param>
        /// <param name="devNo">设备序号1~12</param>
        /// <param name="password">设备配置密码（800H修改IP需要密码，默认88888）</param>
        /// <returns>true成功，false失败</returns>
        public static bool SetPresetIpByDevNo(string mac, int devNo, string password)
        {
            // 校验设备编号范围
            if (devNo < 1 || devNo > 12)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }
            string ip = GetPresetIpByDevNo(devNo);
            // 子网掩码、网关，加上密码传给底层SDK
            return SetDeviceIp(mac, ip, "255.255.255.0", "192.168.0.1", password);
        }


    }

    /// <summary>搜索返回设备信息实体</summary>
    public class NetCanDeviceInfo
    {
        public string Mac { get; set; }
        public string CurrentIp { get; set; }
        public string SubnetMask { get; set; }
        public string Gateway { get; set; }
    }
}
