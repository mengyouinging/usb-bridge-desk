// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json;
using UsbBridge;

internal static class Program
{
    private static int passed,
        failed;

    private static void Is(bool condition)
    {
        if (!condition)
            throw new Exception("assertion failed");
    }

    private static void Throws(Action op)
    {
        try
        {
            op();
        }
        catch
        {
            return;
        }
        throw new Exception("expected exception");
    }

    private static async Task Test(string name, Func<Task> fn)
    {
        try
        {
            await fn();
            Console.WriteLine("PASS " + name);
            passed++;
        }
        catch (Exception e)
        {
            Console.WriteLine($"FAIL {name}: {e}");
            failed++;
        }
    }

    private static Task Check(Action a)
    {
        a();
        return Task.CompletedTask;
    }

    private static readonly Target uu = Transports.Make("a", "工作室", "uu", "ignored", 13240);
    private static readonly Target lan = Transports.Make("b", "实验室", "direct", "lab.lan", 3240);
    private static readonly Device disk = new("1-3", "Disk", "0951", "1666"),
        scan = new("2-4", "Scanner", "0c2e", "0b61");

    private sealed class Fake : IUsbClient
    {
        public bool Ready = true,
            Online = true;
        public List<Device> Offered = [disk, scan];
        public List<Imported> Active = [];
        public int Attachments,
            Detached;
        public Action? BeforeAttach;

        public Task<List<Device>> Discover(Target t) =>
            Online ? Task.FromResult(Offered) : throw new IOException("offline");

        public Task<List<Imported>> Ports() =>
            Ready
                ? Task.FromResult(Active.ToList())
                : throw new IOException("controller unavailable");

        public Task<int> Attach(Target t, string b)
        {
            BeforeAttach?.Invoke();
            Attachments++;
            Active.Add(new(Offered.Single(d => d.Bus == b), t.Host, t.Port, Attachments));
            return Task.FromResult(Attachments);
        }

        public Task StopAttach(Target t, string b) =>
            Ready ? Task.CompletedTask : throw new IOException("controller unavailable");

        public Task Detach(int port)
        {
            Detached++;
            Active.RemoveAll(x => x.LocalPort == port);
            return Task.CompletedTask;
        }
    }

    private static Session Rig(
        Fake f,
        Settings? s = null,
        Action<Settings>? save = null,
        Func<long>? time = null
    )
    {
        var session = new Session(f, s ?? new(), save ?? (_ => { }), time ?? (() => 10000));
        if (session.Data.Targets.Count == 0)
        {
            session.SetTarget(uu);
            session.SetTarget(lan);
        }
        return session;
    }

    public static async Task<int> Main()
    {
        await Test("UU loopback, independent port allocation, direct IPv6 normalization", () =>
            Check(() =>
            {
                Is(uu.Host == "127.0.0.1" && Transports.NextPort([uu]) == 13241);
                Is(
                    Transports.Make("x", "a", "direct", "[2001:db8::7]", 3240).Address
                        == "[2001:db8::7]:3240"
                );
            })
        );
        await Test("transport input and port bounds", () => Check(() =>
            {
                Throws(() => Transports.Make("x", "x", "easytier", "foo.net", 3240));
                Throws(() => Transports.Make("x", "x", "direct", "-x", 3240));
                foreach (var n in new[] { 0, 1023, 65536 })
                    Throws(() => Transports.Make("x", "x", "uu", "", n));
                foreach (var n in new[] { 1024, 65535 })
                    Is(Transports.Make("x", "x", "direct", "localhost", n).Port == n);
            }));
        await Test("usbipd local/shared/attached/missing state", () => Check(() =>
            {
                var data = new[]
                {
                    new
                    {
                        BusId = "1-3",
                        PersistedGuid = "",
                        ClientIPAddress = "",
                    },
                    new
                    {
                        BusId = "2-1",
                        PersistedGuid = "x",
                        ClientIPAddress = "",
                    },
                    new
                    {
                        BusId = "3-1",
                        PersistedGuid = "y",
                        ClientIPAddress = "peer",
                    },
                    new
                    {
                        BusId = "",
                        PersistedGuid = "z",
                        ClientIPAddress = "",
                    },
                };
                Is(
                    Parsers
                        .Host("\uFEFF" + JsonSerializer.Serialize(new { Devices = data }))
                        .Select(x => x.Status)
                        .SequenceEqual(new[] { "未共享", "已共享", "使用中", "已拔出" })
                );
            }));
        await Test("export list and malformed product", () => Check(() =>
            {
                Is(
                    Parsers
                        .Exports(
                            "Exportable USB devices\r\n===\r\n  1-3 : Vendor : Disk (0951:1666)\r\n"
                        )
                        .Single() == disk
                );
                Is(Parsers.Exports("").Count == 0);
                Throws(() => Parsers.Exports(" 1-3 : bad"));
                Throws(() => Parsers.Exports("invalid"));
            }));
        await Test("imported IPv6 address and partial output rejection", () => Check(() =>
            {
                var s =
                    "Port 01: device in use at High Speed\r\n  V : Disk (0951:1666)\r\n  -> usbip://[2001:db8::7]:3240/1-3\r\n  -> remote bus/dev: 001/003\r\n";
                Is(Parsers.Ports(s).Single() == new Imported(disk, "2001:db8::7", 3240, 1));
                Throws(() => Parsers.Ports(s + "Port 02: missing data\n"));
            }));
        await Test("two targets can share a bus ID, multiple selections", async () =>
        {
            var f = new Fake();
            var s = Rig(f);
            await s.Connect(uu, [disk, scan]);
            await s.Connect(lan, [disk]);
            await s.Poll();
            Is(
                s.Data.Mappings.Count == 3
                    && s.Data.Mappings.All(m => m.State == "connected")
                    && f.Attachments == 3
            );
        });
        await Test("durable intent precedes attachment and repeat attach is idempotent", async () =>
        {
            Settings? persisted = null;
            var f = new Fake();
            var s = Rig(f, save: d =>
                persisted = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(d))
            );
            f.BeforeAttach = () =>
                Is(persisted!.Mappings.Count == 1 && persisted.Mappings[0].State == "retry");
            await s.Connect(uu, [disk, disk]);
            await s.Connect(uu, [disk]);
            Is(f.Attachments == 1);
        });
        await Test("driver unconfirmed on restart", async () =>
        {
            var dir = Directory.CreateTempSubdirectory();
            try
            {
                var store = new Store(Path.Combine(dir.FullName, "new.json"));
                var f = new Fake();
                var s = Rig(f, save: store.Write);
                await s.Connect(uu, [disk]);
                var loaded = store.Load();
                Is(loaded.Mappings[0].LocalPort == null && loaded.Mappings[0].State == "unknown");
                var restart = Rig(f, loaded);
                await restart.Poll();
                Is(loaded.Mappings[0].State == "connected" && f.Attachments == 1);
            }
            finally
            {
                dir.Delete(true);
            }
        });
        await Test("VID/PID change blocks reconnect; same identity recovers", async () =>
        {
            long now = 10000;
            var f = new Fake();
            var s = Rig(f, time: () => now);
            await s.Connect(uu, [disk]);
            f.Active.Clear();
            f.Offered = [disk with { Pid = "ffff" }];
            await s.Poll();
            Is(s.Data.Mappings[0].State == "retry" && f.Attachments == 1);
            now += 2001;
            f.Offered = [disk];
            await s.Poll();
            Is(s.Data.Mappings[0].LocalPort == 2);
        });
        await Test("unreachable transport backs off", async () =>
        {
            long now = 10000;
            var f = new Fake { Online = false };
            var s = Rig(f, time: () => now);
            await s.Connect(uu, [disk]);
            var m = s.Data.Mappings[0];
            Is(m.Next == 12000);
            await s.Poll();
            Is(m.Attempts == 1);
            now = 12000;
            await s.Poll();
            Is(m.Next == 16000);
        });
        await Test("cancel survives restart while controller unavailable", async () =>
        {
            var f = new Fake();
            var s = Rig(f);
            await s.Connect(uu, [disk]);
            f.Ready = false;
            var m = s.Data.Mappings[0];
            await s.Release(m);
            Is(m.State == "stopping");
            var resumed = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s.Data))!;
            f.Ready = true;
            var r = Rig(f, resumed, time: () => 22000);
            await r.Poll();
            Is(f.Attachments == 1 && f.Detached == 1 && resumed.Mappings.Count == 0);
        });
        await Test("recycled virtual port preserved", async () =>
        {
            var f = new Fake();
            var s = Rig(f);
            await s.Connect(uu, [disk]);
            f.Active[0] = new(scan, lan.Host, lan.Port, 1);
            await s.Release(s.Data.Mappings[0]);
            Is(f.Detached == 0 && f.Active.Count == 1 && s.Data.Mappings.Count == 0);
        });
        await Test("duplicate endpoint, active target edit/delete guarded", async () =>
        {
            var s = Rig(new());
            Throws(() => s.SetTarget(uu with { Id = "other" }));
            await s.Connect(uu, [disk]);
            Throws(() => s.SetTarget(uu with { Port = 13241 }));
            Throws(() => s.RemoveTarget(uu));
            s.SetTarget(uu with { Name = "新名称" });
            Is(s.Data.Targets.Count == 2);
        });
        await Test("1.0.1 JSON migration and corrupt original preservation", () => Check(() =>
            {
                var dir = Directory.CreateTempSubdirectory();
                try
                {
                    var old = Path.Combine(dir.FullName, "profiles.json");
                    File.WriteAllText(
                        old,
                        "{\"targets\":[],\"mappings\":[],\"logs\":[],\"theme\":\"dark\"}"
                    );
                    var store = new Store(Path.Combine(dir.FullName, "R3", "profiles.json"));
                    var settings = store.Load(old);
                    Is(settings.Theme == "dark" && settings.ToTray);
                    File.WriteAllText(old, "broken");
                    Throws(() => store.Load(old));
                    Is(File.ReadAllText(old) == "broken");
                }
                finally
                {
                    dir.Delete(true);
                }
            }));
        Console.WriteLine($"RESULT {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
