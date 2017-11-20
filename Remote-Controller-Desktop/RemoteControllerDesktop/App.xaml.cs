using System;
using System.Windows;

namespace RemoteController.Desktop
{
    public partial class App : Application
    {
        static System.Windows.Forms.NotifyIcon NOTIFY_ICON = null;

        public static void ShowNotifyIcon(System.Drawing.Icon icon, string balloonTip)
        {
            if (NOTIFY_ICON == null)
                InitializeNotifyIcon();

            NOTIFY_ICON.BalloonTipText = balloonTip;
            NOTIFY_ICON.Icon = icon;
            NOTIFY_ICON.Visible = true;
            NOTIFY_ICON.ShowBalloonTip(500);
        }

        public static void HideNotifyIcon()
        {
            if (NOTIFY_ICON == null)
                return;

            NOTIFY_ICON.Visible = false;
            NOTIFY_ICON.Dispose();
            NOTIFY_ICON = null;
        }

        static void InitializeNotifyIcon()
        {
            NOTIFY_ICON = new System.Windows.Forms.NotifyIcon();
            NOTIFY_ICON.Visible = false;
            NOTIFY_ICON.Text = "Remote Controller Desktop";
            NOTIFY_ICON.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            NOTIFY_ICON.BalloonTipTitle = "Remote Controller Desktop";
            NOTIFY_ICON.DoubleClick += NotifyIcon_DoubleClick;
            
            NOTIFY_ICON.ContextMenu = new System.Windows.Forms.ContextMenu();
            System.Windows.Forms.MenuItem notifyIconMenuTitle = new System.Windows.Forms.MenuItem("Remote Controller Desktop");
            notifyIconMenuTitle.Enabled = false;
            System.Windows.Forms.MenuItem notifyIconMenuShow = new System.Windows.Forms.MenuItem("Show");
            System.Windows.Forms.MenuItem notifyIconMenuExit = new System.Windows.Forms.MenuItem("Exit");
            notifyIconMenuShow.Click += NotifyIconMenu_Click;
            notifyIconMenuExit.Click += NotifyIconMenu_Click;
            NOTIFY_ICON.ContextMenu.MenuItems.Add(notifyIconMenuTitle);
            NOTIFY_ICON.ContextMenu.MenuItems.Add("-");
            NOTIFY_ICON.ContextMenu.MenuItems.Add(notifyIconMenuShow);
            NOTIFY_ICON.ContextMenu.MenuItems.Add(notifyIconMenuExit);
        }

        static void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            App.Current.MainWindow.Show();
            HideNotifyIcon();
        }

        static void NotifyIconMenu_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.MenuItem menuItem = sender as System.Windows.Forms.MenuItem;
            if (menuItem.Text.Equals("Show"))
            {
                App.Current.MainWindow.Show();
                HideNotifyIcon();
            }
            else if (menuItem.Text.Equals("Exit"))
                App.Current.Shutdown();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            HideNotifyIcon();
        }
    }
}
