using BepInEx.Configuration;
using UnityEngine;

namespace SangriaCompanion;

internal sealed class SettingsModule
{
    internal void Draw(Rect area, SCStyles styles)
    {
        SCUI.SectionHeader(area, "CONFIGURAÇÕES", styles);
        var top = area.y + 46f;
        var gap = 12f;
        var columnWidth = (area.width - gap) / 2f;
        var leftX = area.x;
        var rightX = area.x + columnWidth + gap;

        DrawAlertsColumn(new Rect(leftX, top, columnWidth, 210f), styles);
        DrawDashboardColumn(new Rect(rightX, top, columnWidth, 210f), styles);

        var controlsY = top + 222f;
        DrawVisualControls(new Rect(area.x, controlsY, area.width, 48f), styles);

        var contactY = controlsY + 60f;
        SCUI.Card(new Rect(area.x, contactY, area.width, 126f), "SOBRE E CONTATO", styles);
        SCUI.Label(new Rect(area.x + 18f, contactY + 46f, area.width - 36f, 22f), "Desenvolvido por Dr. Calcinha Vulgo Álvaro", styles.Label);
        SCUI.Label(new Rect(area.x + 18f, contactY + 72f, area.width - 36f, 20f), "Discord: Orbestein", styles.Muted);
        SCUI.Label(new Rect(area.x + 18f, contactY + 96f, area.width - 36f, 20f), "E-mail: dtialvaro1@gmail.com", styles.Muted);
    }

    private static void DrawAlertsColumn(Rect rect, SCStyles styles)
    {
        SCUI.Card(rect, "AVISOS", styles);
        var y = rect.y + 44f;
        DrawCompactToggle(rect.x + 10f, ref y, rect.width - 20f, "Bosses", Plugin.BossAlertsEnabled, styles);
        DrawCompactToggle(rect.x + 10f, ref y, rect.width - 20f, "Eventos", Plugin.EventAlertsEnabled, styles);
        DrawCompactToggle(rect.x + 10f, ref y, rect.width - 20f, "Só favoritos", Plugin.ShowOnlyFavorites, styles);
        DrawCompactToggle(rect.x + 10f, ref y, rect.width - 20f, "HUD de favoritos", Plugin.FavoriteBossHudEnabled, styles);
    }

    private static void DrawDashboardColumn(Rect rect, SCStyles styles)
    {
        SCUI.Card(rect, "CAIXAS DO DASHBOARD", styles);
        var innerX = rect.x + 10f;
        var innerWidth = rect.width - 20f;
        var half = (innerWidth - 8f) / 2f;
        var y = rect.y + 44f;

        DrawMiniToggle(new Rect(innerX, y, half, 30f), "Vivos", Plugin.DashboardAlive, styles);
        DrawMiniToggle(new Rect(innerX + half + 8f, y, half, 30f), "Mortos", Plugin.DashboardDead, styles);
        y += 36f;
        DrawMiniToggle(new Rect(innerX, y, half, 30f), "Favoritos", Plugin.DashboardFavorites, styles);
        DrawMiniToggle(new Rect(innerX + half + 8f, y, half, 30f), "Eventos", Plugin.DashboardEventsActive, styles);
        y += 36f;
        DrawMiniToggle(new Rect(innerX, y, half, 30f), "Próximo", Plugin.DashboardNextEvent, styles);
        DrawMiniToggle(new Rect(innerX + half + 8f, y, half, 30f), "Lista fav.", Plugin.DashboardFavoriteBosses, styles);
    }

    private static void DrawVisualControls(Rect rect, SCStyles styles)
    {
        SCUI.Panel(rect, SCTheme.PanelAlt, SCTheme.BorderSoft);
        var y = rect.y + 10f;
        var half = rect.width / 2f;

        SCUI.Label(new Rect(rect.x + 12f, y, 118f, 26f), $"Escala: {Plugin.UiScale.Value:0.00}", styles.Label);
        if (SCUI.Button(new Rect(rect.x + 126f, y, 28f, 26f), "−", styles.Button)) ChangeScale(-0.05f);
        if (SCUI.Button(new Rect(rect.x + 160f, y, 28f, 26f), "+", styles.Button)) ChangeScale(0.05f);

        SCUI.Label(new Rect(rect.x + half + 8f, y, 138f, 26f), $"Opacidade: {Plugin.UiOpacity.Value:P0}", styles.Label);
        if (SCUI.Button(new Rect(rect.xMax - 68f, y, 28f, 26f), "−", styles.Button)) ChangeOpacity(-0.05f);
        if (SCUI.Button(new Rect(rect.xMax - 34f, y, 28f, 26f), "+", styles.Button)) ChangeOpacity(0.05f);
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

    private static void DrawMiniToggle(Rect rect, string label, ConfigEntry<bool> entry, SCStyles styles)
    {
        SCUI.Panel(rect, SCTheme.PanelAlt, entry.Value ? SCTheme.GoldSoft : SCTheme.BorderSoft);
        if (SCUI.Button(rect, $"{label}: {(entry.Value ? "SIM" : "NÃO")}", entry.Value ? styles.Gold : styles.Button, true))
        {
            entry.Value = !entry.Value;
            Plugin.SaveState();
        }
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
}
