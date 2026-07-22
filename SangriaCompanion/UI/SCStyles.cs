using UnityEngine;

namespace SangriaCompanion;

internal sealed class SCStyles
{
    internal GUIStyle Title { get; private set; } = null!;
    internal GUIStyle SectionTitle { get; private set; } = null!;
    internal GUIStyle Label { get; private set; } = null!;
    internal GUIStyle Muted { get; private set; } = null!;
    internal GUIStyle Dim { get; private set; } = null!;
    internal GUIStyle Gold { get; private set; } = null!;
    internal GUIStyle Green { get; private set; } = null!;
    internal GUIStyle Blood { get; private set; } = null!;
    internal GUIStyle Center { get; private set; } = null!;
    internal GUIStyle MetricValue { get; private set; } = null!;
    internal GUIStyle Metric { get; private set; } = null!;
    internal GUIStyle Large { get; private set; } = null!;
    internal GUIStyle Small { get; private set; } = null!;
    internal GUIStyle Tiny { get; private set; } = null!;
    internal GUIStyle Button { get; private set; } = null!;
    internal GUIStyle SidebarButton { get; private set; } = null!;
    internal GUIStyle SidebarActive { get; private set; } = null!;
    internal GUIStyle Input { get; private set; } = null!;

    private float _lastScale = -1f;

    internal void EnsureCreated(float scale)
    {
        scale = Mathf.Clamp(scale, 0.8f, 1.25f);
        if (Label != null && Mathf.Abs(_lastScale - scale) < 0.001f)
        {
            return;
        }

        _lastScale = scale;
        var fontSize = Mathf.RoundToInt(Mathf.Clamp(Plugin.FontSize.Value, 12, 20) * scale);
        Title = CreateText(fontSize + 8, FontStyle.Bold, TextAnchor.MiddleLeft, SCTheme.Gold);
        SectionTitle = CreateText(fontSize + 4, FontStyle.Bold, TextAnchor.MiddleCenter, SCTheme.Gold);
        Label = CreateText(fontSize, FontStyle.Normal, TextAnchor.MiddleLeft, SCTheme.Text);
        Muted = CreateText(fontSize - 1, FontStyle.Normal, TextAnchor.MiddleLeft, SCTheme.Muted, true);
        Dim = CreateText(fontSize - 2, FontStyle.Normal, TextAnchor.MiddleLeft, SCTheme.Dim, true);
        Gold = CreateText(fontSize, FontStyle.Bold, TextAnchor.MiddleLeft, SCTheme.Gold);
        Green = CreateText(fontSize, FontStyle.Bold, TextAnchor.MiddleLeft, SCTheme.Green);
        Blood = CreateText(fontSize, FontStyle.Bold, TextAnchor.MiddleLeft, SCTheme.Blood);
        Center = CreateText(fontSize + 10, FontStyle.Bold, TextAnchor.MiddleCenter, SCTheme.Text);
        MetricValue = CreateText(fontSize + 15, FontStyle.Bold, TextAnchor.MiddleCenter, SCTheme.Text);
        Metric = MetricValue;
        Large = CreateText(fontSize + 6, FontStyle.Bold, TextAnchor.MiddleLeft, SCTheme.Text, true);
        Small = CreateText(fontSize - 2, FontStyle.Normal, TextAnchor.MiddleCenter, SCTheme.Muted);
        Tiny = CreateText(fontSize - 3, FontStyle.Normal, TextAnchor.MiddleLeft, SCTheme.Muted);
        Button = CreateButton(fontSize - 1, SCTheme.Text);
        SidebarButton = CreateButton(fontSize, SCTheme.Muted);
        SidebarActive = CreateButton(fontSize, SCTheme.Gold);

        Input = new GUIStyle
        {
            fontSize = fontSize,
            alignment = TextAnchor.MiddleLeft
        };
        Input.normal.textColor = SCTheme.Text;
        Input.focused.textColor = Color.white;
    }

    private static GUIStyle CreateText(int size, FontStyle weight, TextAnchor alignment, Color color, bool wrap = false)
    {
        var style = new GUIStyle
        {
            fontSize = Mathf.Max(9, size),
            fontStyle = weight,
            alignment = alignment,
            wordWrap = wrap
        };
        style.normal.textColor = color;
        return style;
    }

    private static GUIStyle CreateButton(int size, Color color)
    {
        var style = new GUIStyle
        {
            fontSize = Mathf.Max(9, size),
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = color;
        style.hover.textColor = Color.white;
        style.active.textColor = SCTheme.Gold;
        return style;
    }
}
