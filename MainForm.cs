using System.Drawing.Drawing2D;

namespace NovaBattery;

internal sealed class MainForm : Form
{
    private readonly Label percentLabel = new();
    private readonly Label statusLabel = new();
    private readonly Label detailLabel = new();
    private readonly ProgressBar batteryBar = new();
    private readonly Button refreshButton = new();
    private readonly CheckBox startupCheck = new();
    private readonly NotifyIcon tray = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 15_000 };
    private bool refreshing;
    private bool allowClose;
    private bool lowBatteryNotified;

    internal MainForm()
    {
        Text = "Nova Battery";
        ClientSize = new Size(420, 330);
        MinimumSize = new Size(390, 330);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(15, 20, 28);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);
        Icon = CreateIcon(null, false);

        var title = new Label
        {
            Text = "NOVA 2 LITE",
            ForeColor = Color.FromArgb(125, 145, 170),
            Font = new Font("Segoe UI Semibold", 10f),
            AutoSize = true,
            Location = new Point(28, 25)
        };
        percentLabel.SetBounds(25, 58, 370, 82);
        percentLabel.Font = new Font("Segoe UI", 42f, FontStyle.Bold);
        percentLabel.Text = "—";
        statusLabel.SetBounds(29, 139, 360, 27);
        statusLabel.Font = new Font("Segoe UI Semibold", 12f);
        statusLabel.Text = "Looking for controller…";
        detailLabel.SetBounds(29, 174, 360, 44);
        detailLabel.ForeColor = Color.FromArgb(155, 170, 190);
        detailLabel.Text = "Connect with the receiver or a USB-C cable.";

        batteryBar.SetBounds(29, 224, 362, 10);
        batteryBar.Style = ProgressBarStyle.Continuous;
        batteryBar.Maximum = 100;

        refreshButton.Text = "Refresh now";
        refreshButton.SetBounds(29, 258, 118, 38);
        refreshButton.FlatStyle = FlatStyle.Flat;
        refreshButton.FlatAppearance.BorderColor = Color.FromArgb(65, 140, 230);
        refreshButton.BackColor = Color.FromArgb(28, 91, 165);
        refreshButton.ForeColor = Color.White;
        refreshButton.Click += async (_, _) => await RefreshAsync();

        startupCheck.Text = "Start with Windows";
        startupCheck.AutoSize = true;
        startupCheck.Location = new Point(234, 267);
        startupCheck.Checked = SettingsStore.StartsWithWindows;
        startupCheck.CheckedChanged += (_, _) =>
        {
            try { SettingsStore.StartsWithWindows = startupCheck.Checked; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Nova Battery"); }
        };

        Controls.AddRange([title, percentLabel, statusLabel, detailLabel, batteryBar, refreshButton, startupCheck]);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => ShowWindow());
        menu.Items.Add("Refresh", null, async (_, _) => await RefreshAsync());
        menu.Items.Add("Copy status", null, (_, _) => Clipboard.SetText($"{percentLabel.Text} — {statusLabel.Text}"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => { allowClose = true; Close(); });
        tray.Text = "Nova Battery";
        tray.Icon = Icon;
        tray.Visible = true;
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowWindow();

        timer.Tick += async (_, _) => await RefreshAsync();
        timer.Start();
        Shown += async (_, _) => await RefreshAsync();
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) Hide(); };
        FormClosing += (_, e) =>
        {
            if (!allowClose) { e.Cancel = true; Hide(); }
            else tray.Visible = false;
        };
    }

    private async Task RefreshAsync()
    {
        if (refreshing) return;
        refreshing = true;
        refreshButton.Enabled = false;
        statusLabel.Text = "Reading controller…";
        try
        {
            BatteryReading result = await NovaBatteryReader.ReadAsync();
            Render(result);
        }
        finally
        {
            refreshing = false;
            refreshButton.Enabled = true;
        }
    }

    private void Render(BatteryReading reading)
    {
        if (!reading.IsConnected)
        {
            percentLabel.Text = "—";
            statusLabel.Text = "Controller not connected";
            detailLabel.Text = "Plug in the receiver or USB-C cable, then refresh.";
            batteryBar.Value = 0;
            SetTray(null, false, "Nova Battery — disconnected");
            return;
        }

        if (reading.Percent is int exact)
        {
            SettingsStore.SaveLastPercent(exact);
        }

        int? display = reading.Percent;
        percentLabel.Text = display is int p ? $"{p}%" : reading.IsCharging ? "Charging" : "—";
        statusLabel.Text = reading.IsCharging ? "Charging" : "On battery";
        detailLabel.Text = reading.IsCharging && reading.Percent is null
            ? $"Percentage unavailable • {reading.Connection} • updated {DateTime.Now:t}"
            : $"{reading.Connection} • updated {DateTime.Now:t}";
        batteryBar.Value = Math.Clamp(display ?? 0, 0, 100);
        SetTray(display, reading.IsCharging,
            display is int value ? $"Nova Battery — {value}%{(reading.IsCharging ? " charging" : "")}" : "Nova Battery — charging");

        if (!reading.IsCharging && display is <= 15 && !lowBatteryNotified)
        {
            tray.ShowBalloonTip(5000, "Nova 2 Lite battery low", $"{display}% remaining", ToolTipIcon.Warning);
            lowBatteryNotified = true;
        }
        if (display is > 20) lowBatteryNotified = false;
    }

    private void SetTray(int? percent, bool charging, string text)
    {
        tray.Text = text.Length <= 63 ? text : text[..63];
        var old = tray.Icon;
        tray.Icon = CreateIcon(percent, charging);
        Icon = tray.Icon;
        old?.Dispose();
    }

    private static Icon CreateIcon(int? percent, bool charging)
    {
        using var bitmap = new Bitmap(32, 32);
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        Color color = charging ? Color.FromArgb(70, 205, 125)
            : percent is <= 15 ? Color.FromArgb(245, 90, 85)
            : Color.FromArgb(75, 155, 245);
        using var outline = new Pen(Color.White, 2.3f);
        using var fill = new SolidBrush(color);
        g.DrawRoundedRectangle(outline, new RectangleF(3, 8, 24, 17), 4);
        g.FillRectangle(Brushes.White, 27, 13, 3, 7);
        if (percent is int p)
            g.FillRectangle(fill, 6, 11, 18f * p / 100f, 11);
        else if (charging)
            g.FillRectangle(fill, 6, 11, 18, 11);
        IntPtr handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); tray.Dispose(); }
        base.Dispose(disposing);
    }
}

internal static class GraphicsExtensions
{
    internal static void DrawRoundedRectangle(this Graphics graphics, Pen pen, RectangleF bounds, float radius)
    {
        float d = radius * 2;
        using var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        graphics.DrawPath(pen, path);
    }
}
