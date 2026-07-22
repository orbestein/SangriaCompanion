using UnityEngine;

namespace SangriaCompanion;

internal readonly record struct CompanionNotification(string Title, string Message, float Duration, Color Accent, string Category, DateTime CreatedAt);
internal readonly record struct NotificationHistoryItem(string Title, string Message, string Category, DateTime CreatedAt);

internal static class NotificationCenter
{
    private static readonly Queue<CompanionNotification> Pending = new();
    private static readonly List<NotificationHistoryItem> History = new();
    private static readonly Dictionary<string, float> RecentKeys = new(StringComparer.OrdinalIgnoreCase);
    private static CompanionNotification? _current;
    private static float _visibleUntil;

    internal static IReadOnlyList<NotificationHistoryItem> Recent => History;

    internal static void Enqueue(string title, string message, Color accent, float duration, string category)
    {
        // Segunda barreira contra spam: a mesma notificação não entra novamente na
        // fila durante 30 segundos, ainda que outra integração repita o estado.
        var key = $"{category}|{title}|{message}";
        if (RecentKeys.TryGetValue(key, out var until) && Time.unscaledTime < until) return;
        RecentKeys[key] = Time.unscaledTime + 30f;

        var item = new CompanionNotification(title, message, Mathf.Clamp(duration, 2f, 20f), accent, category, DateTime.Now);
        Pending.Enqueue(item);
        History.Insert(0, new NotificationHistoryItem(title, message, category, item.CreatedAt));
        if (History.Count > 40) History.RemoveRange(40, History.Count - 40);
    }

    internal static void Update()
    {
        if (_current.HasValue && Time.unscaledTime >= _visibleUntil) _current = null;
        if (!_current.HasValue && Pending.Count > 0)
        {
            _current = Pending.Dequeue();
            _visibleUntil = Time.unscaledTime + _current.Value.Duration;
        }

        if (RecentKeys.Count > 100)
        {
            var expired = RecentKeys.Where(x => Time.unscaledTime >= x.Value).Select(x => x.Key).ToArray();
            foreach (var key in expired) RecentKeys.Remove(key);
        }
    }

    internal static void Draw(SCStyles styles)
    {
        if (!_current.HasValue) return;
        var item = _current.Value;
        var width = Mathf.Min(390f, Mathf.Max(280f, Screen.width - 32f));
        var rect = new Rect(Mathf.Max(16f, Screen.width - width - 24f), 24f, width, 98f);
        InputBlockService.RegisterScreenArea(rect);
        InputBlockService.ObservePointer(rect);
        SCUI.Panel(rect, new Color(0.025f, 0.03f, 0.038f, 0.97f), item.Accent, 2f);
        SCTheme.Fill(new Rect(rect.x, rect.y, 5f, rect.height), item.Accent);
        SCUI.Label(new Rect(rect.x + 18f, rect.y + 11f, rect.width - 36f, 28f), item.Title.ToUpperInvariant(), styles.Gold);
        SCUI.Label(new Rect(rect.x + 18f, rect.y + 43f, rect.width - 56f, 42f), item.Message, styles.Label);
        if (SCUI.Button(new Rect(rect.xMax - 30f, rect.y + 8f, 22f, 22f), "×", styles.Button, true))
        {
            _current = null;
            _visibleUntil = 0f;
        }
    }
}
