// SPDX-License-Identifier: GPL-3.0-only
namespace UsbBridge;

public interface IUsbClient
{
    Task<List<Imported>> Ports();
    Task<List<Device>> Discover(Target target);
    Task<int> Attach(Target target, string bus);
    Task StopAttach(Target target, string bus);
    Task Detach(int port);
}

// Callers serialize commands and polling. Intent is committed before changing the driver.
public sealed class Session(IUsbClient engine, Settings data, Action<Settings> save, Func<long> now)
{
    public Settings Data => data;
    public string DriverError { get; private set; } = "正在检测";

    public void Save() => save(data);

    public void Log(string text, string level = "info")
    {
        data.Logs.Insert(0, new(now(), text, level));
        if (data.Logs.Count > 250)
            data.Logs.RemoveRange(250, data.Logs.Count - 250);
        Save();
    }

    public void SetTarget(Target t)
    {
        var existing = data.Targets.FindIndex(x => x.Id == t.Id);
        if (data.Targets.Any(x => x.Id != t.Id && x.Host == t.Host && x.Port == t.Port))
            throw new InvalidOperationException("此目标地址与端口已存在。");
        if (existing < 0 && data.Targets.Count >= 24)
            throw new InvalidOperationException("最多保存 24 个目标。");
        if (existing >= 0 && data.Mappings.Any(m => m.TargetId == t.Id))
        {
            var old = data.Targets[existing];
            if ((old.Host, old.Port, old.Mode) != (t.Host, t.Port, t.Mode))
                throw new InvalidOperationException("请先断开此目标的映射，再修改传输方式或地址。");
        }
        if (existing < 0)
            data.Targets.Add(t);
        else
            data.Targets[existing] = t;
        Log($"已保存目标 · {t.Name} · {t.Address}");
    }

    public void RemoveTarget(Target t)
    {
        if (data.Mappings.Any(m => m.TargetId == t.Id))
            throw new InvalidOperationException("请先断开此目标的映射。");
        data.Targets.Remove(t);
        Log($"已移除目标 · {t.Name}");
    }

    private Target Owner(Mapping m) => data.Targets.Single(t => t.Id == m.TargetId);

    private static Imported? Find(IEnumerable<Imported> ports, Target t, Mapping m) =>
        ports.FirstOrDefault(p => p.Host == t.Host && p.Port == t.Port && m.Matches(p.Device));

    private async Task<List<Imported>> ReadPorts()
    {
        try
        {
            var ports = await engine.Ports();
            DriverError = "";
            return ports;
        }
        catch (Exception e)
        {
            DriverError = e.Message;
            throw;
        }
    }

    public async Task Connect(Target target, IReadOnlyList<Device> picked)
    {
        if (picked.Count == 0)
            throw new InvalidOperationException("请先选择设备。");
        var pending = picked
            .DistinctBy(d => d.Bus)
            .Where(d => !data.Mappings.Any(m => m.TargetId == target.Id && m.Bus == d.Bus))
            .ToArray();
        if (pending.Length + data.Mappings.Count > 60)
            throw new InvalidOperationException("最多保留 60 个映射。");
        await ReadPorts();
        foreach (var d in pending)
        {
            var m = new Mapping
            {
                TargetId = target.Id,
                Name = d.Name,
                Bus = d.Bus,
                Vid = d.Vid,
                Pid = d.Pid,
            };
            data.Mappings.Add(m);
            Save();
            await TryConnect(target, m);
        }
    }

    private void Connected(Mapping m, int port)
    {
        if (m.State == "connected" && m.LocalPort == port)
            return;
        m.State = "connected";
        m.LocalPort = port;
        m.Attempts = 0;
        m.Next = 0;
        m.Error = "";
        Log($"{m.Name} · 已连接到虚拟端口 {port}", "ok");
    }

    private async Task TryConnect(Target target, Mapping m)
    {
        try
        {
            var active = Find(await ReadPorts(), target, m);
            if (active != null)
            {
                Connected(m, active.LocalPort);
                return;
            }
            await engine.StopAttach(target, m.Bus);
            var offered = await engine.Discover(target);
            if (!offered.Any(m.Matches))
                throw new InvalidOperationException("设备已占用、已移除或 VID/PID 已变更。");
            Connected(m, await engine.Attach(target, m.Bus));
        }
        catch (Exception e)
        {
            m.State = "retry";
            m.Error = e.Message;
            m.LocalPort = null;
            m.Attempts = Math.Min(20, m.Attempts + 1);
            m.Next = now() + Math.Min(30000, 2000L << Math.Min(4, m.Attempts - 1));
            if (m.Attempts is 1 or 4)
                Log($"{m.Name} · 等待重连 · {e.Message}", "warn");
            else
                Save();
        }
    }

    public async Task Release(Mapping m)
    {
        m.State = "stopping";
        m.LocalPort = null;
        m.Next = 0;
        Save();
        await FinishRelease(m);
    }

    private async Task FinishRelease(Mapping m)
    {
        var t = Owner(m);
        try
        {
            await engine.StopAttach(t, m.Bus);
            var active = Find(await ReadPorts(), t, m);
            if (active != null)
                await engine.Detach(active.LocalPort);
            await engine.StopAttach(t, m.Bus);
            data.Mappings.Remove(m);
            Log($"{m.Name} · 已停止重连并释放映射");
        }
        catch (Exception e)
        {
            m.Next = now() + 10000;
            m.Error = e.Message;
            Save();
        }
    }

    public async Task Poll()
    {
        List<Imported> ports;
        try
        {
            ports = await ReadPorts();
        }
        catch
        {
            foreach (var m in data.Mappings.Where(m => m.State == "connected"))
            {
                m.State = "unknown";
                m.LocalPort = null;
                m.Error = DriverError;
            }
            Save();
            return;
        }
        foreach (var m in data.Mappings.ToArray())
        {
            if (m.State == "stopping")
                continue;
            var active = Find(ports, Owner(m), m);
            if (active != null)
                Connected(m, active.LocalPort);
            else if (m.State == "connected")
            {
                m.State = "retry";
                m.LocalPort = null;
                m.Next = 0;
                Log($"{m.Name} · 虚拟设备已掉线", "warn");
            }
        }
        var due = data
            .Mappings.Where(m => m.State != "connected" && m.Next <= now())
            .OrderBy(m => m.Next)
            .FirstOrDefault();
        if (due != null)
        {
            if (due.State == "stopping")
                await FinishRelease(due);
            else
                await TryConnect(Owner(due), due);
        }
        Save();
    }
}
