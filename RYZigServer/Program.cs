using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RYZigServer
{
    class Program
    {
        static void Main(string[] args)
        {
            //string ddd=TKcrc(13);
            string ddd = TKcrc2();

            //string dd1 = CRCCalc("06 06 00 02 01 04");
            //string dd2 = CRCCalc("06 05 00 08 00 01");
            //string dd3 = CRCCalc("06 05 00 08 01 01");
            //string dd4 = CRCCalc("06 05 00 08 02 01");
            RuiYingServer ruiYingServer = new RuiYingServer();
            ruiYingServer.Start();

            Console.Read();
        }

        /// <summary>
        /// CRC校验
        /// </summary>
        /// <param name="data">校验数据</param>
        /// <returns>高低8位</returns>
        public static string CRCCalc(string data)
        {
            string[] datas = data.Split(' ');
            List<byte> bytedata = new List<byte>();

            foreach (string str in datas)
            {
                bytedata.Add(byte.Parse(str, System.Globalization.NumberStyles.AllowHexSpecifier));
            }
            byte[] crcbuf = bytedata.ToArray();
            //计算并填写CRC校验码
            int crc = 0xffff;
            int len = crcbuf.Length;
            for (int n = 0; n < len; n++)
            {
                byte i;
                crc = crc ^ crcbuf[n];
                for (i = 0; i < 8; i++)
                {
                    int TT;
                    TT = crc & 1;
                    crc = crc >> 1;
                    crc = crc & 0x7fff;
                    if (TT == 1)
                    {
                        crc = crc ^ 0xa001;
                    }
                    crc = crc & 0xffff;
                }

            }
            string[] redata = new string[2];
            redata[1] = Convert.ToString((byte)((crc >> 8) & 0xff), 16);
            redata[0] = Convert.ToString((byte)((crc & 0xff)), 16);
            return (redata[0]) + " " + (redata[1]);
        }

        public static string TKcrc(int dd)
        {
            //sbyte d = Convert.ToSByte("23", 16);
            //Console.WriteLine(d);
            //Console.WriteLine(SByte.MaxValue.ToString("X"));
            //d = (sbyte)~d;
            //Console.WriteLine(d.ToString("X"));
            //Console.ReadLine();

            string strA = dd.ToString("x2"); 

            string dat = "0xE3 0xCC 0x00 0x01 01 "+ strA;
            dat = dat.Replace("0x", "");
            string[] array = dat.Split(' ');
            int sum = 0;
            foreach (string arrayElement in array)
            {
                sum += int.Parse(arrayElement, System.Globalization.NumberStyles.HexNumber);
            }
            //sum = ~sum + 1;
            sum = (sbyte)~sum;
            sum += 1;
            string strB = sum.ToString("x2");
            dat = dat + " 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " + strB + " 0D";
            return dat;
        }

        public static string TKcrc2()
        {
            string ddd = 253.ToString("x2");
            string dat = "02 55 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";
            string[] array = dat.Split(' ');
            int sum = 0;
            foreach (string arrayElement in array)
            {
                sum += int.Parse(arrayElement, System.Globalization.NumberStyles.HexNumber);
            }

            sum = (sbyte)~sum;
            sum += 1;
            //string strB = sum.ToString("X2");

            string strB = "";
            string sum16 = Convert.ToString(sum, 16);
            int sum16Length = sum16.Length;
            if (sum16Length>=2)
            {
                strB= sum16.Substring(sum16Length - 2, 2);
            }
            dat = dat +" "+ strB + " 0D";
            return dat;
        }
    }
}
