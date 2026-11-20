// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UsbBridge;

public static class Parsers
{
    public static bool ValidBus(string b) =>
        Regex.IsMatch(b, @"^\d{1,5}-\d{1,5}(?:\.\d{1,5}){0,5}$");

    public static List<Local> Host(string json)
    {
        using var doc = JsonDocument.Parse(json.TrimStart('\uFEFF'));
        var result = new List<Local>();
        foreach (var d in doc.RootElement.GetProperty("Devices").EnumerateArray())
        {
            string Get(string name) => d.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";
            string bus = Get("BusId"),
                guid = Get("PersistedGuid"),
                instance = Get("InstanceId"),
                peer = Get("ClientIPAddress");
            if (bus.Length == 0 && guid.Length == 0)
                continue;
            var ids = Regex.Match(
                instance,
                @"VID_([\da-f]{4})&PID_([\da-f]{4})",
                RegexOptions.IgnoreCase
            );
            var name = Get("Description");
            var device = new Device(
                bus,
                name.Length == 0 ? "USB 设备" : name,
                ids.Groups[1].Value.ToLowerInvariant(),
                ids.Groups[2].Value.ToLowerInvariant()
            );
            result.Add(
                new(
                    device,
                    instance,
                    guid,
                    peer.Length > 0 ? "使用中"
                        : guid.Length == 0 ? "未共享"
                        : bus.Length == 0 ? "已拔出"
                        : "已共享",
                    peer
                )
            );
        }
        return result;
    }

    private static Device Product(string bus, string raw)
    {
        var id = Regex.Match(
            raw.Trim(),
            @"\(([\da-f]{4}):([\da-f]{4})\)$",
            RegexOptions.IgnoreCase
        );
        if (!ValidBus(bus) || !id.Success)
            throw new InvalidDataException("设备缺少有效的总线或 VID/PID。");
        var name = raw.Trim()[..id.Index].Trim().Split(" : ").Last();
        return new(
            bus,
            name.Length == 0 ? "USB 设备" : name,
            id.Groups[1].Value.ToLowerInvariant(),
            id.Groups[2].Value.ToLowerInvariant()
        );
    }

    public static List<Device> Exports(string text)
    {
        var rows = new List<Device>();
        foreach (string line in text.Split('\n'))
        {
            var match = Regex.Match(line, @"^\s*(\d+-\d+(?:\.\d+)*)\s*:\s*(.+)$");
            if (match.Success)
                rows.Add(Product(match.Groups[1].Value, match.Groups[2].Value));
        }
        if (
            rows.Count == 0
            && !Regex.IsMatch(
                text.Trim(),
                @"^(?:Exportable USB devices\s*=*)?$",
                RegexOptions.IgnoreCase
            )
        )
            throw new InvalidDataException("无法识别远端设备列表。");
        return rows;
    }

    public static List<Imported> Ports(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        const string pattern =
            @"^Port\s+(\d+): device in use at [^\r\n]+\r?\n\s*([^\r\n]+)\r?\n\s*-> usbip://(.+):(\d+)/([^\s]+)\s*$";
        var rows = new List<Imported>();
        foreach (Match m in Regex.Matches(text, pattern, RegexOptions.Multiline))
        {
            int local = int.Parse(m.Groups[1].Value);
            if (local is < 1 or > 255)
                throw new InvalidDataException("虚拟端口越界。");
            rows.Add(
                new(
                    Product(m.Groups[5].Value, m.Groups[2].Value),
                    m.Groups[3].Value.Trim('[', ']').ToLowerInvariant(),
                    int.Parse(m.Groups[4].Value),
                    local
                )
            );
        }
        if (
            rows.Count == 0
            || rows.Count != Regex.Matches(text, @"^Port\s+\d+:", RegexOptions.Multiline).Count
        )
            throw new InvalidDataException("无法识别虚拟控制器端口列表。");
        return rows;
    }
}
