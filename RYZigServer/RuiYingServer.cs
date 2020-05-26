using Hsf.Redis.Service;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RYZigServer
{
    public class RuiYingServer
    {
        private static log4net.ILog log = log4net.LogManager.GetLogger("RuiYingServer");
        private static ConcurrentDictionary<string, Socket> Gateway_SessionDic = new ConcurrentDictionary<string, Socket>();
        public void Start()
        {
            //点击开始监听时 在服务端创建一个负责监听IP和端口号的Socket
            Socket socketWatch = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ip = IPAddress.Any;
            //创建对象端口
            IPEndPoint point = new IPEndPoint(ip, Convert.ToInt32("50000"));

            socketWatch.Bind(point);//绑定端口号
            Console.WriteLine("监听成功!"+DateTime.Now.ToString());
            log.Debug("监听成功!" + DateTime.Now.ToString());
            socketWatch.Listen(10);//设置监听

            //创建监听线程
            Thread thread = new Thread(Listen);
            thread.IsBackground = true;
            thread.Start(socketWatch);
        }

        /// <summary>
        /// 等待客户端的连接 并且创建与之通信的Socket
        /// </summary>
        public void Listen(object o)
        {
            try
            {
                Socket socketWatch = o as Socket;
                while (true)
                {
                    Socket socketSend = socketWatch.Accept();//等待接收客户端连接
                    Console.WriteLine(socketSend.RemoteEndPoint.ToString() + ":" + "连接成功!" + DateTime.Now.ToString());
                    log.Debug(socketSend.RemoteEndPoint.ToString() + ":" + "连接成功!" + DateTime.Now.ToString());
                    //开启一个新线程，执行接收消息方法
                    Thread r_thread = new Thread(Received);
                    r_thread.IsBackground = true;
                    r_thread.Start(socketSend);
                }
            }
            catch { }
        }

        /// <summary>
        /// 服务器端不停的接收客户端发来的消息
        /// </summary>
        /// <param name="o"></param>
        public void Received(object o)
        {
            try
            {
                Socket socketSend = o as Socket;
                string ip = ((IPEndPoint)socketSend.RemoteEndPoint).Address.ToString();
                while (true)
                {
                    //客户端连接服务器成功后，服务器接收客户端发送的消息
                    byte[] buffer = new byte[1024 * 1024 * 2];//
                    //实际接收到的有效字节数
                    int len = socketSend.Receive(buffer);
                    if (len == 0)
                    {
                        break;
                    }
                    //204为ccdd开头的cc
                    //99为从汉字转换到16进制ToHex》16进制字节strToToHexByte》
                    //《字节转16进制X2，《16进制字符串转字符串UnHex
                    if (buffer[0] == 204 || buffer[0] == 99 || buffer[0] == 227 || buffer[0] == 236)
                    {
                        string strData = string.Empty;
                        for (int i = 0; i < len; i++)
                        {
                            strData += buffer[i].ToString("X2") + " "; //十六进制
                        }
                        log.Debug(socketSend.RemoteEndPoint + "收到消息<<<<<<<<<：" + strData);
                        //Console.WriteLine(socketSend.RemoteEndPoint + "收到消息<<<<<<<<<：" + strData);

                        //string dd = buffer[0].ToString();
                        //log.Debug($"字节1为：{dd}， 转字符串长度：{strData.Length}");

                        if (strData.Length == 33 && strData.Substring(12, 2)=="05")
                        {
                            using (RedisHashService service = new RedisHashService())
                            {
                                //命令开关
                                //CC DD F1 06 05 00 08 01 01 8D EF 
                                //cc dd 开头固定
                                //f1 第一个485通道
                                //06 物理地址
                                //05 00 08
                                //00 继电器
                                //01 为打开继电器
                                //8D C8 为crc校验码
                                bool st = strData.Substring(24, 2) == "01" ? true : false;
                                string ff = strData.Substring(6, 2);
                                string mac = strData.Substring(9, 2);
                                string port = strData.Substring(21, 2);
                                int iport = int.Parse(port) + 1;
                                string key = ip + ";" + ff + ";" + mac + "_0;" + iport;//58.57.32.162;F1;06_0;3
                                service.SetEntryInHash("DeviceStatus", key, st.ToString());//解决默认posid都为0的问题
                                log.Debug($"状态改变：{key}：{st.ToString()}");
                            }
                        }
                        else if (strData.Length == 39 && strData.Substring(12, 2) == "20")
                        {
                            //39：手动操作开关为一组，
                            //cc dd f1 06 20 10 11 00 01 00 00 4e b0 cc dd f1 06 20 10 11 00 01 00 7f 0f 50 
                            //cc dd f1 01 20 10 13 00 01 00 00 76 96 cc dd f1 01 20 10 13 00 01 00 7f 37 76 
                            using (RedisStringService service = new RedisStringService())
                            {
                                Gateway_SessionDic.AddOrUpdate(ip, socketSend, (Func<string, Socket, Socket>)((key, oldValue) => socketSend));
                                service.Set("BusSwitch_" + ip, strData.Substring(9, 2), new TimeSpan(0, 0, 0, 120));//缓存2分钟
                                log.Debug($"缓存mac：BusSwitch_{ip}：{strData.Substring(9, 2)}");

                                //第七位为继电器编号左侧1,11（比指令00+11），第11位为状态（ff开，f7关）
                                //第一个开
                                //cc dd f1 05 20 10 11 00 01 00 80 0f 05 
                                //cc dd f1 05 20 10 11 00 01 00 ff 4e e5
                                //中间关
                                //cc dd f1 05 20 10 12 00 01 00 00 4a a5
                                //cc dd f1 05 20 10 12 00 01 00 7f 0b 45

                            }
                        }
                        else if (strData.Length == 24)
                        {
                            //修改mac地址CC DD F1 01 06 00 02 01 03 69 9B
                            //第一种24 
                            //cc dd f1 01 06 00 (01->02)
                            //cc dd f1 02 02 01 e8 aa （第二行的第四位02）
                            //CC DD F1 02 06 00 (02->04)
                            //CC DD F1 02 04 01 EB 39 （第二行的第四位04）
                            //cc dd f1 02 06 00 (02->03)
                            //cc dd f1 02 03 01 e9 09 
                            //cc dd f1 03 06 00 (03->02)
                            //cc dd f1 02 02 01 e9 48 
                            //cc dd f1 02 06 00 (02->01)
                            //cc dd f1 02 01 01 e8 69 
                            using (RedisStringService service = new RedisStringService())
                            {
                                Gateway_SessionDic.AddOrUpdate(ip, socketSend, (Func<string, Socket, Socket>)((key, oldValue) => socketSend));
                                service.Set("BusSwitch_" + ip, strData.Substring(12, 2), new TimeSpan(0, 0, 0, 120));//缓存2分钟
                                log.Debug($"BusSwitch_{ip} 修改为新的mac： {strData.Substring(12, 2)}");
                            }
                        }
                        else if (strData.Length == 21)
                        {
                            //修改mac地址成功
                            //CC DD F1 01 06 00 02 01 03 69 9B
                            //cc dd f1 01 06 00 02 01 01 e8 5a 
                            //CC DD F1 01 06 00 02 01 01 E8 5A
                            //第二种* 21
                            //CC DD F1 02 06 00 02 (02->01)
                            //CC DD F1 01 01 E8 69 
                            //CC DD F1 04 06 00 02 
                            //CC DD F1 05 01 EA CF 
                            //cc dd F1 03 06 00 02 01 04 29 bb(03->04)
                            //CC DD F1 03 06 00 02
                            //CC DD F1 04 01 EA E8 
                            //第三种33
                            //cc dd f1 01 06 00 02 02 01 e8 aa (未处于配置状态)
                            using (RedisStringService service = new RedisStringService())
                            {
                                Gateway_SessionDic.AddOrUpdate(ip, socketSend, (Func<string, Socket, Socket>)((key, oldValue) => socketSend));
                                service.Set("BusSwitch_" + ip, strData.Substring(9, 2), new TimeSpan(0, 0, 0, 120));//缓存2分钟
                                log.Debug($"BusSwitch_{ip} 修改为新的mac： {strData.Substring(9, 2)}");
                            }
                        }
                        else if (strData.Contains("7C") && strData.Contains("3B"))//|;
                        {
                            //35 38 2E 35 37 2E 33 32 2E 31 36 32 3B 46 31 3B 30 35 7C 63 63 20 64 64 20 30 31 20 30 36 20 30 30 20 30 32 20 30 31 20 30 35 20 65 39 20 39 39 
                            //63 63 20 64 64 20 
                            //cc dd f1 06 05 00 08 00 01 8c 7f|192.168.82.107;f1;06
                            //cc dd f1 01 06 00 02 01 05 e9 99|58.57.32.162;F1;05  
                            //cc dd f1 01 06 00 02 01 05 e9 99|192.168.82.107;f1;06
                            string strDataStr = UnHex(strData, "utf-8");
                            log.Debug($"收到请求： {strDataStr}");

                            string ipmac = strDataStr.Split('|')[1].ToString();
                            string wwip = ipmac.Split(';')[0].ToString();
                            string msg = strDataStr.Split('|')[0].ToString();
                            //1.开关开关指令，需要三个参数，开关物理地址+继电器地址+开关值+crc校验
                            //01 05 00 08 00 01 8D C8 
                            //第一位 01 为开关物理地址
                            //第五位 00(00为第一个继电器，01为第二个继电器，02为第三个继电器)
                            //第六位 01 为打开继电器，00为关闭继电器
                            //第七八位 为crc校验码，百度查找c# 的crc校验函数

                            //2.发送配置物理地址指令
                            //01 06 00 02 01 03 69 9B
                            //第六位 03 为开关新的物理地址,第七八位 为crc校验码
                            //新的固件第一位01固定，老的固件第一位为原mac地址

                            if (Gateway_SessionDic.ContainsKey(wwip))
                            {
                                byte[] bytes = strToToHexByte(msg);
                                Gateway_SessionDic[wwip].Send(bytes);
                                log.Debug($"给控制器{wwip}发送指令：{msg}");
                            }
                            else
                            {
                                log.Debug($"请求网关的session不存在 {ipmac}： {msg}");
                            }
                        }
                        else if (strData.Length == 66)
                        {
                            string mac = strData.Substring(6, 2);
                            string fl = strData.Substring(15, 2);

                            byte[] bytes = strToToHexByte(strData);
                            //Gateway_SessionDic[fl].Send(bytes);
                            log.Debug($"给控制器{fl}发送指令：{strData}");


                            //if (Gateway_SessionDic.ContainsKey(fl))
                            //{
                            //    byte[] bytes = strToToHexByte(strData);
                            //    Gateway_SessionDic[fl].Send(bytes);
                            //    log.Debug($"给控制器{fl}发送指令：{strData}");
                            //}
                            //else
                            //{
                            //    log.Debug($"请求网关的session不存在 {fl}： {strData}");
                            //}

                            using (RedisHashService service = new RedisHashService())
                            {
                                service.SetEntryInHash("DeviceStatus", mac, "_"+fl);
                                log.Debug($"状态电梯改变：{mac}：{fl}");
                            }
                        }
                        else if (strData.Length == 15)
                        {
                            //确认梯控应答
                            //EC 88 08 01 0D
                            string mac = strData.Substring(6, 2);
                            using (RedisHashService service = new RedisHashService())
                            {
                                string _fl=service.GetValueFromHash("DeviceStatus", mac);
                                string fl = _fl.Replace("_", "");
                                service.SetEntryInHash("DeviceStatus", mac,  fl);
                                log.Debug($"状态电梯改变：{mac}：{fl}");
                            }
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 十六进制字符串转十六进制字节数组
        /// </summary>
        /// <param name="hexString">十六进制字符串</param>
        /// <returns></returns>
        private byte[] strToToHexByte(string hexString)
        {
            hexString = hexString.Replace(" ", "");
            if ((hexString.Length % 2) != 0)
                hexString += " ";
            byte[] returnBytes = new byte[hexString.Length / 2];
            for (int i = 0; i < returnBytes.Length; i++)
                returnBytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2).Trim(), 16);
            return returnBytes;
        }

        /// <summary>
        /// 从汉字转换到16进制
        /// </summary>
        /// <param name="s"></param>
        /// <param name="charset">编码,如"utf-8","gb2312"</param>
        /// <param name="fenge">是否每字符用逗号分隔</param>
        /// <returns></returns>
        public static string ToHex(string s, string charset, bool fenge)
        {
            if ((s.Length % 2) != 0)
            {
                s += " ";//空格
                         //throw new ArgumentException("s is not valid chinese string!");
            }
            System.Text.Encoding chs = System.Text.Encoding.GetEncoding(charset);
            byte[] bytes = chs.GetBytes(s);
            string str = "";
            for (int i = 0; i < bytes.Length; i++)
            {
                str += string.Format("{0:X}", bytes[i]);
                if (fenge && (i != bytes.Length - 1))
                {
                    str += string.Format("{0}", ",");
                }
            }
            return str.ToLower();
        }

        ///<summary>
        /// 从16进制转换成汉字
        /// </summary>
        /// <param name="hex"></param>
        /// <param name="charset">编码,如"utf-8","gb2312"</param>
        /// <returns></returns>
        public static string UnHex(string hex, string charset)
        {
            if (hex == null)
                throw new ArgumentNullException("hex");
            hex = hex.Replace(",", "");
            hex = hex.Replace("\n", "");
            hex = hex.Replace("\\", "");
            hex = hex.Replace(" ", "");
            if (hex.Length % 2 != 0)
            {
                hex += "20";//空格
            }
            // 需要将 hex 转换成 byte 数组。 
            byte[] bytes = new byte[hex.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                try
                {
                    // 每两个字符是一个 byte。 
                    bytes[i] = byte.Parse(hex.Substring(i * 2, 2),
                    System.Globalization.NumberStyles.HexNumber);
                }
                catch
                {
                    // Rethrow an exception with custom message. 
                    throw new ArgumentException("hex is not a valid hex number!", "hex");
                }
            }
            System.Text.Encoding chs = System.Text.Encoding.GetEncoding(charset);
            return chs.GetString(bytes);
        }
        ///// <summary>
        ///// 服务器向客户端发送消息
        ///// </summary>
        ///// <param name="str"></param>
        //public void Send(string str)
        //{
        //    //byte[] buffer = Encoding.UTF8.GetBytes(str);
        //    //socketSend.Send(buffer);

        //    //发送数据（只支持十六进制数据）
        //    if (str.Trim().Length < 1) return;
        //    string strData = str.Trim();
        //    if (null != socketSend)
        //    {
        //        if (SendData(strData))
        //        {
        //            Console.WriteLine("发送成功：" + strData);
        //        }
        //        else
        //        {
        //            Console.WriteLine("发送失败：" + strData);
        //        }
        //    }
        //}

        ///// <summary>
        ///// 发送数据
        ///// </summary>
        ///// <param name="strData">十六进制字符串</param>
        ///// <returns>是否发送成功</returns>
        //public bool SendData(string strData)
        //{
        //    if (string.IsNullOrEmpty(strData))
        //    {
        //        return false;
        //    }
        //    byte[] bytes = null;
        //    if (strData.StartsWith("2a"))
        //    {
        //        bytes = strToToHexByte(strData);
        //    }
        //    else
        //    {
        //        bytes = Encoding.UTF8.GetBytes(strData);
        //    }

        //    try
        //    {
        //        socketSend.Send(bytes);
        //        return true;
        //    }
        //    catch
        //    {
        //        //socketSend.Shutdown(SocketShutdown.Both);
        //        //socketSend.Close();
        //        //if (Connect())
        //        //{
        //        //    return SendData(strData);
        //        //}
        //    }
        //    return false;
        //}
    }
}
