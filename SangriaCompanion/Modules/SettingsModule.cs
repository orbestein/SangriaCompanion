using BepInEx.Configuration;
using UnityEngine;

namespace SangriaCompanion;

internal sealed class SettingsModule
{
    private static readonly string[] EventPresets =
    {
        "600,300,60",
        "1800,900,300,60",
        "3600,1800,1200,900,600,300,60"
    };

    private static readonly string[] BossPresets =
    {
        "300,60",
        "600,300,60",
        "600,300,120,60"
    };

    internal void Draw(Rect area, SCStyles styles)
    {
        SCUI.SectionHeader(area, "CONFIGURAÇÕES", styles);
        var top = area.y + 42f;
        var gap = 10f;
        var columnWidth = (area.width - gap) / 2f;

        DrawNotificationColumn(new Rect(area.x, top, columnWidth, 244f), styles);
        DrawTimingColumn(new Rect(area.x + columnWidth + gap, top, columnWidth, 244f), styles);

        var y = top + 254f;
        DrawQuickMute(new Rect(area.x, y, area.width, 48f), styles);
        y += 56f;
        DrawVisualControls(new Rect(area.x, y, area.width, 48f), styles);
    }

    private static void DrawNotificationColumn(Rect rect, SCStyles styles)
    {
        SCUI.Card(rect, "AVISOS E SILÊNCIO", styles);
        var y = rect.y + 42f;
        DrawCompactToggle(rect.x + 10f, ref y, rect.width - 20f, "Silenciar tudo", Plugin.MuteAllNotifications, styles);
        DrawCompactToggle(rect.x + 10f, ref y, rect.width - 20f, "Bosses", Plugin.BossAlertsEnabled, styles);
        DrawCompactToggle(rect.x + 10f, ref y, rect.width - 20f, "Eventos", Plugin.EventAlertsEnabled, styles);
        DrawCompactToggle(rect.x + 10f, ref y, rect.width - 20f, "Coletas", Plugin.CollectionAlertsEnabled, styles);
        DrawCompactToggle(rect.x + 10f, ref y, rect.width - 20f, "Pets e almas", Plugin.SessionDropAlertsEnabled, styles);
    }

    private static void DrawTimingColumn(Rect rect, SCStyles styles)
    {
        SCUI.Card(rect, "TEMPOS DOS AVISOS", styles);
        var x = rect.x + 10f;
        var width = rect.width - 20f;
        var y = rect.y + 43f;

        DrawPresetRow(new Rect(x, y, width, 34f), "Eventos", DescribePreset(Plugin.EventAlertThresholds.Value, EventPresets), () => CyclePreset(Plugin.EventAlertThresholds, EventPresets), styles);
        y += 40f;
        DrawPresetRow(new Rect(x, y, width, 34f), "Bosses", DescribePreset(Plugin.BossAlertThresholds.Value, BossPresets), () => CyclePreset(Plugin.BossAlertThresholds, BossPresets), styles);
        y += 44f;
        DrawDurationRow(new Rect(x, y, width, 34f), "Aviso evento", Plugin.EventAlertDuration, styles);
        y += 40f;
        DrawDurationRow(new Rect(x, y, width, 34f), "Aviso boss", Plugin.BossAlertDuration, styles);
        y += 40f;
        DrawDurationRow(new Rect(x, y, width, 34f), "Aviso coleta", Plugin.CollectionAlertDuration, styles);
    }


    private static void DrawQuickMute(Rect rect, SCStyles styles)
    {
        SCUI.Panel(rect, SCTheme.PanelAlt, NotificationCenter.IsTemporarilyMuted ? SCTheme.GoldSoft : SCTheme.BorderSoft);
        SCUI.Label(new Rect(rect.x + 12f, rect.y + 5f, rect.width - 24f, 18f), "SILÊNCIO TEMPORÁRIO", styles.Tiny);
        var y = rect.y + 24f;
        var buttonWidth = (rect.width - 48f) / 3f;
        if (SCUI.Button(new Rect(rect.x + 12f, y, buttonWidth, 24f), "1 HORA", styles.Button, true)) NotificationCenter.MuteFor(3600f);
        if (SCUI.Button(new Rect(rect.x + 18f + buttonWidth, y, buttonWidth, 24f), "ATÉ REINICIAR", styles.Button, true)) NotificationCenter.MuteUntilRestart();
        if (SCUI.Button(new Rect(rect.x + 24f + buttonWidth * 2f, y, buttonWidth, 24f), "REATIVAR", styles.Button, true)) NotificationCenter.ClearTemporaryMute();
    }

    private static void DrawPresetRow(Rect rect, string label, string value, Action onClick, SCStyles styles)
    {
        SCUI.Panel(rect, SCTheme.PanelAlt, SCTheme.BorderSoft);
        SCUI.Label(new Rect(rect.x + 8f, rect.y + 5f, 72f, 24f), label, styles.Label);
        if (SCUI.Button(new Rect(rect.x + 82f, rect.y + 5f, rect.width - 90f, 24f), value, styles.Button, true)) onClick();
    }

    private static void DrawDurationRow(Rect rect, string label, ConfigEntry<float> entry, SCStyles styles)
    {
        SCUI.Panel(rect, SCTheme.PanelAlt, SCTheme.BorderSoft);
        SCUI.Label(new Rect(rect.x + 8f, rect.y + 5f, rect.width - 112f, 24f), $"{label}: {entry.Value:0}s", styles.Label);
        if (SCUI.Button(new Rect(rect.xMax - 98f, rect.y + 5f, 42f, 24f), "−", styles.Button, true)) ChangeDuration(entry, -1f);
        if (SCUI.Button(new Rect(rect.xMax - 50f, rect.y + 5f, 42f, 24f), "+", styles.Button, true)) ChangeDuration(entry, 1f);
    }

    private static string DescribePreset(string current, IReadOnlyList<string> presets)
    {
        var index = -1;
        for (var i = 0; i < presets.Count; i++)
            if (string.Equals(current, presets[i], StringComparison.Ordinal)) index = i;

        var name = index switch
        {
            0 => "BÁSICO",
            1 => "INTERMEDIÁRIO",
            2 => "COMPLETO",
            _ => "PERSONALIZADO"
        };
        var count = current.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
        return $"{name} ({count} avisos)";
    }

    private static void CyclePreset(ConfigEntry<string> entry, IReadOnlyList<string> presets)
    {
        var index = -1;
        for (var i = 0; i < presets.Count; i++)
            if (string.Equals(entry.Value, presets[i], StringComparison.Ordinal)) index = i;
        entry.Value = presets[(index + 1) % presets.Count];
        Plugin.SaveState();
    }

    private static void DrawCompactToggle(float x, ref float y, float width, string label, ConfigEntry<bool> entry, SCStyles styles)
    {
        var rect = new Rect(x, y, width, 34f);
        SCUI.Panel(rect, SCTheme.PanelAlt, entry.Value ? SCTheme.GoldSoft : SCTheme.BorderSoft);
        SCUI.Label(new Rect(rect.x + 9f, rect.y + 5f, rect.width - 88f, 24f), label, styles.Label);
        if (SCUI.Button(new Rect(rect.xMax - 76f, rect.y + 5f, 68f, 24f), entry.Value ? "SIM" : "NÃO", styles.Button, true))
        {
            entry.Value = !entry.Value;
            Plugin.SaveState();
        }
        y += 39f;
    }

    private static void DrawVisualControls(Rect rect, SCStyles styles)
    {
        SCUI.Panel(rect, SCTheme.PanelAlt, SCTheme.BorderSoft);
        var y = rect.y + 10f;
        const float smallButton = 28f;
        const float gap = 6f;
        var restoreWidth = Mathf.Clamp(rect.width * 0.24f, 126f, 150f);
        var available = rect.width - restoreWidth - 30f;
        var groupWidth = available / 2f;

        SCUI.Label(new Rect(rect.x + 12f, y, groupWidth - 72f, 26f), $"Escala: {Plugin.UiScale.Value:0.00}", styles.Label);
        if (SCUI.Button(new Rect(rect.x + groupWidth - 54f, y, smallButton, 26f), "−", styles.Button)) ChangeScale(-0.05f);
        if (SCUI.Button(new Rect(rect.x + groupWidth - 20f, y, smallButton, 26f), "+", styles.Button)) ChangeScale(0.05f);

        var opacityX = rect.x + 12f + groupWidth + gap;
        SCUI.Label(new Rect(opacityX, y, groupWidth - 72f, 26f), $"Opacidade: {Plugin.UiOpacity.Value:P0}", styles.Label);
        if (SCUI.Button(new Rect(opacityX + groupWidth - 66f, y, smallButton, 26f), "−", styles.Button)) ChangeOpacity(-0.05f);
        if (SCUI.Button(new Rect(opacityX + groupWidth - 32f, y, smallButton, 26f), "+", styles.Button)) ChangeOpacity(0.05f);

        if (SCUI.Button(new Rect(rect.xMax - restoreWidth - 10f, y, restoreWidth, 26f), "RESTAURAR PADRÕES", styles.Button, true))
            RestoreDefaults();
    }

    private static void ChangeDuration(ConfigEntry<float> entry, float delta)
    {
        entry.Value = Mathf.Clamp(entry.Value + delta, 2f, 20f);
        Plugin.SaveState();
    }

    private static void ChangeScale(float delta)
    {
        Plugin.UiScale.Value = Mathf.Clamp(Plugin.UiScale.Value + delta, 0.8f, 1.25f);
        Plugin.SaveState();
    }

    private static void ChangeOpacity(float delta)
    {
        Plugin.UiOpacity.Value = Mathf.Clamp(Plugin.UiOpacity.Value + delta, 0.55f, 1f);
        Plugin.SaveState();
    }

    private static void RestoreDefaults()
    {
        Plugin.MuteAllNotifications.Value = false;
        Plugin.BossAlertsEnabled.Value = true;
        Plugin.EventAlertsEnabled.Value = true;
        Plugin.CollectionAlertsEnabled.Value = true;
        Plugin.SessionDropAlertsEnabled.Value = true;
        Plugin.RecipeAlertsEnabled.Value = true;
        Plugin.FavoriteBossHudEnabled.Value = true;
        Plugin.EventHudEnabled.Value = true;
        Plugin.EventAlertThresholds.Value = EventPresets[2];
        Plugin.BossAlertThresholds.Value = BossPresets[2];
        Plugin.EventAlertDuration.Value = 6f;
        Plugin.BossAlertDuration.Value = 5f;
        Plugin.CollectionAlertDuration.Value = 5f;
        Plugin.UiScale.Value = 1f;
        Plugin.UiOpacity.Value = 0.94f;
        NotificationCenter.ClearTemporaryMute();
        Plugin.SaveState();
    }
}
