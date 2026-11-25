// Alaamri Revit MCP - Revit add-in side.
// Developed by Dr Ibrahim M.A. Alaamri. MIT License.
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Color = System.Windows.Media.Color;
using FormattedText = System.Windows.Media.FormattedText;
using Point = System.Windows.Point;

namespace Alaamri.RevitMcp
{
    public class App : IExternalApplication
    {
        public const string Version = "0.1.0";
        public const string Author = "Dr Ibrahim M.A. Alaamri";
        public const string RepoUrl = "https://github.com/engibrrahem-wq/alaamri-revit-mcp";
        public const string LinkedIn = "https://www.linkedin.com/in/dribrahim-alameri/";

        internal static Bridge Bridge;
        internal static PushButton ToggleButton;
        internal static UIControlledApplication UiApp;

        public Result OnStartup(UIControlledApplication app)
        {
            UiApp = app;
            Bridge = new Bridge();

            const string tab = "Alaamri";
            try { app.CreateRibbonTab(tab); } catch { /* tab exists */ }
            RibbonPanel panel = app.CreateRibbonPanel(tab, "Claude MCP");
            string asm = Assembly.GetExecutingAssembly().Location;

            var toggle = new PushButtonData("AlaamriMcpToggle", "Start\nClaude", asm, typeof(ToggleCommand).FullName)
            {
                ToolTip = "Start / stop the bridge that lets Claude control this Revit session (localhost only).",
                LongDescription = $"Alaamri Revit MCP {Version}\nDeveloped by {Author}\n{RepoUrl}",
            };
            ToggleButton = (PushButton)panel.AddItem(toggle);
            ToggleButton.AvailabilityClassName = typeof(AlwaysAvailable).FullName;

            var about = new PushButtonData("AlaamriMcpAbout", "About", asm, typeof(AboutCommand).FullName)
            {
                ToolTip = "Alaamri Revit MCP: version, status, author.",
            };
            var aboutBtn = (PushButton)panel.AddItem(about);
            aboutBtn.AvailabilityClassName = typeof(AlwaysAvailable).FullName;
            aboutBtn.LargeImage = Icon("i", Color.FromRgb(0xC9, 0xA2, 0x27), 32);
            aboutBtn.Image = Icon("i", Color.FromRgb(0xC9, 0xA2, 0x27), 16);

            if (Settings.AutoStart) Start(); else UpdateButton();
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            Bridge?.Stop();
            return Result.Succeeded;
        }

        internal static string Start()
        {
            string err = Bridge.Start();
            UpdateButton();
            return err;
        }

        internal static void Stop()
        {
            Bridge.Stop();
            UpdateButton();
        }

        internal static void UpdateButton()
        {
            if (ToggleButton == null) return;
            bool on = Bridge.IsRunning;
            ToggleButton.ItemText = on ? "Stop\nClaude" : "Start\nClaude";
            var c = on ? Color.FromRgb(0x14, 0xA3, 0x8B) : Color.FromRgb(0x7A, 0x86, 0x99);
            ToggleButton.LargeImage = Icon("AI", c, 32);
            ToggleButton.Image = Icon("AI", c, 16);
            ToggleButton.ToolTip = on
                ? $"Claude is CONNECTED on 127.0.0.1:{Bridge.Port}. Click to stop."
                : "Claude is OFF. Click to start the bridge.";
        }

        static BitmapSource Icon(string text, Color bg, int size)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0x0B, 0x1F, 0x3A)), null,
                    new Rect(0, 0, size, size), size * 0.18, size * 0.18);
                dc.DrawRoundedRectangle(new SolidColorBrush(bg), null,
                    new Rect(size * 0.1, size * 0.1, size * 0.8, size * 0.8), size * 0.14, size * 0.14);
                var ft = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                    FontWeights.Bold, FontStretches.Normal), size * (text.Length > 1 ? 0.42 : 0.55),
                    Brushes.White, 1.0);
                dc.DrawText(ft, new Point((size - ft.Width) / 2, (size - ft.Height) / 2));
            }
            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Freeze();
            return bmp;
        }
    }

    public class AlwaysAvailable : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication a, CategorySet c) => true;
    }

    [Transaction(TransactionMode.Manual)]
    public class ToggleCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            if (App.Bridge.IsRunning)
            {
                App.Stop();
                Settings.AutoStart = false;
            }
            else
            {
                string err = App.Start();
                if (err != null)
                {
                    TaskDialog.Show("Alaamri Revit MCP", "Could not start the bridge:\n" + err);
                    return Result.Failed;
                }
                Settings.AutoStart = true;
            }
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class AboutCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var td = new TaskDialog("Alaamri Revit MCP")
            {
                MainInstruction = $"Alaamri Revit MCP {App.Version}",
                MainContent =
                    $"Lets Claude (or any MCP client) control Revit with full API access.\n\n" +
                    $"Bridge: {(App.Bridge.IsRunning ? "RUNNING on 127.0.0.1:" + App.Bridge.Port : "STOPPED")}\n" +
                    $"Requests served: {App.Bridge.Served}\n" +
                    $"Log: {Settings.LogPath}\n\n" +
                    $"Developed by {App.Author}\n{App.RepoUrl}",
                FooterText = $"<a href=\"{App.LinkedIn}\">LinkedIn</a>",
            };
            td.Show();
            return Result.Succeeded;
        }
    }

    internal static class Settings
    {
        public static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Alaamri-Revit-MCP");
        static string Flag => Path.Combine(Dir, "autostart.off");
        public static string LogPath => Path.Combine(Dir, "bridge.log");
        public static string InfoPath => Path.Combine(Dir, "bridge.json");

        public static bool AutoStart
        {
            get => !File.Exists(Flag);
            set
            {
                Directory.CreateDirectory(Dir);
                if (value) { if (File.Exists(Flag)) File.Delete(Flag); }
                else File.WriteAllText(Flag, "off");
            }
        }

        public static void Log(string msg)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
