// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DSN.Core;

namespace DSN.Viewer;

internal static class UILocalization
{
    private sealed class Entries
    {
        public Dictionary<string, object?> Values { get; } = [];
        public Action? Redraw;
    }
    private sealed class LanguageVersion : INotifyPropertyChanged
    {
        public int Version { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Advance() { Version++; PropertyChanged?.Invoke(this, new(nameof(Version))); }
    }
    private static readonly ConditionalWeakTable<object, Entries> entries = new();
    private static readonly List<WeakReference<object>> targets = [];
    private static readonly LanguageVersion version = new();
    private static string? settingsPath;
    public static string? SettingsDiagnostic { get; private set; }
    static UILocalization() => DSNText.LanguageChanged += Update;
    public static void Initialize(string root)
    {
        DSNText.Configure(root); settingsPath = Path.Combine(root, "settings", "ui.json");
        var language = "ru";
        try
        {
            if (File.Exists(settingsPath) && new FileInfo(settingsPath).Length <= 4096)
            {
                using var saved = JsonDocument.Parse(File.ReadAllText(settingsPath));
                var selected = saved.RootElement.GetProperty("Language").GetString();
                if (selected is "ru" or "en") language = selected;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException) { }
        DSNText.SetLanguage(language);
    }
    public static void SelectLanguage(string language, bool persist = true)
    {
        DSNText.SetLanguage(language); SettingsDiagnostic = null;
        if (!persist || settingsPath is null) return;
        var temporary = settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { Language = language }));
            File.Move(temporary, settingsPath, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { SettingsDiagnostic = DSNText.Russian("Localization.SettingsError", error.Message); }
        finally { if (File.Exists(temporary)) { try { File.Delete(temporary); } catch (IOException) { } } }
    }
    private static Entries Entry(object target)
    {
        if (entries.TryGetValue(target, out var found)) return found;
        var created = new Entries(); entries.Add(target, created); targets.Add(new(target)); return created;
    }
    public static T With<T>(T target, params (string Property, Func<object?> Value)[] values) where T : class
    { foreach (var (property, value) in values) Set(target, property, value); return target; }
    public static void Set(object target, string property, Func<object?> value)
    {
        var current = value();
        if (target is TextBox && property == "Text") { Apply(target, property, current); return; }
        Entry(target).Values[property] = current; Apply(target, property, current);
    }
    public static void Append(object target, string property, Func<object?> value)
    {
        var values = Entry(target).Values; var previous = values.GetValueOrDefault(property) ?? target.GetType().GetProperty(property)!.GetValue(target);
        var combined = DSNText.Concat(previous, value()); Set(target, property, () => combined);
    }
    private static void Apply(object target, string property, object? value) =>
        target.GetType().GetProperty(property)!.SetValue(target, value is string text ? DSNText.Translate(text) : value);
    public static void Observe(object target, Action action) => Entry(target).Redraw = action;
    private static void Update()
    {
        foreach (var weak in targets.ToArray())
            if (weak.TryGetTarget(out var target) && entries.TryGetValue(target, out var data))
            { foreach (var (property, value) in data.Values) Apply(target, property, value); data.Redraw?.Invoke(); }
        targets.RemoveAll(w => !w.TryGetTarget(out _)); version.Advance();
    }
    public static MultiBinding Binding(Binding binding)
    {
        var result = new MultiBinding { Mode = binding.Mode, UpdateSourceTrigger = binding.UpdateSourceTrigger, Converter = new Converter() };
        result.Bindings.Add(binding); result.Bindings.Add(new Binding(nameof(LanguageVersion.Version)) { Source = version, Mode = BindingMode.OneWay }); return result;
    }
    private sealed class Converter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type type, object parameter, CultureInfo culture)
        {
            var value = values[0];
            if (value == DependencyProperty.UnsetValue) return value;
            if (value is string text) return DSNText.Translate(text);
            return type == typeof(string) ? System.Convert.ToString(value, CultureInfo.InvariantCulture) : value;
        }
        public object[] ConvertBack(object value, Type[] types, object parameter, CultureInfo culture) => [value, System.Windows.Data.Binding.DoNothing];
    }
    public static ComboBoxItem Choice(string text) => With(new ComboBoxItem(), ("Content", () => text));
    public static string Caption(object item) => item is ComboBoxItem choice ? choice.Content?.ToString() ?? "" : item.ToString() ?? "";
}
