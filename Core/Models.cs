// SPDX-License-Identifier: GPL-3.0-only
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace UsbBridge;

public sealed record Target(string Id, string Name, string Mode, string Host, int Port)
{
    [JsonIgnore]
    public string Address => Host.Contains(':') ? $"[{Host}]:{Port}" : $"{Host}:{Port}";

    public override string ToString() => $"{Name}  ·  {Address}";
}

public sealed record Transport(string Id, string Title, string Hint, Func<string, string> Resolve)
{
    public override string ToString() => Title;
}

public static class Transports
{
    public static readonly Transport[] All =
    [
        new(
            "uu",
            "UU 远程",
            "在 UU 主控端将本地 TCP 端口映射到被控端 127.0.0.1:3240，保持映射面板打开。",
            _ => "127.0.0.1"
        ),
        new("easytier", "EasyTier", "两端加入同一虚拟网络，填写共享电脑的虚拟 IP。", s =>
            IPAddress.TryParse(s, out var ip)
                ? ip.ToString()
                : throw new ArgumentException("请输入 EasyTier 虚拟 IP。")
        ),
        new("direct", "TCP 直连", "填写可信网络内共享电脑的 IP 或主机名。", s => s),
    ];

    public static Target Make(string id, string name, string mode, string host, int port)
    {
        var transport = All.SingleOrDefault(t => t.Id == mode)
            ?? throw new ArgumentException("传输方式无效。");
        name = name.Trim();
        if (name.Length is 0 or > 48)
            throw new ArgumentException("目标名称应为 1–48 个字符。");
        host = transport.Resolve(host.Trim().Trim('[', ']').ToLowerInvariant());
        if (
            host.Length > 253
            || (
                !IPAddress.TryParse(host, out _)
                && !Regex.IsMatch(
                    host,
                    @"^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)*[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.?$"
                )
            )
        )
            throw new ArgumentException("请输入有效的 IP 地址或主机名。");
        if (port is < 1024 or > 65535)
            throw new ArgumentException("端口范围为 1024–65535。");
        return new(id, name, mode, host, port);
    }

    public static int NextPort(IEnumerable<Target> targets) =>
        Enumerable
            .Range(13240, 65536 - 13240)
            .First(n => !targets.Any(t => t.Host == "127.0.0.1" && t.Port == n));
}

public record Device(string Bus, string Name, string Vid, string Pid);

public sealed record Local(
    Device Device,
    string Instance,
    string? Guid,
    string Status,
    string? Peer
);

public sealed record Imported(Device Device, string Host, int Port, int LocalPort);

public sealed record Server(bool Running, IReadOnlyList<Local> Devices);

public sealed record Entry(long Time, string Message, string Level);

public sealed class Mapping
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TargetId { get; set; } = "";
    public string Bus { get; set; } = "";
    public string Name { get; set; } = "";
    public string Vid { get; set; } = "";
    public string Pid { get; set; } = "";
    public string State { get; set; } = "retry";
    public int? LocalPort { get; set; }
    public int Attempts { get; set; }
    public long Next { get; set; }
    public string Error { get; set; } = "";

    public bool Matches(Device d) =>
        Bus == d.Bus
        && Vid.Equals(d.Vid, StringComparison.OrdinalIgnoreCase)
        && Pid.Equals(d.Pid, StringComparison.OrdinalIgnoreCase);
}

public sealed class Settings
{
    public List<Target> Targets { get; set; } = [];
    public List<Mapping> Mappings { get; set; } = [];
    public List<Entry> Logs { get; set; } = [];
    public string Theme { get; set; } = "system";
    public bool ToTray { get; set; } = true;
    public bool ReduceMotion { get; set; }
    public string Material { get; set; } = "mica";
}

public sealed class Store(string path)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
    private string? saved;

    public Settings Load(string? legacyPath = null)
    {
        string? input = File.Exists(path) ? path : legacyPath;
        if (input is null || !File.Exists(input))
            return new();
        var data = JsonSerializer.Deserialize<Settings>(File.ReadAllText(input), Json)
            ?? throw new InvalidDataException("配置内容为空。");
        foreach (var target in data.Targets)
        {
            var valid = Transports.Make(
                target.Id,
                target.Name,
                target.Mode,
                target.Host,
                target.Port
            );
            if (target != valid || string.IsNullOrEmpty(target.Id))
                throw new InvalidDataException("配置中的目标无效。");
        }
        if (
            data.Targets.Select(t => t.Id).Distinct().Count() != data.Targets.Count
            || data.Mappings.Any(m =>
                !data.Targets.Any(t => t.Id == m.TargetId)
                || !Parsers.ValidBus(m.Bus)
                || !Regex.IsMatch(m.Vid + ":" + m.Pid, "^[a-fA-F0-9]{4}:[a-fA-F0-9]{4}$")
            )
        )
            throw new InvalidDataException("映射配置不完整，请检查配置备份。");
        foreach (var m in data.Mappings)
        {
            if (m.State != "stopping")
                m.State = "unknown";
            m.LocalPort = null;
            m.Next = 0;
        }
        return data;
    }

    public void Write(Settings data)
    {
        var contents = JsonSerializer.Serialize(data, Json);
        if (saved == contents)
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (
            var file = new FileStream(
                path + ".new",
                FileMode.Create,
                FileAccess.Write,
                FileShare.None
            )
        )
        {
            using var writer = new StreamWriter(file, leaveOpen: true);
            writer.Write(contents);
            writer.Flush();
            file.Flush(true);
        }
        File.Move(path + ".new", path, true);
        saved = contents;
    }
}
