using UnityEngine;

namespace SangriaCompanion;

internal static class EventNotificationService
{
    private static readonly HashSet<string> Fired = new(StringComparer.OrdinalIgnoreCase);
    private static float _nextCheck;

    internal static void Update()
    {
        if (!Plugin.EventAlertsEnabled.Value || !EventScheduleService.IsSynchronized) return;
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 0.5f;

        var selected = ParseSelected();
        foreach (var occurrence in EventScheduleService.GetUpcoming(8))
        {
            if (!selected.Contains(occurrence.Name)) continue;
            // TimeSpan não aceita o formato "O". Usar Ticks também evita a
            // FormatException do interpolated-string handler no IL2CPP.
            var startKey = occurrence.Name + "|" + occurrence.Start.Ticks;
            var seconds = occurrence.UntilStart.TotalSeconds;

            if (!occurrence.IsActive)
            {
                TryFire(startKey + "|600", seconds <= 600 && seconds > 570, occurrence.Name, "Começa em " + EventScheduleService.FormatCountdown(occurrence.UntilStart) + " (10 minutos).", SCTheme.Gold);
                TryFire(startKey + "|300", seconds <= 300 && seconds > 270, occurrence.Name, "Começa em " + EventScheduleService.FormatCountdown(occurrence.UntilStart) + " (5 minutos).", SCTheme.Gold);
                TryFire(startKey + "|60", seconds <= 60 && seconds > 30, occurrence.Name, "Começa em " + EventScheduleService.FormatCountdown(occurrence.UntilStart) + " (1 minuto).", SCTheme.Gold);
                TryFire(startKey + "|start", seconds <= 2 && seconds >= -2, occurrence.Name, "COMEÇOU!", SCTheme.Green);
            }
            else
            {
                TryFire(startKey + "|active", occurrence.UntilEnd.TotalSeconds > 0, occurrence.Name, "Evento ativo • termina em " + EventScheduleService.FormatCountdown(occurrence.UntilEnd), SCTheme.Green);
            }
        }
    }

    internal static bool IsEnabledFor(string eventName) => ParseSelected().Contains(eventName);

    internal static void Toggle(string eventName)
    {
        var selected = ParseSelected();
        if (!selected.Add(eventName)) selected.Remove(eventName);
        Plugin.EventAlertsSelected.Value = string.Join(",", selected.OrderBy(x => x));
        Plugin.SaveState();
    }

    private static HashSet<string> ParseSelected() => Plugin.EventAlertsSelected.Value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void TryFire(string key, bool condition, string title, string message, Color accent)
    {
        if (!condition || !Fired.Add(key)) return;
        NotificationCenter.Enqueue(title, message, accent, Plugin.EventAlertDuration.Value, "Evento");
    }
}
