// SPDX-License-Identifier: GPL-3.0-only
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using UsbBridge;

namespace Desk;

internal sealed class Engines : IUsbClient
{
    private static readonly string Sys = Environment.SystemDirectory;
    private static readonly string Host = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "host",
        "usbipd.exe"
    );
    private static readonly string Client = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "USB Bridge Desk Engines",
        "Client",
        "usbip.exe"
    );
    private static readonly string Sc = Path.Combine(Sys, "sc.exe");
    private static readonly string Ps = Path.Combine(
        Sys,
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe"
    );

    private static async Task<string> Run(string exe, int timeout, params string[] args)
    {
        if (!File.Exists(exe))
            throw new FileNotFoundException($"缺少 {Path.GetFileName(exe)}，请运行原安装包修复。");
        var start = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        using var p = Process.Start(start) ?? throw new InvalidOperationException("组件启动失败。");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(timeout);
        try
        {
            await p.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            if (!p.HasExited)
                p.Kill(entireProcessTree: true);
            await p.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
            throw new TimeoutException("设备或目标响应超时。");
        }
        var err = (await stderr).Trim();
        var output = await stdout;
        if (p.ExitCode != 0)
        {
            var detail = err.Length > 0 ? err : output.Trim();
            throw new IOException(
                $"{Path.GetFileName(exe)} ({p.ExitCode}) · {detail[..Math.Min(detail.Length, 650)]}"
            );
        }
        return output;
    }

    private static string Q(string s) => "'" + s.Replace("'", "''") + "'";

    private static async Task Admin(string script)
    {
        var encoded = Convert.ToBase64String(
            Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop'; " + script)
        );
        var start = new ProcessStartInfo(Ps)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        start.Arguments = "-NoProfile -NonInteractive -EncodedCommand " + encoded;
        try
        {
            using var p = Process.Start(start)
                ?? throw new InvalidOperationException("管理员操作未启动。");
            await p.WaitForExitAsync();
            if (p.ExitCode != 0)
                throw new InvalidOperationException(
                    $"操作未全部完成（{p.ExitCode}）。请刷新并检查设备状态。"
                );
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException("已取消管理员授权。");
        }
    }

    public async Task<Server> ReadHost()
    {
        var query = await Run(Sc, 6000, "query", "usbipd");
        return new(
            Regex.IsMatch(query, @"^\s*[^\r\n:]+:\s*4\s", RegexOptions.Multiline),
            Parsers.Host(await Run(Host, 12000, "state"))
        );
    }

    public async Task Service(bool start)
    {
        await Admin(
            $"& {Q(Sc)} {(start ? "start" : "stop")} usbipd; if ($LASTEXITCODE -in @(0,{(start ? 1056 : 1062)})) {{exit 0}}; exit 1"
        );
        for (int i = 0; i < 6; i++)
        {
            if ((await ReadHost()).Running == start)
                return;
            await Task.Delay(500);
        }
        throw new IOException("服务尚未完成切换，请刷新检查。");
    }

    public async Task Share(IEnumerable<Local> selection, bool share)
    {
        var rows = selection.ToList();
        if (rows.Count is 0 or > 6)
            throw new ArgumentException("每批请选择 1–6 个设备。");
        var cmd = new StringBuilder("$bad=$false; ");
        foreach (var row in rows)
        {
            if (!share && Guid.TryParse(row.Guid, out var id))
            {
                cmd.Append(
                    $"& {Q(Host)} unbind --guid {Q(id.ToString())}; if ($LASTEXITCODE) {{$bad=$true}}; "
                );
                continue;
            }
            if (!Parsers.ValidBus(row.Device.Bus))
                throw new ArgumentException("设备的总线 ID 无效。");
            // Check physical identity after elevation to avoid acting on a reused bus.
            var verb = share ? "bind" : "unbind";
            cmd.Append(
                $$"""
                $d=(& {{Q(
                    Host
                )}} state | Out-String | ConvertFrom-Json).Devices | Where-Object { $_.BusId -eq {{Q(
                    row.Device.Bus
                )}} -and $_.InstanceId -eq {{Q(row.Instance)}} };
                if ($d) { & {{Q(Host)}} {{verb}} --busid {{Q(
                    row.Device.Bus
                )}}; if($LASTEXITCODE){ $bad=$true } } else { $bad=$true };
                """
            );
        }
        await Admin(cmd + "if($bad){exit 2}; exit 0");
    }

    public static IEnumerable<(string Name, string Address)> Addresses() =>
        NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(n =>
                n.OperationalStatus == OperationalStatus.Up
                && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
            )
            .SelectMany(n =>
                n.GetIPProperties()
                    .UnicastAddresses.Where(a =>
                        a.Address.AddressFamily == AddressFamily.InterNetwork
                        || (
                            a.Address.AddressFamily == AddressFamily.InterNetworkV6
                            && !a.Address.IsIPv6LinkLocal
                        )
                    )
                    .Select(a =>
                        (
                            n.Name,
                            a.Address.AddressFamily == AddressFamily.InterNetwork
                                ? $"{a.Address}:3240"
                                : $"[{a.Address}]:3240"
                        )
                    )
            );

    public async Task<List<Device>> Discover(Target t) =>
        Parsers.Exports(
            await Run(Client, 8500, "--tcp-port", t.Port.ToString(), "list", "-r", t.Host)
        );

    public async Task<List<Imported>> Ports() => Parsers.Ports(await Run(Client, 7000, "port"));

    public async Task<int> Attach(Target t, string bus)
    {
        var s = await Run(
            Client,
            15000,
            "--tcp-port",
            t.Port.ToString(),
            "attach",
            "-r",
            t.Host,
            "-b",
            bus,
            "--terse",
            "--once"
        );
        return int.TryParse(s.Trim(), out var p) && p is >= 1 and <= 255
            ? p
            : throw new IOException("驱动未确认虚拟端口。");
    }

    public async Task StopAttach(Target t, string bus) =>
        await Run(
            Client,
            7000,
            "--tcp-port",
            t.Port.ToString(),
            "attach",
            "-r",
            t.Host,
            "-b",
            bus,
            "--stop"
        );

    public async Task Detach(int port) => await Run(Client, 10000, "detach", "-p", port.ToString());
}
