using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
        Application.Run(new MainForm(mode));
    }
}

class MainForm : Form
{
    const string ACC = "space3";   // ten tai khoan trong SoftEther
    const string NIC = "VPN";      // ten card mang ao

    string vpncmd;
    string mode;
    bool busy;
    Label lblStatus;
    TextBox txtPass, txtLog;
    Button btnConnect, btnDisconnect;
    System.Windows.Forms.Timer timer;

    public MainForm(string mode)
    {
        this.mode = mode;
        Text = "Space3 VPN";
        ClientSize = new Size(360, 330);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        lblStatus = new Label();
        lblStatus.SetBounds(12, 12, 336, 36);
        lblStatus.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
        lblStatus.Text = "Đang kiểm tra...";

        Label lblPass = new Label();
        lblPass.SetBounds(12, 58, 336, 18);
        lblPass.Text = "Mật khẩu (nhập lần đầu, để trống nếu đã lưu):";

        txtPass = new TextBox();
        txtPass.SetBounds(12, 78, 336, 24);
        txtPass.UseSystemPasswordChar = true;

        btnConnect = new Button();
        btnConnect.SetBounds(12, 112, 164, 36);
        btnConnect.Text = "Kết nối";
        btnConnect.Click += delegate { StartConnect(); };

        btnDisconnect = new Button();
        btnDisconnect.SetBounds(184, 112, 164, 36);
        btnDisconnect.Text = "Ngắt kết nối";
        btnDisconnect.Click += delegate { StartDisconnect(); };

        txtLog = new TextBox();
        txtLog.SetBounds(12, 160, 336, 158);
        txtLog.Multiline = true;
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = ScrollBars.Vertical;

        Controls.Add(lblStatus);
        Controls.Add(lblPass);
        Controls.Add(txtPass);
        Controls.Add(btnConnect);
        Controls.Add(btnDisconnect);
        Controls.Add(txtLog);

        vpncmd = FindVpncmd();
        if (vpncmd == null)
            Log("Không tìm thấy SoftEther VPN Client.\r\nHãy cài \"SoftEther VPN Client\" từ softether-download.com rồi mở lại.");

        timer = new System.Windows.Forms.Timer();
        timer.Interval = 4000;
        timer.Tick += delegate
        {
            if (!busy) ThreadPool.QueueUserWorkItem(delegate { RefreshStatus(); });
        };
        timer.Start();

        Shown += delegate
        {
            if (mode == "/connect") StartConnect();
            else if (mode == "/disconnect") StartDisconnect();
            else ThreadPool.QueueUserWorkItem(delegate { RefreshStatus(); });
        };
    }

    // ---------- tien ich UI ----------
    void UI(Action a)
    {
        if (InvokeRequired) BeginInvoke(a); else a();
    }

    void Log(string s)
    {
        UI(delegate { txtLog.AppendText(s + "\r\n"); });
    }

    void SetStatus(string s, Color c)
    {
        UI(delegate { lblStatus.Text = s; lblStatus.ForeColor = c; });
    }

    void SetBusy(bool b)
    {
        UI(delegate
        {
            busy = b;
            btnConnect.Enabled = !b && vpncmd != null;
            btnDisconnect.Enabled = !b && vpncmd != null;
        });
    }

    // ---------- goi vpncmd ----------
    static string FindVpncmd()
    {
        string[] roots = {
            Environment.GetEnvironmentVariable("ProgramFiles"),
            Environment.GetEnvironmentVariable("ProgramW6432"),
            Environment.GetEnvironmentVariable("ProgramFiles(x86)")
        };
        string[] names = { "vpncmd.exe", "vpncmd_x64.exe" };
        foreach (string r in roots)
        {
            if (string.IsNullOrEmpty(r)) continue;
            foreach (string n in names)
            {
                string p = Path.Combine(Path.Combine(r, "SoftEther VPN Client"), n);
                if (File.Exists(p)) return p;
            }
        }
        return null;
    }

    static string Cmd(string exe, string args)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (Process p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return o;
            }
        }
        catch (Exception ex)
        {
            return "ERROR: " + ex.Message;
        }
    }

    string Vpn(string args)
    {
        return Cmd(vpncmd, "localhost /CLIENT /CMD " + args);
    }

    string GetStatus()
    {
        string o = Vpn("AccountStatusGet " + ACC);
        foreach (string line in o.Split('\n'))
        {
            if (line.StartsWith("Session Status"))
            {
                int i = line.IndexOf('|');
                if (i >= 0) return line.Substring(i + 1).Trim();
            }
        }
        return null;
    }

    void RefreshStatus()
    {
        if (vpncmd == null)
        {
            SetStatus("Chưa cài SoftEther", Color.Firebrick);
            return;
        }
        string st = GetStatus();
        if (st != null && st.Contains("Connection Completed"))
            SetStatus("● Đã kết nối", Color.SeaGreen);
        else if (st != null && st.Contains("Connecting"))
            SetStatus("● Đang kết nối...", Color.DarkOrange);
        else
            SetStatus("● Chưa kết nối", Color.Gray);
    }

    // ---------- hanh dong ----------
    void StartConnect()
    {
        if (busy || vpncmd == null) return;
        string pass = txtPass.Text;
        SetBusy(true);
        ThreadPool.QueueUserWorkItem(delegate { DoConnect(pass); });
    }

    void StartDisconnect()
    {
        if (busy || vpncmd == null) return;
        SetBusy(true);
        ThreadPool.QueueUserWorkItem(delegate
        {
            Vpn("AccountDisconnect " + ACC);
            Log("Đã ngắt kết nối.");
            RefreshStatus();
            SetBusy(false);
        });
    }

    void DoConnect(string pass)
    {
        try
        {
            SetStatus("● Đang chuẩn bị...", Color.DarkOrange);
            Cmd("net", "start SEVPNCLIENT");   // bo qua loi neu service dang chay

            if (!Vpn("NicList").Contains("|" + NIC))
            {
                Log("Tạo card mạng ảo \"" + NIC + "\"...");
                Vpn("NicCreate " + NIC);
            }

            if (!Vpn("AccountList").Contains("|" + ACC))
            {
                Log("Import cấu hình " + ACC + "...");
                string tmp = Path.Combine(Path.GetTempPath(), "space3.vpn");
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("space3.vpn"))
                using (FileStream f = File.Create(tmp))
                {
                    s.CopyTo(f);
                }
                Vpn("AccountImport \"" + tmp + "\"");
                try { File.Delete(tmp); } catch (Exception) { }
            }

            if (pass.Length > 0)
            {
                Log("Lưu mật khẩu...");
                Vpn("AccountPasswordSet " + ACC + " /TYPE:standard \"/PASSWORD:" + pass.Replace("\"", "\\\"") + "\"");
            }

            Log("Đang kết nối...");
            SetStatus("● Đang kết nối...", Color.DarkOrange);
            Vpn("AccountConnect " + ACC);

            bool ok = false;
            for (int i = 0; i < 20; i++)
            {
                string st = GetStatus();
                if (st != null && st.Contains("Connection Completed")) { ok = true; break; }
                Thread.Sleep(1000);
            }

            if (ok)
            {
                Cmd("ipconfig", "/renew \"*" + NIC + "*\"");
                Log("Đã kết nối VPN " + ACC + ".");
            }
            else
            {
                Log("Không kết nối được sau 20 giây.\r\nKiểm tra mật khẩu / mạng rồi thử lại.");
            }
        }
        catch (Exception ex)
        {
            Log("Lỗi: " + ex.Message);
        }
        RefreshStatus();
        SetBusy(false);
    }
}
