using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using System.Net;

public class MonitorPacket
{
    public string Mode { get; set; } // DATA / COMMAND
    public double Cpu { get; set; }
    public double Mem { get; set; }
    public double Gpu { get; set; }
    public double Net { get; set; }
    public double Disk { get; set; }
    public string Command { get; set; }
}

namespace SystemMonitor.Server
{
    class Program
    {
        static void Main(string[] args)
        {

            var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            var memCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
            var diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");

            var category = new PerformanceCounterCategory("GPU Engine");
            var gpuCounters = category.GetInstanceNames()
                .Where(instance => instance.EndsWith("engtype_3D"))
                .SelectMany(instance => category.GetCounters(instance))
                .Where(counter => counter.CounterName == "Utilization Percentage")
                .ToList();


            var netCategory = new PerformanceCounterCategory("Network Interface");
            string instanceName = netCategory.GetInstanceNames()[0];
            var recvCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", instanceName);
            var sentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instanceName);

            //접속
            TcpListener listener = new TcpListener(IPAddress.Any, 5000);
            listener.Start();
            Console.WriteLine("서버 대기 중... (Port: 5000)");

            while (true)
            {
                using (TcpClient client = listener.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                {
                    Console.WriteLine("클라이언트 연결됨.");

                    while (client.Connected)
                    {
                        try
                        {
                            
                            float cpu = cpuCounter.NextValue();
                            float mem = memCounter.NextValue();
                            float disk = diskCounter.NextValue();
                            float gpuUsage = 0;
                            try { gpuUsage = gpuCounters.Sum(c => c.NextValue()); } catch { }
                            float netSpeed = (recvCounter.NextValue() + sentCounter.NextValue()) / 1024 / 1024;

                            //json
                            var packetObj = new MonitorPacket
                            {
                                Mode = "DATA",
                                Cpu = Math.Round(cpu, 1),
                                Mem = Math.Round(mem, 1),
                                Gpu = Math.Round(gpuUsage, 1),
                                Net = Math.Round(netSpeed, 2),
                                Disk = Math.Round(disk, 1)
                            };

                            string json = JsonConvert.SerializeObject(packetObj) + "\n";
                            byte[] data = Encoding.UTF8.GetBytes(json);
                            stream.Write(data, 0, data.Length);
                            Console.WriteLine($"[전송] {json.Trim()}");

                            if (stream.DataAvailable)
                            {
                                byte[] buffer = new byte[1024];
                                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                                string receivedJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                var cmd = JsonConvert.DeserializeObject<MonitorPacket>(receivedJson);

                                if (cmd != null && cmd.Mode == "COMMAND")
                                {
                                    HandleCommand(cmd.Command);
                                }
                            }
                        }
                        catch { break; }
                        Thread.Sleep(1000);
                    }
                }
            }
        }

        static void HandleCommand(string command)
        {
            Console.WriteLine($"[명령 수신] {command}");
            if (command == "SHUTDOWN") Process.Start("shutdown", "/s /t 0");
            if (command == "REBOOT") Process.Start("shutdown", "/r /t 0");
        }
    }
}