using System;
using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static class Program
{
    internal static readonly string Source = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        @"OEM\PredatorSense\ProfilePool\config.json");

    private static readonly Regex AdapterRecord = new Regex(
        @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2},\d{3})[^\r\n]*?(?:updateData:ADAPTOR_STATUS,v:|CALLBACK: ADAPTOR_CHANGED |apaStatus Status:)(\d+)(?:,|\s|$)",
        RegexOptions.Multiline | RegexOptions.RightToLeft);
    private static FileInfo cachedLog;
    private static int? cachedAdapter;

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();

    internal static int? ReadAdapter()
    {
        try
        {
            var packages = new DirectoryInfo(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages"));
            var logs = packages.GetDirectories("ULICTekInc.PredatorSenseforNotebook_*")
                .Select(package => new DirectoryInfo(Path.Combine(package.FullName,
                    @"LocalCache\Roaming\acerPredatorSense\logs")))
                .Where(directory => directory.Exists)
                .SelectMany(directory => directory.GetFiles("predatorsense*.log"))
                .OrderByDescending(file => file.LastWriteTimeUtc).Take(2);
            DateTime boot = DateTime.Now.AddMilliseconds(-(double)GetTickCount64());
            foreach (FileInfo log in logs)
            {
                if (log.LastWriteTime < boot || log.Length > 4 * 1024 * 1024) continue;
                if (cachedLog != null && cachedLog.FullName == log.FullName &&
                    cachedLog.Length == log.Length && cachedLog.LastWriteTimeUtc == log.LastWriteTimeUtc)
                {
                    if (cachedAdapter.HasValue) return cachedAdapter;
                    continue;
                }
                using (var reader = new StreamReader(new FileStream(log.FullName, FileMode.Open,
                    FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)))
                {
                    cachedAdapter = ParseAdapter(reader.ReadToEnd(), boot);
                    cachedLog = log;
                }
                if (cachedAdapter.HasValue) return cachedAdapter;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    internal static int? ParseAdapter(string log, DateTime boot)
    {
        Match match = AdapterRecord.Match(log);
        DateTime recorded;
        int adapter;
        if (match.Success &&
            DateTime.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd HH:mm:ss,fff",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out recorded) &&
            recorded >= boot && int.TryParse(match.Groups[2].Value, out adapter))
            return adapter;
        return null;
    }

    [STAThread]
    private static void Main(string[] args)
    {
        if ((args.Length == 3 || args.Length == 4) && args[0] == "--probe")
        {
            int adapter;
            int? status = args.Length == 4 && int.TryParse(args[3], out adapter)
                ? (int?)adapter : ReadAdapter();
            File.WriteAllText(args[2], ReadState(args[1], status));
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

    internal static string ReadState(string path, int? adapter)
    {
        try
        {
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            if (data == null) return "indisponivel";
            if (adapter.HasValue && (adapter.Value & 3) != 0)
            {
                object scenarioValue, activeValue, profilesValue, modeValue;
                if (!data.TryGetValue("Scenario", out scenarioValue)) return "indisponivel";
                var scenario = scenarioValue as Dictionary<string, object>;
                if (scenario == null || !scenario.TryGetValue("active", out activeValue) ||
                    !(activeValue is int) || !scenario.TryGetValue("profiles", out profilesValue))
                    return "indisponivel";
                var profiles = profilesValue as IList;
                int active = (int)activeValue;
                if (profiles == null || active < 0 || active >= profiles.Count) return "indisponivel";
                var profile = profiles[active] as Dictionary<string, object>;
                if (profile == null || !profile.TryGetValue("opMode", out modeValue) || !(modeValue is int))
                    return "indisponivel";
                switch ((int)modeValue)
                {
                    case 0: return "silencioso";
                    case 1: return "equilibrado";
                    case 4: return "desempenho";
                    case 5: return "turbo";
                }
                return "indisponivel";
            }
            object value;
            if (data.TryGetValue("Enable_Eco", out value) && value is int)
            {
                if ((int)value == 1) return "eco";
                if ((int)value == 0) return adapter.HasValue ? "equilibrado-usbc" : "eco-desativado";
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
    private readonly Dictionary<string, string> labels = new Dictionary<string, string>
    {
        { "eco", "Eco ativado" },
        { "eco-desativado", "Eco desativado (perfil desconhecido)" },
        { "equilibrado-usbc", "Equilibrado (USB-C / bateria)" },
        { "equilibrado", "Equilibrado" },
        { "silencioso", "Silencioso" },
        { "desempenho", "Desempenho" },
        { "turbo", "Turbo" },
        { "indisponivel", "Estado indisponível" }
    };
    private readonly string statusPath;
    private string current;

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    internal TrayContext(string status)
    {
        statusPath = status;
        icons.Add("eco", LoadIcon("eco.ico"));
        icons.Add("eco-desativado", LoadIcon("equilibrado.ico"));
        icons.Add("equilibrado-usbc", LoadIcon("equilibrado.ico"));
        icons.Add("equilibrado", LoadIcon("equilibrado.ico"));
        icons.Add("silencioso", MakeIcon(Color.WhiteSmoke, true));
        icons.Add("desempenho", LoadIcon("desempenho.ico"));
        icons.Add("turbo", LoadIcon("turbo.ico"));
        icons.Add("indisponivel", MakeIcon(Color.FromArgb(128, 128, 128)));
        menu = new ContextMenuStrip();
        stateItem = new ToolStripMenuItem("Consultando PredatorSense...") { Enabled = false };
        menu.Items.Add(stateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Atualizar agora", null, delegate { RefreshState(); });
        menu.Items.Add("Sobre o indicador", null, delegate {
            MessageBox.Show("Folha laranja: Eco.\nVelocímetro verde: Equilibrado ou Eco desativado.\nLua branca: Silencioso.\nRaio roxo: Desempenho.\nFoguete magenta: Turbo.\nCinza: leitura indisponível.\n\nConsulta o perfil salvo e o último estado do adaptador registrado pelo PredatorSense a cada 5 segundos.", "Indicador Predator", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        int? adapter = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline
            ? (int?)0 : Program.ReadAdapter();
        string state = Program.ReadState(Program.Source, adapter);
        if (state == current) return;
        current = state;
        tray.Icon = icons[state];
        string label = labels[state];
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

    private static Icon MakeIcon(Color color, bool crescent = false)
    {
        using (Bitmap bitmap = new Bitmap(32, 32))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (SolidBrush brush = new SolidBrush(color))
        using (Pen border = new Pen(Color.FromArgb(190, 30, 30, 30), 1))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.FillEllipse(brush, 4, 4, 24, 24);
            if (crescent)
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                using (var cutout = new SolidBrush(Color.Transparent))
                    graphics.FillEllipse(cutout, 12, 0, 23, 23);
            }
            else
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
