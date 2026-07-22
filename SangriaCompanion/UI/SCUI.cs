using UnityEngine;

namespace SangriaCompanion;

internal static class SCUI
{
    internal static void Panel(Rect rect, Color? background = null, Color? border = null, float thickness = 1f)
    {
        SCTheme.Fill(rect, background ?? SCTheme.Panel);
        SCTheme.BorderRect(rect, border ?? SCTheme.Border, thickness);
    }

    internal static bool Button(Rect rect, string text, GUIStyle style, bool framed = false)
    {
        var hovered = rect.Contains(Event.current.mousePosition);
        InputBlockService.ObservePointer(rect);
        if (framed)
        {
            SCTheme.Fill(rect, hovered ? SCTheme.PanelHover : SCTheme.PanelAlt);
            SCTheme.BorderRect(rect, hovered ? SCTheme.Blood : SCTheme.BorderSoft);
        }
        var clicked = GUI.Button(rect, text, style);
        var current = Event.current;
        if (hovered && (current.type is EventType.MouseDown or EventType.MouseUp or EventType.MouseDrag))
        {
            InputBlockService.BlockFor(0.45f);
            current.Use();
        }
        return clicked;
    }

    internal static void Label(Rect rect, string text, GUIStyle style) => GUI.Label(rect, text, style);

    private static bool _searchFocused;

    // GUI.TextField/DoTextField não é compatível com o trampoline IL2CPP desta
    // versão do jogo. Este campo captura as teclas manualmente e desenha apenas
    // um Label, evitando Method unstripping failed e mantendo a lista visível.
    internal static string SearchBox(Rect rect, string value, GUIStyle style, out bool focused, string placeholder = "Buscar...")
    {
        value ??= string.Empty;
        var current = Event.current;
        var hovered = rect.Contains(current.mousePosition);

        SCTheme.Fill(rect, SCTheme.PanelAlt);
        SCTheme.BorderRect(rect, _searchFocused ? SCTheme.Gold : hovered ? SCTheme.GoldSoft : SCTheme.Border);

        if (current.type == EventType.MouseDown && current.button == 0)
        {
            _searchFocused = hovered;
            if (hovered)
            {
                InputBlockService.BlockFor(0.30f);
                current.Use();
            }
        }

        if (_searchFocused && current.isKey)
        {
            InputBlockService.BlockFor(0.30f);
            if (current.type == EventType.KeyDown)
            {
                if (current.keyCode == KeyCode.Backspace)
                {
                    if (value.Length > 0) value = value.Substring(0, value.Length - 1);
                }
                else if (current.keyCode == KeyCode.Delete)
                {
                    value = string.Empty;
                }
                else if (current.keyCode is KeyCode.Escape or KeyCode.Return or KeyCode.KeypadEnter)
                {
                    _searchFocused = false;
                }
                else
                {
                    var character = current.character;
                    if (character != '\0' && !char.IsControl(character) && value.Length < 60)
                        value += character;
                }
                current.Use();
            }
            else if (current.type == EventType.KeyUp)
            {
                current.Use();
            }
        }

        var visibleText = value;
        if (_searchFocused && (Time.realtimeSinceStartup % 1f) < 0.55f) visibleText += "|";
        if (string.IsNullOrEmpty(value) && !_searchFocused) visibleText = placeholder;
        Label(new Rect(rect.x + 10f, rect.y, rect.width - 18f, rect.height), visibleText, style);

        InputBlockService.SetTextEntryActive(_searchFocused);
        focused = _searchFocused;
        return value;
    }

    internal static void SectionHeader(Rect area, string title, SCStyles styles)
    {
        Label(new Rect(area.x, area.y, area.width, 30f), title, styles.SectionTitle);
        SCTheme.Fill(new Rect(area.x, area.y + 34f, area.width, 1f), SCTheme.Border);
    }

    internal static void Card(Rect rect, string title, SCStyles styles)
    {
        Panel(rect, SCTheme.Panel, SCTheme.Border);
        Label(new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, 28f), title, styles.Gold);
        SCTheme.Fill(new Rect(rect.x + 14f, rect.y + 44f, rect.width - 28f, 1f), SCTheme.GoldSoft);
    }

    internal static void MetricCard(Rect rect, string caption, string value, string footer, Color accent, SCStyles styles)
    {
        Panel(rect, SCTheme.Panel, SCTheme.BorderSoft);
        SCTheme.Fill(new Rect(rect.x, rect.y, 4f, rect.height), accent);
        Label(new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, 22f), caption, styles.Small);
        var old = styles.MetricValue.normal.textColor;
        styles.MetricValue.normal.textColor = accent;
        Label(new Rect(rect.x + 8f, rect.y + 38f, rect.width - 16f, 42f), value, styles.MetricValue);
        styles.MetricValue.normal.textColor = old;
        Label(new Rect(rect.x + 10f, rect.yMax - 26f, rect.width - 20f, 18f), footer, styles.Small);
    }

    internal static bool Toggle(Rect rect, string label, bool value, SCStyles styles)
    {
        Panel(rect, SCTheme.PanelAlt, value ? SCTheme.GoldSoft : SCTheme.BorderSoft);
        Label(new Rect(rect.x + 10f, rect.y, rect.width - 70f, rect.height), label, value ? styles.Gold : styles.Muted);
        var switchRect = new Rect(rect.xMax - 50f, rect.y + 7f, 38f, rect.height - 14f);
        SCTheme.Fill(switchRect, value ? SCTheme.Green : SCTheme.Dim);
        var knobX = value ? switchRect.xMax - 13f : switchRect.x + 3f;
        SCTheme.Fill(new Rect(knobX, switchRect.y + 3f, 10f, switchRect.height - 6f), SCTheme.Text);
        InputBlockService.ObservePointer(rect);
        var clicked = GUI.Button(rect, string.Empty, styles.Button);
        var current = Event.current;
        if (rect.Contains(current.mousePosition) && (current.type is EventType.MouseDown or EventType.MouseUp))
        {
            InputBlockService.BlockFor(0.45f);
            current.Use();
        }
        return clicked;
    }

    internal static string FormatTime(float seconds)
    {
        var safe = Mathf.Max(0, Mathf.FloorToInt(seconds));
        var minutes = safe / 60;
        var remaining = safe % 60;
        return $"{minutes:00}:{remaining:00}";
    }
}
