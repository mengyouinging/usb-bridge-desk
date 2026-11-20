// SPDX-License-Identifier: GPL-3.0-only
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.XamlTypeInfo;
using WinRT;

namespace Desk;

internal static class Program
{
    internal static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "USB Bridge Desk"
    );
    internal static readonly string LogPath = Path.Combine(Folder, "startup.log");

    [DllImport(
        "Microsoft.WindowsAppRuntime.Bootstrap.dll",
        CharSet = CharSet.Unicode,
        ExactSpelling = true
    )]
    private static extern int MddBootstrapInitialize(uint release, string? tag, ulong minVersion);

    [DllImport("Microsoft.WindowsAppRuntime.Bootstrap.dll")]
    private static extern void MddBootstrapShutdown();

    [DllImport("Microsoft.WindowsAppRuntime.dll", ExactSpelling = true)]
    private static extern int WindowsAppRuntime_EnsureIsLoaded();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int MessageBox(nint hwnd, string message, string title, uint type);

    internal static void Trace(string message)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    internal static void Fatal(Exception e)
    {
        Trace(e.ToString());
        MessageBox(
            0,
            $"USB Bridge Desk 启动或运行遇到问题。\n{e.Message}\n\n请运行安装包修复运行时或查看日志：\n{LogPath}",
            "USB Bridge Desk",
            0x10
        );
    }

    internal static EventWaitHandle? Wake;

    [STAThread]
    private static int Main()
    {
        bool initialized = false;
        using var single = new Mutex(true, @"Local\UsbBridgeDesk-R3", out bool first);
        if (!first)
        {
            for (int i = 0; i < 20; i++)
            {
                if (EventWaitHandle.TryOpenExisting(@"Local\UsbBridgeDesk-Show", out var existing))
                {
                    using (existing)
                        existing.Set();
                    break;
                }
                Thread.Sleep(100);
            }
            return 0;
        }
        try
        {
            Wake = new(false, EventResetMode.AutoReset, @"Local\UsbBridgeDesk-Show");
            Marshal.ThrowExceptionForHR(
                MddBootstrapInitialize(
                    0x00010008,
                    null,
                    ((ulong)8000 << 48) | ((ulong)994 << 32) | ((ulong)2142 << 16)
                )
            );
            initialized = true;
            Marshal.ThrowExceptionForHR(WindowsAppRuntime_EnsureIsLoaded());
            StartUI();
            return 0;
        }
        catch (Exception e)
        {
            Fatal(e);
            return 1;
        }
        finally
        {
            Wake?.Dispose();
            if (initialized)
                MddBootstrapShutdown();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void StartUI()
    {
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread())
            );
            _app = new DeskApp();
        });
    }

    private static DeskApp? _app;
}

internal sealed partial class DeskApp : Application, IXamlMetadataProvider
{
    private readonly Lazy<XamlControlsXamlMetaDataProvider> provider = new(() => new());
    private Shell? window;

    public IXamlType GetXamlType(Type type) => provider.Value.GetXamlType(type);

    public IXamlType GetXamlType(string fullName) => provider.Value.GetXamlType(fullName);

    public XmlnsDefinition[] GetXmlnsDefinitions() => provider.Value.GetXmlnsDefinitions();

    public DeskApp()
    {
        Resources.MergedDictionaries.Add(new XamlControlsResources());
        UnhandledException += (_, e) =>
        {
            Program.Fatal(e.Exception);
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            window = new Shell();
            window.Activate();
        }
        catch (Exception e)
        {
            Program.Fatal(e);
            Exit();
        }
    }
}
