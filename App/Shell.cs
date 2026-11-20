// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using UsbBridge;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Desk;

internal sealed partial class Shell : Window
{
    private readonly Engines engines = new();
    private readonly Store store = new(Path.Combine(Program.Folder, "profiles.json"));
    private Session session = null!;
    private Settings Pref => session.Data;
    private readonly Grid root = new(),
        surface = new();
    private readonly NavigationView nav = new()
    {
        IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
        IsSettingsVisible = false,
        IsPaneToggleButtonVisible = true,
        OpenPaneLength = 206,
        CompactPaneLength = 48,
        PaneDisplayMode = NavigationViewPaneDisplayMode.Auto,
        ExpandedModeThresholdWidth = 920,
    };
    private readonly ProgressBar progress = new()
    {
        Visibility = Visibility.Collapsed,
        Height = 3,
        VerticalAlignment = VerticalAlignment.Top,
    };
    private readonly InfoBar notice = new() { IsOpen = false, IsClosable = true };
    private readonly TextBlock driver = new()
    {
        Margin = new(16, 8, 8, 16),
        TextWrapping = TextWrapping.Wrap,
        FontSize = 13,
    };
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer timer;
    private readonly Dictionary<string, NavigationViewItem> items = [];
    private readonly UISettings uiSettings = new();
    private RegisteredWaitHandle? wake;
    private Tray? tray;
    private Server? server;
    private string hostError = "正在读取本机服务状态",
        page = "home",
        selectedId = "",
        lastView = "",
        selectedAddress = "";
    private DateTime nextHost = DateTime.MinValue;
    private readonly HashSet<string> localPicked = [],
        remotePicked = [];

    private sealed record Discovery(List<Device>? Rows, string Summary);

    private readonly Dictionary<string, Discovery> discoveryByTarget = [];
    private List<Device>? exports;
    private string discovery = "";
    private bool working,
        exiting,
        closed,
        hidden,
        loaded,
        pending;
    private TaskCompletionSource? pollFinished;
    private bool Motion => !Pref.ReduceMotion && uiSettings.AnimationsEnabled;
    private bool Dark => root.ActualTheme == ElementTheme.Dark;
    private nint Hwnd => WinRT.Interop.WindowNative.GetWindowHandle(this);
    private static readonly string[] PageNames =
    [
        "连接概览",
        "共享本机 USB",
        "连接远端 USB",
        "映射列表",
        "活动日志",
        "设置",
    ];

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint h);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint h, int attr, ref int value, int size);

    public Shell()
    {
        Title = "USB Bridge Desk";
        var local = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string? legacy = new[] { "usb-bridge-desk", "USB Bridge Desk" }
            .Select(p => Path.Combine(local, p, "profiles.json"))
            .FirstOrDefault(File.Exists);
        Settings cfg;
        string? configWarning = null;
        try
        {
            cfg = store.Load(legacy);
        }
        catch (Exception e)
            when (e
                    is System.Text.Json.JsonException
                        or InvalidDataException
                        or ArgumentException
                        or NullReferenceException
            )
        {
            var profilePath = Path.Combine(Program.Folder, "profiles.json");
            if (File.Exists(profilePath))
                File.Move(
                    profilePath,
                    profilePath + "." + Guid.NewGuid().ToString("N") + ".invalid"
                );
            configWarning = "原始配置已保留。已以空设备列表启动，可在诊断目录查看详情。";
            Program.Trace($"Profile validation: {e}");
            cfg = new();
        }
        session = new(engines, cfg, store.Write, () =>
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        );
        selectedId = Pref.Targets.FirstOrDefault()?.Id ?? "";
        ExtendsContentIntoTitleBar = true;
        root.RowDefinitions.Add(new() { Height = new(44) });
        root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var title = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new(16, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.Children.Add(new FontIcon { Glyph = "\uE88E", FontSize = 18 });
        title.Children.Add(
            new TextBlock
            {
                Text = Title,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            }
        );
        var drag = new Grid();
        drag.Children.Add(title);
        root.Children.Add(drag);
        string[] keys = ["home", "host", "client", "maps", "logs", "settings"],
            glyphs = ["\uE80F", "\uE968", "\uE774", "\uE71B", "\uE9D5", "\uE713"];
        for (int i = 0; i < keys.Length; i++)
        {
            var item = new NavigationViewItem
            {
                Content = PageNames[i],
                Tag = keys[i],
                Icon = new FontIcon { Glyph = glyphs[i] },
            };
            items[keys[i]] = item;
            if (i >= 4)
                nav.FooterMenuItems.Add(item);
            else
                nav.MenuItems.Add(item);
        }
        nav.PaneFooter = driver;
        var body = new Grid { Padding = new(24, 12, 24, 12) };
        body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        body.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var scroll = new ScrollViewer
        {
            Content = surface,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        body.Children.Add(notice);
        Grid.SetRow(scroll, 1);
        body.Children.Add(scroll);
        body.Children.Add(progress);
        nav.Content = body;
        Grid.SetRow(nav, 1);
        root.Children.Add(nav);
        Content = root;
        SetTitleBar(drag);
        nav.SelectionChanged += (_, e) =>
        {
            if (e.SelectedItem is NavigationViewItem n)
            {
                page = (string)n.Tag;
                scroll.ChangeView(null, 0, null, true);
                Render(true);
            }
        };
        root.ActualThemeChanged += (_, _) =>
        {
            SetCaption();
            if (loaded)
                Render(true);
        };
        SetLook();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "icon.ico"));
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var dpi = GetDpiForWindow(Hwnd) / 96d;
        AppWindow.Resize(
            new(Math.Min((int)(1136 * dpi), area.Width), Math.Min((int)(762 * dpi), area.Height))
        );
        int round = 2;
        DwmSetWindowAttribute(Hwnd, 33, ref round, 4);
        int noBorder = unchecked((int)0xFFFFFFFE);
        DwmSetWindowAttribute(Hwnd, 34, ref noBorder, 4);
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            RoundDownlevelWindow();
            AppWindow.Changed += (_, args) =>
            {
                if (args.DidSizeChange || args.DidPresenterChange)
                    RoundDownlevelWindow();
            };
        }
        var shortcut = new KeyboardAccelerator
        {
            Key = VirtualKey.S,
            Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
        };
        shortcut.Invoked += async (sender, e) =>
        {
            e.Handled = true;
            if (working || exiting)
                return;
            try
            {
                await Screenshot();
            }
            catch (Exception ex)
            {
                Alert("截图未保存", ex.Message);
            }
        };
        root.KeyboardAccelerators.Add(shortcut);
        try
        {
            tray = new(
                ShowMain,
                () =>
                {
                    ShowMain();
                    Navigate("maps");
                },
                RequestExit
            );
        }
        catch (Exception e)
        {
            Program.Trace(e.ToString());
            Alert("托盘暂不可用", e.Message);
        }
        AppWindow.Closing += (_, e) =>
        {
            if (closed)
                return;
            e.Cancel = true;
            if (!exiting && Pref.ToTray && tray != null)
            {
                AppWindow.Hide();
                hidden = true;
                tray.Notify("应用已转入后台。双击图标可恢复窗口。");
            }
            else
                RequestExit();
        };
        Closed += (_, _) =>
        {
            closed = true;
            timer?.Stop();
            wake?.Unregister(null);
            tray?.Dispose();
        };
        timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(3);
        timer.Tick += async (_, _) => await Poll();
        wake = ThreadPool.RegisterWaitForSingleObject(
            Program.Wake!,
            (_, _) => DispatcherQueue.TryEnqueue(ShowMain),
            null,
            Timeout.Infinite,
            false
        );
        loaded = true;
        Navigate("home");
        Render(true);
        if (configWarning != null)
            Alert("配置需要检查", configWarning);
        root.Loaded += async (_, _) =>
        {
            timer.Start();
            await Poll();
        };
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint h);

    [DllImport("gdi32.dll")]
    private static extern nint CreateRoundRectRgn(
        int x,
        int y,
        int right,
        int bottom,
        int width,
        int height
    );

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint handle);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(nint window, nint region, bool redraw);

    private void RoundDownlevelWindow()
    {
        if (
            AppWindow.Presenter is OverlappedPresenter presenter
            && presenter.State == OverlappedPresenterState.Maximized
        )
        {
            SetWindowRgn(Hwnd, 0, true);
            return;
        }
        int diameter = (int)Math.Round(16 * GetDpiForWindow(Hwnd) / 96d);
        var region = CreateRoundRectRgn(
            0,
            0,
            AppWindow.Size.Width + 1,
            AppWindow.Size.Height + 1,
            diameter,
            diameter
        );
        if (region != 0 && SetWindowRgn(Hwnd, region, true) == 0)
            DeleteObject(region);
    }

    private void RequestExit()
    {
        exiting = true;
        timer.Stop();
        if (working)
        {
            ShowMain();
            Alert("正在完成当前操作", "完成后将自动退出。");
            return;
        }
        closed = true;
        Close();
    }

    private void ShowMain()
    {
        if (closed)
            return;
        hidden = false;
        AppWindow.Show();
        if (
            AppWindow.Presenter is OverlappedPresenter p
            && p.State == OverlappedPresenterState.Minimized
        )
            p.Restore();
        Activate();
        SetForegroundWindow(Hwnd);
    }

    private void Navigate(string key)
    {
        nav.SelectedItem = items[key];
    }

    private void SetCaption()
    {
        var title = AppWindow.TitleBar;
        if (!AppWindowTitleBar.IsCustomizationSupported())
            return;
        title.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        title.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        title.ButtonForegroundColor = Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
    }

    private void SetLook()
    {
        root.RequestedTheme = Pref.Theme switch
        {
            "dark" => ElementTheme.Dark,
            "light" => ElementTheme.Light,
            _ => ElementTheme.Default,
        };
        try
        {
            SystemBackdrop =
                Pref.Material == "acrylic"
                && Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported()
                    ? new DesktopAcrylicBackdrop()
                : Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported()
                    ? new MicaBackdrop()
                : null;
            if (
                SystemBackdrop == null
                && Pref.Material == "mica"
                && Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported()
            )
                SystemBackdrop = new DesktopAcrylicBackdrop();
        }
        catch (Exception e)
        {
            Program.Trace(e.ToString());
            SystemBackdrop = null;
        }
        root.Background = SystemBackdrop == null ? Brush(Dark ? 0x182124 : 0xF2F6F5) : null;
        SetCaption();
    }

    private void Alert(string title, string detail)
    {
        notice.Title = title;
        notice.Message = detail;
        notice.Severity = InfoBarSeverity.Warning;
        notice.IsOpen = true;
    }

    private async Task Act(Func<Task> fn)
    {
        if (exiting || pending || (working && pollFinished == null))
            return;
        pending = true;
        var outstanding = pollFinished?.Task;
        if (outstanding != null)
            await outstanding;
        pending = false;
        if (exiting || closed)
            return;
        working = true;
        nav.IsHitTestVisible = false;
        progress.Visibility = Visibility.Visible;
        progress.IsIndeterminate = Motion;
        progress.Value = 50;
        try
        {
            await fn();
        }
        catch (Exception e)
        {
            Program.Trace(e.ToString());
            Alert("操作未完成", e.Message);
        }
        finally
        {
            working = false;
            nav.IsHitTestVisible = true;
            progress.Visibility = Visibility.Collapsed;
            progress.IsIndeterminate = false;
            if (exiting)
                RequestExit();
            else
                Render(true);
        }
    }

    private async Task RefreshHost()
    {
        try
        {
            server = await engines.ReadHost();
            hostError = "";
            localPicked.IntersectWith(server.Devices.Select(x => x.Instance));
        }
        catch (Exception e)
        {
            server = null;
            hostError = e.Message;
            localPicked.Clear();
        }
        nextHost = DateTime.UtcNow.AddSeconds(15);
    }

    private async Task Poll()
    {
        if (working || pending || exiting)
            return;
        working = true;
        pollFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            if (DateTime.UtcNow >= nextHost)
                await RefreshHost();
            await session.Poll();
            Render(false);
        }
        catch (Exception e)
        {
            Program.Trace(e.ToString());
            Alert("无法更新状态", e.Message);
        }
        finally
        {
            working = false;
            var done = pollFinished;
            pollFinished = null;
            done!.TrySetResult();
            if (exiting)
                RequestExit();
        }
    }

    private void Render(bool force)
    {
        if (!loaded || closed)
            return;
        var active = Pref.Mappings.Count(x => x.State == "connected");
        var waiting = Pref.Mappings.Count - active;
        string status =
            waiting > 0 ? $"{active} 已连接 · {waiting} 待处理"
            : active > 0 ? $"{active} 个设备已连接"
            : server?.Running == true ? "共享服务运行中"
            : "空闲";
        bool fault = session.DriverError.Length > 0 || hostError.Length > 0;
        tray?.Update(
            fault || waiting > 0 ? "warning"
                : active > 0 || server?.Running == true ? "active"
                : "idle",
            fault ? "组件待检查" : status,
            hidden
        );
        driver.Text = session.DriverError == "" ? "●  虚拟控制器已就绪" : "○  控制器待检查";
        ToolTipService.SetToolTip(
            driver,
            session.DriverError == "" ? "USB/IP UDE" : session.DriverError
        );
        var key = System.Text.Json.JsonSerializer.Serialize(
            new
            {
                page,
                server,
                hostError,
                session.DriverError,
                Pref.Targets,
                Mappings = Pref.Mappings.Select(m => new
                {
                    m.Id,
                    m.State,
                    m.LocalPort,
                    m.Error,
                }),
                Pref.Logs,
                Pref.Theme,
                Pref.Material,
                Pref.ReduceMotion,
                selectedId,
                discovery,
            }
        );
        if (!force && key == lastView)
            return;
        bool changedData = key != lastView;
        bool changedPage = lastPage != page;
        lastPage = page;
        lastView = key;
        var stack = Column(16);
        stack.MaxWidth = 1080;
        stack.HorizontalAlignment = HorizontalAlignment.Stretch;
        switch (page)
        {
            case "home":
                Home(stack);
                break;
            case "host":
                HostPage(stack);
                break;
            case "client":
                ClientPage(stack);
                break;
            case "maps":
                Maps(stack);
                break;
            case "logs":
                Logs(stack);
                break;
            default:
                SettingsPage(stack);
                break;
        }
        if (Motion && (changedPage || changedData))
            stack.Transitions = new TransitionCollection
            {
                new EntranceThemeTransition { FromVerticalOffset = changedPage ? 12 : 3 },
            };
        surface.ChildrenTransitions =
            Motion && !changedPage && changedData
                ? new TransitionCollection
                {
                    new AddDeleteThemeTransition(),
                    new RepositionThemeTransition(),
                }
                : new TransitionCollection();
        surface.Children.Clear();
        surface.Children.Add(stack);
    }

    private string lastPage = "";

    private static SolidColorBrush Brush(int rgb, byte a = 255) =>
        new(Color.FromArgb(a, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));

    private static StackPanel Column(double gap = 8) => new() { Spacing = gap };

    private static StackPanel Row(double gap = 8) =>
        new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = gap,
            VerticalAlignment = VerticalAlignment.Center,
        };

    private static TextBlock Text(string s, double size = 15, bool bold = false) =>
        new()
        {
            Text = s,
            FontSize = size,
            FontWeight = bold
                ? Microsoft.UI.Text.FontWeights.SemiBold
                : Microsoft.UI.Text.FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

    private TextBlock Muted(string s)
    {
        var t = Text(s, 14);
        t.Opacity = .76;
        return t;
    }

    private Border Card(UIElement child, bool accent = false) =>
        new()
        {
            Child = child,
            Padding = new(20, 17, 20, 17),
            CornerRadius = new(12),
            Background = accent
                ? Brush(Dark ? 0x21413D : 0xDEEFEA, 238)
                : Brush(Dark ? 0x323A3D : 0xFFFFFF, Dark ? (byte)208 : (byte)226),
            BorderBrush = Brush(Dark ? 0x647977 : 0xA5B9B2, 80),
            BorderThickness = new(1),
        };

    private Button Btn(string title, Func<Task> fn, bool primary = false, bool enabled = true)
    {
        var b = new Button
        {
            Content = title,
            IsEnabled = enabled,
            CornerRadius = new(7),
            MinHeight = 34,
            Padding = new(14, 6, 14, 6),
        };
        if (Motion)
        {
            b.TranslationTransition = new Microsoft.UI.Xaml.Vector3Transition
            {
                Duration = TimeSpan.FromMilliseconds(110),
            };
            b.PointerEntered += (_, _) =>
            {
                if (Motion)
                    b.Translation = new(0, -1, 0);
            };
            b.PointerExited += (_, _) => b.Translation = System.Numerics.Vector3.Zero;
        }
        if (primary)
            b.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        b.Click += async (_, _) => await Act(fn);
        return b;
    }

    private static void Add(Panel p, params UIElement[] elements)
    {
        foreach (var e in elements)
            p.Children.Add(e);
    }

    private static void Heading(StackPanel p, string title, string sub)
    {
        var h = Column(5);
        Add(h, Text(title, 30, true), Text(sub, 15));
        p.Children.Add(h);
    }

    private static Grid Split(FrameworkElement left, FrameworkElement right)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Grid.SetColumn(right, 1);
        Add(g, left, right);
        return g;
    }

    private static Task Done(Action f)
    {
        f();
        return Task.CompletedTask;
    }

    private void Copy(string text)
    {
        var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
        pkg.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
    }
}
