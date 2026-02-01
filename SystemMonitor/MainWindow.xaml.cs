using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Windows.Threading;
using System.Net;
using System.Net.Sockets;


namespace SystemMonitor
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private TcpListener server;
        private bool isRunning = true;
        private List<double> cpuValues = new List<double>();
        private List<double> ramValues = new List<double>();
        private List<double> gpuValues = new List<double>();
        private List<double> netValues = new List<double>();
        private const int MaxPoints = 50;//화면에 표시할 점 개수
        double minCpu = 100;
        double maxCpu = 0;
        double sumCpu = 0;
        int count = 0;

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += (s, e) => {
                DrawGrid(CpuGraphCanvas);
                DrawGrid(RamGraphCanvas);
                DrawGrid(GpuGraphCanvas);
                DrawGrid(NetGraphCanvas);
            };

            StartServer();//서버 켜기
            
        }
        private async void StartServer()
        {
            server = new TcpListener(IPAddress.Any, 5000); //포트 5000
            server.Start();

            while (isRunning)
            {
                try
                {
                    using (TcpClient client = await server.AcceptTcpClientAsync())
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);//데이터 받기
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);//데이터 변환

                        string[] parts = message.Split('|');

                        if (parts.Length == 4)
                        {
                            float cpuVal = float.Parse(parts[0].Trim());//혹시 모르기 공백제거 추가해서 변환
                            float memVal = float.Parse(parts[1].Trim());
                            float gpuVal = float.Parse(parts[2].Trim());
                            float netVal = float.Parse(parts[3].Trim());

                            UpdateAllGraphs(cpuVal, memVal, gpuVal, netVal);//그래프 업데이트
                            UpdateGauge(cpuVal);//게이지 업데이트
                            UpdateRamGauge(memVal);//게이지 업데이트
                            if (memVal > 90)
                            {
                                AddLog($"⚠️ 메모리 부족 위험: {memVal:F1}%");
                            }
                            
                            GpuBar.Value = gpuVal;
                            GpuText.Text = $"{gpuVal:F0}%";

                            NetBar.Value = Math.Min(netVal, 100);
                            NetText.Text = $"{netVal:F1} MB/s";

                            Dispatcher.Invoke(() => {//ui업데이트
                                CpuText.Text = $"| CPU: {cpuVal}% | MEM: {memVal:F1}% | GPU: {gpuVal:F1}% | SPEED: {netVal:F1}MB/s |";
                            });
                        }

                        
                    }
                }
                catch (Exception ex) { /* 예외 처리 */ }
            }
        }
        private void UpdateAllGraphs(double cpu, double ram, double gpu, double net)
        {
            Dispatcher.Invoke(() => {
                // 각 리스트 업데이트 및 점 찍기
                UpdateSingleLine(cpu, cpuValues, CpuLine, CpuGraphCanvas);
                UpdateSingleLine(ram, ramValues, RamLine, RamGraphCanvas);
                UpdateSingleLine(gpu, gpuValues, GpuLine, GpuGraphCanvas);
                UpdateSingleLine(net, netValues, NetLine, NetGraphCanvas, 300); // 네트워크는 최대값 조절 가능
            });
        }

        private void UpdateSingleLine(double val, List<double> list, Polyline line, Canvas canvas, double maxVal = 100)
        {
            list.Add(val);
            if (list.Count > MaxPoints) list.RemoveAt(0);


            PointCollection points = new PointCollection();
            double w = canvas.ActualWidth;
            double h = canvas.ActualHeight;
            if (w <= 0 || h <= 0) return; // 캔버스가 아직 안 그려졌을 때 방지

            double xStep = w / (MaxPoints - 1);
            for (int i = 0; i < list.Count; i++)
            {
                double x = i * xStep;
                double y = h - (list[i] / maxVal * h);
                points.Add(new Point(x, y));
            }
            
            if (line == NetLine)
            {
                double currentMax = list.Max();
                maxVal = currentMax > 10 ? currentMax * 1.2 : 10; // 상단 여유 20% 추가

                DrawGrid(canvas, maxVal);
            }
            line.Points = points;
        }

        private void UpdateRamGauge(double value)
        {
            RamGaugeText.Text = value.ToString("F0");

            double stroke = 20;
            double radius = (200 - stroke) / 2.0;
            double circumference = 2 * Math.PI * radius;

            double visiblePx = circumference * (value / 100.0);
            double visibleDash = visiblePx / stroke;

            RamGaugeEllipse.StrokeDashArray = 
                new DoubleCollection { visibleDash, 1000 };
        }
        private void UpdateGauge(double value)
        {
            Dispatcher.Invoke(() => {
                GaugeText.Text = value.ToString("F0");

                double stroke = 20;
                double radius = (200 - stroke) / 2.0;
                double circumference = 2 * Math.PI * radius;

                double visiblePx = circumference * (value / 100.0);
                double visibleDash = visiblePx / stroke;

                GaugeEllipse.StrokeDashArray =
                    new DoubleCollection { visibleDash, 1000 };

                UpdateStatistics(value);//최저최소평균 계산

                if (value > 80) GaugeEllipse.Stroke = Brushes.Red;//임계치 이상 색 변경
                else if (value > 50) GaugeEllipse.Stroke = Brushes.Orange;
                else GaugeEllipse.Stroke = Brushes.Cyan;
            });
        }

        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(sender is Canvas canvas)
            {
                DrawGrid(canvas);
            }
        }

        private void DrawGrid(Canvas targetCanvas, double maxVal = 100)
        {
            // 1. 해당 캔버스에서 기존 가이드라인만 찾아 지우기
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

            // 2. 가로 점선 그리기 (0, 25, 50, 75, 100%)
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
                // 숫자 표기
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
        private void UpdateStatistics(double value)
        {

            //로그찍기
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

        private void AddLog(string message)
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";

            LogList.Items.Insert(0, logEntry);

            if (LogList.Items.Count > 50)
            {
                LogList.Items.RemoveAt(50);
            }
        }
    }
}
