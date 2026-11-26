// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DSN.Core;

namespace DSN.Viewer;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            string? language = null;
            var languageIndex = Array.IndexOf(args, "--language");
            if (languageIndex >= 0)
            {
                if (languageIndex + 1 >= args.Length || args.Count(a => a == "--language") != 1 || args[languageIndex + 1].ToLowerInvariant() is not ("ru" or "en"))
                    throw new ArgumentException(DSNText.Russian("Localization.LanguageArgument"));
                language = args[languageIndex + 1].ToLowerInvariant();
                args = args.Where((_, index) => index != languageIndex && index != languageIndex + 1).ToArray();
            }
            DSNText.Configure(AppContext.BaseDirectory);
            DSNText.SetLanguage(language ?? "ru");
            if (args.Length is 3 or 5 && args[0] == "--render")
            {
                var width = args.Length == 5 ? int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 1800;
                var height = args.Length == 5 ? int.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 600;
                if (width is < 200 or > 10000 || height is < 200 or > 10000 || (long)width * height > 30_000_000)
                    throw new ArgumentException(DSNText.Russian("Viewer.Program.Text001"));
                var document = DSNReader.Read(args[1]);
                var canvas = new LogicProgramCanvas(); canvas.SetDocument(document, document.Pages[0]);
                canvas.Measure(new Size(width, height)); canvas.Arrange(new Rect(0, 0, width, height));
                canvas.Fit(false); canvas.UpdateLayout();
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(canvas);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(args[2]); encoder.Save(output);
                return 0;
            }
            if (args.Length == 3 && args[0] == "--inspect")
            {
                var document = DSNReader.Read(args[1]);
                File.WriteAllText(args[2], LogicProgramModelFile.Serialize(document));
                return 0;
            }
            if (args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException(DSNText.Russian("Viewer.Program.Text002"));
            UILocalization.Initialize(AppContext.BaseDirectory);
            if (language is not null) UILocalization.SelectLanguage(language, false);
            var app = new Application();
            var window = new MainWindow(args.FirstOrDefault());
            return app.Run(window);
        }
        catch (Exception error)
        {
            if (args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal))
            {
                var log = args.Length > 2 ? DSNText.Concat(args[2], ".error.txt") : Path.Combine(AppContext.BaseDirectory, "error.txt");
                File.WriteAllText(log, error.ToString());
            }
            else MessageBox.Show(DSNText.Translate(error.Message), "DSN Compiler", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }
}
