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
            var memCounter=new PerformanceCounter("Memory", "% Committed Bytes In Use"); //사용중인 메모리%
            var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");//cpu
            var category = new PerformanceCounterCategory("GPU Engine");//gpu
            var counters = category.GetInstanceNames()
                .Where(instance => instance.EndsWith("engtype_3D")) //3D 렌더링 부하 == 실제 사용량
                .SelectMany(instance => category.GetCounters(instance))
                .Where(counter => counter.CounterName == "Utilization Percentage")
                .ToList();
            var netCategory = new PerformanceCounterCategory("Network Interface");//네트워크 속도
            string instanceName = netCategory.GetInstanceNames()[0];
            var recvCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", instanceName);
            var sentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instanceName);

            while (true)
            {
                float cpu = cpuCounter.NextValue();
                float mem = memCounter.NextValue();
                //string status = "Normal";
                float gpuUsage;
                try
                {
                    gpuUsage = counters.Sum(c => c.NextValue());
                }
                catch
                {
                    gpuUsage = 0;
                }
                
                float netSpeed = (recvCounter.NextValue()+sentCounter.NextValue()) / 1024 / 1024; //==> 업로드+다운로드 MB/s
                

                string packet = $"{cpu:F1}|{mem}|{gpuUsage}|{netSpeed}";


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
