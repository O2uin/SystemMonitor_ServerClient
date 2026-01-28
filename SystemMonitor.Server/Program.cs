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
            var memCounter=new PerformanceCounter("Memory", "Available MBytes"); //남은 메모리 MB
            var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

            while (true)
            {
                float cpu = cpuCounter.NextValue();
                float mem = memCounter.NextValue();
                string status = "Normal";

                string packet = $"{cpu:F1}|{mem}|{status}";


                try
                {
                    using (TcpClient client = new TcpClient("127.0.0.1", 5000)) //접속 ip 127.0.0.1=>내 컴퓨터 변경하면 다른 컴에서 접속 가능
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] data = Encoding.UTF8.GetBytes(packet);
                        stream.Write(data, 0, data.Length);
                        Console.WriteLine($"[전송] {packet}");
                    }
                }
                catch { Console.WriteLine("서버가 꺼져있습니다."); }

                Thread.Sleep(1000);
            }
        }
    }
}
