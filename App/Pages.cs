// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using UsbBridge;
using Forms = System.Windows.Forms;

namespace Desk;

internal sealed partial class Shell
{
    private Target? Selected => Pref.Targets.FirstOrDefault(t => t.Id == selectedId);

    private void Home(StackPanel p)
    {
        Heading(p, "连接概览", "在两台 Windows 电脑之间共享 USB 设备。");
        var hero = Column(13);
        Add(
            hero,
            Text("共享与连接", 21, true),
            Text("在插有设备的电脑启动共享，在需要使用设备的电脑添加远端目标。")
        );
        var routes = Row();
        Add(
            routes,
            Btn("共享本机 USB", () => Done(() => Navigate("host")), true),
            Btn("连接远端 USB", () => Done(() => Navigate("client")))
        );
        Add(hero, routes);
        p.Children.Add(Card(hero, true));
        var stats = new Grid { ColumnSpacing = 12 };
        var cells = new[]
        {
            (
                "本机已共享",
                server == null
                    ? "—"
                    : server.Devices.Count(d => d.Status is "已共享" or "使用中").ToString()
            ),
            ("已保存目标", Pref.Targets.Count.ToString()),
            ("已连接映射", Pref.Mappings.Count(m => m.State == "connected").ToString()),
        };
        for (var i = 0; i < cells.Length; i++)
        {
            stats.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            var column = Column(5);
            Add(column, Muted(cells[i].Item1), Text(cells[i].Item2, 27, true));
            var dest = new[] { "host", "client", "maps" }[i];
            var tile = new Button
            {
                Content = Card(column),
                Padding = new(0),
                BorderThickness = new(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            tile.Click += (_, _) => Navigate(dest);
            Grid.SetColumn(tile, i);
            stats.Children.Add(tile);
        }
        Add(
            p,
            stats,
            Split(Text("最近活动", 19, true), Btn("查看日志", () => Done(() => Navigate("logs"))))
        );
        Activities(p, Pref.Logs.Take(3));
    }

    private void HostPage(StackPanel p)
    {
        Heading(p, "共享本机 USB", "将选中的本机设备提供给远端。每批最多选择 6 个。");
        var info = Column(5);
        Add(
            info,
            Text(
                server?.Running == true ? "共享服务运行中"
                    : server == null ? "共享服务待检查"
                    : "共享服务已停止",
                20,
                true
            ),
            Muted(hostError == "" ? "USB/IP  ·  TCP 3240" : hostError)
        );
        var service = Btn(
            server?.Running == true ? "停止服务" : "启动服务",
            async () =>
            {
                var start = server?.Running != true;
                if (
                    !start
                    && !await Confirm("停止共享服务？", "请先在客户端结束 USB 读写。", "停止服务")
                )
                    return;
                try
                {
                    await engines.Service(start);
                    session.Log(start ? "共享服务已启动 · TCP 3240" : "共享服务已停止");
                }
                finally
                {
                    await RefreshHost();
                }
            },
            true,
            server != null
        );
        p.Children.Add(Card(Split(info, service), true));
        var addresses = Engines.Addresses().ToArray();
        var link = Column(9);
        Add(
            link,
            Split(
                Text("UU 被控端  ·  127.0.0.1:3240", 16, true),
                Btn("复制", () => Done(() => Copy("127.0.0.1:3240")))
            ),
            Text("EasyTier / TCP 本机地址", 16, true)
        );
        var combo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 140,
            ItemsSource = addresses.Select(a => $"{a.Name}   ·   {a.Address}").ToArray(),
        };
        if (addresses.Length > 0)
            combo.SelectedIndex = Math.Max(
                0,
                Array.FindIndex(addresses, a => a.Address == selectedAddress)
            );
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0)
                selectedAddress = addresses[combo.SelectedIndex].Address;
        };
        Add(
            link,
            Split(
                combo,
                Btn(
                    "复制地址",
                    () => Done(() => Copy(addresses[combo.SelectedIndex].Address)),
                    enabled: addresses.Length > 0
                )
            )
        );
        p.Children.Add(Card(link));
        var devices = Column(10);
        var buttons = Row();
        var share = Btn("共享所选", () => ShareSelected(true), true);
        var unshare = Btn("停止共享", () => ShareSelected(false));
        void ToggleActions()
        {
            var picked = server?.Devices.Where(d => localPicked.Contains(d.Instance)).ToArray()
                ?? [];
            share.IsEnabled =
                picked.Length is >= 1 and <= 6 && picked.Any(d => d.Status == "未共享");
            unshare.IsEnabled =
                picked.Length is >= 1 and <= 6 && picked.Any(d => d.Status != "未共享");
        }
        ToggleActions();
        Add(buttons, Btn("刷新", RefreshHost), unshare, share);
        Add(devices, Split(Text("本机设备", 19, true), buttons));
        if (server?.Devices.Count > 0)
        {
            var keys = server.Devices.Select(d => d.Instance).ToArray();
            Add(devices, SelectAll(keys, localPicked));
            foreach (var d in server.Devices)
            {
                var box = Choose(
                    d.Device.Name,
                    $"{d.Device.Bus}   ·   {d.Device.Vid}:{d.Device.Pid}  {d.Peer}",
                    localPicked.Contains(d.Instance),
                    v =>
                    {
                        Pick(localPicked, d.Instance, v);
                        ToggleActions();
                    }
                );
                Add(devices, Split(box, Muted(d.Status)));
            }
        }
        else
            Add(
                devices,
                Muted(
                    hostError == ""
                        ? "暂未检测到 USB 设备。连接设备后点击刷新。"
                        : "组件恢复后可查看本机设备。"
                )
            );
        p.Children.Add(Card(devices));
    }

    private async Task ShareSelected(bool enable)
    {
        var rows = server
            ?.Devices.Where(d =>
                localPicked.Contains(d.Instance)
                && (enable ? d.Status == "未共享" : d.Status != "未共享")
            )
            .ToArray()
            ?? [];
        if (
            !enable
            && !await Confirm("停止共享所选设备？", "请先在客户端结束 USB 读写。", "停止共享")
        )
            return;
        try
        {
            await engines.Share(rows, enable);
            session.Log($"{(enable ? "共享" : "停止共享")} · {rows.Length} 个本机设备");
            localPicked.Clear();
        }
        finally
        {
            await RefreshHost();
        }
    }

    private Button SelectAll(string[] keys, HashSet<string> selection)
    {
        bool all = keys.Length > 0 && keys.All(selection.Contains);
        return Btn(
            all ? "清除选择" : "全选",
            () =>
                Done(() =>
                {
                    if (keys.All(selection.Contains))
                        selection.ExceptWith(keys);
                    else
                        selection.UnionWith(keys);
                }),
            enabled: keys.Length > 0
        );
    }

    private static void Pick(HashSet<string> selection, string key, bool value)
    {
        if (value)
            selection.Add(key);
        else
            selection.Remove(key);
    }

    private CheckBox Choose(string name, string detail, bool selected, Action<bool> change)
    {
        var texts = Column(2);
        Add(texts, Text(name, 15, true), Muted(detail));
        var check = new CheckBox
        {
            Content = texts,
            IsChecked = selected,
            Padding = new(8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        check.Checked += (_, _) => change(true);
        check.Unchecked += (_, _) => change(false);
        return check;
    }

    private void ClientPage(StackPanel p)
    {
        Heading(p, "连接远端 USB", "选择目标电脑，发现它共享的设备并连接到本机。");
        var t = Selected;
        var targets = Column(12);
        Add(targets, Split(Text("目标电脑", 19, true), Btn("添加目标", () => Edit(null), true)));
        if (t != null)
        {
            var choose = new ComboBox
            {
                ItemsSource = Pref.Targets.ToArray(),
                SelectedItem = t,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            choose.SelectionChanged += (_, _) =>
            {
                if (choose.SelectedItem is Target v && v.Id != selectedId)
                {
                    SelectTarget(v.Id);
                    Render(true);
                }
            };
            var actions = Row();
            Add(actions, Btn("编辑", () => Edit(t)), Btn("移除", async () =>
                {
                    if (await Confirm($"移除“{t.Name}”？", "此操作会删除已保存的目标。", "移除"))
                    {
                        session.RemoveTarget(t);
                        discoveryByTarget.Remove(t.Id);
                        SelectTarget(Pref.Targets.FirstOrDefault()?.Id ?? "");
                    }
                }));
            Add(
                targets,
                Split(choose, actions),
                Muted(Transports.All.Single(x => x.Id == t.Mode).Hint)
            );
        }
        else
            Add(targets, Muted("添加第一台远端电脑，填写 UU 本地映射端口或共享电脑地址。"));
        p.Children.Add(Card(targets, true));
        if (t == null)
            return;
        var list = Column(10);
        var controls = Row();
        var attach = Btn(
            "连接所选",
            async () =>
            {
                await session.Connect(
                    t,
                    (exports ?? []).Where(d => remotePicked.Contains(d.Bus)).ToArray()
                );
                remotePicked.Clear();
                Navigate("maps");
            },
            true,
            remotePicked.Count > 0 && session.DriverError == ""
        );
        Add(controls, Btn("发现设备", async () =>
            {
                exports = null;
                remotePicked.Clear();
                discovery = "";
                try
                {
                    exports = await engines.Discover(t);
                    discovery = $"在线 · 发现 {exports.Count} 个设备 · {DateTime.Now:HH:mm:ss}";
                    session.Log($"{t.Name} · {discovery}");
                }
                catch (Exception e)
                {
                    discovery = $"离线 · {DateTime.Now:HH:mm:ss} · {e.Message}";
                    session.Log($"{t.Name} · {discovery}", "warn");
                    throw;
                }
                finally
                {
                    discoveryByTarget[t.Id] = new(exports, discovery);
                }
            }), attach);
        Add(list, Split(Text("可连接设备", 19, true), controls));
        if (exports?.Count > 0)
        {
            var available = exports
                .Where(d => !Pref.Mappings.Any(m => m.TargetId == t.Id && m.Bus == d.Bus))
                .Select(d => d.Bus)
                .ToArray();
            Add(list, Split(Muted(discovery), SelectAll(available, remotePicked)));
            foreach (var d in exports)
            {
                bool mapped = Pref.Mappings.Any(m => m.TargetId == t.Id && m.Bus == d.Bus);
                var box = Choose(
                    d.Name,
                    $"{d.Bus}   ·   {d.Vid}:{d.Pid}" + (mapped ? "   ·   已在映射列表" : ""),
                    remotePicked.Contains(d.Bus),
                    v =>
                    {
                        Pick(remotePicked, d.Bus, v);
                        attach.IsEnabled = remotePicked.Count > 0 && session.DriverError == "";
                    }
                );
                box.IsEnabled = !mapped;
                list.Children.Add(box);
            }
        }
        else
            Add(
                list,
                Muted(discovery.Length > 0 ? discovery : "点击「发现设备」读取此目标的共享列表。")
            );
        p.Children.Add(Card(list));
        if (session.DriverError != "")
            p.Children.Add(Muted("虚拟控制器 · " + session.DriverError));
    }

    private void SelectTarget(string id)
    {
        selectedId = id;
        remotePicked.Clear();
        discoveryByTarget.TryGetValue(id, out var result);
        exports = result?.Rows;
        discovery = result?.Summary ?? "";
    }

    private async Task Edit(Target? t)
    {
        var fields = Column(10);
        var name = new TextBox
        {
            Header = "目标名称",
            MaxLength = 48,
            Text = t?.Name ?? "",
            PlaceholderText = "例如 工作室电脑",
        };
        var mode = new ComboBox
        {
            Header = "传输方式",
            ItemsSource = Transports.All,
            SelectedItem = Transports.All.Single(x => x.Id == (t?.Mode ?? "uu")),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var host = new TextBox
        {
            Header = "地址",
            Text = t?.Host ?? "127.0.0.1",
            IsEnabled = t != null && t.Mode != "uu",
        };
        var port = new TextBox
        {
            Header = "TCP 端口",
            Text = (t?.Port ?? Transports.NextPort(Pref.Targets)).ToString(),
        };
        var tip = Muted(((Transport)mode.SelectedItem).Hint);
        var error = Text("");
        error.Visibility = Visibility.Collapsed;
        string previous = host.IsEnabled ? host.Text : "";
        mode.SelectionChanged += (_, _) =>
        {
            var m = (Transport)mode.SelectedItem;
            if (host.IsEnabled)
                previous = host.Text;
            host.IsEnabled = m.Id != "uu";
            host.Text = host.IsEnabled ? previous : "127.0.0.1";
            port.Text = (m.Id == "uu" ? Transports.NextPort(Pref.Targets) : 3240).ToString();
            tip.Text = m.Hint;
        };
        Add(fields, name, mode, host, port, tip, error);
        var dialog = Dialog(t == null ? "添加连接目标" : "编辑连接目标", fields, "保存");
        dialog.PrimaryButtonClick += (_, e) =>
        {
            try
            {
                if (!int.TryParse(port.Text, out var number))
                    throw new ArgumentException("请输入整数端口。");
                var target = Transports.Make(
                    t?.Id ?? Guid.NewGuid().ToString(),
                    name.Text,
                    ((Transport)mode.SelectedItem).Id,
                    host.Text,
                    number
                );
                session.SetTarget(target);
                discoveryByTarget.Remove(target.Id);
                SelectTarget(target.Id);
            }
            catch (Exception ex)
            {
                error.Text = ex.Message;
                error.Visibility = Visibility.Visible;
                e.Cancel = true;
            }
        };
        await dialog.ShowAsync();
    }

    private ContentDialog Dialog(string title, object content, string primary)
    {
        var d = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primary,
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = root.XamlRoot,
            RequestedTheme = root.RequestedTheme,
        };
        if (!Motion)
            d.Transitions = new TransitionCollection();
        return d;
    }

    private async Task<bool> Confirm(string title, string body, string action) =>
        await Dialog(title, body, action).ShowAsync() == ContentDialogResult.Primary;

    private void Maps(StackPanel p)
    {
        Heading(p, "映射列表", "查看远端设备与本机虚拟端口的对应关系。");
        if (Pref.Mappings.Count == 0)
        {
            var empty = Column(10);
            Add(
                empty,
                Text("尚无设备映射", 19, true),
                Muted("连接成功后，可在此查看虚拟端口和重连状态。"),
                Btn("连接远端 USB", () => Done(() => Navigate("client")), true)
            );
            p.Children.Add(Card(empty));
            return;
        }
        foreach (var m in Pref.Mappings)
        {
            var t = Pref.Targets.Single(t => t.Id == m.TargetId);
            var detail = Column(6);
            string state = m.State switch
            {
                "connected" => $"已连接  ·  虚拟端口 {m.LocalPort}",
                "retry" => "等待重连",
                "stopping" => "等待释放",
                _ => "正在核对控制器",
            };
            Add(
                detail,
                Text(m.Name, 18, true),
                Text(state),
                Muted($"{t.Name}   ·   {t.Address}   /   {m.Bus}   ·   {m.Vid}:{m.Pid}")
            );
            if (m.Error.Length > 0)
                detail.Children.Add(Muted(m.Error));
            var release = Btn(
                m.State == "retry" ? "取消重连" : "断开",
                async () =>
                {
                    if (
                        await Confirm(
                            "移除此映射？",
                            "请结束设备读写，存储设备请先安全弹出。",
                            "移除并停止重连"
                        )
                    )
                        await session.Release(m);
                },
                enabled: m.State != "stopping"
            );
            p.Children.Add(Card(Split(detail, release)));
        }
    }

    private void Activities(StackPanel p, IEnumerable<Entry> entries)
    {
        var box = Column(12);
        foreach (var entry in entries)
        {
            var line = Split(
                Text(entry.Message),
                Muted(
                    DateTimeOffset
                        .FromUnixTimeMilliseconds(entry.Time)
                        .ToLocalTime()
                        .ToString("MM-dd HH:mm")
                )
            );
            box.Children.Add(line);
        }
        if (box.Children.Count == 0)
            box.Children.Add(Muted("暂无活动。共享、连接与重连记录会显示在这里。"));
        p.Children.Add(Card(box));
    }

    private void Logs(StackPanel p)
    {
        Heading(p, "活动日志", "保留最近 250 条本机操作和连接记录。");
        var row = Row();
        Add(
            row,
            Btn(
                "复制日志",
                () =>
                    Done(() =>
                        Copy(
                            string.Join(
                                Environment.NewLine,
                                Pref.Logs.Select(x =>
                                    $"{DateTimeOffset.FromUnixTimeMilliseconds(x.Time):u}  {x.Message}"
                                )
                            )
                        )
                    ),
                enabled: Pref.Logs.Count > 0
            ),
            Btn(
                "清空日志",
                async () =>
                {
                    if (await Confirm("清空活动日志？", "此操作会删除本机保存的活动记录。", "清空"))
                    {
                        Pref.Logs.Clear();
                        session.Save();
                    }
                },
                enabled: Pref.Logs.Count > 0
            )
        );
        p.Children.Add(row);
        Activities(p, Pref.Logs);
    }

    private void SettingsPage(StackPanel p)
    {
        Heading(p, "设置", "调整外观、动态效果和后台行为。");
        var theme = new ComboBox
        {
            ItemsSource = new[] { "跟随系统", "浅色", "黑夜" },
            SelectedIndex = Pref.Theme switch
            {
                "dark" => 2,
                "light" => 1,
                _ => 0,
            },
        };
        theme.SelectionChanged += (_, _) =>
        {
            Pref.Theme = new[] { "system", "light", "dark" }[theme.SelectedIndex];
            session.Save();
            SetLook();
            Render(true);
        };
        var material = new ComboBox
        {
            ItemsSource = new[] { "云母", "亚克力" },
            SelectedIndex = Pref.Material == "acrylic" ? 1 : 0,
        };
        material.SelectionChanged += (_, _) =>
        {
            Pref.Material = material.SelectedIndex == 1 ? "acrylic" : "mica";
            session.Save();
            SetLook();
        };
        var motion = new ToggleSwitch
        {
            IsOn = Pref.ReduceMotion,
            OnContent = "开",
            OffContent = "关",
        };
        motion.Toggled += (_, _) =>
        {
            Pref.ReduceMotion = motion.IsOn;
            session.Save();
            Render(true);
        };
        var background = new ToggleSwitch
        {
            IsOn = Pref.ToTray,
            OnContent = "开",
            OffContent = "关",
        };
        background.Toggled += (_, _) =>
        {
            Pref.ToTray = background.IsOn;
            session.Save();
        };
        var panel = Column(18);
        Add(
            panel,
            Split(Text("主题", 16, true), theme),
            Split(Text("窗口材质", 16, true), material),
            Split(Text("减弱动效", 16, true), motion),
            Split(Text("关闭窗口后驻留托盘", 16, true), background)
        );
        p.Children.Add(Card(panel));
        var help = Column(10);
        Add(
            help,
            Text("使用帮助与许可", 18, true),
            Muted("USB Bridge Desk 1.1.0 · WinUI 3。按 Ctrl+Shift+S 保存当前窗口。")
        );
        var row = Row();
        Add(
            row,
            Btn("使用说明", () => OpenFile("README.md")),
            Btn("第三方许可", () => OpenFile("notices")),
            Btn("诊断目录", () => Done(() => Open(Program.Folder)))
        );
        Add(help, row);
        p.Children.Add(Card(help));
    }

    private static void Open(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    private static Task OpenFile(string name) =>
        Done(() =>
        {
            var file = Path.Combine(AppContext.BaseDirectory, name);
            if (Directory.Exists(file))
            {
                Open(file);
                return;
            }
            var start = new ProcessStartInfo(
                Path.Combine(Environment.SystemDirectory, "notepad.exe")
            )
            {
                UseShellExecute = false,
            };
            start.ArgumentList.Add(file);
            Process.Start(start);
        });

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left,
            Top,
            Right,
            Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint w, int attr, out Rect rect, int count);

    private async Task Screenshot()
    {
        if (DwmGetWindowAttribute(Hwnd, 9, out var r, 16) != 0)
            throw new IOException("无法读取当前窗口边界。");
        using var image = new System.Drawing.Bitmap(r.Right - r.Left, r.Bottom - r.Top);
        await Task.Yield();
        using (var g = System.Drawing.Graphics.FromImage(image))
            g.CopyFromScreen(r.Left, r.Top, 0, 0, image.Size);
        using var save = new Forms.SaveFileDialog
        {
            Filter = "PNG 图像|*.png",
            FileName = $"USB-Bridge-{DateTime.Now:yyyyMMdd-HHmmss}.png",
            AddExtension = true,
        };
        if (save.ShowDialog(new WindowOwner(Hwnd)) == Forms.DialogResult.OK)
            image.Save(save.FileName, ImageFormat.Png);
    }

    private sealed class WindowOwner(nint hwnd) : Forms.IWin32Window
    {
        public nint Handle => hwnd;
    }
}
