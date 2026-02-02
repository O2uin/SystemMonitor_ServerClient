using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json; // NuGet에서 설치 필수
using System.IO;

namespace SystemMonitor
{
    // 데이터 규격 클래스 (서버와 동일해야 함)
    public class MonitorPacket
    {
        public string Mode { get; set; } // DATA 또는 COMMAND
        public double Cpu { get; set; }
        public double Mem { get; set; }
        public double Gpu { get; set; }
        public double Net { get; set; }
        public double Disk { get; set; }
        public string Command { get; set; }
    }

    public partial class MainWindow : Window
    {
        private TcpClient client;
        private NetworkStream stream;
        private bool isRunning = true;

        // 데이터 리스트
        private List<double> cpuValues = new List<double>();
        private List<double> ramValues = new List<double>();
        private List<double> gpuValues = new List<double>();
        private List<double> netValues = new List<double>();

        private const int MaxPoints = 50;
        private double minCpu = 100, maxCpu = 0, sumCpu = 0;
        private int count = 0;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += (s, e) => {
                InitializeGrids();
                ConnectToServer(); // 서버에 접속 시작
            };
        }

        private void InitializeGrids()
        {
            DrawGrid(CpuGraphCanvas);
            DrawGrid(RamGraphCanvas);
            DrawGrid(GpuGraphCanvas);
            DrawGrid(NetGraphCanvas);
        }

        private async void ConnectToServer()
        {
            while (isRunning)
            {
                try
                {
                    client = new TcpClient();
                    await client.ConnectAsync("127.0.0.1", 5000);
                    stream = client.GetStream();
                    AddLog("서버에 연결되었습니다.");

                    byte[] buffer = new byte[4096];
                    while (client.Connected)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;

                        string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        ProcessPacket(json);
                    }
                }
                catch
                {
                    AddLog("서버 연결 실패. 5초 후 재시도...");
                    await Task.Delay(5000);
                }
            }
        }

        private void ProcessPacket(string json)
        {
            try
            {
                var packets = json.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in packets)
                {
                    var packet = JsonConvert.DeserializeObject<MonitorPacket>(p);
                    if (packet == null || packet.Mode != "DATA") continue;

                    Dispatcher.Invoke(() => {
                        UpdateAllGraphs(packet.Cpu, packet.Mem, packet.Gpu, packet.Net);
                        UpdateGauges(packet);
                        UpdateStatistics(packet.Cpu);

                        CpuText.Text = $"| CPU: {packet.Cpu}% | MEM: {packet.Mem:F1}% | GPU: {packet.Gpu:F1}% | SPEED: {packet.Net:F1}MB/s |";

                        if (packet.Cpu > 90)
                        {
                            AddLog($"경고: CPU 과부하 {packet.Cpu}%");
                            //SaveLogToFile($"CPU Warning: {packet.Cpu}%");
                        }
                    });
                }
            }
            catch { }
        }


        private void SendControlCommand(string commandStr)
        {
            if (stream == null || !client.Connected) return;

            try
            {
                var cmdPacket = new MonitorPacket { Mode = "COMMAND", Command = commandStr };
                string json = JsonConvert.SerializeObject(cmdPacket);
                byte[] data = Encoding.UTF8.GetBytes(json);
                stream.Write(data, 0, data.Length);
                AddLog($"명령 전송: {commandStr}");
            }
            catch (Exception ex) { AddLog($"명령 실패: {ex.Message}"); }
        }


        private void UpdateAllGraphs(double cpu, double ram, double gpu, double net)
        {
            UpdateSingleLine(cpu, cpuValues, CpuLine, CpuGraphCanvas);
            UpdateSingleLine(ram, ramValues, RamLine, RamGraphCanvas);
            UpdateSingleLine(gpu, gpuValues, GpuLine, GpuGraphCanvas);
            UpdateSingleLine(net, netValues, NetLine, NetGraphCanvas);
        }

        private void UpdateStatistics(double value)
        {

            if (value >= 80)
            {
                AddLog($"⚠️ CPU 과부하: {value:F1}%");
            }

            if (count == 1)
            {//최초 값으로 초기화 ->min 값 0 고정 방지
                AddLog($"모니터링 시스템 시작");
                minCpu = value;
            }

            if (value > maxCpu) maxCpu = value;
            if (value < minCpu) minCpu = value;

            count++;
            sumCpu += value;
            double avgCpu = sumCpu / count;

            TxtMin.Text = $"Min: {minCpu:F1}%";
            TxtMax.Text = $"Max: {maxCpu:F1}%";
            TxtAvg.Text = $"Avg: {avgCpu:F1}%";
        }

        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Canvas canvas)
            {
                DrawGrid(canvas);
            }
        }


        private void DrawGrid(Canvas targetCanvas, double maxVal = 100)
        {
            var itemsToRemove = targetCanvas.Children.OfType<Line>().Cast<UIElement>()
                                .Concat(targetCanvas.Children.OfType<TextBlock>().Cast<UIElement>())
                                .ToList();

            foreach (var item in itemsToRemove)
            {
                targetCanvas.Children.Remove(item);
            }

            double width = targetCanvas.ActualWidth;
            double height = targetCanvas.ActualHeight;
            if (width <= 0 || height <= 0) return;

            //점선
            for (int i = 0; i <= 4; i++)
            {
                double y = (height / 4) * i;
                Line gridLine = new Line
                {
                    X1 = 0,
                    X2 = width,
                    Y1 = y,
                    Y2 = y,
                    Stroke = Brushes.DimGray,
                    StrokeDashArray = new DoubleCollection(new double[] { 4, 4 }),
                    Opacity = 0.3
                };
                targetCanvas.Children.Add(gridLine);

                double labelValue = maxVal - (i * (maxVal / 4));

                TextBlock label = new TextBlock
                {
                    Text = labelValue > 1 ? labelValue.ToString("F1") : labelValue.ToString("F2"),
                    Foreground = Brushes.Gray,
                    FontSize = 9
                };
                Canvas.SetLeft(label, 2);
                Canvas.SetTop(label, y - 12);
                targetCanvas.Children.Add(label);
            }
        }

        private void UpdateSingleLine(double val, List<double> list, Polyline line, Canvas canvas, double maxVal = 100)
        {
            list.Add(val);
            if (list.Count > MaxPoints) list.RemoveAt(0);

            double w = canvas.ActualWidth;
            double h = canvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            if (line == NetLine)
            {
                double currentMax = list.Max();
                maxVal = currentMax > 10 ? currentMax * 1.2 : 10;
                DrawGrid(canvas, maxVal);
            }

            PointCollection points = new PointCollection();
            double xStep = w / (MaxPoints - 1);
            for (int i = 0; i < list.Count; i++)
            {
                double x = i * xStep;
                double y = h - (list[i] / maxVal * h);
                points.Add(new Point(x, y));
            }
            line.Points = points;
        }

        private void UpdateGauges(MonitorPacket p)
        {
            GaugeText.Text = p.Cpu.ToString("F0");
            UpdateGaugePath(p.Cpu, GaugeEllipse);

            RamGaugeText.Text = p.Mem.ToString("F0");
            UpdateGaugePath(p.Mem, RamGaugeEllipse);

            GpuBar.Value = p.Gpu;
            GpuText.Text = $"{p.Gpu:F0}%";
            NetBar.Value = Math.Min(p.Net, 100);
            NetText.Text = $"{p.Net:F1} MB/s";
        }

        private void UpdateGaugePath(double val, Ellipse ellipse)
        {
            double stroke = 20;
            double radius = (200 - stroke) / 2.0;
            double circumference = 2 * Math.PI * radius;
            double visiblePx = circumference * (val / 100.0);
            ellipse.StrokeDashArray = new DoubleCollection { visiblePx / stroke, 1000 };

            if (val > 80) ellipse.Stroke = Brushes.Red;
            else if (val > 50) ellipse.Stroke = Brushes.Orange;
            else ellipse.Stroke = Brushes.Cyan;
        }

        private void SaveLogToFile(string msg)
        {
            try { File.AppendAllText("logs.txt", $"[{DateTime.Now}] {msg}\n"); } catch { }
        }

        private void AddLog(string message)
        {
            Dispatcher.Invoke(() => {
                LogList.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
                if (LogList.Items.Count > 50) LogList.Items.RemoveAt(50);
            });
        }

        private void btnShutdown_Click(object sender, RoutedEventArgs e) => SendControlCommand("SHUTDOWN");
        private void btnReboot_Click(object sender, RoutedEventArgs e) => SendControlCommand("REBOOT");
    }
}