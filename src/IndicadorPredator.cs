using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static class Program
{
    internal static readonly string Source = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        @"OEM\PredatorSense\ProfilePool\config.json");

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--probe")
        {
            File.WriteAllText(args[2], ReadState(args[1]));
            return;
        }
        bool created;
        using (Mutex mutex = new Mutex(true, @"Local\PredatorModeNotificationIcon", out created))
        {
            if (!created) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string status = args.Length == 2 && args[0] == "--status" ? args[1] : null;
            using (TrayContext context = new TrayContext(status)) Application.Run(context);
        }
    }

    internal static string ReadState(string path)
    {
        try
        {
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            object value;
            if (data != null && data.TryGetValue("Enable_Eco", out value) && value is int)
            {
                if ((int)value == 1) return "eco";
                if ((int)value == 0) return "equilibrado-usbc";
            }
        }
        catch { }
        return "indisponivel";
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon tray;
    private readonly System.Windows.Forms.Timer timer;
    private readonly ContextMenuStrip menu;
    private readonly ToolStripMenuItem stateItem;
    private readonly Dictionary<string, Icon> icons = new Dictionary<string, Icon>();
    private readonly string statusPath;
    private string current;

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    internal TrayContext(string status)
    {
        statusPath = status;
        icons.Add("eco", LoadIcon("eco.ico"));
        icons.Add("equilibrado-usbc", LoadIcon("equilibrado.ico"));
        // Artes prontas; não são usadas antes de existir detecção confiável.
        icons.Add("desempenho", LoadIcon("desempenho.ico"));
        icons.Add("turbo", LoadIcon("turbo.ico"));
        icons.Add("indisponivel", MakeIcon(Color.FromArgb(128, 128, 128)));
        menu = new ContextMenuStrip();
        stateItem = new ToolStripMenuItem("Consultando PredatorSense...") { Enabled = false };
        menu.Items.Add(stateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Atualizar agora", null, delegate { RefreshState(); });
        menu.Items.Add("Sobre o indicador", null, delegate {
            MessageBox.Show("Laranja: Eco ativado.\nVerde: Eco desativado; Equilibrado no cenário USB-C atual.\nCinza: leitura indisponível.\n\nConsulta o estado salvo pelo PredatorSense a cada 5 segundos.\nCom o carregador original, verde também pode representar outros modos.\nO estado pode ficar desatualizado se o PredatorSense não salvar as mudanças.\n\nNão altera energia nem iluminação.", "Indicador Predator", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, delegate { ExitThread(); });
        tray = new NotifyIcon { ContextMenuStrip = menu };
        RefreshState();
        tray.Visible = true;
        timer = new System.Windows.Forms.Timer { Interval = 5000 };
        timer.Tick += delegate { RefreshState(); };
        timer.Start();
    }

    private void RefreshState()
    {
        string state = Program.ReadState(Program.Source);
        if (state == current) return;
        current = state;
        tray.Icon = icons[state];
        string label = state == "eco" ? "Eco ativado" : state == "equilibrado-usbc" ? "Equilibrado (USB-C) / Eco desativado" : "Estado indisponível";
        tray.Text = "Predator: " + label;
        stateItem.Text = label;
        if (!String.IsNullOrEmpty(statusPath))
        {
            try { File.WriteAllText(statusPath, DateTimeOffset.Now.ToString("o") + " | " + state + " | PID=" + System.Diagnostics.Process.GetCurrentProcess().Id); }
            catch { }
        }
    }

    private static Icon LoadIcon(string name)
    {
        using (Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        {
            if (stream == null) throw new InvalidOperationException("Ícone ausente: " + name);
            using (Icon icon = new Icon(stream, SystemInformation.SmallIconSize))
                return (Icon)icon.Clone();
        }
    }

    private static Icon MakeIcon(Color color)
    {
        using (Bitmap bitmap = new Bitmap(32, 32))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (SolidBrush brush = new SolidBrush(color))
        using (Pen border = new Pen(Color.FromArgb(190, 30, 30, 30), 1))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.FillEllipse(brush, 4, 4, 24, 24);
            graphics.DrawEllipse(border, 4, 4, 24, 24);
            IntPtr handle = bitmap.GetHicon();
            try { using (Icon temporary = Icon.FromHandle(handle)) return (Icon)temporary.Clone(); }
            finally { DestroyIcon(handle); }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop();
            timer.Dispose();
            tray.Visible = false;
            tray.Dispose();
            menu.Dispose();
            foreach (Icon icon in icons.Values) icon.Dispose();
        }
        base.Dispose(disposing);
    }
}
