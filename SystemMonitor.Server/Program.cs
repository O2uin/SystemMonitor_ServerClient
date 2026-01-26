using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;

namespace SystemMonitor.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

            while (true)
            {
                float cpuUsage = cpuCounter.NextValue();
                string message = cpuUsage.ToString("F1");

                try
                {
                    using (TcpClient client = new TcpClient("127.0.0.1", 5000)) //접속
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] data = Encoding.UTF8.GetBytes(message);
                        stream.Write(data, 0, data.Length);
                        Console.WriteLine($"전송 완료: {message}%");
                    }
                }
                catch { Console.WriteLine("서버가 꺼져있습니다."); }

                Thread.Sleep(1000);
            }
        }
    }
}
