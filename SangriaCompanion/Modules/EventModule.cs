using UnityEngine;

namespace SangriaCompanion;

internal sealed class EventModule
{
    internal void Draw(Rect area, SCStyles styles)
    {
        SCUI.SectionHeader(area, "EVENTOS", styles);
        var next = EventScheduleService.GetNextOccurrence();
        var y = area.y + 44f;

        var globalRect = new Rect(area.x, y, area.width, 36f);
        SCUI.Panel(globalRect, SCTheme.PanelAlt, Plugin.EventAlertsEnabled.Value ? SCTheme.GoldSoft : SCTheme.BorderSoft);
        SCUI.Label(new Rect(globalRect.x + 12f, globalRect.y + 6f, globalRect.width - 260f, 24f), "Eventos", styles.Label);
        if (SCUI.Button(new Rect(globalRect.xMax - 236f, globalRect.y + 6f, 108f, 24f), Plugin.EventAlertsEnabled.Value ? "AVISOS: ON" : "AVISOS: OFF", styles.Button, true))
        {
            Plugin.EventAlertsEnabled.Value = !Plugin.EventAlertsEnabled.Value;
            Plugin.SaveState();
        }
        if (SCUI.Button(new Rect(globalRect.xMax - 118f, globalRect.y + 6f, 106f, 24f), Plugin.EventHudEnabled.Value ? "HUD: ON" : "HUD: OFF", styles.Button, true))
        {
            Plugin.EventHudEnabled.Value = !Plugin.EventHudEnabled.Value;
            Plugin.SaveState();
        }

        y += 46f;
        SCUI.Card(new Rect(area.x, y, area.width, 116f), next.IsActive ? "EVENTO ATIVO" : "PRÓXIMO EVENTO", styles);
        SCUI.Label(new Rect(area.x + 18f, y + 42f, area.width - 36f, 24f), next.Name, styles.Gold);
        SCUI.Label(new Rect(area.x + 18f, y + 66f, area.width - 36f, 22f), next.IsActive
            ? $"Termina em {EventScheduleService.FormatCountdown(next.UntilEnd)}"
            : $"Início às {next.Start.ToString(@"hh\:mm")} • em {EventScheduleService.FormatCountdown(next.UntilStart)}", styles.Label);
        SCUI.Label(new Rect(area.x + 18f, y + 88f, area.width - 36f, 22f), next.Description, styles.Muted);

        y += 128f;
        SCUI.Card(new Rect(area.x, y, area.width, 214f), "PRÓXIMOS HORÁRIOS E ALERTAS", styles);
        SCUI.Label(new Rect(area.x + 18f, y + 42f, area.width * 0.43f, 18f), "EVENTO", styles.Tiny);
        SCUI.Label(new Rect(area.x + area.width * 0.47f, y + 42f, 72f, 18f), "HORÁRIO", styles.Tiny);
        SCUI.Label(new Rect(area.x + area.width - 206f, y + 42f, 94f, 18f), "COMEÇA EM", styles.Tiny);
        SCUI.Label(new Rect(area.x + area.width - 102f, y + 42f, 84f, 18f), "AVISO", styles.Tiny);
        SCTheme.Fill(new Rect(area.x + 18f, y + 62f, area.width - 36f, 1f), SCTheme.BorderSoft);

        var upcoming = EventScheduleService.GetUpcoming(4);
        var rowY = y + 68f;
        foreach (var item in upcoming)
        {
            SCUI.Label(new Rect(area.x + 18f, rowY, area.width * 0.43f, 25f), item.Name, styles.Label);
            SCUI.Label(new Rect(area.x + area.width * 0.47f, rowY, 72f, 25f), item.Start.ToString(@"hh\:mm"), styles.Gold);
            SCUI.Label(new Rect(area.x + area.width - 206f, rowY, 94f, 25f), item.IsActive ? "ATIVO" : EventScheduleService.FormatCountdown(item.UntilStart), item.IsActive ? styles.Green : styles.Muted);
            var enabled = EventNotificationService.IsEnabledFor(item.Name);
            if (SCUI.Button(new Rect(area.x + area.width - 102f, rowY, 84f, 24f), enabled ? "SIM" : "NÃO", styles.Button, true))
                EventNotificationService.Toggle(item.Name);
            rowY += 34f;
        }
    }
}
