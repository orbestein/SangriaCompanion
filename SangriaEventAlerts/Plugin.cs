using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using ProjectM;
using ProjectM.Network;
using ProjectM.UI;
using SangrisInterface.Patches;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SangriaEventAlerts;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("SangrisInterface", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "lucas.vrising.sangriaeventalerts";
    public const string PluginName = "Sangria Event Alerts";
    public const string PluginVersion = "0.1.4-bridge-fix";

    internal static Plugin Instance { get; private set; } = null!;
    internal static EventOverlayBehaviour Behaviour { get; private set; } = null!;
    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowStandaloneOverlay { get; private set; } = null!;
    internal static ConfigEntry<float> PositionX { get; private set; } = null!;
    internal static ConfigEntry<float> PositionY { get; private set; } = null!;
    internal static ConfigEntry<float> PanelWidth { get; private set; } = null!;
    internal static ConfigEntry<float> PanelHeight { get; private set; } = null!;
    internal static ConfigEntry<int> FontSize { get; private set; } = null!;
    internal static ConfigEntry<float> SyncIntervalSeconds { get; private set; } = null!;
    internal static ConfigEntry<int> ServerUtcOffsetHours { get; private set; } = null!;
    internal static ConfigEntry<string> ExtraInvestidaTimes { get; private set; } = null!;

    private Harmony? _harmony;
    private GameObject? _host;

    public override void Load()
    {
        Instance = this;
        Enabled = Config.Bind("General", "Enabled", true, "Exibe a agenda de eventos.");
        SyncIntervalSeconds = Config.Bind("General", "SyncIntervalSeconds", 15f, "Intervalo da sincronizacao da hora do servidor.");
        ServerUtcOffsetHours = Config.Bind("General", "ServerUtcOffsetHours", -3, "Fuso horario do servidor em relacao ao UTC; Sangria Falls normalmente usa UTC-3.");
        ExtraInvestidaTimes = Config.Bind("Events", "ExtraInvestidaTimes", string.Empty, "Horarios extras HH:mm para a Investida Cientifica, separados por virgula.");
        ShowStandaloneOverlay = Config.Bind("UI", "ShowStandaloneOverlay", false, "Exibe a HUD antiga de eventos. Deixe falso ao usar o Sangria Companion.");
        PositionX = Config.Bind("UI", "PositionX", -1f, "Posicao X; -1 inicia no canto superior direito.");
        PositionY = Config.Bind("UI", "PositionY", 28f, "Posicao Y em pixels.");
        PanelWidth = Config.Bind("UI", "PanelWidth", 430f, "Largura do painel.");
        PanelHeight = Config.Bind("UI", "PanelHeight", 520f, "Altura do painel.");
        FontSize = Config.Bind("UI", "FontSize", 14, "Tamanho da fonte.");

        ClassInjector.RegisterTypeInIl2Cpp<EventOverlayBehaviour>();
        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(typeof(Plugin).Assembly);

        _host = new GameObject("SangriaEventAlertsHost");
        _host.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(_host);
        Behaviour = _host.AddComponent<EventOverlayBehaviour>();
        Log.LogInfo($"{PluginName} {PluginVersion} carregado. A hora exibida depende da sincronizacao nativa com o servidor.");
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        if (_host != null) Object.Destroy(_host);
        Behaviour = null!;
        return true;
    }
}

internal sealed class ScheduledEvent
{
    internal ScheduledEvent(string name, string[] times, int durationMinutes, string access, string sourceNote)
    {
        Name = name;
        Times = times.Select(ParseTime).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        DurationMinutes = durationMinutes;
        Access = access;
        SourceNote = sourceNote;
    }

    internal string Name { get; }
    internal TimeSpan[] Times { get; }
    internal int DurationMinutes { get; }
    internal string Access { get; }
    internal string SourceNote { get; }

    private static TimeSpan? ParseTime(string value)
    {
        return TimeSpan.TryParseExact(value.Trim(), new[] { "h\\:mm", "hh\\:mm", "H\\:mm", "HH\\:mm" }, null, out var result)
            ? result
            : null;
    }
}

internal readonly record struct EventOccurrence(ScheduledEvent Event, TimeSpan Start, TimeSpan UntilStart, bool Active, TimeSpan UntilEnd);

internal sealed class ServerClock
{
    private TimeSpan _serverTime;
    private float _localAnchor;
    internal bool IsSynchronized { get; private set; }
    internal string LastRawValue { get; private set; } = string.Empty;

    internal void Apply(string raw, float localTime)
    {
        var match = System.Text.RegularExpressions.Regex.Match(raw, @"(?<!\d)(\d{1,2}):(\d{2})(?::(\d{2}))?(?!\d)");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var hour) || !int.TryParse(match.Groups[2].Value, out var minute)) return;
        var second = match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out var parsedSecond) ? parsedSecond : 0;
        if (hour > 23 || minute > 59 || second > 59) return;
        _serverTime = new TimeSpan(hour, minute, second);
        _localAnchor = localTime;
        IsSynchronized = true;
        LastRawValue = raw;
    }

    internal void Apply(TimeSpan serverTime, float localTime, string raw)
    {
        _serverTime = TimeSpan.FromSeconds(((serverTime.TotalSeconds % 86400) + 86400) % 86400);
        _localAnchor = localTime;
        IsSynchronized = true;
        LastRawValue = raw;
    }

    internal TimeSpan Now(float localTime)
    {
        var value = _serverTime + TimeSpan.FromSeconds(Mathf.Max(0f, localTime - _localAnchor));
        return TimeSpan.FromSeconds(((value.TotalSeconds % 86400) + 86400) % 86400);
    }
}

internal sealed class EventOverlayBehaviour : MonoBehaviour
{
    private const float RequestTimeout = 8f;
    private static ComponentType[]? _requestComponents;
    private static readonly NetworkEventType TimeRequestNetworkEvent = new()
    {
        IsAdminEvent = false,
        EventId = NetworkEvents.EventId_GetServerTimeInfoRequestEvent,
        IsDebugEvent = false
    };

    internal static EventOverlayBehaviour? Instance { get; private set; }
    private readonly ServerClock _clock = new();
    private readonly List<ScheduledEvent> _events = new();
    private World? _clientWorld;
    private EntityQuery? _responseQuery;
    private float _nextRequestAt = 2f;
    private float _requestSentAt = -1f;
    private bool _requestPending;
    private bool _minimized;
    private Vector2 _position;
    private bool _positionInitialized;
    private Vector2 _scroll;
    private bool _draggingPanel;
    private Vector2 _dragOffset;
    private Vector2 _dragStartMouse;
    private bool _dragMoved;
    private GUIStyle? _boxStyle;
    private GUIStyle? _headerStyle;
    private GUIStyle? _labelStyle;
    private GUIStyle? _smallStyle;
    private GUIStyle? _nextStyle;
    private GUIStyle? _activeHighlightStyle;
    private GUIStyle? _nextHighlightStyle;
    private GUIStyle? _highlightNameStyle;
    private GUIStyle? _highlightDetailStyle;

    private static ComponentType[] RequestComponents => _requestComponents ??= new[]
    {
        ComponentType.ReadOnly(Il2CppType.Of<FromCharacter>()),
        ComponentType.ReadOnly(Il2CppType.Of<NetworkEventType>()),
        ComponentType.ReadOnly(Il2CppType.Of<SendNetworkEventTag>()),
        ComponentType.ReadOnly(Il2CppType.Of<GetServerTimeInfoRequestEvent>())
    };

    private void Awake()
    {
        Instance = this;
        BuildEvents();
        Plugin.Instance.Log.LogInfo($"Agenda carregada: {string.Join(" | ", _events.Select(e => e.Name))}");
    }

    private void BuildEvents()
    {
        var investidaTimes = new List<string>
        {
            "00:30", "02:30", "04:30", "06:30", "08:30", "10:30",
            "12:30", "14:30", "17:30", "18:30", "20:30", "22:30"
        };
        foreach (var token in Plugin.ExtraInvestidaTimes.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (TimeSpan.TryParseExact(token.Trim(), "hh\\:mm", null, out _)) investidaTimes.Add(token.Trim());
        }

        _events.Add(new ScheduledEvent("Chefe Supremo", new[] { "10:00", "16:00", "21:00" }, 10, "Mortium: siga a caveira/marcador do boss mundial no mapa.", "Agenda informada pelo servidor."));
        _events.Add(new ScheduledEvent("Dantos Sangrentum", new[] { "20:00" }, 10, "Use .ds lutar; entrada exige no minimo 1 Exo.", "Evento diario."));
        _events.Add(new ScheduledEvent("Piracema", new[] { "19:30" }, 10, "Lago da Pesca, no ponto do Finn; spots infinitos por 10 minutos.", "Evento diario de pesca."));
        _events.Add(new ScheduledEvent("Investida Cientifica / Invasao", investidaTimes.ToArray(), 10, "Procure a caveira vermelha no mapa; ondas por 5 min e elite por mais 5 min.", "Agenda configurada conforme os horarios informados do servidor."));
    }

    private void Update()
    {
        if (!Plugin.Enabled.Value) return;
        if (_requestPending && Time.unscaledTime - _requestSentAt > RequestTimeout)
        {
            _requestPending = false;
            _nextRequestAt = Time.unscaledTime + 3f;
            Plugin.Instance.Log.LogWarning("A consulta de hora do servidor expirou; tentando novamente.");
        }

        if (_clientWorld == null || !_clientWorld.IsCreated || Time.unscaledTime < _nextRequestAt || _requestPending) return;
        TrySendServerTimeRequest();
    }

    internal void HandleClientWorld(World world)
    {
        if (_clientWorld != world) _responseQuery = null;
        _clientWorld = world;
        ConsumeResponses(world);
    }

    internal void HandleChatMessages(ClientChatSystem chatSystem)
    {
        try
        {
            var entities = chatSystem._ReceiveChatMessagesQuery.ToEntityArray(Allocator.Temp);
            try
            {
                var manager = chatSystem.World.EntityManager;
                foreach (var entity in entities)
                {
                    if (!manager.Exists(entity) || !manager.HasComponent<ChatMessageServerEvent>(entity)) continue;
                    var message = manager.GetComponentData<ChatMessageServerEvent>(entity);
                    if (!TryConvertServerTimestamp(message.TimeUTC, out var utc)) continue;

                    var offset = Mathf.Clamp(Plugin.ServerUtcOffsetHours.Value, -14, 14);
                    var serverTime = utc.ToOffset(TimeSpan.FromHours(offset));
                    var wasSynchronized = _clock.IsSynchronized;
                    _clock.Apply(serverTime.TimeOfDay, Time.unscaledTime, $"chat.TimeUTC={message.TimeUTC}");
                    _requestPending = false;
                    _nextRequestAt = Time.unscaledTime + Mathf.Clamp(Plugin.SyncIntervalSeconds.Value, 5f, 120f);
                    if (!wasSynchronized)
                    {
                        Plugin.Instance.Log.LogInfo($"Hora sincronizada por chat.TimeUTC: {serverTime:HH:mm:ss} (UTC{offset:+#;-#;0})");
                    }
                    return;
                }
            }
            finally
            {
                if (entities.IsCreated) entities.Dispose();
            }
        }
        catch (Exception ex)
        {
            Plugin.Instance.Log.LogError($"Falha ao ler timestamp do chat: {ex}");
        }
    }

    private static bool TryConvertServerTimestamp(long value, out DateTimeOffset utc)
    {
        utc = default;
        try
        {
            // TimeUTC pode ser serializado como ticks .NET ou como Unix seconds/ms,
            // dependendo da versão do servidor/interop.
            if (value >= 10_000_000_000_000_000 && value <= DateTime.MaxValue.Ticks)
            {
                utc = new DateTimeOffset(new DateTime(value, DateTimeKind.Utc));
                return true;
            }

            if (value >= 100_000_000_000)
            {
                utc = DateTimeOffset.FromUnixTimeMilliseconds(value);
                return true;
            }

            if (value >= 1_000_000_000)
            {
                utc = DateTimeOffset.FromUnixTimeSeconds(value);
                return true;
            }
        }
        catch
        {
            // Timestamp inválido: aguarda a próxima mensagem ou o fallback nativo.
        }

        return false;
    }

    private void ConsumeResponses(World world)
    {
        try
        {
            var manager = world.EntityManager;
            _responseQuery ??= manager.CreateEntityQuery(ComponentType.ReadOnly<GetServerTimeInfoResponseEvent>());
            var entities = _responseQuery.Value.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var entity in entities)
                {
                    if (!manager.Exists(entity) || !manager.HasComponent<GetServerTimeInfoResponseEvent>(entity)) continue;
                    var response = manager.GetComponentData<GetServerTimeInfoResponseEvent>(entity);
                    var raw = response.ServerTimeString.ToString();
                    _clock.Apply(raw, Time.unscaledTime);
                    _requestPending = false;
                    _nextRequestAt = Time.unscaledTime + Mathf.Clamp(Plugin.SyncIntervalSeconds.Value, 5f, 120f);
                    manager.DestroyEntity(entity);
                    Plugin.Instance.Log.LogDebug($"Hora do servidor sincronizada: {raw}");
                }
            }
            finally
            {
                if (entities.IsCreated) entities.Dispose();
            }
        }
        catch (Exception ex)
        {
            Plugin.Instance.Log.LogError($"Falha ao ler resposta de hora do servidor: {ex}");
        }
    }

    private void TrySendServerTimeRequest()
    {
        var character = ClientChatPatch.LocalCharacter;
        var user = ClientChatPatch.LocalUser;
        if (character == Entity.Null || user == Entity.Null || _clientWorld == null || !_clientWorld.IsCreated)
        {
            _nextRequestAt = Time.unscaledTime + 2f;
            return;
        }

        try
        {
            var manager = _clientWorld.EntityManager;
            if (!manager.HasComponent<NetworkId>(user))
            {
                _nextRequestAt = Time.unscaledTime + 2f;
                return;
            }

            var entity = manager.CreateEntity(RequestComponents);
            manager.SetComponentData(entity, new FromCharacter { Character = character, User = user });
            manager.SetComponentData(entity, TimeRequestNetworkEvent);
            manager.SetComponentData(entity, new GetServerTimeInfoRequestEvent { Format = ServerTimeRequestFormat.ShortTime });
            _requestPending = true;
            _requestSentAt = Time.unscaledTime;
        }
        catch (Exception ex)
        {
            _requestPending = false;
            _nextRequestAt = Time.unscaledTime + 5f;
            Plugin.Instance.Log.LogError($"Falha ao solicitar hora do servidor: {ex}");
        }
    }

    internal EventBridgeState GetPublicState(int upcomingCount)
    {
        if (!_clock.IsSynchronized)
            return new EventBridgeState(false, TimeSpan.Zero, Array.Empty<EventBridgeOccurrence>());
        var now = _clock.Now(Time.unscaledTime);
        var all = _events.Select(GetNextOccurrence).OrderBy(x => x.Active ? TimeSpan.Zero : x.UntilStart).ToList();
        var selected = all.Where(x => x.Active).Concat(all.Where(x => !x.Active)).Take(Math.Max(1, upcomingCount))
            .Select(x => new EventBridgeOccurrence(x.Event.Name, x.Start, x.UntilStart, x.Active, x.UntilEnd, x.Event.Access))
            .ToArray();
        return new EventBridgeState(true, now, selected);
    }

    private void OnGUI()
    {
        if (!Plugin.Enabled.Value || !Plugin.ShowStandaloneOverlay.Value) return;
        EnsureStyles();
        var width = Mathf.Clamp(Plugin.PanelWidth.Value, 350f, 700f);
        var height = Mathf.Clamp(Plugin.PanelHeight.Value, 330f, Mathf.Max(330f, Screen.height - 12f));
        InitializePanelPosition(width, height);
        ClampPanelPosition(width, height);

        if (_minimized)
        {
            var collapsed = new Rect(_position.x, _position.y, 120f, 30f);
            GUI.Box(collapsed, GUIContent.none, _boxStyle);
            GUI.Label(new Rect(collapsed.x + 10f, collapsed.y + 4f, collapsed.width - 20f, 22f), "Eventos  (clique)", _smallStyle);
            if (Event.current != null && Event.current.type == EventType.MouseDown && collapsed.Contains(Event.current.mousePosition))
            {
                _minimized = false;
                Event.current.Use();
            }
            return;
        }

        var panelRect = new Rect(_position.x, _position.y, width, height);
        HandlePanelDragging(panelRect, 34f);
        ClampPanelPosition(width, height);
        GUI.Box(panelRect, GUIContent.none, _boxStyle);
        GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 6f, width - 24f, 26f), "<b>Eventos Sangria</b>  (clique para ocultar; arraste para mover)", _headerStyle);

        var status = _clock.IsSynchronized
            ? $"Horario do servidor: {_clock.Now(Time.unscaledTime):hh\\:mm\\:ss}"
            : "Horario do servidor: sincronizando...";
        GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 38f, width - 24f, 22f), status, _smallStyle);

        var occurrenceList = _clock.IsSynchronized
            ? _events.Select(GetNextOccurrence).OrderBy(x => x.UntilStart).ToList()
            : new List<EventOccurrence>();
        var active = occurrenceList
            .Where(x => x.Event != null && x.Active)
            .OrderBy(x => x.Start)
            .FirstOrDefault();
        var next = occurrenceList
            .Where(x => x.Event != null && !x.Active)
            .OrderBy(x => x.UntilStart)
            .FirstOrDefault();

        var summaryTop = 65f;
        var summaryCardHeight = 82f;
        var summaryGap = 6f;
        var summaryHeight = 0f;
        if (active.Event != null)
        {
            DrawHighlightCard(panelRect, width, summaryTop + summaryHeight, active, true, summaryCardHeight);
            summaryHeight += summaryCardHeight + summaryGap;
        }
        if (next.Event != null)
        {
            DrawHighlightCard(panelRect, width, summaryTop + summaryHeight, next, false, summaryCardHeight);
            summaryHeight += summaryCardHeight + summaryGap;
        }

        var contentTop = summaryHeight > 0f ? summaryTop + summaryHeight + 2f : 72f;
        var viewport = new Rect(panelRect.x + 8f, panelRect.y + contentTop, width - 16f, height - contentTop - 12f);
        var rowHeight = 62f;
        var contentHeight = Math.Max(viewport.height, occurrenceList.Count * rowHeight + 30f);
        if (Event.current != null && Event.current.type == EventType.ScrollWheel && viewport.Contains(Event.current.mousePosition))
        {
            _scroll.y = Mathf.Clamp(_scroll.y + Event.current.delta.y * rowHeight, 0f, Mathf.Max(0f, contentHeight - viewport.height));
            Event.current.Use();
        }
        _scroll.y = Mathf.Clamp(_scroll.y, 0f, Mathf.Max(0f, contentHeight - viewport.height));
        GUI.BeginGroup(viewport);
        var y = 4f - _scroll.y;
        foreach (var occurrence in occurrenceList)
        {
            var isNext = !occurrence.Active && next.Event != null && occurrence.Event == next.Event && occurrence.Start == next.Start;
            var label = occurrence.Active
                ? $"<color=#55FF77><b>{occurrence.Event.Name}</b></color>  <color=#55FF77><b>ATIVO</b></color>"
                : isNext
                    ? $"<color=#FFD35A><b>{occurrence.Event.Name}</b></color>  <color=#FFD35A><b>PRÓXIMO</b></color>  {FormatCountdown(occurrence.UntilStart)}"
                    : $"<b>{occurrence.Event.Name}</b>  {occurrence.Start:hh\\:mm}  {FormatCountdown(occurrence.UntilStart)}";
            GUI.Label(new Rect(4f, y, width - 45f, 22f), label, _labelStyle);
            GUI.Label(new Rect(4f, y + 22f, width - 45f, 36f), occurrence.Event.Access, _smallStyle);
            y += rowHeight;
        }
        if (!_clock.IsSynchronized) GUI.Label(new Rect(4f, y, width - 45f, 40f), "Aguardando resposta nativa do servidor...", _smallStyle);
        GUI.EndGroup();
    }

    private void DrawHighlightCard(Rect panelRect, float width, float top, EventOccurrence occurrence, bool active, float cardHeight)
    {
        var cardRect = new Rect(panelRect.x + 10f, panelRect.y + top, width - 20f, cardHeight);
        var previousColor = GUI.color;
        GUI.color = active
            ? new Color(0.04f, 0.28f, 0.12f, 0.94f)
            : new Color(0.28f, 0.22f, 0.04f, 0.94f);
        GUI.DrawTexture(cardRect, Texture2D.whiteTexture);
        GUI.color = previousColor;

        var title = active ? "ATIVO AGORA" : "PRÓXIMO EVENTO";
        var titleStyle = active ? _activeHighlightStyle : _nextHighlightStyle;
        var countdown = active
            ? $"termina em {FormatCountdown(occurrence.UntilEnd)}"
            : $"em {FormatCountdown(occurrence.UntilStart)}  •  início {occurrence.Start:hh\\:mm}";
        GUI.Label(new Rect(cardRect.x + 10f, cardRect.y + 6f, cardRect.width - 20f, 22f), title, titleStyle);
        GUI.Label(new Rect(cardRect.x + 10f, cardRect.y + 28f, cardRect.width - 20f, 23f), $"{occurrence.Event.Name}  ({countdown})", _highlightNameStyle);
        GUI.Label(new Rect(cardRect.x + 10f, cardRect.y + 51f, cardRect.width - 20f, cardRect.height - 55f), occurrence.Event.Access, _highlightDetailStyle);
    }

    private void InitializePanelPosition(float width, float height)
    {
        if (_positionInitialized) return;
        var x = Plugin.PositionX.Value < 0 ? Screen.width - width - 70f : Plugin.PositionX.Value;
        _position = new Vector2(Mathf.Max(4f, x), Mathf.Max(4f, Plugin.PositionY.Value));
        _positionInitialized = true;
    }

    private void ClampPanelPosition(float width, float height)
    {
        _position.x = Mathf.Clamp(_position.x, 4f, Mathf.Max(4f, Screen.width - width - 40f));
        _position.y = Mathf.Clamp(_position.y, 4f, Mathf.Max(4f, Screen.height - 38f));
    }

    private void HandlePanelDragging(Rect panelRect, float headerHeight)
    {
        var current = Event.current;
        if (current == null || current.button != 0) return;
        if (current.type == EventType.MouseDown && new Rect(panelRect.x, panelRect.y, panelRect.width - 44f, headerHeight).Contains(current.mousePosition))
        {
            _draggingPanel = true;
            _dragMoved = false;
            _dragStartMouse = current.mousePosition;
            _dragOffset = current.mousePosition - _position;
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && _draggingPanel)
        {
            _dragMoved = _dragMoved || Vector2.Distance(current.mousePosition, _dragStartMouse) > 4f;
            _position = current.mousePosition - _dragOffset;
            current.Use();
        }
        else if (current.type == EventType.MouseUp && _draggingPanel)
        {
            _draggingPanel = false;
            if (_dragMoved)
            {
                Plugin.PositionX.Value = _position.x;
                Plugin.PositionY.Value = _position.y;
                Plugin.Instance.Config.Save();
            }
            else
            {
                _minimized = true;
            }
            current.Use();
        }
    }

    private EventOccurrence GetNextOccurrence(ScheduledEvent scheduled)
    {
        var now = _clock.Now(Time.unscaledTime);
        foreach (var start in scheduled.Times.OrderBy(x => x))
        {
            var delta = start - now;
            if (delta.TotalSeconds >= -scheduled.DurationMinutes * 60 && delta.TotalSeconds <= 0)
                return new EventOccurrence(scheduled, start, TimeSpan.Zero, true, TimeSpan.FromMinutes(scheduled.DurationMinutes) + delta);
            if (delta.TotalSeconds > 0) return new EventOccurrence(scheduled, start, delta, false, TimeSpan.Zero);
        }
        var tomorrow = scheduled.Times.OrderBy(x => x).First() + TimeSpan.FromDays(1);
        return new EventOccurrence(scheduled, scheduled.Times.OrderBy(x => x).First(), tomorrow - now, false, TimeSpan.Zero);
    }

    private static string FormatCountdown(TimeSpan value)
    {
        var seconds = Math.Max(0, (int)Math.Ceiling(value.TotalSeconds));
        return seconds >= 3600 ? $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}" : $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private void EnsureStyles()
    {
        if (_boxStyle != null) return;
        var size = Mathf.Clamp(Plugin.FontSize.Value, 11, 18);
        _boxStyle = new GUIStyle { alignment = TextAnchor.UpperLeft, normal = { textColor = new Color(1f, 1f, 1f, 0.92f) } };
        _headerStyle = new GUIStyle { fontSize = size + 3, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, richText = true, normal = { textColor = Color.white } };
        _labelStyle = new GUIStyle { fontSize = size, alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = true, normal = { textColor = Color.white } };
        _smallStyle = new GUIStyle { fontSize = Mathf.Max(11, size - 2), alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = true, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
        _nextStyle = new GUIStyle { fontStyle = FontStyle.Bold, fontSize = size + 1, alignment = TextAnchor.MiddleLeft, richText = true, normal = { textColor = Color.white } };
        _activeHighlightStyle = new GUIStyle { fontStyle = FontStyle.Bold, fontSize = size + 1, alignment = TextAnchor.MiddleLeft, richText = true, normal = { textColor = new Color(0.35f, 1f, 0.5f) } };
        _nextHighlightStyle = new GUIStyle { fontStyle = FontStyle.Bold, fontSize = size + 1, alignment = TextAnchor.MiddleLeft, richText = true, normal = { textColor = new Color(1f, 0.84f, 0.3f) } };
        _highlightNameStyle = new GUIStyle { fontStyle = FontStyle.Bold, fontSize = size + 2, alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = true, normal = { textColor = Color.white } };
        _highlightDetailStyle = new GUIStyle { fontSize = Mathf.Max(11, size - 1), alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = true, normal = { textColor = new Color(0.95f, 0.95f, 0.95f) } };
    }

    internal void SavePosition()
    {
        Plugin.PositionX.Value = _position.x;
        Plugin.PositionY.Value = _position.y;
        Plugin.Instance.Config.Save();
    }

    private void OnDestroy()
    {
        SavePosition();
        if (Instance == this) Instance = null;
    }
}


public sealed class EventBridgeOccurrence
{
    public EventBridgeOccurrence(string name, TimeSpan start, TimeSpan untilStart, bool active, TimeSpan untilEnd, string description)
    { Name=name; Start=start; UntilStart=untilStart; Active=active; UntilEnd=untilEnd; Description=description; }
    public string Name { get; }
    public TimeSpan Start { get; }
    public TimeSpan UntilStart { get; }
    public bool Active { get; }
    public TimeSpan UntilEnd { get; }
    public string Description { get; }
}
public sealed class EventBridgeState
{
    public EventBridgeState(bool synchronized, TimeSpan serverTime, IReadOnlyList<EventBridgeOccurrence> occurrences)
    { Synchronized=synchronized; ServerTime=serverTime; Occurrences=occurrences; }
    public bool Synchronized { get; }
    public TimeSpan ServerTime { get; }
    public IReadOnlyList<EventBridgeOccurrence> Occurrences { get; }
}
public static class SangriaEventApi
{
    public static EventBridgeState GetState(int upcomingCount = 5) => Plugin.Behaviour?.GetPublicState(upcomingCount) ?? new EventBridgeState(false, TimeSpan.Zero, Array.Empty<EventBridgeOccurrence>());
}

[HarmonyPatch(typeof(ClientChatSystem), nameof(ClientChatSystem.OnUpdate))]
internal static class ClientChatSystemPatch
{
    private static void Prefix(ClientChatSystem __instance)
    {
        EventOverlayBehaviour.Instance?.HandleClientWorld(__instance.World);
        EventOverlayBehaviour.Instance?.HandleChatMessages(__instance);
    }
}
