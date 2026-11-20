// SPDX-License-Identifier: GPL-3.0-only
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace Desk;

internal sealed class Tray : IDisposable
{
    private readonly Forms.NotifyIcon icon = new();
    private readonly Forms.ContextMenuStrip menu = new();
    private readonly Forms.ToolStripMenuItem status = new() { Enabled = false };
    private readonly Dictionary<string, Icon> icons = new();
    private string previous = "";

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint hIcon);

    public Tray(Action show, Action maps, Action exit)
    {
        using var original = new Icon(Path.Combine(AppContext.BaseDirectory, "icon.ico"), 32, 32);
        foreach (
            var (key, color) in new[]
            {
                ("idle", Color.SlateGray),
                ("active", Color.MediumSpringGreen),
                ("warning", Color.Goldenrod),
            }
        )
        {
            using var image = original.ToBitmap();
            using (var g = Graphics.FromImage(image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var dot = new SolidBrush(color);
                g.FillEllipse(Brushes.Black, 17, 17, 15, 15);
                g.FillEllipse(dot, 19, 19, 11, 11);
            }
            var handle = image.GetHicon();
            try
            {
                using var wrapped = Icon.FromHandle(handle);
                icons[key] = (Icon)wrapped.Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }
        menu.Items.Add(status);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("打开 USB Bridge Desk", null, (_, _) => show());
        menu.Items.Add("查看映射", null, (_, _) => maps());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());
        icon.ContextMenuStrip = menu;
        icon.DoubleClick += (_, _) => show();
        icon.Icon = icons["idle"];
        icon.Text = "USB Bridge Desk";
        icon.Visible = true;
    }

    public void Update(string state, string label, bool hidden)
    {
        icon.Icon = icons[state];
        icon.Text = "USB Bridge Desk · " + label;
        status.Text = label;
        if (hidden && previous != label && previous.Length > 0)
            Notify(label);
        previous = label;
    }

    public void Notify(string message) =>
        icon.ShowBalloonTip(2500, "USB Bridge Desk", message, Forms.ToolTipIcon.Info);

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
        menu.Dispose();
        foreach (var i in icons.Values)
            i.Dispose();
    }
}
