﻿using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Newtonsoft.Json.Linq;
using QRCoder;
using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace RemoteController.Desktop
{
    public partial class MainWindow : MetroWindow
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, int dx, int dy, uint cButtons, uint dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessageW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;
        private const int MOUSEEVENTF_WHEEL = 0x0800;
        private const int MOUSEEVENTF_HWHEEL = 0x01000;

        protected string LocalIP
        {
            get
            {
                NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface adapter in networkInterfaces)
                {
                    if (adapter.OperationalStatus == OperationalStatus.Up)
                    {
                        IPInterfaceProperties properties = adapter.GetIPProperties();
                        foreach (var ip in properties.UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                                return ip.Address.ToString();
                        }
                    }
                }

                return null;
            }
        }

        protected string PIN
        {
            get
            {
                return new Random().Next(0, 9999).ToString("0000");
            }
        }

        protected string Token
        {
            get
            {
                return Guid.NewGuid().ToString();
            }
        }

        static string LocalhostIP = "127.0.0.1";
        static string CurrentIP = null;
        static string CurrentPIN = null;
        public static string CurrentToken = null;

        static int Port = 13000;

        static float ScaleXFactor = 1f;
        static float ScaleYFactor = 1f;

        bool MouseButtonPressing = false;
        int PressingMouseButtonID = 0;

        Point CapturedMouseLocation;

        ProgressDialogController Progress;

        public MainWindow()
        {
            InitializeComponent();

            if (InitializeWindow().Result)
            {
                TcpListener initialTcpListener = new TcpListener(IPAddress.Any, Port);
                initialTcpListener.Start();

                initialTcpListener.BeginAcceptTcpClient(new AsyncCallback(AcceptTcpClientCallback), initialTcpListener);
            }
        }

        private async Task<bool> InitializeWindow()
        {
            CurrentIP = LocalIP;
            CurrentPIN = PIN;
            CurrentToken = Token;

            if (CurrentIP == null)
            {
                await this.ShowMessageAsync("Error", "Local IP Address cannot be retrieved.");

                this.LabelIP.Content = "IP: N/A";
                return false;
            }

            this.LabelIP.Content = "IP: " + CurrentIP;
            this.LabelPIN.Content = "PIN: " + CurrentPIN;

            this.ImageQRCode.Source = GenerateQRCode(CurrentIP, CurrentPIN);

            return true;
        }

        static BitmapImage GenerateQRCode(string ip, string pin)
        {
            JObject qrData = new JObject();
            qrData.Add("IP", ip);
            qrData.Add("PIN", pin);
            qrData.Add("Port", Port);

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrData.ToString(Newtonsoft.Json.Formatting.None), QRCodeGenerator.ECCLevel.Q);

            Bitmap qrCodeBitmap = new QRCode(qrCodeData).GetGraphic(7);

            return BitmapToImageSource(qrCodeBitmap);
        }

        static BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
                memoryStream.Position = 0;

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                bitmap.Dispose();

                return bitmapImage;
            }
        }

        async void AcceptTcpClientCallback(IAsyncResult ar)
        {
            TcpListener listener = (TcpListener)ar.AsyncState;
            TcpClient client = listener.EndAcceptTcpClient(ar);

            NetworkStream stream = client.GetStream();
            byte[] dataBytes = new byte[client.ReceiveBufferSize];

            int bytesRead = await stream.ReadAsync(dataBytes, 0, client.ReceiveBufferSize);
            string request = Encoding.ASCII.GetString(dataBytes, 0, bytesRead);
            
            try
            {
                JObject jsonObject = JObject.Parse(request);
                BaseAction baseAction = new BaseAction(jsonObject);

                if (baseAction.action == BaseAction.ActionType.Hello)
                {
                    HelloCommand action = new HelloCommand(jsonObject);
                    if (action.pin.Equals(CurrentPIN))
                    {
                        await Dispatcher.BeginInvoke((Action)(async() =>
                        {
                            Progress = await this.ShowProgressAsync("Please wait", "Authenticating...");
                        }));

                        JObject authData = new JObject();
                        authData.Add("Result", true);
                        authData.Add("Token", CurrentToken);
                        byte[] authDataBytes = Encoding.UTF8.GetBytes(authData.ToString());

                        lock (stream)
                        {
                            stream.Write(authDataBytes, 0, authDataBytes.Length);
                        }

                        if (this.Visibility == System.Windows.Visibility.Visible)
                            await Dispatcher.BeginInvoke((Action)(() =>
                            {
                                App.ShowNotifyIcon(Properties.Resources.app_icon, "Connected.");
                                
                                this.Hide();
                            }));
                    }
                    else
                    {
                        JObject authData = new JObject();
                        authData.Add("Result", false);
                        byte[] authDataBytes = Encoding.UTF8.GetBytes(authData.ToString());

                        lock (stream)
                        {
                            stream.Write(authDataBytes, 0, authDataBytes.Length);
                        }
                    }
                }

                if (baseAction.action == BaseAction.ActionType.Authenticate)
                {
                    AuthBaseAction action = new AuthBaseAction(jsonObject, false);
                    if (action.secret.Equals(CurrentToken))
                    {
                        ProgressDialogController progress = await this.ShowProgressAsync("Please wait", "Authenticating...");

                        JObject authData = new JObject();
                        authData.Add("Result", true);
                        authData.Add("Token", CurrentToken);
                        byte[] authDataBytes = Encoding.UTF8.GetBytes(authData.ToString());

                        lock (stream)
                        {
                            stream.Write(authDataBytes, 0, authDataBytes.Length);
                        }

                        await progress.CloseAsync();

                        if (this.Visibility == System.Windows.Visibility.Visible)
                            await Dispatcher.BeginInvoke((Action)(() =>
                            {
                                App.ShowNotifyIcon(Properties.Resources.app_icon, "Connected.");

                                this.Hide();
                            }));
                    }
                    else
                    {
                        JObject authData = new JObject();
                        authData.Add("Result", false);
                        byte[] authDataBytes = Encoding.UTF8.GetBytes(authData.ToString());

                        lock (stream)
                        {
                            stream.Write(authDataBytes, 0, authDataBytes.Length);
                        }
                    }
                }

                if (baseAction.action == BaseAction.ActionType.Goodbye)
                {
                    await Dispatcher.BeginInvoke((Action)(() =>
                    {
                        App.ShowNotifyIcon(Properties.Resources.app_icon, "Disconnected.");
                    }));
                }

                if (baseAction.action == BaseAction.ActionType.ScreenRecognize)
                {
                    ScreenRecognizeCommand action = new ScreenRecognizeCommand(jsonObject);
  
                    int primaryScreenX = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
                    int primaryScreenY = (int)System.Windows.SystemParameters.PrimaryScreenHeight;

                    ScaleXFactor = (float)primaryScreenX / action.remoteScreenX;
                    ScaleYFactor = (float)primaryScreenY / action.remoteScreenY;
                }

                if (baseAction.action == BaseAction.ActionType.Clipboard)
                {
                    ClipboardCommand action = new ClipboardCommand(jsonObject);
                    await Dispatcher.BeginInvoke((Action)(() =>
                    {
                        System.Windows.Clipboard.SetText(action.data);
                        App.ShowNotifyIcon(Properties.Resources.app_icon, "Text copied to clipboard.");
                    }));
                }

                if (baseAction.action == BaseAction.ActionType.Text)
                {
                    ClipboardCommand action = new ClipboardCommand(jsonObject);
                    System.Windows.Forms.SendKeys.SendWait(action.data);
                    keybd_event((byte)System.Windows.Forms.Keys.Enter, 0, 0, 0);
                }

                if (baseAction.action == BaseAction.ActionType.MouseMove)
                {
                    MouseMoveCommand action = new MouseMoveCommand(jsonObject);
                    MouseButtonPressing = false;
                    System.Windows.Forms.Cursor.Position = new Point((int)(action.x * ScaleXFactor),
                        (int)(action.y * ScaleYFactor));
                }

                if (baseAction.action == BaseAction.ActionType.MouseMoveRelative)
                {
                    MouseMoveCommand action = new MouseMoveCommand(jsonObject);
                    MouseButtonPressing = false;
                    System.Windows.Forms.Cursor.Position = new Point(System.Windows.Forms.Cursor.Position.X + (int)(action.x * ScaleXFactor),
                        System.Windows.Forms.Cursor.Position.Y + (int)(action.y * ScaleYFactor));
                }

                if (baseAction.action == BaseAction.ActionType.MouseDown)
                {
                    MouseClickCommand action = new MouseClickCommand(jsonObject);
                    MouseButtonPressing = false;
                    if (action.button == BaseAction.MouseButtonType.Left)
                        mouse_event(MOUSEEVENTF_LEFTDOWN, CapturedMouseLocation.X, CapturedMouseLocation.Y, 0, 0);
                    if (action.button == BaseAction.MouseButtonType.Right)
                        mouse_event(MOUSEEVENTF_RIGHTDOWN, CapturedMouseLocation.X, CapturedMouseLocation.Y, 0, 0);
                }

                if (baseAction.action == BaseAction.ActionType.MouseUp)
                {
                    MouseClickCommand action = new MouseClickCommand(jsonObject);
                    MouseButtonPressing = false;
                    if (action.button == BaseAction.MouseButtonType.Left)
                        mouse_event(MOUSEEVENTF_LEFTUP, CapturedMouseLocation.X, CapturedMouseLocation.Y, 0, 0);
                    if (action.button == BaseAction.MouseButtonType.Right)
                        mouse_event(MOUSEEVENTF_RIGHTUP, CapturedMouseLocation.X, CapturedMouseLocation.Y, 0, 0);
                }

                if (baseAction.action == BaseAction.ActionType.MouseClick)
                {
                    MouseClickCommand action = new MouseClickCommand(jsonObject);
                    MouseButtonPressing = false;
                    if (action.button == BaseAction.MouseButtonType.Left)
                        mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, CapturedMouseLocation.X, CapturedMouseLocation.Y, 0, 0);
                    if (action.button == BaseAction.MouseButtonType.Right)
                        mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, CapturedMouseLocation.X, CapturedMouseLocation.Y, 0, 0);
                }

                if (baseAction.action == BaseAction.ActionType.MouseDrag)
                {
                    MouseDragCommand action = new MouseDragCommand(jsonObject);
                    System.Windows.Forms.Cursor.Position = new Point((int)(action.x * ScaleXFactor),
                        (int)(action.y * ScaleYFactor));

                    if (!MouseButtonPressing)
                    {
                        if (action.button == BaseAction.MouseButtonType.Left)
                        {
                            mouse_event(MOUSEEVENTF_LEFTDOWN, CapturedMouseLocation.X, CapturedMouseLocation.Y, 0, 0);
                            PressingMouseButtonID = MOUSEEVENTF_LEFTDOWN;
                        }
                        if (action.button == BaseAction.MouseButtonType.Right)
                        {
                            mouse_event(MOUSEEVENTF_RIGHTDOWN, CapturedMouseLocation.X, CapturedMouseLocation.Y, 0, 0);
                            PressingMouseButtonID = MOUSEEVENTF_RIGHTDOWN;
                        }
                        MouseButtonPressing = true;
                    }
                }

                if (baseAction.action == BaseAction.ActionType.MouseDragRelative)
                {
                    MouseDragCommand action = new MouseDragCommand(jsonObject);
                    System.Windows.Forms.Cursor.Position = new Point(System.Windows.Forms.Cursor.Position.X + (int)(action.x * ScaleXFactor),
                        System.Windows.Forms.Cursor.Position.Y + (int)(action.y * ScaleYFactor));

                    if (!MouseButtonPressing)
                    {
                        if (action.button == BaseAction.MouseButtonType.Left)
                        {
                            mouse_event(MOUSEEVENTF_LEFTDOWN, CapturedMouseLocation.X, CapturedMouseLocation.Y, 0, 0);
                            PressingMouseButtonID = MOUSEEVENTF_LEFTDOWN;
                        }
                        if (action.button == BaseAction.MouseButtonType.Right)
                        {
                            mouse_event(MOUSEEVENTF_RIGHTDOWN, CapturedMouseLocation.X, CapturedMouseLocation.Y, 0, 0);
                            PressingMouseButtonID = MOUSEEVENTF_RIGHTDOWN;
                        }
                        MouseButtonPressing = true;
                    }
                }

                if (baseAction.action == BaseAction.ActionType.Scroll)
                {
                    ScrollCommand action = new ScrollCommand(jsonObject);
                    if (action.direction == BaseAction.ScrollDirectionType.Vertical)
                        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint) action.amount, 0);
                    if (action.direction == BaseAction.ScrollDirectionType.Horizontal)
                        mouse_event(MOUSEEVENTF_HWHEEL, 0, 0, (uint)action.amount, 0);
                }

                if (baseAction.action == BaseAction.ActionType.VolumeUp)
                {
                    keybd_event((byte)System.Windows.Forms.Keys.VolumeUp, 0, 0, 0);
                }

                if (baseAction.action == BaseAction.ActionType.VolumeDown)
                {
                    keybd_event((byte)System.Windows.Forms.Keys.VolumeDown, 0, 0, 0);
                }

                if (baseAction.action == BaseAction.ActionType.VolumeMute)
                {
                    keybd_event((byte)System.Windows.Forms.Keys.VolumeMute, 0, 0, 0);
                }

                if (baseAction.action == BaseAction.ActionType.MediaPlayPause)
                {
                    keybd_event((byte)System.Windows.Forms.Keys.MediaPlayPause, 0, 0, 0);
                }

                if (baseAction.action == BaseAction.ActionType.MediaStop)
                {
                    keybd_event((byte)System.Windows.Forms.Keys.MediaStop, 0, 0, 0);
                }

                if (baseAction.action == BaseAction.ActionType.MediaNextTrack)
                {
                    keybd_event((byte)System.Windows.Forms.Keys.MediaNextTrack, 0, 0, 0);
                }

                if (baseAction.action == BaseAction.ActionType.MediaPrevTrack)
                {
                    keybd_event((byte)System.Windows.Forms.Keys.MediaPreviousTrack, 0, 0, 0);
                }

                if (baseAction.action == BaseAction.ActionType.MediaFastForward)
                {
                    keybd_event((byte)System.Windows.Forms.Keys.Right, 0, 0, 0);
                }

                if (baseAction.action == BaseAction.ActionType.MediaFastRewind)
                {
                    keybd_event((byte)System.Windows.Forms.Keys.Left, 0, 0, 0);
                }

                if (!MouseButtonPressing && PressingMouseButtonID != 0)
                {
                    if (PressingMouseButtonID == MOUSEEVENTF_LEFTDOWN)
                        mouse_event(MOUSEEVENTF_LEFTUP, CapturedMouseLocation.X, CapturedMouseLocation.Y, 0, 0);
                    if (PressingMouseButtonID == MOUSEEVENTF_RIGHTDOWN)
                        mouse_event(MOUSEEVENTF_RIGHTUP, CapturedMouseLocation.X, CapturedMouseLocation.Y, 0, 0);

                    PressingMouseButtonID = 0;
                }
            }
            catch (Exception e)
            {
               
            }
            finally
            {
                await Dispatcher.BeginInvoke((Action)(async () =>
                {
                    if (Progress != null)
                    {
                        await Progress.CloseAsync();
                        Progress = null;
                    }
                }));

                stream.Close();
                client.Close();
                listener.BeginAcceptTcpClient(new AsyncCallback(AcceptTcpClientCallback), listener);
            }
        }

        private void LabelPINRefresh_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CurrentPIN = PIN;

            this.LabelPIN.Content = "PIN: " + CurrentPIN;
            this.ImageQRCode.Source = GenerateQRCode(CurrentIP, CurrentPIN);
        }

        private async void ButtonInfo_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            await this.ShowMessageAsync("Info", "Remote Controller Desktop\nDeveloped by Tuna Emre.\n\nhttps://github.com/tunaemre/Remote-Controller-Windows\n\nhttps://www.linkedin.com/in/tuna-emre");
        }

        private void MetroWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == System.Windows.WindowState.Minimized)
            {
                App.ShowNotifyIcon(Properties.Resources.app_icon, "Remote Controller Desktop still running.");
                this.Hide();
            }
        }
    }
}