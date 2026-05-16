namespace ZombieKid;

public sealed class ZombieKidApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ActivityMonitor _monitor;

    public ZombieKidApplicationContext()
    {
        var settings = SettingsLoader.Load();
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "ZombieKid monitoring",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _monitor = new ActivityMonitor(settings, _notifyIcon);
        _monitor.Start();
        _notifyIcon.ShowBalloonTip(3000, "ZombieKid", "Monitoring started", ToolTipIcon.Info);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monitor.Stop();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open data folder", null, (_, _) => OpenFolder(SettingsLoader.Load().DataDirectory));
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
