using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace HdrBrightness;

public class MainForm : Form
{
    private NativeMethods.DwmpSDRToHDRBoostDelegate? _changeBrightness;
    private IntPtr _primaryMonitor;
    private AppSettings _settings;

    private TrackBar _trackBar = null!;
    private Label _lblValue = null!;
    private Label _lblNits = null!;
    private Button _btnMin = null!;
    private Button _btnMax = null!;
    private Button _btnDefault = null!;
    private NumericUpDown _nudExact = null!;
    private Button _btnSetExact = null!;
    private CheckBox _chkLive = null!;
    private TextBox _txtLog = null!;
    private NumericUpDown _nudMinBrightness = null!;
    private NumericUpDown _nudMaxBrightness = null!;
    private NumericUpDown _nudDefaultBrightness = null!;
    private Button _btnApplyRange = null!;
    private CheckBox _chkAutoStart = null!;
    private CheckBox _chkStartMinimized = null!;
    private NotifyIcon _notifyIcon = null!;
    private ContextMenuStrip _trayMenu = null!;
    private TabControl _tabControl = null!;
    private Label _lblRange = null!;

    private const int SliderResolution = 1000;
    private const string AppName = "HdrBrightness";

    private double CurrentMinBrightness => _settings.MinBrightness;
    private double CurrentMaxBrightness => _settings.MaxBrightness;
    private double CurrentDefaultBrightness => _settings.DefaultBrightness;

    public MainForm(bool startMinimized = false)
    {
        _settings = AppSettings.Load();
        InitializeApi();
        InitializeTrayIcon();
        InitializeComponents();
        LoadCurrentBrightness();

        if (startMinimized && _settings.StartMinimized)
        {
            WindowState = FormWindowState.Minimized;
            Hide();
            ShowInTaskbar = false;
        }
    }

    private void InitializeApi()
    {
        try
        {
            _primaryMonitor = NativeMethods.MonitorFromWindow(IntPtr.Zero, 1);
            var hModule = NativeMethods.LoadLibrary("dwmapi.dll");
            var procAddress = NativeMethods.GetProcAddress(hModule, 171);
            if (procAddress == IntPtr.Zero)
            {
                Log("警告：无法获取 DwmpSDRToHDRBoost 函数地址，HDR 亮度调节不可用");
                return;
            }
            _changeBrightness = Marshal.GetDelegateForFunctionPointer<NativeMethods.DwmpSDRToHDRBoostDelegate>(procAddress);
            Log("API 初始化成功");
        }
        catch (Exception ex)
        {
            Log($"API 初始化失败：{ex.Message}");
        }
    }

    private void LoadCurrentBrightness()
    {
        double currentBrightness = NativeMethods.GetCurrentSdrWhiteLevel();
        Log($"读取到当前 SDR 白色电平：{currentBrightness:F2} ({currentBrightness * 80:F0} nits)");
        SetBrightnessSilent(currentBrightness);
    }

    private void InitializeTrayIcon()
    {
        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("显示主窗口", null, (s, e) => ShowMainForm());
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("最低亮度", null, (s, e) => SetBrightness(CurrentMinBrightness));
        _trayMenu.Items.Add("默认亮度", null, (s, e) => SetBrightness(CurrentDefaultBrightness));
        _trayMenu.Items.Add("最高亮度", null, (s, e) => SetBrightness(CurrentMaxBrightness));
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("退出", null, (s, e) => ExitApp());

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "HDR 亮度调节",
            Visible = true,
            ContextMenuStrip = _trayMenu
        };
        _notifyIcon.DoubleClick += (s, e) => ShowMainForm();
    }

    private void ShowMainForm()
    {
        Show();
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        Activate();
    }

    private void ExitApp()
    {
        _notifyIcon.Visible = false;
        Application.Exit();
    }

    private void InitializeComponents()
    {
        Text = "HDR SDR 亮度调节器";
        ClientSize = new Size(520, 560);
        MinimumSize = new Size(440, 460);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Closing += MainForm_Closing;

        _tabControl = new TabControl { Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", 9F) };

        var tabMain = new TabPage("亮度调节");
        var tabSettings = new TabPage("设置");

        BuildMainTab(tabMain);
        BuildSettingsTab(tabSettings);

        _tabControl.TabPages.Add(tabMain);
        _tabControl.TabPages.Add(tabSettings);
        Controls.Add(_tabControl);

        Log($"显示器句柄: 0x{_primaryMonitor:X}");
        Log($"亮度范围: {CurrentMinBrightness} ~ {CurrentMaxBrightness}");
    }

    private static FlowLayoutPanel NewFlow(FlowDirection dir = FlowDirection.LeftToRight)
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = dir,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(6, 3, 6, 3)
        };
    }

    private void BuildMainTab(TabPage tab)
    {
        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(4)
        };

        var rowValue = NewFlow();
        _lblValue = new Label
        {
            Text = FormatBrightness(1.0),
            Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(2, 2, 12, 2)
        };
        _lblNits = new Label
        {
            Text = FormatNits(1.0),
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.Gray,
            AutoSize = true,
            Margin = new Padding(2, 6, 2, 2)
        };
        rowValue.Controls.AddRange(new Control[] { _lblValue, _lblNits });

        var rowSlider = NewFlow();
        _trackBar = new TrackBar
        {
            Minimum = 0,
            Maximum = SliderResolution,
            Value = BrightnessToSlider(1.0),
            TickFrequency = SliderResolution / 10,
            Width = 460,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        _trackBar.ValueChanged += TrackBar_ValueChanged;
        rowSlider.Controls.Add(_trackBar);

        var rowLive = NewFlow();
        _chkLive = new CheckBox
        {
            Text = "实时调节（拖动时即时生效）",
            Checked = _settings.LiveAdjust,
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F)
        };
        _chkLive.CheckedChanged += (s, e) =>
        {
            _settings.LiveAdjust = _chkLive.Checked;
            _settings.Save();
        };
        rowLive.Controls.Add(_chkLive);

        var rowQuick = NewFlow();
        _btnMin = new Button
        {
            Text = $"最低({CurrentMinBrightness:F1})",
            Size = new Size(140, 32),
            Font = new Font("Microsoft YaHei UI", 9F),
            Margin = new Padding(2, 2, 8, 2)
        };
        _btnMin.Click += (s, e) => SetBrightness(CurrentMinBrightness);

        _btnDefault = new Button
        {
            Text = $"默认({CurrentDefaultBrightness:F1})",
            Size = new Size(140, 32),
            Font = new Font("Microsoft YaHei UI", 9F),
            Margin = new Padding(2, 2, 8, 2)
        };
        _btnDefault.Click += (s, e) => SetBrightness(CurrentDefaultBrightness);

        _btnMax = new Button
        {
            Text = $"最高({CurrentMaxBrightness:F1})",
            Size = new Size(140, 32),
            Font = new Font("Microsoft YaHei UI", 9F)
        };
        _btnMax.Click += (s, e) => SetBrightness(CurrentMaxBrightness);

        rowQuick.Controls.AddRange(new Control[] { _btnMin, _btnDefault, _btnMax });

        var rowExact = NewFlow();
        var lblExact = new Label
        {
            Text = "精确值：",
            AutoSize = true,
            Margin = new Padding(2, 6, 2, 2)
        };
        _nudExact = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 1000,
            Value = 10,
            DecimalPlaces = 1,
            Increment = 1,
            Size = new Size(80, 26),
            Margin = new Padding(2, 2, 4, 2)
        };
        _btnSetExact = new Button
        {
            Text = "设置",
            Size = new Size(55, 28),
            Margin = new Padding(2, 2, 8, 2)
        };
        _btnSetExact.Click += (s, e) =>
        {
            double val = (double)_nudExact.Value / 10.0;
            SetBrightness(val);
        };
        _lblRange = new Label
        {
            Text = $"范围：{CurrentMinBrightness:F1} ~ {CurrentMaxBrightness:F1}",
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Microsoft YaHei UI", 8F),
            Margin = new Padding(2, 6, 2, 2)
        };
        rowExact.Controls.AddRange(new Control[] { lblExact, _nudExact, _btnSetExact, _lblRange });

        var rowLog = NewFlow();
        var lblLog = new Label
        {
            Text = "操作日志：",
            AutoSize = true,
            Margin = new Padding(2, 2, 2, 0)
        };
        rowLog.Controls.Add(lblLog);

        _txtLog = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new Font("Consolas", 8F),
            BackColor = Color.White,
            Height = 140,
            Width = 480,
            Margin = new Padding(2, 2, 2, 2)
        };

        root.Controls.AddRange(new Control[] { rowValue, rowSlider, rowLive, rowQuick, rowExact, rowLog, _txtLog });
        tab.Controls.Add(root);
    }

    private void BuildSettingsTab(TabPage tab)
    {
        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(4)
        };

        var groupRange = new GroupBox
        {
            Text = "亮度范围设置（不同显示器支持不同范围）",
            Width = 490,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Font = new Font("Microsoft YaHei UI", 9F),
            Margin = new Padding(2)
        };
        var rangeInner = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 4, 8, 4),
            Dock = DockStyle.Fill
        };

        var rowMin = NewFlow();
        var lblMin = new Label { Text = "最低亮度：", AutoSize = true, Margin = new Padding(2, 6, 2, 2) };
        _nudMinBrightness = new NumericUpDown
        {
            Minimum = 1, Maximum = 500,
            Value = (decimal)_settings.MinBrightness * 10,
            DecimalPlaces = 1, Increment = 1,
            Size = new Size(80, 26)
        };
        var lblMinNits = new Label
        {
            Text = $"(≈{_settings.MinBrightness * 80:F0} nits)",
            AutoSize = true, ForeColor = Color.Gray,
            Font = new Font("Microsoft YaHei UI", 8F),
            Margin = new Padding(4, 6, 2, 2)
        };
        rowMin.Controls.AddRange(new Control[] { lblMin, _nudMinBrightness, lblMinNits });

        var rowMax = NewFlow();
        var lblMax = new Label { Text = "最高亮度：", AutoSize = true, Margin = new Padding(2, 6, 2, 2) };
        _nudMaxBrightness = new NumericUpDown
        {
            Minimum = 10, Maximum = 1000,
            Value = (decimal)_settings.MaxBrightness * 10,
            DecimalPlaces = 1, Increment = 1,
            Size = new Size(80, 26)
        };
        var lblMaxNits = new Label
        {
            Text = $"(≈{_settings.MaxBrightness * 80:F0} nits)",
            AutoSize = true, ForeColor = Color.Gray,
            Font = new Font("Microsoft YaHei UI", 8F),
            Margin = new Padding(4, 6, 2, 2)
        };
        rowMax.Controls.AddRange(new Control[] { lblMax, _nudMaxBrightness, lblMaxNits });

        var rowDefault = NewFlow();
        var lblDefault = new Label { Text = "默认亮度：", AutoSize = true, Margin = new Padding(2, 6, 2, 2) };
        _nudDefaultBrightness = new NumericUpDown
        {
            Minimum = 1, Maximum = 1000,
            Value = (decimal)_settings.DefaultBrightness * 10,
            DecimalPlaces = 1, Increment = 1,
            Size = new Size(80, 26)
        };
        var lblDefaultNits = new Label
        {
            Text = $"(≈{_settings.DefaultBrightness * 80:F0} nits)",
            AutoSize = true, ForeColor = Color.Gray,
            Font = new Font("Microsoft YaHei UI", 8F),
            Margin = new Padding(4, 6, 2, 2)
        };
        rowDefault.Controls.AddRange(new Control[] { lblDefault, _nudDefaultBrightness, lblDefaultNits });

        var rowApply = NewFlow();
        _btnApplyRange = new Button
        {
            Text = "应用设置",
            Size = new Size(100, 30),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(2, 2, 10, 2)
        };
        _btnApplyRange.Click += ApplyRange;
        var lblHint = new Label
        {
            Text = "1.0=80nits, 6.0=480nits，超出显示器能力会过曝",
            AutoSize = true, ForeColor = Color.Gray,
            Font = new Font("Microsoft YaHei UI", 8F),
            Margin = new Padding(2, 6, 2, 2)
        };
        rowApply.Controls.AddRange(new Control[] { _btnApplyRange, lblHint });

        rangeInner.Controls.AddRange(new Control[] { rowMin, rowMax, rowDefault, rowApply });
        groupRange.Controls.Add(rangeInner);

        var groupStartup = new GroupBox
        {
            Text = "启动选项",
            Width = 490,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Font = new Font("Microsoft YaHei UI", 9F),
            Margin = new Padding(2)
        };
        var startupInner = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 4, 8, 4),
            Dock = DockStyle.Fill
        };

        _chkAutoStart = new CheckBox
        {
            Text = "开机自动启动",
            Checked = _settings.AutoStart,
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F)
        };
        _chkAutoStart.CheckedChanged += (s, e) =>
        {
            _settings.AutoStart = _chkAutoStart.Checked;
            _settings.Save();
            SetAutoStart(_chkAutoStart.Checked);
            Log($"开机自启动: {(_chkAutoStart.Checked ? "已开启" : "已关闭")}");
        };

        _chkStartMinimized = new CheckBox
        {
            Text = "启动时最小化到系统托盘",
            Checked = _settings.StartMinimized,
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F)
        };
        _chkStartMinimized.CheckedChanged += (s, e) =>
        {
            _settings.StartMinimized = _chkStartMinimized.Checked;
            _settings.Save();
        };

        startupInner.Controls.AddRange(new Control[] { _chkAutoStart, _chkStartMinimized });
        groupStartup.Controls.Add(startupInner);

        var groupInfo = new GroupBox
        {
            Text = "当前显示器信息",
            Width = 490,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Font = new Font("Microsoft YaHei UI", 9F),
            Margin = new Padding(2)
        };

        double currentSdr = NativeMethods.GetCurrentSdrWhiteLevel();
        var lblInfo = new Label
        {
            Text = $"当前 SDR 白色电平：{currentSdr:F2} ({currentSdr * 80:F0} nits)\n" +
                   $"API 状态：{(_changeBrightness != null ? "可用" : "不可用")}\n" +
                   $"配置文件：{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HdrBrightness", "settings.json")}",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 8F),
            Padding = new Padding(8, 4, 8, 4)
        };
        groupInfo.Controls.Add(lblInfo);

        root.Controls.AddRange(new Control[] { groupRange, groupStartup, groupInfo });
        tab.Controls.Add(root);
    }

    private void ApplyRange(object? sender, EventArgs e)
    {
        double newMin = (double)_nudMinBrightness.Value / 10.0;
        double newMax = (double)_nudMaxBrightness.Value / 10.0;
        double newDefault = (double)_nudDefaultBrightness.Value / 10.0;

        if (newMin >= newMax)
        {
            MessageBox.Show("最低亮度必须小于最高亮度", "无效范围", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (newDefault < newMin || newDefault > newMax)
        {
            MessageBox.Show("默认亮度必须在最低和最高亮度之间", "无效范围", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.MinBrightness = newMin;
        _settings.MaxBrightness = newMax;
        _settings.DefaultBrightness = newDefault;
        _settings.Save();

        _nudExact.Minimum = (decimal)newMin * 10;
        _nudExact.Maximum = (decimal)newMax * 10;

        _btnMin.Text = $"最低({newMin:F1})";
        _btnDefault.Text = $"默认({newDefault:F1})";
        _btnMax.Text = $"最高({newMax:F1})";
        _lblRange.Text = $"范围：{newMin:F1} ~ {newMax:F1}";

        double currentBrightness = SliderToBrightness(_trackBar.Value);
        currentBrightness = Math.Clamp(currentBrightness, newMin, newMax);
        _trackBar.Value = BrightnessToSlider(currentBrightness);

        Log($"亮度设置已更新：最低={newMin:F1}, 默认={newDefault:F1}, 最高={newMax:F1}");
    }

    private void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Application.ExecutablePath;
                key.SetValue(AppName, $"\"{exePath}\" --minimized");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch (Exception ex)
        {
            Log($"设置自启动失败：{ex.Message}");
        }
    }

    private void MainForm_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        ShowInTaskbar = false;
        _notifyIcon.ShowBalloonTip(2000, "HDR 亮度调节", "程序已最小化到系统托盘，双击图标恢复", ToolTipIcon.Info);
    }

    private void TrackBar_ValueChanged(object? sender, EventArgs e)
    {
        double brightness = SliderToBrightness(_trackBar.Value);
        _lblValue.Text = FormatBrightness(brightness);
        _lblNits.Text = FormatNits(brightness);
        _nudExact.Value = (decimal)Math.Round(brightness, 1) * 10;

        if (_chkLive.Checked)
        {
            ApplyBrightness(brightness);
        }
    }

    private void SetBrightness(double brightness)
    {
        brightness = Math.Clamp(brightness, CurrentMinBrightness, CurrentMaxBrightness);
        _trackBar.Value = BrightnessToSlider(brightness);
        _lblValue.Text = FormatBrightness(brightness);
        _lblNits.Text = FormatNits(brightness);
        _nudExact.Value = (decimal)Math.Round(brightness, 1) * 10;
        ApplyBrightness(brightness);
    }

    private void SetBrightnessSilent(double brightness)
    {
        brightness = Math.Clamp(brightness, CurrentMinBrightness, CurrentMaxBrightness);
        _trackBar.ValueChanged -= TrackBar_ValueChanged;
        _trackBar.Value = BrightnessToSlider(brightness);
        _trackBar.ValueChanged += TrackBar_ValueChanged;
        _lblValue.Text = FormatBrightness(brightness);
        _lblNits.Text = FormatNits(brightness);
        _nudExact.Value = (decimal)Math.Round(brightness, 1) * 10;
    }

    private void ApplyBrightness(double brightness)
    {
        if (_changeBrightness == null)
        {
            Log("错误：API 未初始化，无法设置亮度");
            return;
        }

        try
        {
            int hr = _changeBrightness(_primaryMonitor, brightness);
            if (hr == 0)
            {
                Log($"设置亮度: {brightness:F2} ({brightness * 80:F0} nits) - 成功");
            }
            else
            {
                Log($"设置亮度: {brightness:F2} - 失败 (HRESULT: 0x{hr:X8})");
            }
        }
        catch (Exception ex)
        {
            Log($"异常: {ex.Message}");
        }
    }

    private int BrightnessToSlider(double brightness)
    {
        double range = CurrentMaxBrightness - CurrentMinBrightness;
        if (range <= 0) return 0;
        double ratio = (brightness - CurrentMinBrightness) / range;
        return (int)Math.Clamp(ratio * SliderResolution, 0, SliderResolution);
    }

    private double SliderToBrightness(int sliderValue)
    {
        double ratio = (double)sliderValue / SliderResolution;
        return CurrentMinBrightness + ratio * (CurrentMaxBrightness - CurrentMinBrightness);
    }

    private static string FormatBrightness(double brightness)
    {
        return $"当前亮度：{brightness:F2}";
    }

    private static string FormatNits(double brightness)
    {
        double nits = brightness * 80;
        return $"≈ {nits:F0} nits";
    }

    private void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        _txtLog?.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon?.Dispose();
            _trayMenu?.Dispose();
        }
        base.Dispose(disposing);
    }
}
