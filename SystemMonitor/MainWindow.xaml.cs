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
        private const int MaxPoints = 50;//화면에 표시할 점 개수

        public MainWindow()
        {
            InitializeComponent();

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

                        if (parts.Length == 3)
                        {
                            string cpu = parts[0];
                            string mem = parts[1];
                            string state = parts[2];

                            if (double.TryParse(cpu, out double cpuValue))
                            {
                                UpdateGraph(cpuValue);//그래프 업데이트
                                UpdateGauge(cpuValue);
                            }

                            Dispatcher.Invoke(() => {//ui업데이트
                            CpuText.Text = $"CPU: {cpu}% | MEM: {mem}MB | 상태: {state}";
                        });
                        }

                        
                    }
                }
                catch (Exception ex) { /* 예외 처리 */ }
            }
        }
        private void UpdateGraph(double newValue)
        {
            Dispatcher.Invoke(() => {
                cpuValues.Add(newValue);//데이터 저장
                if (cpuValues.Count > MaxPoints) cpuValues.RemoveAt(0);

                PointCollection points = new PointCollection();//점 찍기
                double canvasWidth = GraphCanvas.ActualWidth;
                double canvasHeight = GraphCanvas.ActualHeight;
                double xStep = canvasWidth / (MaxPoints - 1);

                for (int i = 0; i < cpuValues.Count; i++)
                {
                    double x = i * xStep;
                    double y = canvasHeight - (cpuValues[i] / 100.0 * canvasHeight);//CPU 값(0~100)을 캔버스 높이에 맞게 변환

                    points.Add(new Point(x, y));
                }

                CpuLine.Points = points;
            });
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

                if (value > 80) GaugeEllipse.Stroke = Brushes.Red;//임계치 이상 색 변경
                else if (value > 50) GaugeEllipse.Stroke = Brushes.Orange;
                else GaugeEllipse.Stroke = Brushes.Lime;
            });
        }

        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawGrid();
        }

        private void DrawGrid()
        {
            var itemsToRemove = GraphCanvas.Children.OfType<Line>().Cast<UIElement>()
                        .Concat(GraphCanvas.Children.OfType<TextBlock>().Cast<UIElement>())
                        .ToList();

            foreach (var item in itemsToRemove)
            {
                GraphCanvas.Children.Remove(item);
            }
            double width = GraphCanvas.ActualWidth;
            double height = GraphCanvas.ActualHeight;

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
                    StrokeDashArray = new DoubleCollection(new double[] { 4, 4 }), //점선
                    Opacity = 0.5
                };
                GraphCanvas.Children.Add(gridLine);

                //숫자표기
                TextBlock label = new TextBlock
                {
                    Text = $"{(100 - i * 25)}",
                    Foreground = Brushes.Gray,
                    FontSize = 10
                };
                Canvas.SetLeft(label, 5);
                Canvas.SetTop(label, y - 12);
                GraphCanvas.Children.Add(label);
            }
        }
    }
}
