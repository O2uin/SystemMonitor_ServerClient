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

                        Dispatcher.Invoke(() => {//ui업데이트
                            CpuText.Text = $"수신된 데이터: {message}%";
                        });
                    }
                }
                catch (Exception ex) { /* 예외 처리 */ }
            }
        }

    }
}
