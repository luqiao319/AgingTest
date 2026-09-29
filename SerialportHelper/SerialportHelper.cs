using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;


namespace Common.SerialportHelper
{
    /// <summary>
    /// 冠佳GJDA‑100‑32 32通道电子负载 RS485驱动类
    /// 协议版本V1.0，私有二进制帧，非Modbus
    /// 规则：协议固定常量写死；所有业务电压、电流、模式、附加参数全部外部传入
    /// </summary>
    public class GJDA10032
    {
        #region 【仅协议固定常量，不包含任何业务数值】
        /// <summary>帧头 SOI 协议固定</summary>
        private const byte SOI = 0xEE;
        /// <summary>帧尾 EOI 协议固定</summary>
        private const byte EOI = 0xEF;
        /// <summary>转义替换字节 协议固定</summary>
        private const byte ESC_REPLACE = 0xED;
        /// <summary>设备出厂默认从站地址77(0x4D)，仅做默认值，可外部构造函数覆盖</summary>
        public byte DefaultDeviceAddress = 0x4D;

        /// <summary>CID：读取全部32通道实时数据 协议固定命令码</summary>
        public const byte CID_ReadAll32Ch = 0x0F;
        /// <summary>CID：设置1‑16通道负载参数，保存E2ROM 协议固定命令码</summary>
        public const byte CID_Set16Ch_Save = 0x40;
        /// <summary>CID：设置1‑16通道负载参数，不保存E2ROM 协议固定命令码</summary>
        public const byte CID_Set16Ch_NoSave = 0x41;
        /// <summary>CID：设置17‑32通道负载参数，保存E2ROM 协议固定命令码</summary>
        public const byte CID_Set17_32Ch_Save = 0x42;
        /// <summary>CID：设置17‑32通道负载参数，不保存E2ROM 协议固定命令码</summary>
        public const byte CID_Set17_32Ch_NoSave = 0x43;
        /// <summary>CID：单通道设置负载参数，不保存E2ROM 协议固定命令码</summary>
        public const byte CID_SetSingleCh_NoSave = 0x11;
        /// <summary>CID：单通道设置负载参数，保存E2ROM 协议固定命令码</summary>
        public const byte CID_SetSingleCh_Save = 0x12;
        /// <summary>CID：全部32通道设置CC‑Slow电流，不保存E2ROM 协议固定命令码</summary>
        public const byte CID_SetAllCh_CCSlowCurrent = 0x46;
        /// <summary>CID：全部32通道设置Von电压，不保存E2ROM 协议固定命令码</summary>
        public const byte CID_SetAllCh_Von = 0x47;

        /// <summary>返回码：正常F0 协议固定</summary>
        public const byte RTN_OK = 0xF0;
        /// <summary>返回码：校验和错误F1 协议固定</summary>
        public const byte RTN_ChecksumErr = 0xF1;
        /// <summary>返回码：长度错误F2 协议固定</summary>
        public const byte RTN_LengthErr = 0xF2;
        /// <summary>返回码：无效命令CID F3 协议固定</summary>
        public const byte RTN_CidInvalid = 0xF3;
        /// <summary>返回码：无效数据F4 协议固定</summary>
        public const byte RTN_DataInvalid = 0xF4;
        #endregion

        /// <summary>设备型号标识</summary>
        public string PowerType { get; set; } = "GJDA‑100‑32";

        /// <summary>串口实例</summary>
        private SerialPort? _serialPort;

        /// <summary>当前设备从站地址，可外部修改</summary>
        public byte DeviceAddress { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="portName">串口名 COMx</param>
        /// <param name="baud">波特率9600/19200/38400/57600，外部传入</param>
        /// <param name="devAddr">设备从站地址，不传使用默认0x4D</param>
        public void Open(string portName, int baud, byte? devAddr = null)
        {
            if (_serialPort != null && _serialPort.IsOpen)
                return;
            DeviceAddress = devAddr ?? DefaultDeviceAddress;
            _serialPort = new SerialPort(portName);
            _serialPort.BaudRate = baud;
            _serialPort.DataBits = 8;
            _serialPort.Parity = Parity.None;
            _serialPort.StopBits = StopBits.One;
            _serialPort.ReadTimeout = 2000;
            _serialPort.WriteTimeout = 1000;
            _serialPort.Open();
        }

        /// <summary>关闭串口释放资源</summary>
        //GJDA10032类里Close参考
        public void Close()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }
            _serialPort?.Dispose();
            _serialPort = null;
        }


        #region 底层协议工具（仅协议算法，无业务数值）
        /// <summary>帧字节转义：0xEE、0xEF替换为0xED，协议规则</summary>
        private byte[] EscapeBytes(byte[] input)
        {
            List<byte> outBuf = new List<byte>();
            foreach (var b in input)
            {
                if (b == SOI || b == EOI)
                {
                    outBuf.Add(ESC_REPLACE);
                }
                else
                {
                    outBuf.Add(b);
                }
            }
            return outBuf.ToArray();
        }

        /// <summary>反向转义预留，协议说明不需要还原</summary>
        private byte[] UnEscapeBytes(byte[] input)
        {
            return input;
        }

        /// <summary>计算校验和，协议算法</summary>
        private byte[] CalcCheckSum(byte adr, byte cid, byte len, byte[] info)
        {
            int sum = adr + cid + len;
            foreach (var b in info) sum += b;
            ushort total = (ushort)(sum & 0xFFFF);
            byte h = (byte)((total >> 8) & 0xFF);
            byte l = (byte)(total & 0xFF);
            return new[] { h, l };
        }

        /// <summary>组装完整发送帧，协议帧结构</summary>
        private byte[] BuildFrame(byte adr, byte cid, byte[] info)
        {
            byte length = (byte)(info.Length + 3);
            byte[] chkSum = CalcCheckSum(adr, cid, length, info);

            List<byte> mid = new List<byte>();
            mid.Add(adr);
            mid.Add(cid);
            mid.Add(length);
            mid.AddRange(info);
            mid.AddRange(chkSum);

            byte[] midEsc = EscapeBytes(mid.ToArray());

            List<byte> fullFrame = new List<byte>();
            fullFrame.Add(SOI);
            fullFrame.AddRange(midEsc);
            fullFrame.Add(EOI);
            return fullFrame.ToArray();
        }

        /// <summary>底层发送接收帧，协议通信逻辑</summary>
        private byte[] SendFrameAndReceive(byte adr, byte cid, byte[] info, bool expectResponse = true, int retry = 0)
        {
            if (!_serialPort.IsOpen)
                throw new InvalidOperationException($"{PowerType}串口未打开，请检查串口");

            byte[] sendBuf = BuildFrame(adr, cid, info);
            _serialPort.Write(sendBuf, 0, sendBuf.Length);

            if (!expectResponse)
            {
                return null;
            }

            List<byte> recvBuf = new List<byte>();
            DateTime start = DateTime.Now;
            int timeoutMs = 2000;
            bool foundSoi = false;
            byte[] resultInfo = null;
            while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
            {
                if (_serialPort.BytesToRead > 0)
                {
                    byte temp = (byte)_serialPort.ReadByte();
                    if (!foundSoi)
                    {
                        if (temp == SOI)
                        {
                            foundSoi = true;
                            recvBuf.Clear();
                        }
                        continue;
                    }
                    if (temp == EOI)
                    {
                        byte rtnCid = recvBuf[1];
                        if (rtnCid != RTN_OK)
                        {
                            string errMsg = rtnCid switch
                            {
                                RTN_ChecksumErr => "校验和错误F1",
                                RTN_LengthErr => "帧长度错误F2",
                                RTN_CidInvalid => "无效命令CID F3",
                                RTN_DataInvalid => "无效数据F4",
                                _ => $"未知返回码0x{rtnCid:X2}"
                            };
                            throw new Exception($"{PowerType}设备返回错误：{errMsg}");
                        }
                        int infoLen = recvBuf[2] - 3;
                        resultInfo = recvBuf.Skip(3).Take(infoLen).ToArray();
                        break;
                    }
                    recvBuf.Add(temp);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }

            if (resultInfo == null)
            {
                retry++;
                if (retry > 2)
                {
                    throw new TimeoutException($"{PowerType}通信超时，未收到设备应答，请检查485接线、波特率、设备地址");
                }
                Thread.Sleep(20);
                return SendFrameAndReceive(adr, cid, info, expectResponse, retry);
            }
            return UnEscapeBytes(resultInfo);
        }
        #endregion

        #region 数据模型
        /// <summary>单通道实时采集结果模型</summary>
        public class ChannelRealData
        {
            /// <summary>通道编号1‑32</summary>
            public int ChNo { get; set; }
            /// <summary>通道电压(V)</summary>
            public double Voltage { get; set; }
            /// <summary>通道电流(A)</summary>
            public double Current { get; set; }
            /// <summary>原始状态字2字节</summary>
            public ushort StatusRaw { get; set; }
            public bool DcDcOn { get; set; }
            public bool Ovp { get; set; }
            public bool Opp { get; set; }
            public bool Otp { get; set; }
            public bool BoostOverCurrent { get; set; }
            public bool LlcOverCurrent { get; set; }
        }

        /// <summary>全部32通道读取返回结果</summary>
        public class All32ChannelResult
        {
            public List<ChannelRealData> Channels { get; set; }
            public byte InverterStatusRaw { get; set; }
        }

        /// <summary>单通道设置参数结构体，所有业务参数全部由调用方赋值，类内部不写死任何默认业务值</summary>
        public class ChSetParam
        {
            /// <summary>模式：0=CCSlow，1=CV，2=CP，3=CR，4=CCFast，调用方传入</summary>
            public byte Mode { get; set; }
            /// <summary>Von点电压(V)，调用方传入</summary>
            public double VonVolt { get; set; }
            /// <summary>负载设定值(A/V/W/Ω)，调用方传入</summary>
            public double SetValue { get; set; }
            /// <summary>附加参数，调用方传入</summary>
            public byte ExtraParam { get; set; }
        }
        #endregion

        #region 对外业务接口，全部业务数值由参数传入
        /// <summary>读取全部32通道实时数据</summary>
        public All32ChannelResult ReadAll32Channel()
        {
            byte[] infoResp = SendFrameAndReceive(DeviceAddress, CID_ReadAll32Ch, new byte[0], true);
            if (infoResp.Length != 193)
                throw new Exception($"{PowerType}读取32通道返回数据长度异常，预期193字节，实际{infoResp.Length}");

            All32ChannelResult res = new All32ChannelResult();
            res.Channels = new List<ChannelRealData>();
            res.InverterStatusRaw = infoResp[192];

            for (int i = 0; i < 32; i++)
            {
                int offset = i * 6;
                byte b1 = infoResp[offset];
                byte b2 = infoResp[offset + 1];
                byte b3 = infoResp[offset + 2];
                byte b4 = infoResp[offset + 3];
                byte b5 = infoResp[offset + 4];
                byte b6 = infoResp[offset + 5];

                ushort voltRaw = (ushort)((b1 << 8) | b2);
                ushort currRaw = (ushort)((b3 << 8) | b4);
                ushort statusRaw = (ushort)((b5 << 8) | b6);

                ChannelRealData ch = new ChannelRealData();
                ch.ChNo = i + 1;
                ch.Voltage = voltRaw / 20.0;
                ch.Current = currRaw / 100.0;
                ch.StatusRaw = statusRaw;

                ch.DcDcOn = (statusRaw & (1 << 0)) != 0;
                ch.Ovp = (statusRaw & (1 << 1)) != 0;
                ch.Opp = (statusRaw & (1 << 4)) != 0;
                ch.Otp = (statusRaw & (1 << 5)) != 0;
                ch.BoostOverCurrent = (statusRaw & (3 << 8)) != 0;
                ch.LlcOverCurrent = (statusRaw & (1 << 11)) != 0;
                res.Channels.Add(ch);
            }
            return res;
        }

        /// <summary>设置1‑16通道批量参数，全部配置由外部paramList传入</summary>
        /// <param name="paramList">16个通道参数，外部构造</param>
        /// <param name="saveToE2Rom">是否保存到E2ROM</param>
        public void SetBatch1To16Channel(List<ChSetParam> paramList, bool saveToE2Rom)
        {
            if (paramList.Count != 16)
                throw new ArgumentException("SetBatch1To16Channel必须传入16个通道配置");
            List<byte> infoBuf = new List<byte>();
            foreach (var p in paramList)
            {
                infoBuf.Add(p.Mode);
                ushort vonRaw = (ushort)(p.VonVolt * 20);
                infoBuf.Add((byte)((vonRaw >> 8) & 0xFF));
                infoBuf.Add((byte)(vonRaw & 0xFF));

                ushort valRaw = p.Mode switch
                {
                    0 or 4 => (ushort)(p.SetValue * 100),
                    1 => (ushort)(p.SetValue * 20),
                    2 => (ushort)(p.SetValue * 10),
                    3 => (ushort)(p.SetValue * 10),
                    _ => 0
                };
                infoBuf.Add((byte)((valRaw >> 8) & 0xFF));
                infoBuf.Add((byte)(valRaw & 0xFF));
                infoBuf.Add(p.ExtraParam);
            }
            byte cid = saveToE2Rom ? CID_Set16Ch_Save : CID_Set16Ch_NoSave;
            SendFrameAndReceive(DeviceAddress, cid, infoBuf.ToArray(), true);
        }

        /// <summary>设置17‑32通道批量参数，全部配置外部传入</summary>
        public void SetBatch17To32Channel(List<ChSetParam> paramList, bool saveToE2Rom)
        {
            if (paramList.Count != 16)
                throw new ArgumentException("SetBatch17To32Channel必须传入16个通道配置");
            List<byte> infoBuf = new List<byte>();
            foreach (var p in paramList)
            {
                infoBuf.Add(p.Mode);
                ushort vonRaw = (ushort)(p.VonVolt * 20);
                infoBuf.Add((byte)((vonRaw >> 8) & 0xFF));
                infoBuf.Add((byte)(vonRaw & 0xFF));

                ushort valRaw = p.Mode switch
                {
                    0 or 4 => (ushort)(p.SetValue * 100),
                    1 => (ushort)(p.SetValue * 20),
                    2 => (ushort)(p.SetValue * 10),
                    3 => (ushort)(p.SetValue * 10),
                    _ => 0
                };
                infoBuf.Add((byte)((valRaw >> 8) & 0xFF));
                infoBuf.Add((byte)(valRaw & 0xFF));
                infoBuf.Add(p.ExtraParam);
            }
            byte cid = saveToE2Rom ? CID_Set17_32Ch_Save : CID_Set17_32Ch_NoSave;
            SendFrameAndReceive(DeviceAddress, cid, infoBuf.ToArray(), true);
        }

        /// <summary>单通道设置，通道号、参数全部外部传入</summary>
        public void SetSingleChannel(int chNo, ChSetParam param, bool saveE2Rom)
        {
            if (chNo < 1 || chNo > 32) throw new ArgumentOutOfRangeException(nameof(chNo));
            List<byte> infoBuf = new List<byte>();
            infoBuf.Add((byte)chNo);
            infoBuf.Add(param.Mode);
            ushort vonRaw = (ushort)(param.VonVolt * 20);
            infoBuf.Add((byte)((vonRaw >> 8) & 0xFF));
            infoBuf.Add((byte)(vonRaw & 0xFF));

            ushort valRaw = param.Mode switch
            {
                0 or 4 => (ushort)(param.SetValue * 100),
                1 => (ushort)(param.SetValue * 20),
                2 => (ushort)(param.SetValue * 10),
                3 => (ushort)(param.SetValue * 10),
                _ => 0
            };
            infoBuf.Add((byte)((valRaw >> 8) & 0xFF));
            infoBuf.Add((byte)(valRaw & 0xFF));
            infoBuf.Add(param.ExtraParam);

            byte cid = saveE2Rom ? CID_SetSingleCh_Save : CID_SetSingleCh_NoSave;
            SendFrameAndReceive(DeviceAddress, cid, infoBuf.ToArray(), true);
        }

        /// <summary>全部32通道设置CC‑Slow电流，电流数组外部完整传入</summary>
        public void SetAllChannelCcSlowCurrent(List<double> currList)
        {
            if (currList.Count != 32) throw new ArgumentException("必须传入32通道电流数组");
            List<byte> infoBuf = new List<byte>();
            foreach (var a in currList)
            {
                ushort raw = (ushort)(a * 100);
                infoBuf.Add((byte)((raw >> 8) & 0xFF));
                infoBuf.Add((byte)(raw & 0xFF));
            }
            SendFrameAndReceive(DeviceAddress, CID_SetAllCh_CCSlowCurrent, infoBuf.ToArray(), true);
        }

        /// <summary>设置全部32通道Von电压，电压数组外部完整传入</summary>
        public void SetAllChannelVonVoltage(List<double> vonList)
        {
            if (vonList.Count != 32) throw new ArgumentException("必须传入32通道Von电压数组");
            List<byte> infoBuf = new List<byte>();
            foreach (var v in vonList)
            {
                ushort raw = (ushort)(v * 20);
                infoBuf.Add((byte)((raw >> 8) & 0xFF));
                infoBuf.Add((byte)(raw & 0xFF));
            }
            SendFrameAndReceive(DeviceAddress, CID_SetAllCh_Von, infoBuf.ToArray(), true);
        }

        /// <summary>连通检查</summary>
        public bool CheckConnect()
        {
            try
            {
                _ = ReadAll32Channel();
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}
