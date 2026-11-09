using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    public delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint thread, uint time);
    public delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)] public struct MSLLHOOKSTRUCT { public int x, y; public uint mouseData, flags, time; public IntPtr extra; }
    [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc proc, IntPtr module, uint threadId);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc proc, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out RECT rect, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern uint GetPixel(IntPtr hdc, int x, int y);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);

    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; public SIZE(int x, int y) { cx = x; cy = y; } }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, IntPtr pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

    public const uint GW_OWNER = 4;
    public const int GWLP_HWNDPARENT = -8;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint WINEVENT_OUTOFCONTEXT = 0;
    public const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A;
    public const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
    public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    public const uint EVENT_OBJECT_DESTROY = 0x8001;
    public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    public static readonly IntPtr HWND_TOP = IntPtr.Zero;

    public static RECT FrameRect(IntPtr hWnd)
    {
        RECT rect;
        if (DwmGetWindowAttribute(hWnd, 9, out rect, Marshal.SizeOf(typeof(RECT))) != 0) GetWindowRect(hWnd, out rect);
        return rect;
    }
}

internal sealed class Card
{
    public string Summary = "";
    public string Headline = "";
    public List<string[]> Rows = new List<string[]>();
}

internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AntigravitySessionStatsBar";
    private const string SettingsKey = @"Software\AntigravitySessionStats";
    private const string ChoiceName = "AutostartChoice";

    private static string Command()
    {
        return "\"" + Application.ExecutablePath + "\"";
    }

    public static bool IsEnabled()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
        {
            var value = key == null ? null : key.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(value);
        }
    }

    public static void Set(bool enable)
    {
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enable) key.SetValue(ValueName, Command());
                else if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName, false);
            }
        }
        catch { }
    }

    public static void SaveChoice(bool enable)
    {
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                key.SetValue(ChoiceName, enable ? 1 : 0, RegistryValueKind.DWord);
        }
        catch { }
    }

    private static int? LoadChoice()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                var value = key == null ? null : key.GetValue(ChoiceName);
                return value is int ? (int?)(int)value : null;
            }
        }
        catch { return null; }
    }

    public static void EnsureDefault()
    {
        int? choice = LoadChoice();
        if (choice == 0) return;
        if (choice == null) SaveChoice(true);
        Set(true);
    }
}

internal static class Theme
{
    public static readonly Color BarBack = Color.FromArgb(246, 246, 248);
    public static readonly Color Text = Color.FromArgb(32, 33, 36);
    public static readonly Color Muted = Color.FromArgb(118, 120, 128);
    public static readonly Color Hover = Color.FromArgb(232, 232, 236);
    public static readonly Color Border = Color.FromArgb(228, 228, 232);
    public static readonly Color Live = Color.FromArgb(34, 164, 93);

    public static Color BarText(bool dark) { return dark ? Color.FromArgb(236, 236, 240) : Text; }
    public static Color BarMuted(bool dark) { return dark ? Color.FromArgb(162, 164, 172) : Muted; }
    public static Color BarHover(bool dark) { return dark ? Color.FromArgb(34, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0); }
    public static Color Accent(bool dark) { return dark ? Color.FromArgb(155, 140, 255) : Color.FromArgb(109, 93, 252); }

    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void Gauge(Graphics g, RectangleF r, Color color, float width)
    {
        using (var pen = new Pen(color, width))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            g.DrawArc(pen, r, 135, 270);
            var c = new PointF(r.X + r.Width / 2, r.Y + r.Height / 2);
            float len = r.Width * 0.32f;
            g.DrawLine(pen, c, new PointF(c.X + len * 0.7071f, c.Y - len * 0.7071f));
        }
    }

    public static void Database(Graphics g, RectangleF r, Color color, float width)
    {
        using (var pen = new Pen(color, width))
        {
            float eh = r.Height * 0.32f;
            g.DrawEllipse(pen, r.X, r.Y, r.Width, eh);
            g.DrawLine(pen, r.X, r.Y + eh / 2, r.X, r.Bottom - eh / 2);
            g.DrawLine(pen, r.Right, r.Y + eh / 2, r.Right, r.Bottom - eh / 2);
            g.DrawArc(pen, r.X, r.Y + (r.Height - eh) / 2, r.Width, eh, 0, 180);
            g.DrawArc(pen, r.X, r.Bottom - eh, r.Width, eh, 0, 180);
        }
    }
}

internal sealed class DetailPopup : Form
{
    private readonly float scale;
    private readonly Font headFont = new Font("Microsoft YaHei UI", 10.5f);
    private readonly Font boldFont = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
    private readonly Font rowFont = new Font("Microsoft YaHei UI", 10f);
    private Card card;
    private bool tokenIcon;
    private string title = "";

    public DetailPopup(float scale)
    {
        this.scale = scale;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;
        DoubleBuffered = true;
        Width = S(300);
        Height = S(190);
    }

    private int S(float value) { return (int)Math.Round(value * scale); }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080;
            cp.ClassStyle |= 0x00020000;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int round = 2;
        Native.DwmSetWindowAttribute(Handle, 33, ref round, 4);
    }

    public void SetCard(string newTitle, Card newCard, bool useTokenIcon)
    {
        title = newTitle;
        card = newCard;
        tokenIcon = useTokenIcon;
        Height = S(54 + 30 * Math.Max(1, card.Rows.Count) + 10);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (card == null) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var left = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        using (var border = new Pen(Theme.Border))
        using (var text = new SolidBrush(Theme.Text))
        using (var muted = new SolidBrush(Theme.Muted))
        {
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            var icon = new RectangleF(S(17), S(18), S(14), S(14));
            if (tokenIcon) Theme.Database(g, icon, Theme.Text, 1.3f * scale);
            else Theme.Gauge(g, icon, Theme.Text, 1.3f * scale);
            g.DrawString(title, headFont, text, new RectangleF(S(40), S(8), S(150), S(34)), left);
            g.DrawString(card.Headline, boldFont, text, new RectangleF(S(110), S(8), Width - S(126), S(34)), right);
            g.DrawLine(border, S(16), S(46), Width - S(16), S(46));
            float y = S(52);
            foreach (var row in card.Rows)
            {
                g.DrawString(row[0], rowFont, muted, new RectangleF(S(16), y, S(180), S(30)), left);
                g.DrawString(row[1], rowFont, muted, new RectangleF(S(130), y, Width - S(146), S(30)), right);
                y += S(30);
            }
        }
    }
}

internal sealed class StatsBar : Form
{
    private static readonly string[] HostProcessNames = { "Antigravity" };
    private const double ChatAreaLeftRatio = 0.2665;
    private readonly float scale;
    private readonly int barHeight;
    private readonly System.Windows.Forms.Timer followTimer;
    private readonly string feedScript;
    private readonly DetailPopup popup;
    private readonly Font font = new Font("Microsoft YaHei UI", 9f);
    private Process feed;
    private volatile IntPtr host = IntPtr.Zero;
    private DateTime lastHostScan = DateTime.MinValue;
    private Card session = new Card { Summary = "会话统计读取中…" };
    private Card tokens = new Card();
    private bool running;
    private bool darkBackground;
    private int themeTick;
    private RectangleF sessionRect, tokenRect;
    private int hover = -1;
    private int opened = -1;
    private readonly Native.LowLevelMouseProc mouseProc;
    private IntPtr mouseHook = IntPtr.Zero;
    private readonly Native.WinEventProc winEventProc;
    private IntPtr systemHook = IntPtr.Zero;
    private IntPtr objectHook = IntPtr.Zero;
    private volatile bool moving;
    private volatile bool closing;
    private readonly AutoResetEvent probeSignal = new AutoResetEvent(true);
    private double composerOffset = double.NaN;
    private int composerBaseWidth;
    private int lastWidth, lastHeight;
    private AutomationElement composer;
    private string lastTitle = "";
    private DateTime lastFullSearch = DateTime.MinValue;
    private long lastResizeTicks;

    public StatsBar(string feedScript, float scale)
    {
        this.feedScript = feedScript;
        this.scale = scale;
        barHeight = S(19);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.BarBack;
        DoubleBuffered = true;
        Height = barHeight;
        Width = S(320);
        Location = new Point(-32000, -32000);
        popup = new DetailPopup(scale);

        var menu = new ContextMenuStrip();
        var autostartItem = new ToolStripMenuItem("开机自启") { CheckOnClick = false };
        autostartItem.Click += (s, e) =>
        {
            bool enable = !Autostart.IsEnabled();
            Autostart.Set(enable);
            Autostart.SaveChoice(enable);
        };
        menu.Opening += (s, e) => autostartItem.Checked = Autostart.IsEnabled();
        menu.Items.Add(autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出 Antigravity 会话统计条", null, (s, e) => Close());
        ContextMenuStrip = menu;

        winEventProc = OnWinEvent;
        mouseProc = OnGlobalMouse;
        followTimer = new System.Windows.Forms.Timer { Interval = 250 };
        followTimer.Tick += (s, e) => Follow();
        followTimer.Start();
        var prober = new Thread(ProbeLoop);
        prober.IsBackground = true;
        prober.Start();
        StartFeed();
    }

    private int S(float value) { return (int)Math.Round(value * scale); }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00080000;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Render();
    }

    protected override void OnInvalidated(InvalidateEventArgs e)
    {
        Render();
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    private void Render()
    {
        if (!IsHandleCreated) return;
        int w = Math.Max(1, Width), h = Math.Max(1, barHeight);
        using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(1, 0, 0, 0));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                DrawGroup(g, sessionRect, session.Summary, false, hover == 0 || opened == 0);
                DrawGroup(g, tokenRect, tokens.Summary, true, hover == 1 || opened == 1);
            }
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr old = Native.SelectObject(memDc, hBitmap);
            try
            {
                var size = new Native.SIZE(w, h);
                var source = new Native.POINT();
                var blend = new Native.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                Native.UpdateLayeredWindow(Handle, screenDc, IntPtr.Zero, ref size, memDc, ref source, 0, ref blend, 2);
            }
            finally
            {
                Native.SelectObject(memDc, old);
                Native.DeleteObject(hBitmap);
                Native.DeleteDC(memDc);
                Native.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }

    private static double Luminance(uint pixel)
    {
        double r = pixel & 0xFF, g = (pixel >> 8) & 0xFF, b = (pixel >> 16) & 0xFF;
        return 0.299 * r + 0.587 * g + 0.114 * b;
    }

    private void DetectBackground()
    {
        if (!Visible || host == IntPtr.Zero) return;
        Native.RECT rect = Native.FrameRect(host);
        int y = Top + barHeight / 2;
        var xs = new[] { Left - S(24), Right + S(24), Left - S(60), Right + S(60) };
        IntPtr dc = Native.GetDC(IntPtr.Zero);
        double total = 0;
        int count = 0;
        try
        {
            foreach (int x in xs)
            {
                if (x <= rect.Left + 2 || x >= rect.Right - 2) continue;
                uint pixel = Native.GetPixel(dc, x, y);
                if (pixel == 0xFFFFFFFF) continue;
                total += Luminance(pixel);
                count++;
            }
        }
        finally { Native.ReleaseDC(IntPtr.Zero, dc); }
        if (count == 0) return;
        bool dark = total / count < 128;
        if (dark != darkBackground) { darkBackground = dark; Render(); }
    }

    private void StartFeed()
    {
        var info = new ProcessStartInfo("node", "\"" + feedScript + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        try { feed = Process.Start(info); }
        catch (Exception error) { session = new Card { Summary = "无法启动 node：" + error.Message }; return; }
        var reader = new Thread(() =>
        {
            var json = new JavaScriptSerializer();
            string line;
            while ((line = feed.StandardOutput.ReadLine()) != null)
            {
                Dictionary<string, object> data;
                try { data = json.Deserialize<Dictionary<string, object>>(line); } catch { continue; }
                try { BeginInvoke((Action)(() => Apply(data))); } catch { return; }
            }
        });
        reader.IsBackground = true;
        reader.Start();
    }

    private static string Str(Dictionary<string, object> map, string key)
    {
        object value;
        return map.TryGetValue(key, out value) ? value as string ?? "" : "";
    }

    private static Card ReadCard(object value)
    {
        var card = new Card();
        var map = value as Dictionary<string, object>;
        if (map == null) return card;
        card.Summary = Str(map, "summary");
        card.Headline = Str(map, "headline");
        object rows;
        if (map.TryGetValue("rows", out rows) && rows is ArrayList)
            foreach (var row in (ArrayList)rows)
            {
                var pair = row as ArrayList;
                if (pair != null && pair.Count == 2) card.Rows.Add(new[] { pair[0] as string, pair[1] as string });
            }
        return card;
    }

    private void Apply(Dictionary<string, object> data)
    {
        object ok;
        if (data.TryGetValue("ok", out ok) && ok is bool && (bool)ok)
        {
            session = ReadCard(data["session"]);
            tokens = ReadCard(data["tokens"]);
            object live;
            running = data.TryGetValue("running", out live) && live is bool && (bool)live;
        }
        else
        {
            session = new Card { Summary = Str(data, "message") };
            tokens = new Card();
            running = false;
        }
        Relayout();
        if (opened == 0) popup.SetCard("会话统计", session, false);
        if (opened == 1) popup.SetCard("Token 用量", tokens, true);
    }

    private float GroupWidth(Graphics g, string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return S(30) + g.MeasureString(text, font).Width + S(8);
    }

    private void Relayout()
    {
        using (var g = CreateGraphics())
        {
            float a = GroupWidth(g, session.Summary);
            float b = GroupWidth(g, tokens.Summary);
            float gap = b > 0 ? S(8) : 0;
            sessionRect = new RectangleF(0, 0, a, barHeight);
            tokenRect = new RectangleF(a + gap, 0, b, barHeight);
            int width = (int)Math.Ceiling(a + gap + b);
            if (width != Width) { Width = width; Reposition(); }
        }
        Invalidate();
    }

    private void DrawGroup(Graphics g, RectangleF r, string text, bool tokenIcon, bool active)
    {
        if (string.IsNullOrEmpty(text) || r.Width <= 0) return;
        if (active)
            using (var back = new SolidBrush(Theme.BarHover(darkBackground)))
            using (var path = Theme.Rounded(new RectangleF(r.X, r.Y + 1, r.Width, r.Height - 2), S(6)))
                g.FillPath(back, path);
        var icon = new RectangleF(r.X + S(10), r.Y + (r.Height - S(12)) / 2, S(12), S(12));
        Color iconColor = tokenIcon ? Theme.BarMuted(darkBackground) : running ? Theme.Live : Theme.Accent(darkBackground);
        if (tokenIcon) Theme.Database(g, icon, iconColor, 1.2f * scale);
        else Theme.Gauge(g, icon, iconColor, 1.2f * scale);
        var format = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        using (var brush = new SolidBrush(active ? Theme.BarText(darkBackground) : Theme.BarMuted(darkBackground)))
            g.DrawString(text, font, brush, new RectangleF(r.X + S(28), r.Y, r.Width - S(28), r.Height), format);
    }

    private int HitTest(Point p)
    {
        if (sessionRect.Contains(p)) return 0;
        if (tokenRect.Contains(p)) return 1;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int now = HitTest(e.Location);
        if (now == hover) return;
        hover = now;
        Cursor = now >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hover = -1;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        int target = HitTest(e.Location);
        if (target < 0 || target == opened) { ClosePopup(); return; }
        opened = target;
        if (target == 0) popup.SetCard("会话统计", session, false);
        else popup.SetCard("Token 用量", tokens, true);
        PlacePopup();
        StartOutsideClick();
        Invalidate();
    }

    private void ClosePopup()
    {
        opened = -1;
        StopOutsideClick();
        if (popup.Visible) popup.Hide();
        Invalidate();
    }

    private void StartOutsideClick()
    {
        if (mouseHook != IntPtr.Zero) return;
        mouseHook = Native.SetWindowsHookEx(14, mouseProc, Native.GetModuleHandle(null), 0);
    }

    private void StopOutsideClick()
    {
        if (mouseHook == IntPtr.Zero) return;
        Native.UnhookWindowsHookEx(mouseHook);
        mouseHook = IntPtr.Zero;
    }

    private IntPtr OnGlobalMouse(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && opened >= 0)
        {
            int message = wParam.ToInt32();
            if (message == 0x0201 || message == 0x0204 || message == 0x0207)
            {
                var info = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                var point = new Point(info.x, info.y);
                bool onBar = Visible && Bounds.Contains(point);
                bool onPopup = popup.Visible && popup.Bounds.Contains(point);
                if (!onBar && !onPopup) BeginInvoke((Action)ClosePopup);
            }
        }
        return Native.CallNextHookEx(mouseHook, code, wParam, lParam);
    }

    private void PlacePopup()
    {
        if (opened < 0) return;
        RectangleF anchor = opened == 0 ? sessionRect : tokenRect;
        int x = Left + (int)(anchor.X + anchor.Width / 2) - popup.Width / 2;
        int y = Top - popup.Height - S(8);
        var screen = Screen.FromHandle(Handle).WorkingArea;
        x = Math.Max(screen.Left + S(4), Math.Min(x, screen.Right - popup.Width - S(4)));
        if (!popup.Visible)
        {
            Native.SetWindowLongPtr(popup.Handle, Native.GWLP_HWNDPARENT, Handle);
            popup.Show();
        }
        Native.SetWindowPos(popup.Handle, Native.HWND_TOP, x, y, popup.Width, popup.Height, Native.SWP_NOACTIVATE);
    }

    private static bool IsHostProcess(IntPtr hWnd)
    {
        uint pid;
        Native.GetWindowThreadProcessId(hWnd, out pid);
        try
        {
            string name = Process.GetProcessById((int)pid).ProcessName;
            foreach (var candidate in HostProcessNames)
                if (string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase)) return true;
        }
        catch { }
        return false;
    }

    private IntPtr FindHost()
    {
        IntPtr best = IntPtr.Zero;
        long bestArea = 0;
        Native.EnumWindows((hWnd, lParam) =>
        {
            if (!Native.IsWindowVisible(hWnd) || Native.GetWindow(hWnd, Native.GW_OWNER) != IntPtr.Zero) return true;
            if (Native.GetWindowTextLength(hWnd) == 0 || !IsHostProcess(hWnd)) return true;
            Native.RECT r;
            Native.GetWindowRect(hWnd, out r);
            long area = (long)(r.Right - r.Left) * (r.Bottom - r.Top);
            if (area > bestArea) { bestArea = area; best = hWnd; }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    private void Follow()
    {
        if (++themeTick % 4 == 0) DetectBackground();
        if (host == IntPtr.Zero || !Native.IsWindowVisible(host))
        {
            HideAll();
            if ((DateTime.Now - lastHostScan).TotalSeconds < 2) return;
            lastHostScan = DateTime.Now;
            IntPtr found = FindHost();
            if (found == IntPtr.Zero) return;
            Attach(found);
        }
        Reposition();
    }

    private void Attach(IntPtr found)
    {
        Unhook();
        host = found;
        composer = null;
        lastTitle = "";
        composerOffset = double.NaN;
        Native.SetWindowLongPtr(Handle, Native.GWLP_HWNDPARENT, found);
        uint pid;
        Native.GetWindowThreadProcessId(found, out pid);
        systemHook = Native.SetWinEventHook(Native.EVENT_SYSTEM_MOVESIZESTART, Native.EVENT_SYSTEM_MINIMIZEEND, IntPtr.Zero, winEventProc, pid, 0, Native.WINEVENT_OUTOFCONTEXT);
        objectHook = Native.SetWinEventHook(Native.EVENT_OBJECT_DESTROY, Native.EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, winEventProc, pid, 0, Native.WINEVENT_OUTOFCONTEXT);
        lastFullSearch = DateTime.MinValue;
        probeSignal.Set();
    }

    private void Unhook()
    {
        if (systemHook != IntPtr.Zero) { Native.UnhookWinEvent(systemHook); systemHook = IntPtr.Zero; }
        if (objectHook != IntPtr.Zero) { Native.UnhookWinEvent(objectHook); objectHook = IntPtr.Zero; }
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hWnd != host || idObject != 0 || idChild != 0) return;
        if (eventType == Native.EVENT_OBJECT_DESTROY) { host = IntPtr.Zero; Unhook(); HideAll(); return; }
        if (eventType == Native.EVENT_SYSTEM_MOVESIZESTART) moving = true;
        else if (eventType == Native.EVENT_SYSTEM_MOVESIZEEND) { moving = false; probeSignal.Set(); }
        Reposition();
    }

    private void Reposition()
    {
        IntPtr target = host;
        if (target == IntPtr.Zero) return;
        if (!Native.IsWindowVisible(target) || Native.IsIconic(target)) { HideAll(); return; }
        Native.RECT rect = Native.FrameRect(target);
        int hostWidth = rect.Right - rect.Left;
        int hostHeight = rect.Bottom - rect.Top;
        if (hostWidth != lastWidth || hostHeight != lastHeight)
        {
            lastWidth = hostWidth;
            lastHeight = hostHeight;
            Interlocked.Exchange(ref lastResizeTicks, DateTime.UtcNow.Ticks);
            probeSignal.Set();
        }
        int center;
        if (!double.IsNaN(composerOffset) && hostWidth == composerBaseWidth)
            center = rect.Left + (int)Math.Round(composerOffset);
        else
            center = hostWidth < S(900) ? (rect.Left + rect.Right) / 2 : (rect.Left + (int)(hostWidth * ChatAreaLeftRatio) + rect.Right) / 2;
        center = Math.Max(rect.Left + Width / 2, Math.Min(center, rect.Right - Width / 2));
        int x = center - Width / 2;
        int y = rect.Bottom - barHeight - S(1);
        if (Visible && x == Left && y == Top) return;
        if (!Visible) Show();
        Native.SetWindowPos(Handle, IntPtr.Zero, x, y, Width, barHeight, Native.SWP_NOACTIVATE | Native.SWP_NOZORDER);
        PlacePopup();
    }

    private void ProbeLoop()
    {
        while (!closing)
        {
            bool signaled = probeSignal.WaitOne(700);
            if (closing) return;
            IntPtr target = host;
            if (target == IntPtr.Zero) continue;
            ReadActiveTitle(target);
            if (!signaled)
            {
                if (!moving && !Native.IsIconic(target)) TryProbe(target);
                continue;
            }
            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while (!closing && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(100);
                if (moving || Native.IsIconic(target)) continue;
                long sinceResize = DateTime.UtcNow.Ticks - Interlocked.Read(ref lastResizeTicks);
                if (sinceResize < TimeSpan.FromMilliseconds(250).Ticks) continue;
                if (TryProbe(target)) break;
            }
        }
    }

    private void ReadActiveTitle(IntPtr target)
    {
        try
        {
            var root = AutomationElement.FromHandle(target);
            string contextTitle = "";
            var document = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
            if (document != null) contextTitle = (document.Current.Name ?? "").Trim();
            string title = FindActiveConversationTitle(root, contextTitle);
            if (string.IsNullOrEmpty(title)) title = contextTitle;
            title = (title ?? "").Trim();
            if (title == "Antigravity") title = "";
            if (title == lastTitle) return;
            lastTitle = title;
            SendTitle(title);
        }
        catch (Exception error)
        {
            Log("title error: " + error.Message);
        }
    }

    private static bool IsGenericTitleLink(string name)
    {
        switch ((name ?? "").Trim())
        {
            case "新建对话":
            case "在项目中新建对话":
            case "自定义":
            case "New conversation":
            case "New conversation in project":
            case "Customize":
            case "Click to open docs":
                return true;
            default:
                return false;
        }
    }

    private static bool IsTitlePrefix(string candidate, string contextTitle)
    {
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(contextTitle)) return false;
        if (contextTitle.Equals(candidate, StringComparison.Ordinal)) return true;
        if (contextTitle.StartsWith(candidate + " - ", StringComparison.Ordinal)) return true;
        if (contextTitle.StartsWith(candidate + " – ", StringComparison.Ordinal)) return true;
        if (contextTitle.StartsWith(candidate + " — ", StringComparison.Ordinal)) return true;
        if (contextTitle.StartsWith(candidate + " · ", StringComparison.Ordinal)) return true;
        string clipped = contextTitle.TrimEnd('.', '…').Trim();
        if (clipped.Length >= 8 && clipped.Length < contextTitle.Length && candidate.StartsWith(clipped, StringComparison.Ordinal)) return true;
        return false;
    }

    private static string FindActiveConversationTitle(AutomationElement root, string contextTitle)
    {
        if (string.IsNullOrEmpty(contextTitle) || contextTitle == "Antigravity") return "";
        string best = "";
        try
        {
            var links = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink));
            foreach (AutomationElement link in links)
            {
                string name;
                try { name = (link.Current.Name ?? "").Trim(); } catch { continue; }
                if (name.Length == 0 || IsGenericTitleLink(name)) continue;
                if (IsTitlePrefix(name, contextTitle) && name.Length > best.Length) best = name;
            }
        }
        catch { }
        return best.Length > 0 ? best : contextTitle;
    }

    private void SendTitle(string title)
    {
        var process = feed;
        if (process == null) return;
        try
        {
            if (process.HasExited) return;
            var json = new JavaScriptSerializer().Serialize(new Dictionary<string, object> { { "title", title } });
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            process.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
            process.StandardInput.BaseStream.Flush();
        }
        catch (Exception error) { Log("send title error: " + error.Message); }
    }

    private static bool CenterInside(System.Windows.Rect r, Native.RECT hostRect)
    {
        double cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        return r.Width > 0 && cx > hostRect.Left && cx < hostRect.Right && cy > hostRect.Top && cy < hostRect.Bottom;
    }

    private static bool FitsInside(System.Windows.Rect r, Native.RECT hostRect)
    {
        return !r.IsEmpty && r.Width > 0 && r.Left >= hostRect.Left - 2 && r.Right <= hostRect.Right + 2 && r.Top >= hostRect.Top && r.Bottom <= hostRect.Bottom + 2;
    }

    private static bool SameRect(Native.RECT a, Native.RECT b)
    {
        return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
    }

    private bool TryProbe(IntPtr target)
    {
        try
        {
            Native.RECT before = Native.FrameRect(target);
            if (composer == null)
            {
                if ((DateTime.UtcNow - lastFullSearch).TotalMilliseconds < 300) return false;
                lastFullSearch = DateTime.UtcNow;
                var watch = Stopwatch.StartNew();
                System.Windows.Rect found;
                composer = FindComposer(target, before, out found);
                Log("composer search " + watch.ElapsedMilliseconds + "ms, found=" + (composer != null) + (composer != null ? ", rect=" + found : ""));
                if (composer == null) return false;
            }
            System.Windows.Rect first = composer.Current.BoundingRectangle;
            Thread.Sleep(80);
            System.Windows.Rect second = composer.Current.BoundingRectangle;
            Native.RECT after = Native.FrameRect(target);
            if (moving || !SameRect(before, after) || first != second) return false;
            if (!FitsInside(second, after)) { composer = null; return false; }
            double offset = second.X + second.Width / 2 - after.Left;
            int width = after.Right - after.Left;
            BeginInvoke((Action)(() =>
            {
                if (Math.Abs(offset - composerOffset) < 0.5 && width == composerBaseWidth) return;
                composerOffset = offset;
                composerBaseWidth = width;
                Reposition();
            }));
            return true;
        }
        catch (Exception error)
        {
            composer = null;
            Log("probe error: " + error.Message);
            return false;
        }
    }

    private AutomationElement FindComposer(IntPtr target, Native.RECT hostRect, out System.Windows.Rect bounds)
    {
        bounds = System.Windows.Rect.Empty;
        var root = AutomationElement.FromHandle(target);
        AutomationElement best = null;
        double middle = hostRect.Top + (hostRect.Bottom - hostRect.Top) * 0.5;
        var controls = new System.Collections.ArrayList();
        controls.AddRange(root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)));
        controls.AddRange(root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox)));
        foreach (AutomationElement edit in controls)
        {
            System.Windows.Rect r;
            try { r = edit.Current.BoundingRectangle; } catch { continue; }
            if (r.IsEmpty || r.Width < S(200) || r.Y < middle || !CenterInside(r, hostRect)) continue;
            if (best == null || r.Bottom > bounds.Bottom) { best = edit; bounds = r; }
        }
        return best;
    }

    private static void Log(string message)
    {
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "antigravity-session-stats-bar.log");
            if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024) File.Delete(path);
            File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine);
        }
        catch { }
    }

    private void HideAll()
    {
        if (Visible) Hide();
        if (opened >= 0) ClosePopup();
        else if (popup.Visible) popup.Hide();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        closing = true;
        probeSignal.Set();
        StopOutsideClick();
        Unhook();
        followTimer.Stop();
        try { if (feed != null && !feed.HasExited) feed.Kill(); } catch { }
        base.OnFormClosed(e);
    }

    [STAThread]
    private static void Main(string[] args)
    {
        bool created;
        using (var mutex = new Mutex(true, "Local\\AntigravitySessionStatsBar", out created))
        {
            if (!created) return;
            Native.SetProcessDPIAware();
            if (Array.IndexOf(args, "--no-registry") < 0)
            {
                if (Array.IndexOf(args, "--no-autostart") >= 0) { Autostart.Set(false); Autostart.SaveChoice(false); }
                else Autostart.EnsureDefault();
            }
            float scale;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string script = null;
            foreach (var arg in args) if (!arg.StartsWith("--")) { script = arg; break; }
            if (script == null) script = Path.GetFullPath(Path.Combine(baseDir, "..", "server", "bar-feed.mjs"));
            Application.EnableVisualStyles();
            Application.Run(new StatsBar(script, scale));
        }
    }
}
