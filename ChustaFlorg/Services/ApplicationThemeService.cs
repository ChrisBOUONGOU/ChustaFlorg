using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ChustaFlorg.Services;

public static class ApplicationThemeService
{
    private const string PreferenceKey = "ChustaFlorg.ApplicationTheme";

    public static IReadOnlyList<string> ThemeNames { get; } = new[]
    {
        "Default", "Midnight", "Ocean", "Forest", "Wine", "Royal", "Sunset",
        "Crimson", "Teal", "Gold", "Arctic", "Pure White", "Warm", "Neon", "Classic"
    };

    private sealed record Palette(
        string Background, string Surface, string SurfaceAlt, string Border,
        string Text, string Muted, string Accent, string Primary, string Button,
        string Danger, string DangerText, string Success, string Preview);

    private static readonly IReadOnlyDictionary<string, Palette> Palettes =
        new Dictionary<string, Palette>(StringComparer.OrdinalIgnoreCase)
        {
            ["Default"] = new("#0B0F17","#121A28","#1A2435","#334155","#FFFFFF","#94A3B8","#D8B46A","#7A1F3D","#202C40","#30202A","#FCA5A5","#86EFAC","#111827"),
            ["Midnight"] = new("#080B14","#101626","#1A2238","#303B55","#F4F6FF","#9AA8C7","#9B8CFF","#51458F","#252D49","#39263B","#FFC1D0","#9CE6D4","#0D1425"),
            ["Ocean"] = new("#071521","#0D2433","#123448","#285064","#F1FAFF","#9DBBCB","#53C7E8","#126B86","#183D52","#3B2934","#FFC0B8","#8CE4C1","#0B2638"),
            ["Forest"] = new("#091711","#10251B","#193528","#315443","#F1FFF5","#A5C4AE","#91C788","#326B45","#214633","#3B2D27","#FFD0B5","#A6E6B2","#10291D"),
            ["Wine"] = new("#190D16","#281522","#39202F","#593748","#FFF5FA","#C9AAB9","#E7A0BE","#853653","#482638","#49252D","#FFC0C8","#B8E0C2","#2B1422"),
            ["Royal"] = new("#100D20","#1B1731","#292344","#453B68","#F8F5FF","#B9AED8","#C4A7FF","#6848A8","#342A55","#462B43","#FFC1D4","#B6E5D0","#1D1735"),
            ["Sunset"] = new("#1B1110","#2A1B19","#3A2722","#60433A","#FFF8F0","#D0B7A6","#FFB36B","#B85B3D","#503126","#552D2A","#FFD0C2","#C7E4B5","#301B18"),
            ["Crimson"] = new("#190B10","#28121A","#391A25","#5B2B39","#FFF5F6","#D1AAB3","#FF8298","#A52F4B","#4A202E","#54202B","#FFC0C8","#B6E4C5","#2D111A"),
            ["Teal"] = new("#081817","#102523","#193633","#315B57","#F1FFFD","#A5C6C2","#68D5C7","#247D75","#204844","#3B3032","#FFD0C8","#A7E6CE","#102D2A"),
            ["Gold"] = new("#17140B","#262115","#39301D","#5B4B2C","#FFFAE8","#C9BC98","#E8C66A","#967329","#473B22","#4C3025","#FFD2BD","#C5E6B4","#2A2415"),
            ["Arctic"] = new("#101B27","#1A2A3A","#263B4D","#405A70","#F5FBFF","#B0C4D4","#82D5F5","#397D9E","#2A465C","#49343B","#FFD0D0","#B6E8D1","#203448"),
            ["Pure White"] = new("#F4F6F8","#FFFFFF","#E8EDF2","#CBD5E1","#17212B","#64748B","#2876C7","#245B91","#DCE7F2","#FCE7E7","#9F3030","#26734D","#E9EFF5"),
            ["Warm"] = new("#211B16","#30271F","#40352A","#665747","#FFF8EF","#CBBBA8","#E7B77B","#96613B","#4B3B2D","#4D3029","#FFD0C2","#BFE0B4","#392D23"),
            ["Neon"] = new("#090D16","#111A2B","#1B2940","#344B69","#F6FBFF","#A7B9D1","#5CF2D6","#8C4DCE","#263C54","#49243F","#FFBDEB","#9CF5C2","#101B30"),
            ["Classic"] = new("#171717","#242424","#333333","#555555","#FFFFFF","#B8B8B8","#D7D7D7","#555555","#3A3A3A","#4A3030","#F0B4B4","#B9D9B9","#202020")
        };

    public static string GetSavedThemeName()
    {
        var name = Preferences.Default.Get(PreferenceKey, "Default");
        return Palettes.ContainsKey(name) ? name : "Default";
    }

    public static void Apply(VisualElement root, string themeName)
    {
        if (!Palettes.TryGetValue(themeName, out var palette))
            palette = Palettes["Default"];

        ApplyToElement(root, palette);
        Preferences.Default.Set(PreferenceKey, themeName);
    }

    private static void ApplyToElement(VisualElement element, Palette p)
    {
        Color? Map(Color? color)
        {
            if (color is null) return null;
            var hex = color.ToHex().ToUpperInvariant();
            if (hex.Length == 9)
                hex = "#" + hex[3..];
            return hex switch
            {
                "#0B0F17" => Color.FromArgb(p.Background),
                "#121A28" => Color.FromArgb(p.Surface),
                "#1A2435" => Color.FromArgb(p.SurfaceAlt),
                "#263246" or "#334155" => Color.FromArgb(p.Border),
                "#FFFFFF" => Color.FromArgb(p.Text),
                "#94A3B8" or "#64748B" => Color.FromArgb(p.Muted),
                "#D8B46A" => Color.FromArgb(p.Accent),
                "#7A1F3D" => Color.FromArgb(p.Primary),
                "#202C40" => Color.FromArgb(p.Button),
                "#30202A" => Color.FromArgb(p.Danger),
                "#FCA5A5" => Color.FromArgb(p.DangerText),
                "#86EFAC" => Color.FromArgb(p.Success),
                "#111827" => Color.FromArgb(p.Preview),
                "#E2E8F0" or "#F1F5F9" => Color.FromArgb(p.SurfaceAlt),
                "#5B1D35" => Color.FromArgb(p.Primary),
                "#164E3B" => Color.FromArgb(p.Button),
                _ => color
            };
        }

        switch (element)
        {
            case ContentPage page:
                page.BackgroundColor = Map(page.BackgroundColor);
                break;
            case Border border:
                border.BackgroundColor = Map(border.BackgroundColor);
                if (border.Stroke is SolidColorBrush stroke)
                    border.Stroke = new SolidColorBrush(Map(stroke.Color) ?? stroke.Color);
                break;
            case Layout layout:
                layout.BackgroundColor = Map(layout.BackgroundColor);
                break;
            case Button button:
                button.BackgroundColor = Map(button.BackgroundColor);
                button.TextColor = Map(button.TextColor) ?? button.TextColor;
                break;
            case Label label:
                label.TextColor = Map(label.TextColor) ?? label.TextColor;
                break;
            case Entry entry:
                entry.BackgroundColor = Map(entry.BackgroundColor);
                entry.TextColor = Map(entry.TextColor) ?? entry.TextColor;
                entry.PlaceholderColor = Map(entry.PlaceholderColor) ?? entry.PlaceholderColor;
                break;
            case Editor editor:
                editor.BackgroundColor = Map(editor.BackgroundColor);
                editor.TextColor = Map(editor.TextColor) ?? editor.TextColor;
                editor.PlaceholderColor = Map(editor.PlaceholderColor) ?? editor.PlaceholderColor;
                break;
            case Picker picker:
                picker.BackgroundColor = Map(picker.BackgroundColor);
                picker.TextColor = Map(picker.TextColor) ?? picker.TextColor;
                break;
            case BoxView box:
                box.Color = Map(box.Color) ?? box.Color;
                break;
        }

        if (element is IVisualTreeElement tree)
        {
            foreach (var child in tree.GetVisualChildren())
                if (child is VisualElement visualChild)
                    ApplyToElement(visualChild, p);
        }
    }
}
