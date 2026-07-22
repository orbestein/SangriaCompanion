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

namespace BossRespawnOverlay;

internal sealed class BossDefinition
{
    internal BossDefinition(string displayName, int level, string? commandName = null)
    {
        var resolvedCommandName = commandName ?? displayName.ToLowerInvariant();
        if (resolvedCommandName.Equals("bar~ao", StringComparison.OrdinalIgnoreCase))
        {
            resolvedCommandName = "bar\u00e3o";
            displayName = "Bar\u00e3o";
        }

        DisplayName = displayName;
        Level = level;
        CommandName = resolvedCommandName;
    }

    internal string DisplayName { get; }
    internal int Level { get; }
    internal string CommandName { get; }
}

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("SangrisInterface", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "lucas.vrising.bossrespawnoverlay";
    public const string PluginName = "Boss Respawn Overlay";
    public const string PluginVersion = "0.6.3-bridge";

    internal static readonly BossDefinition[] DefaultBosses =
    [
        new("Keely", 30),
        new("Errol", 30),
        new("Rufus", 30),
        new("Grayson", 37),
        new("Goreswine", 37),
        new("Lidia", 40),
        new("Clive", 40),
        new("Finn", 42),
        new("Polora", 45),
        new("Kodia", 45),
        new("Nicolau", 45),
        new("Quincey", 47),
        new("Beatrice", 50),
        new("Vincent", 54),
        new("Christina", 54),
        new("Tristan", 54),
        new("Erwin", 56),
        new("Kriig", 57),
        new("Leandra", 57),
        new("Maja", 57),
        new("Bane", 60),
        new("Grethel", 60),
        new("Meredith", 60),
        new("Terah", 63),
        new("Frostmaw", 63),
        new("Elena", 63),
        new("Gaius", 65),
        new("Cassius", 67),
        new("Jade", 67),
        new("Raziel", 67),
        new("Octavian", 68),
        new("Ziva", 70),
        new("Domina", 70),
        new("Angram", 71),
        new("Ungora", 73),
        new("Ben", 73),
        new("Foulrot", 73),
        new("Albert", 74),
        new("Willfred", 74),
        new("Cyril", 75),
        new("Magnus", 76),
        new("Barão", 80, "bar~ao"),
        new("Morian", 80),
        new("Mairwyn", 80),
        new("Henry", 84),
        new("Jakira", 85),
        new("Stavros", 85),
        new("Lucile", 86),
        new("Matka", 86),
        new("Terrorclaw", 86),
        new("Azariel", 89),
        new("Voltatia", 89),
        new("Simon", 90),
        new("Dantos", 92),
        new("Styx", 94),
        new("Gorecrusher", 94),
        new("Valencia", 94),
        new("Solarus", 96),
        new("Talzur", 96),
        new("Megara", 98),
        new("Adam", 98)
    ];

    internal static Plugin Instance { get; private set; } = null!;
    internal static BossRespawnOverlayBehaviour Behaviour { get; private set; } = null!;

    private Harmony _harmony = null!;
    private GameObject? _host;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<float> PollIntervalSeconds { get; private set; } = null!;
    internal static ConfigEntry<float> InitialDelaySeconds { get; private set; } = null!;
    internal static ConfigEntry<float> RightOffset { get; private set; } = null!;
    internal static ConfigEntry<float> TopOffset { get; private set; } = null!;
    internal static ConfigEntry<float> PanelWidth { get; private set; } = null!;
    internal static ConfigEntry<float> PanelHeight { get; private set; } = null!;
    internal static ConfigEntry<float> PositionX { get; private set; } = null!;
    internal static ConfigEntry<float> PositionY { get; private set; } = null!;
    internal static ConfigEntry<int> FontSize { get; private set; } = null!;
    internal static ConfigEntry<string> Bosses { get; private set; } = null!;
    internal static ConfigEntry<string> PinnedBosses { get; private set; } = null!;
    internal static ConfigEntry<string> ExpandedActs { get; private set; } = null!;

    internal static ConfigEntry<bool> ShowActs { get; private set; } = null!;
    internal static ConfigEntry<string> VisibleActs { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowExpandButtons { get; private set; } = null!;

    internal static ConfigEntry<bool> ShowStandaloneOverlay { get; private set; } = null!;
    internal static ConfigEntry<bool> NotificationEnabled { get; private set; } = null!;
    internal static ConfigEntry<int> NotificationAtSeconds { get; private set; } = null!;
    internal static ConfigEntry<float> NotificationDurationSeconds { get; private set; } = null!;
    internal static ConfigEntry<int> NotificationFontSize { get; private set; } = null!;

    public override void Load()
    {
        Instance = this;
        Enabled = Config.Bind("General", "Enabled", true, "Exibe o contador no cliente.");
        PollIntervalSeconds = Config.Bind("General", "PollIntervalSeconds", 30f, "Intervalo entre ciclos completos de consulta.");
        InitialDelaySeconds = Config.Bind("General", "InitialDelaySeconds", 5f, "Atraso da primeira consulta depois de entrar no mundo.");
        Bosses = Config.Bind("Boss", "Bosses", string.Join(',', DefaultBosses.Select(boss => boss.CommandName)), "Bosses consultados, separados por vírgula e na ordem desejada.");
        RightOffset = Config.Bind("UI", "RightOffset", 28f, "Distância da borda direita em pixels.");
        TopOffset = Config.Bind("UI", "TopOffset", 28f, "Distância da borda superior em pixels.");
        PanelWidth = Config.Bind("UI", "PanelWidth", 420f, "Largura do painel em pixels.");
        PanelHeight = Config.Bind("UI", "PanelHeight", 650f, "Altura do painel em pixels; a lista rola quando necessário.");
        PositionX = Config.Bind("UI", "PositionX", -1f, "Posição X salva; -1 usa o canto superior direito.");
        PositionY = Config.Bind("UI", "PositionY", -1f, "Posição Y salva; -1 usa o topo.");
        FontSize = Config.Bind("UI", "FontSize", 16, "Tamanho da fonte do contador.");
        ExpandedActs = Config.Bind("UI", "ExpandedActs", string.Empty, "Atos abertos na overlay, por exemplo: 1,3.");
        ShowActs = Config.Bind(
         "UI",
         "ShowActs",
         true,
         "Define se as seções Ato 1, Ato 2, Ato 3 e Ato 4 aparecem.");

        VisibleActs = Config.Bind(
         "UI",
         "VisibleActs",
         "1,2,3,4",
         "Atos visíveis, separados por vírgula. Exemplo: 1,3.");

        ShowExpandButtons = Config.Bind(
         "UI",
         "ShowExpandButtons",
         true,
         "Exibe os botões Abrir todos e Fechar todos.");

        ShowStandaloneOverlay = Config.Bind("UI", "ShowStandaloneOverlay", false, "Exibe a HUD antiga de bosses. Deixe falso ao usar o Sangria Companion.");

        NotificationEnabled = Config.Bind(
         "Notification",
         "Enabled",
         true,
         "Exibe um aviso grande quando um boss estiver próximo de renascer.");

        NotificationAtSeconds = Config.Bind(
         "Notification",
         "WarningAtSeconds",
         20,
         "Segundos restantes em que o aviso será exibido.");

        NotificationDurationSeconds = Config.Bind(
         "Notification",
         "DurationSeconds",
         5f,
         "Tempo em segundos que o aviso permanecerá na tela.");

        NotificationFontSize = Config.Bind(
         "Notification",
         "FontSize",
         32,
         "Tamanho da fonte do aviso central.");
        PinnedBosses = Config.Bind("Boss", "PinnedBosses", string.Empty, "Bosses preferenciais que aparecem no topo, separados por vírgula.");

        // Nesta suíte o Sangria Companion é o responsável pelos popups.
        // Desativamos a notificação antiga do bridge para não haver avisos duplicados.
        if (!ShowStandaloneOverlay.Value && NotificationEnabled.Value)
        {
            NotificationEnabled.Value = false;
            Config.Save();
        }

        // Migra a lista curta usada pelo protótipo anterior para a lista completa.
        if (string.Equals(Bosses.Value.Trim(), "voltatia,ungora,albert,cyril", StringComparison.OrdinalIgnoreCase))
        {
            Bosses.Value = string.Join(',', DefaultBosses.Select(boss => boss.CommandName));
            Config.Save();
        }

        // Corrige o identificador antigo do Willfred e a grafia aproximada do Barão.
        var correctedBosses = string.Join(',', Bosses.Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim().Equals("wilfred", StringComparison.OrdinalIgnoreCase)
                ? "willfred"
                : name.Trim().Equals("bar~ao", StringComparison.OrdinalIgnoreCase)
                    ? "bar\u00e3o"
                    : name.Trim()));
        if (!string.Equals(Bosses.Value, correctedBosses, StringComparison.Ordinal))
        {
            Bosses.Value = correctedBosses;
            Config.Save();
        }

        // A configuração anterior padrão tinha apenas os 30 bosses de nível 70+.
        // Se ela ainda estiver intacta, amplia para a lista completa nova sem
        // sobrescrever uma seleção personalizada do usuário.
        var previousDefault = DefaultBosses
            .Skip(DefaultBosses.Length - 30)
            .Select(boss => boss.CommandName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var configuredNames = Bosses.Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim())
            .ToList();
        if (configuredNames.Count == previousDefault.Count &&
            configuredNames.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(previousDefault))
        {
            Bosses.Value = string.Join(',', DefaultBosses.Select(boss => boss.CommandName));
            Config.Save();
        }

        ClassInjector.RegisterTypeInIl2Cpp<BossRespawnOverlayBehaviour>();
        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(typeof(Plugin).Assembly);

        _host = new GameObject("BossRespawnOverlayHost");
        _host.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(_host);
        Behaviour = _host.AddComponent<BossRespawnOverlayBehaviour>();

        Log.LogInfo($"{PluginName} {PluginVersion} carregado; bosses: {Bosses.Value}; polling: {PollIntervalSeconds.Value:0.#} s.");
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        if (_host != null)
        {
            Object.Destroy(_host);
            _host = null;
        }

        Behaviour = null!;
        return true;
    }

    internal static void LogUnknownMessage(string message)
    {
        Instance.Log.LogWarning($"Resposta de boss não reconhecida: {message}");
    }
}

internal sealed class BossRespawnOverlayBehaviour : MonoBehaviour
{
    private const float RequestTimeoutSeconds = 5f;
    // O servidor tolera a consulta individual, mas pode ignorar mensagens
    // quando várias chegam em sequência muito rápida. Um boss por segundo
    // completa a lista de 30 dentro de um ciclo de aproximadamente 30 s.
    private const float GapBetweenBossQueriesSeconds = 0.75f;

    private static ComponentType[]? _networkEventComponents;

    private static ComponentType[] GetNetworkEventComponents()
    {
        return _networkEventComponents ??= new ComponentType[]
        {
            ComponentType.ReadOnly(Il2CppType.Of<FromCharacter>()),
            ComponentType.ReadOnly(Il2CppType.Of<NetworkEventType>()),
            ComponentType.ReadOnly(Il2CppType.Of<SendNetworkEventTag>()),
            ComponentType.ReadOnly(Il2CppType.Of<ChatMessageEvent>())
        };
    }

    private static readonly NetworkEventType ChatNetworkEventType = new()
    {
        IsAdminEvent = false,
        EventId = NetworkEvents.EventId_ChatMessageEvent,
        IsDebugEvent = false
    };

    internal static BossRespawnOverlayBehaviour? Instance { get; private set; }

    private sealed class BossState
    {
        internal BossState(int index, BossDefinition definition)
        {
            Index = index;
            Definition = definition;
        }

        internal int Index { get; }
        internal BossDefinition Definition { get; }
        internal string CommandName => Definition.CommandName;
        internal string DisplayName => Definition.DisplayName;
        internal int Level => Definition.Level;
        internal bool HasResponse { get; set; }
        internal bool IsAlive { get; set; }
        internal bool HasError { get; set; }
        internal bool IsPinned { get; set; }
        internal float RemainingSeconds { get; set; }
        internal float PreviousRemainingSeconds { get; set; } = -1f;
        internal bool NotificationShown { get; set; }
        internal float LastQueryAt { get; set; } = -999f;
    }

    private readonly List<BossState> _bosses = new();
    private GUIStyle? _boxStyle;
    private GUIStyle? _labelStyle;
    private GUIStyle? _toggleStyle;
    private GUIStyle? _killButtonStyle;
    private GUIStyle? _pinButtonStyle;
    private GUIStyle? _sectionStyle;
    private GUIStyle? _notificationStyle;
    private GUIStyle? _notificationShadowStyle;
    private string _notificationText = string.Empty;
    private float _notificationVisibleUntil;
    private float _nextQueryAt;
    private float _requestSentAt = -1f;
    private int _activeBossIndex = -1;
    private int _nextBossIndex;
    private int _nextPinnedIndex;
    private int _forcedBossIndex = -1;
    private int _pollCycle;
    private bool _activeRequestWasPinned;
    private bool _loggedUnknownResponse;
    private bool _overlayShown;
    private readonly bool[] _expandedActs = new bool[4];
    private World? _clientWorld;
    private Vector2 _panelPosition;
    private Vector2 _scrollPosition;
    private bool _panelPositionInitialized;
    private bool _draggingPanel;
    private Vector2 _dragOffset;

    private int OverlayFontSize => Mathf.Clamp(Plugin.FontSize.Value, 12, 16);

    private BossState? ActiveBoss => _activeBossIndex >= 0 && _activeBossIndex < _bosses.Count
        ? _bosses[_activeBossIndex]
        : null;

    private void Awake()
    {
        Instance = this;
        LoadBosses();
        foreach (var token in Plugin.ExpandedActs.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token.Trim(), out var act) && act >= 1 && act <= 4)
            {
                _expandedActs[act - 1] = true;
            }
        }

        Plugin.Instance.Log.LogInfo($"Lista de bosses consultada ({_bosses.Count}): {string.Join(" -> ", _bosses.Select(boss => $"{boss.DisplayName} ({boss.Level}) [.boss tempo {boss.CommandName}]"))}");
        _nextQueryAt = Time.unscaledTime + Mathf.Max(0.5f, Plugin.InitialDelaySeconds.Value);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LoadBosses()
    {
        _bosses.Clear();
        var configured = Plugin.Bosses.Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < configured.Length; i++)
        {
            var commandName = configured[i].Trim().ToLowerInvariant();
            if (commandName.Length == 0)
            {
                continue;
            }

            var definition = Plugin.DefaultBosses.FirstOrDefault(
                boss => string.Equals(boss.CommandName, commandName, StringComparison.OrdinalIgnoreCase));
            if (definition == null)
            {
                definition = new BossDefinition(commandName, 0);
            }

            _bosses.Add(new BossState(_bosses.Count, definition));
        }

        if (_bosses.Count == 0)
        {
            _bosses.Add(new BossState(0, Plugin.DefaultBosses[20]));
        }

        var pinned = new HashSet<string>(
            Plugin.PinnedBosses.Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim()),
            StringComparer.OrdinalIgnoreCase);
        foreach (var boss in _bosses)
        {
            boss.IsPinned = pinned.Contains(boss.CommandName);
        }
    }

    private static int GetActNumber(BossState boss)
    {
        return boss.Level <= 47 ? 1 :
               boss.Level <= 68 ? 2 :
               boss.Level <= 75 ? 3 : 4;
    }

    private static string GetActTitle(int act)
    {
        return act switch
        {
            1 => "Ato 1  (níveis 30–47)",
            2 => "Ato 2  (níveis 50–68)",
            3 => "Ato 3  (níveis 70–75)",
            _ => "Ato 4  (níveis 76+)",
        };
    }

    private List<BossState> GetActBosses(int act)
    {
        return _bosses
            .Where(boss => GetActNumber(boss) == act && !boss.IsPinned)
            .OrderBy(boss => boss.Index)
            .ToList();
    }

private void ToggleAct(int act)
{
    _expandedActs[act - 1] = !_expandedActs[act - 1];
    SaveExpandedActs();
}
    private HashSet<int> GetVisibleActs()
{
    var visibleActs = new HashSet<int>();

    var configuredActs = Plugin.VisibleActs.Value
        .Split(',', StringSplitOptions.RemoveEmptyEntries);

    foreach (var configuredAct in configuredActs)
    {
        if (int.TryParse(configuredAct.Trim(), out var act) &&
            act >= 1 &&
            act <= 4)
        {
            visibleActs.Add(act);
        }
    }

    // Evita deixar a interface vazia por erro de configuração.
    if (visibleActs.Count == 0)
    {
        visibleActs.UnionWith(new[] { 1, 2, 3, 4 });
    }

    return visibleActs;
}

private void ExpandAllVisibleActs(HashSet<int> visibleActs)
{
    foreach (var act in visibleActs)
    {
        _expandedActs[act - 1] = true;
    }

    SaveExpandedActs();
}

private void CollapseAllVisibleActs(HashSet<int> visibleActs)
{
    foreach (var act in visibleActs)
    {
        _expandedActs[act - 1] = false;
    }

    SaveExpandedActs();
}

private void SaveExpandedActs()
{
    Plugin.ExpandedActs.Value = string.Join(
        ',',
        Enumerable.Range(1, 4)
            .Where(act => _expandedActs[act - 1]));

    Plugin.Instance.Config.Save();
}

    private void DrawBossRow(BossState boss, float rowY, float rowWidth, float rowHeight, float killButtonWidth, float pinButtonWidth)
    {
        var labelWidth = rowWidth - killButtonWidth - pinButtonWidth - 12f;
        var colour = !boss.HasResponse ? "#D0D0D0" : boss.IsAlive ? "#55FF77" : "#FF5555";
        var label = $"<color={colour}><b>{boss.DisplayName} ({boss.Level})</b>: {GetBossStatusText(boss)}</color>";
        GUI.Label(new Rect(4f, rowY, labelWidth, rowHeight), label, _labelStyle);

        if (GUI.Button(
                new Rect(rowWidth - killButtonWidth - pinButtonWidth - 4f, rowY + 1f, killButtonWidth, rowHeight - 2f),
                "Morto",
                _killButtonStyle))
        {
            MarkBossKilled(boss);
        }

        if (GUI.Button(
                new Rect(rowWidth - pinButtonWidth, rowY + 1f, pinButtonWidth, rowHeight - 2f),
                boss.IsPinned ? "Topo" : "Fixar",
                _pinButtonStyle))
        {
            TogglePinned(boss);
        }
    }

    private void Update()
    {
        if (!Plugin.Enabled.Value)
        {
            return;
        }

        foreach (var boss in _bosses)
        {
            if (!boss.HasResponse || boss.HasError)
            {
                continue;
            }

            if (boss.IsAlive)
            {
                boss.NotificationShown = false;
                boss.PreviousRemainingSeconds = -1f;
                continue;
            }

            boss.PreviousRemainingSeconds = boss.RemainingSeconds;
            boss.RemainingSeconds = Mathf.Max(
                0f,
                boss.RemainingSeconds - Time.unscaledDeltaTime);

            var notificationAt = Mathf.Max(1, Plugin.NotificationAtSeconds.Value);

            // Libera uma nova notificação apenas quando o boss voltou a ficar
            // acima da janela configurada, evitando repetição no mesmo respawn.
            if (boss.RemainingSeconds > notificationAt + 2f)
            {
                boss.NotificationShown = false;
            }

            if (Plugin.NotificationEnabled.Value &&
                boss.IsPinned &&
                !boss.NotificationShown &&
                boss.RemainingSeconds > 0f &&
                boss.RemainingSeconds <= notificationAt)
            {
                ShowBossNotification(boss);
            }
        }

        var activeBoss = ActiveBoss;
        if (activeBoss != null && Time.unscaledTime - _requestSentAt > RequestTimeoutSeconds)
        {
            Plugin.Instance.Log.LogWarning($"O comando .boss tempo {activeBoss.CommandName} não respondeu dentro do timeout.");
            CompleteActiveRequest();
        }

        if (ActiveBoss == null && Time.unscaledTime >= _nextQueryAt)
        {
            TrySendBossQuery();
        }
    }


    private void ShowBossNotification(BossState boss)
    {
        boss.NotificationShown = true;
        var seconds = Mathf.Max(1, Mathf.CeilToInt(boss.RemainingSeconds));

        _notificationText =
            $"{boss.DisplayName.ToUpperInvariant()}\n\n" +
            $"RENASCERÁ EM {seconds} SEGUNDOS";

        _notificationVisibleUntil =
            Time.unscaledTime + Mathf.Max(1f, Plugin.NotificationDurationSeconds.Value);

        Plugin.Instance.Log.LogInfo(
            $"Aviso de respawn exibido para {boss.DisplayName}: {seconds} segundos restantes.");
    }

    internal void HandleChatUpdate(ClientChatSystem chatSystem)
    {
        // ClientChatPatch.LocalUser/LocalCharacter pertencem ao mundo do chat.
        // Uma Entity nunca pode ser consultada por outro EntityManager/world.
        _clientWorld = chatSystem.World;

        var activeBoss = ActiveBoss;
        if (activeBoss == null)
        {
            return;
        }

        NativeArray<Entity> entities = default;
        try
        {
            entities = chatSystem._ReceiveChatMessagesQuery.ToEntityArray(Allocator.Temp);
            var entityManager = chatSystem.World.EntityManager;
            var completed = false;

            foreach (var entity in entities)
            {
                if (!entityManager.Exists(entity) || !entityManager.HasComponent<ChatMessageServerEvent>(entity))
                {
                    continue;
                }

                var message = entityManager.GetComponentData<ChatMessageServerEvent>(entity);
                var text = message.MessageText.Value;
                if (!LooksLikeBossResponse(text, activeBoss))
                {
                    continue;
                }

                if (TryApplyResponse(text, activeBoss, out var responseCompleted))
                {
                    completed |= responseCompleted;
                    _loggedUnknownResponse = false;
                }
                else if (!_loggedUnknownResponse)
                {
                    _loggedUnknownResponse = true;
                    Plugin.LogUnknownMessage(text);
                }

                // A consulta e as linhas de resposta são mensagens do protocolo
                // de chat. Removê-las aqui impede que apareçam no chat do jogador.
                entityManager.DestroyEntity(entity);
            }

            if (completed)
            {
                CompleteActiveRequest();
            }
        }
        catch (Exception ex)
        {
            Plugin.Instance.Log.LogError($"Falha ao ler resposta de boss: {ex}");
        }
        finally
        {
            if (entities.IsCreated)
            {
                entities.Dispose();
            }
        }
    }

    internal void NotifyManualChatCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            !text.TrimStart().StartsWith(".boss tempo", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Uma resposta manual nunca deve ser consumida pelo overlay. Se ela
        // acontecer durante uma consulta automática, cancelamos esta consulta;
        // sem um request-id no protocolo do servidor, as duas respostas seriam
        // indistinguíveis depois que chegassem ao cliente.
        if (ActiveBoss != null)
        {
            Plugin.Instance.Log.LogDebug("Consulta automática cancelada para preservar resposta de comando manual.");
            CompleteActiveRequest();
        }
    }

    private List<BossState> GetPinnedBossesForPolling()
    {
        return _bosses
            .Where(boss => boss.IsPinned)
            .OrderBy(boss => boss.Index)
            .ToList();
    }

    private BossState? SelectNextScheduledBoss(out bool isPreferred)
    {
        var pinnedBosses = GetPinnedBossesForPolling();
        if (_bosses.Count == 0)
        {
            isPreferred = false;
            return null;
        }

        // Mantém uma consulta geral a cada cinco consultas para que a lista
        // completa continue atualizando, mas dá prioridade real aos fixados.
        var shouldPollGeneral = pinnedBosses.Count == 0 || (_pollCycle++ % 5) == 4;
        if (!shouldPollGeneral)
        {
            isPreferred = true;

            // Boss morto e próximo do respawn é sempre o mais urgente. Entre
            // vários candidatos, consulta primeiro o que está há mais tempo
            // sem receber uma consulta do servidor.
            return pinnedBosses
                .OrderByDescending(boss => !boss.IsAlive && boss.RemainingSeconds > 0f && boss.RemainingSeconds <= 90f)
                .ThenBy(boss => boss.LastQueryAt)
                .First();
        }

        isPreferred = false;
        return _bosses[_nextBossIndex % _bosses.Count];
    }

    private void AdvanceScheduledBoss(BossState boss, bool wasPreferred)
    {
        if (wasPreferred)
        {
            var pinnedCount = _bosses.Count(item => item.IsPinned);
            _nextPinnedIndex = pinnedCount == 0
                ? 0
                : (_nextPinnedIndex + 1) % pinnedCount;
            return;
        }

        _nextBossIndex = _bosses.Count == 0
            ? 0
            : (boss.Index + 1) % _bosses.Count;
    }

    private void TrySendBossQuery()
    {
        var localCharacter = ClientChatPatch.LocalCharacter;
        var localUser = ClientChatPatch.LocalUser;
        if (localCharacter == Entity.Null || localUser == Entity.Null)
        {
            _nextQueryAt = Time.unscaledTime + 2f;
            return;
        }

        var world = _clientWorld;
        if (world == null || !world.IsCreated)
        {
            _nextQueryAt = Time.unscaledTime + 2f;
            return;
        }

        var isForced = _forcedBossIndex >= 0;
        var isPreferred = false;
        var boss = isForced
            ? _bosses[_forcedBossIndex]
            : SelectNextScheduledBoss(out isPreferred);
        if (boss == null)
        {
            _nextQueryAt = Time.unscaledTime + 2f;
            return;
        }

        var command = $".boss tempo {boss.CommandName}";
        try
        {
            var entityManager = world.EntityManager;
            if (!entityManager.HasComponent<NetworkId>(localUser))
            {
                _nextQueryAt = Time.unscaledTime + 2f;
                return;
            }

            var networkEntity = entityManager.CreateEntity(GetNetworkEventComponents());
            entityManager.SetComponentData(networkEntity, new FromCharacter
            {
                Character = localCharacter,
                User = localUser
            });
            entityManager.SetComponentData(networkEntity, ChatNetworkEventType);
            entityManager.SetComponentData(networkEntity, new ChatMessageEvent
            {
                MessageText = new FixedString512Bytes(command),
                MessageType = ChatMessageType.Local,
                ReceiverEntity = entityManager.GetComponentData<NetworkId>(localUser)
            });

            _activeBossIndex = boss.Index;
            _activeRequestWasPinned = boss.IsPinned;
            boss.LastQueryAt = Time.unscaledTime;
            _forcedBossIndex = -1;
            if (!isForced)
            {
                AdvanceScheduledBoss(boss, isPreferred);
            }
            _requestSentAt = Time.unscaledTime;
            _loggedUnknownResponse = false;
            Plugin.Instance.Log.LogDebug($"Consulta interna enviada: {command}");
        }
        catch (Exception ex)
        {
            Plugin.Instance.Log.LogError($"Falha ao enviar consulta interna {command}: {ex}");
            _nextQueryAt = Time.unscaledTime + 5f;
        }
    }

    private void CompleteActiveRequest()
    {
        _activeBossIndex = -1;
        _loggedUnknownResponse = false;
        _nextQueryAt = Time.unscaledTime + (_activeRequestWasPinned ? 0.45f : GapBetweenBossQueriesSeconds);
        _activeRequestWasPinned = false;
    }

    private void TogglePinned(BossState boss)
    {
        boss.IsPinned = !boss.IsPinned;
        boss.NotificationShown = false;

        Plugin.PinnedBosses.Value = string.Join(',', _bosses
            .Where(item => item.IsPinned)
            .OrderBy(item => item.Index)
            .Select(item => item.CommandName));
        Plugin.Instance.Config.Save();

        // Atualiza imediatamente o boss recém-fixado.
        if (boss.IsPinned)
        {
            _forcedBossIndex = boss.Index;
            if (ActiveBoss != null)
            {
                CompleteActiveRequest();
            }
            _nextQueryAt = Time.unscaledTime + 0.1f;
        }
    }

    private void MarkBossKilled(BossState boss)
    {
        boss.HasResponse = true;
        boss.IsAlive = false;
        boss.HasError = false;
        boss.RemainingSeconds = 0f;
        _forcedBossIndex = boss.Index;

        if (ActiveBoss != null)
        {
            CompleteActiveRequest();
        }

        _nextQueryAt = Time.unscaledTime + 0.1f;
        Plugin.Instance.Log.LogDebug($"Boss marcado como morto manualmente na overlay: {boss.DisplayName}.");
    }

    private static bool TryApplyResponse(string rawText, BossState boss, out bool responseCompleted)
    {
        responseCompleted = false;
        var text = StripRichText(rawText);
        var wasAlive = boss.HasResponse && boss.IsAlive;
        var previousSeconds = boss.RemainingSeconds;

        if (IsAvailableText(text))
        {
            boss.HasResponse = true;
            boss.IsAlive = true;
            boss.HasError = false;
            boss.RemainingSeconds = 0f;
            boss.NotificationShown = false;
            responseCompleted = true;
            return true;
        }

        if (IsNotFoundText(text))
        {
            boss.HasResponse = true;
            boss.IsAlive = false;
            boss.HasError = true;
            boss.RemainingSeconds = 0f;
            responseCompleted = true;
            return true;
        }

        var hasBossStatus =
            (text.Contains(boss.CommandName, StringComparison.OrdinalIgnoreCase) ||
             text.Contains(boss.DisplayName, StringComparison.OrdinalIgnoreCase)) &&
            IsDeadText(text);
        var hasRespawnTime = TryParseTime(text, out var seconds);
        if (!hasBossStatus && !hasRespawnTime)
        {
            return false;
        }

        boss.HasResponse = true;
        boss.IsAlive = false;
        boss.HasError = false;

        // Ao detectar a transição VIVO -> MORTO, libera o alerta do novo ciclo.
        if (wasAlive)
        {
            boss.NotificationShown = false;
        }

        if (hasRespawnTime)
        {
            boss.RemainingSeconds = Mathf.Max(0f, seconds);
            if (previousSeconds <= 0f || seconds > previousSeconds + 5f)
            {
                boss.NotificationShown = false;
            }
            responseCompleted = true;
        }
        else if (hasBossStatus && wasAlive)
        {
            // Atualiza o estado imediatamente, mesmo antes da linha seguinte
            // da resposta trazer o tempo de respawn.
            boss.RemainingSeconds = 0f;
        }

        return true;
    }

    private static bool LooksLikeBossResponse(string text, BossState boss)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains(boss.CommandName, StringComparison.OrdinalIgnoreCase) ||
               text.Contains(boss.DisplayName, StringComparison.OrdinalIgnoreCase) ||
               IsNotFoundText(text) ||
               System.Text.RegularExpressions.Regex.IsMatch(
                   text,
                   "\\b(respawn|renasc|cooldown|tempo restante|tempo para)\\b",
                   System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static bool IsNotFoundText(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            text,
            @"boss\s+n(?:\u00e3o|ao)\s+encontrado",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string StripRichText(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", string.Empty).Trim();
    }

    private static bool IsDeadText(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            text,
            "\\b(morto|morta|dead|destru[ií]do|destru[ií]da)\\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static bool IsAvailableText(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            text,
            "\\b(dispon[ií]vel|available|alive|vivo|viva|renasceu|spawnado|liberado|up)\\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static bool TryParseTime(string text, out float seconds)
    {
        seconds = 0f;

        var clock = System.Text.RegularExpressions.Regex.Match(
            text,
            @"(?<!\d)(?<a>\d{1,3}):(?<b>\d{2})(?::(?<c>\d{2}))?(?!\d)");
        if (clock.Success)
        {
            var a = int.Parse(clock.Groups["a"].Value);
            var b = int.Parse(clock.Groups["b"].Value);
            var c = clock.Groups["c"].Success ? int.Parse(clock.Groups["c"].Value) : 0;
            seconds = clock.Groups["c"].Success ? a * 3600f + b * 60f + c : a * 60f + b;
            return true;
        }

        var unitMatches = System.Text.RegularExpressions.Regex.Matches(
            text,
            @"(?<value>\d+(?:[\.,]\d+)?)\s*(?<unit>d(?:ia|ias)?|h(?:ora|oras)?|m(?:in(?:uto|utos)?)?|s(?:eg(?:undo|undos)?)?)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in unitMatches)
        {
            var value = float.Parse(match.Groups["value"].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
            var unit = match.Groups["unit"].Value.ToLowerInvariant();
            seconds += unit.StartsWith("d") ? value * 86400f :
                        unit.StartsWith("h") ? value * 3600f :
                        unit.StartsWith("m") ? value * 60f : value;
        }

        return unitMatches.Count > 0;
    }

    private void OnGUI()
    {
        if (!Plugin.Enabled.Value || !Plugin.ShowStandaloneOverlay.Value)
        {
            return;
        }

        GUI.depth = -1000;
        EnsureStyles();

        var width = Mathf.Max(420f, Plugin.PanelWidth.Value);
        var height = Mathf.Clamp(Plugin.PanelHeight.Value, 220f, Mathf.Max(220f, Screen.height - 12f));
        const float toggleSize = 34f;
        const float gap = 6f;
        const float headerHeight = 34f;
        const float toolbarHeight = 68f;

        InitializePanelPosition(width, height, toggleSize, gap);
        ClampPanelPosition(width, height, toggleSize, gap);

        var panelRect = new Rect(_panelPosition.x, _panelPosition.y, width, height);
        HandlePanelDragging(panelRect, headerHeight);
        ClampPanelPosition(width, height, toggleSize, gap);

        var toggleRect = new Rect(panelRect.x + width + gap, panelRect.y, toggleSize, toggleSize);
        if (GUI.Button(toggleRect, _overlayShown ? "●" : "○", _toggleStyle))
        {
            _overlayShown = !_overlayShown;
        }

        if (!_overlayShown)
        {
            return;
        }

        GUI.Box(panelRect, GUIContent.none, _boxStyle);
        GUI.Label(
            new Rect(panelRect.x + panelRect.width - 72f, panelRect.y + 6f, 60f, headerHeight - 8f),
            _bosses.Count.ToString(),
            _labelStyle);
        GUI.Label(
            new Rect(panelRect.x + 12f, panelRect.y + 6f, panelRect.width - 92f, headerHeight - 8f),
            "<b>Bosses</b>  (arraste o cabeçalho)",
            _labelStyle);

        var visibleActs = GetVisibleActs();
        var toolbarY = panelRect.y + headerHeight;
        var toolbarX = panelRect.x + 8f;
        var toolbarWidth = panelRect.width - 16f;
        const float buttonGap = 4f;

        var hideButtonWidth = 112f;
        var actionButtonWidth = Mathf.Max(92f, (toolbarWidth - hideButtonWidth - buttonGap * 2f) / 2f);

        if (Plugin.ShowActs.Value && Plugin.ShowExpandButtons.Value)
        {
            if (GUI.Button(new Rect(toolbarX, toolbarY + 3f, actionButtonWidth, 26f), "Expandir todos"))
            {
                ExpandAllVisibleActs(visibleActs);
            }

            if (GUI.Button(new Rect(toolbarX + actionButtonWidth + buttonGap, toolbarY + 3f, actionButtonWidth, 26f), "Recolher todos"))
            {
                CollapseAllVisibleActs(visibleActs);
            }
        }

        var hideButtonX = panelRect.x + panelRect.width - 8f - hideButtonWidth;
        var hideButtonText = Plugin.ShowActs.Value ? "Esconder atos" : "Mostrar atos";
        if (GUI.Button(new Rect(hideButtonX, toolbarY + 3f, hideButtonWidth, 26f), hideButtonText))
        {
            Plugin.ShowActs.Value = !Plugin.ShowActs.Value;
            Plugin.Instance.Config.Save();
            _scrollPosition = Vector2.zero;
        }

        // Configuração do aviso diretamente no jogo.
        var settingsY = toolbarY + 35f;
        var enabledWidth = 104f;
        var warningWidth = 158f;
        var durationWidth = toolbarWidth - enabledWidth - warningWidth - buttonGap * 2f;

        var enabledText = Plugin.NotificationEnabled.Value ? "Aviso: LIGADO" : "Aviso: DESLIGADO";
        if (GUI.Button(new Rect(toolbarX, settingsY, enabledWidth, 26f), enabledText))
        {
            Plugin.NotificationEnabled.Value = !Plugin.NotificationEnabled.Value;
            Plugin.Instance.Config.Save();
            if (!Plugin.NotificationEnabled.Value)
            {
                _notificationText = string.Empty;
                _notificationVisibleUntil = 0f;
            }
        }

        var warningX = toolbarX + enabledWidth + buttonGap;
        if (GUI.Button(new Rect(warningX, settingsY, 28f, 26f), "-"))
        {
            Plugin.NotificationAtSeconds.Value = Mathf.Clamp(Plugin.NotificationAtSeconds.Value - 5, 5, 600);
            Plugin.Instance.Config.Save();
        }
        GUI.Label(new Rect(warningX + 30f, settingsY + 4f, warningWidth - 60f, 22f),
            $"Avisar: {Plugin.NotificationAtSeconds.Value}s", _labelStyle);
        if (GUI.Button(new Rect(warningX + warningWidth - 28f, settingsY, 28f, 26f), "+"))
        {
            Plugin.NotificationAtSeconds.Value = Mathf.Clamp(Plugin.NotificationAtSeconds.Value + 5, 5, 600);
            Plugin.Instance.Config.Save();
        }

        var durationX = warningX + warningWidth + buttonGap;
        if (GUI.Button(new Rect(durationX, settingsY, 28f, 26f), "-"))
        {
            Plugin.NotificationDurationSeconds.Value = Mathf.Clamp(Plugin.NotificationDurationSeconds.Value - 1f, 1f, 20f);
            Plugin.Instance.Config.Save();
        }
        GUI.Label(new Rect(durationX + 30f, settingsY + 4f, durationWidth - 60f, 22f),
            $"Tela: {Plugin.NotificationDurationSeconds.Value:0}s", _labelStyle);
        if (GUI.Button(new Rect(durationX + durationWidth - 28f, settingsY, 28f, 26f), "+"))
        {
            Plugin.NotificationDurationSeconds.Value = Mathf.Clamp(Plugin.NotificationDurationSeconds.Value + 1f, 1f, 20f);
            Plugin.Instance.Config.Save();
        }

        var viewport = new Rect(
            panelRect.x + 8f,
            panelRect.y + headerHeight + toolbarHeight,
            panelRect.width - 16f,
            panelRect.height - headerHeight - toolbarHeight - 8f);

        var rowHeight = Mathf.Max(22f, OverlayFontSize + 8f);
        const float sectionHeaderHeight = 30f;
        var pinnedBosses = _bosses
            .Where(boss => boss.IsPinned)
            .OrderBy(boss => boss.Index)
            .ToList();
        var actBosses = Enumerable.Range(1, 4)
            .Select(GetActBosses)
            .ToList();

        var contentHeight = 8f;
        if (pinnedBosses.Count > 0)
        {
            contentHeight += sectionHeaderHeight + pinnedBosses.Count * rowHeight;
        }
        else
        {
            contentHeight += sectionHeaderHeight;
        }

        if (Plugin.ShowActs.Value)
        {
            foreach (var act in visibleActs.OrderBy(act => act))
            {
                contentHeight += sectionHeaderHeight;
                if (_expandedActs[act - 1])
                {
                    contentHeight += actBosses[act - 1].Count * rowHeight;
                }
            }
        }

        contentHeight = Mathf.Max(viewport.height, contentHeight);
        var maxScrollY = Mathf.Max(0f, contentHeight - viewport.height);
        if (Event.current != null && Event.current.type == EventType.ScrollWheel && viewport.Contains(Event.current.mousePosition))
        {
            _scrollPosition.y = Mathf.Clamp(_scrollPosition.y + Event.current.delta.y * rowHeight, 0f, maxScrollY);
            Event.current.Use();
        }
        _scrollPosition.y = Mathf.Clamp(_scrollPosition.y, 0f, maxScrollY);

        GUI.BeginGroup(viewport);
        var rowWidth = viewport.width - 10f;
        var killButtonWidth = Mathf.Clamp(OverlayFontSize * 3.8f, 62f, 76f);
        var pinButtonWidth = Mathf.Clamp(OverlayFontSize * 2.8f, 48f, 60f);
        var cursorY = 4f - _scrollPosition.y;

        if (pinnedBosses.Count > 0)
        {
            GUI.Label(new Rect(4f, cursorY, rowWidth, sectionHeaderHeight), $"Fixados ({pinnedBosses.Count})", _sectionStyle);
            cursorY += sectionHeaderHeight;
            foreach (var boss in pinnedBosses)
            {
                DrawBossRow(boss, cursorY, rowWidth, rowHeight, killButtonWidth, pinButtonWidth);
                cursorY += rowHeight;
            }
        }
        else
        {
            GUI.Label(new Rect(4f, cursorY, rowWidth, sectionHeaderHeight), "Nenhum boss fixado", _sectionStyle);
            cursorY += sectionHeaderHeight;
        }

        if (Plugin.ShowActs.Value)
        {
            foreach (var act in visibleActs.OrderBy(act => act))
            {
                var actBossList = actBosses[act - 1];
                var actHeader = $"{(_expandedActs[act - 1] ? "[-]" : "[+]")} {GetActTitle(act)} ({actBossList.Count})";
                if (GUI.Button(new Rect(4f, cursorY, rowWidth, sectionHeaderHeight - 2f), actHeader, _sectionStyle))
                {
                    ToggleAct(act);
                }

                cursorY += sectionHeaderHeight;
                if (!_expandedActs[act - 1])
                {
                    continue;
                }

                foreach (var boss in actBossList)
                {
                    DrawBossRow(boss, cursorY, rowWidth, rowHeight, killButtonWidth, pinButtonWidth);
                    cursorY += rowHeight;
                }
            }
        }

        GUI.EndGroup();

        // Desenha por último para ficar acima do painel sem alterar sua cor.
        DrawCentralNotification();
    }

private void DrawCentralNotification()
{
    if (!Plugin.NotificationEnabled.Value ||
        string.IsNullOrWhiteSpace(_notificationText) ||
        Time.unscaledTime >= _notificationVisibleUntil)
    {
        return;
    }

    var previousColor = GUI.color;
    var previousDepth = GUI.depth;

    try
    {
        GUI.depth = -2000;
        GUI.color = Color.white;

        var fontSize = Mathf.Clamp(Plugin.NotificationFontSize.Value, 24, 64);
        _notificationStyle ??= new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            richText = false,
            wordWrap = true,
            fontStyle = FontStyle.Bold
        };
        _notificationShadowStyle ??= new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            richText = false,
            wordWrap = true,
            fontStyle = FontStyle.Bold
        };

        _notificationStyle.fontSize = fontSize;
        _notificationStyle.normal.textColor = new Color(0.95f, 0.08f, 0.08f, 1f);
        _notificationShadowStyle.fontSize = fontSize;
        _notificationShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.95f);

        var width = Mathf.Min(900f, Screen.width - 40f);
        const float height = 150f;
        var rect = new Rect(
            (Screen.width - width) / 2f,
            Screen.height * 0.36f,
            width,
            height);
        var shadowRect = new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height);

        GUI.Label(shadowRect, _notificationText, _notificationShadowStyle);
        GUI.Label(rect, _notificationText, _notificationStyle);
    }
    catch (Exception exception)
    {
        Plugin.Instance.Log.LogError($"Erro ao desenhar notificação: {exception}");
    }
    finally
    {
        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }
}

    private void InitializePanelPosition(float width, float height, float toggleSize, float gap)
    {
        if (_panelPositionInitialized)
        {
            return;
        }

        var defaultX = Screen.width - width - toggleSize - gap - Plugin.RightOffset.Value;
        var defaultY = Plugin.TopOffset.Value;
        _panelPosition = new Vector2(
            Plugin.PositionX.Value >= 0f ? Plugin.PositionX.Value : defaultX,
            Plugin.PositionY.Value >= 0f ? Plugin.PositionY.Value : defaultY);
        _panelPositionInitialized = true;
    }

    private void ClampPanelPosition(float width, float height, float toggleSize, float gap)
    {
        var maxX = Mathf.Max(4f, Screen.width - width - toggleSize - gap - 4f);
        var maxY = Mathf.Max(4f, Screen.height - 38f);
        _panelPosition.x = Mathf.Clamp(_panelPosition.x, 4f, maxX);
        _panelPosition.y = Mathf.Clamp(_panelPosition.y, 4f, maxY);
    }

    private void HandlePanelDragging(Rect panelRect, float headerHeight)
    {
        var currentEvent = Event.current;
        if (currentEvent == null || currentEvent.button != 0)
        {
            return;
        }

        if (currentEvent.type == EventType.MouseDown &&
            new Rect(panelRect.x, panelRect.y, panelRect.width, headerHeight).Contains(currentEvent.mousePosition))
        {
            _draggingPanel = true;
            _dragOffset = currentEvent.mousePosition - _panelPosition;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseDrag && _draggingPanel)
        {
            _panelPosition = currentEvent.mousePosition - _dragOffset;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp && _draggingPanel)
        {
            _draggingPanel = false;
            Plugin.PositionX.Value = _panelPosition.x;
            Plugin.PositionY.Value = _panelPosition.y;
            Plugin.Instance.Config.Save();
            currentEvent.Use();
        }
    }

    internal IReadOnlyList<BossRespawnSnapshot> GetPublicSnapshots()
    {
        return _bosses.Select(boss => new BossRespawnSnapshot(
            boss.DisplayName, boss.CommandName, boss.Level, boss.HasResponse,
            boss.IsAlive, boss.HasError, boss.IsPinned, boss.RemainingSeconds,
            ActiveBoss == boss)).ToArray();
    }

    internal bool SetPinnedFromCompanion(string nameOrCommand, bool pinned)
    {
        var boss = _bosses.FirstOrDefault(item =>
            item.DisplayName.Equals(nameOrCommand, StringComparison.OrdinalIgnoreCase) ||
            item.CommandName.Equals(nameOrCommand, StringComparison.OrdinalIgnoreCase));
        if (boss == null) return false;
        if (boss.IsPinned != pinned) TogglePinned(boss);
        return true;
    }

    internal bool ForceRefreshFromCompanion(string nameOrCommand)
    {
        var boss = _bosses.FirstOrDefault(item =>
            item.DisplayName.Equals(nameOrCommand, StringComparison.OrdinalIgnoreCase) ||
            item.CommandName.Equals(nameOrCommand, StringComparison.OrdinalIgnoreCase));
        if (boss == null) return false;
        _forcedBossIndex = boss.Index;
        if (ActiveBoss != null) CompleteActiveRequest();
        _nextQueryAt = Time.unscaledTime + 0.1f;
        return true;
    }

    private string GetBossStatusText(BossState boss)
    {
        if (!boss.HasResponse)
        {
            return ActiveBoss == boss ? "consultando..." : "aguardando...";
        }

        if (boss.IsAlive)
        {
            return "VIVO";
        }

        if (boss.HasError)
        {
            return "NAO ENCONTRADO";
        }

        if (boss.RemainingSeconds > 0.5f)
        {
            var remaining = TimeSpan.FromSeconds(Math.Ceiling(boss.RemainingSeconds));
            return remaining.TotalDays >= 1
                ? $"{(int)remaining.TotalDays}d {remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}"
                : $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }

        return "MORTO";
    }

    private void EnsureStyles()
    {
            if (_boxStyle != null &&
            _labelStyle != null &&
            _toggleStyle != null &&
            _killButtonStyle != null &&
            _pinButtonStyle != null &&
            _sectionStyle != null &&
            _notificationStyle != null &&
            _notificationShadowStyle != null)
        {
            _labelStyle.fontSize = OverlayFontSize;
            _toggleStyle.fontSize = OverlayFontSize + 4;
            _killButtonStyle.fontSize = Mathf.Max(10, OverlayFontSize - 2);
            _pinButtonStyle.fontSize = Mathf.Max(10, OverlayFontSize - 2);
            _sectionStyle.fontSize = OverlayFontSize;
            _notificationStyle.fontSize = Mathf.Clamp(
            Plugin.NotificationFontSize.Value,
            20,
            64);

            _notificationStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            richText = true,
            wordWrap = true,
            fontSize = Mathf.Clamp(
                Plugin.NotificationFontSize.Value,
                20,
                64),
            normal =
            {
                textColor = new Color(1f, 0.15f, 0.15f, 1f)
            }
        };

        _notificationShadowStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            richText = true,
            wordWrap = true,
            fontSize = Mathf.Clamp(
                Plugin.NotificationFontSize.Value,
                20,
                64),
            normal =
            {
                textColor = new Color(0f, 0f, 0f, 0.95f)
            }
        };

        _notificationShadowStyle.fontSize = Mathf.Clamp(
            Plugin.NotificationFontSize.Value,
            20,
            64);
            return;
        }

        _boxStyle = new GUIStyle
        {
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = new Color(1f, 1f, 1f, 0.92f) }
        };
        _labelStyle = new GUIStyle
        {
            alignment = TextAnchor.UpperLeft,
            richText = true,
            fontSize = OverlayFontSize,
            clipping = TextClipping.Clip,
            normal = { textColor = Color.white }
        };
        _toggleStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = OverlayFontSize + 4,
            normal = { textColor = new Color(0.35f, 0.85f, 1f, 0.95f) },
            hover = { textColor = Color.white },
            active = { textColor = new Color(0.2f, 0.65f, 0.85f, 1f) }
        };
        _killButtonStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Max(10, OverlayFontSize - 2),
            normal = { textColor = new Color(1f, 0.55f, 0.55f, 1f) },
            hover = { textColor = Color.white },
            active = { textColor = new Color(1f, 0.3f, 0.3f, 1f) }
        };
        _pinButtonStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Max(10, OverlayFontSize - 2),
            normal = { textColor = new Color(0.65f, 0.8f, 1f, 1f) },
            hover = { textColor = Color.white },
            active = { textColor = new Color(0.35f, 0.6f, 1f, 1f) }
        };
        _sectionStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = OverlayFontSize,
            normal = { textColor = new Color(0.95f, 0.85f, 0.45f, 1f) },
            hover = { textColor = Color.white },
            active = { textColor = new Color(1f, 0.95f, 0.55f, 1f) }
        };
    }
}


public sealed class BossRespawnSnapshot
{
    public BossRespawnSnapshot(string displayName, string commandName, int level, bool hasResponse, bool isAlive, bool hasError, bool isPinned, float remainingSeconds, bool isQuerying)
    { DisplayName=displayName; CommandName=commandName; Level=level; HasResponse=hasResponse; IsAlive=isAlive; HasError=hasError; IsPinned=isPinned; RemainingSeconds=remainingSeconds; IsQuerying=isQuerying; }
    public string DisplayName { get; }
    public string CommandName { get; }
    public int Level { get; }
    public bool HasResponse { get; }
    public bool IsAlive { get; }
    public bool HasError { get; }
    public bool IsPinned { get; }
    public float RemainingSeconds { get; }
    public bool IsQuerying { get; }
}

public static class BossRespawnApi
{
    public static bool IsReady => Plugin.Behaviour != null;

    public static IReadOnlyList<BossRespawnSnapshot> GetBosses()
    {
        var live = Plugin.Behaviour?.GetPublicSnapshots();
        if (live != null && live.Count > 0)
        {
            return live;
        }

        // A lista deve aparecer imediatamente no Companion, mesmo durante os
        // primeiros quadros de inicialização do bridge ou antes de entrar no mundo.
        return Plugin.DefaultBosses
            .Select(boss => new BossRespawnSnapshot(
                boss.DisplayName,
                boss.CommandName,
                boss.Level,
                false,
                false,
                false,
                false,
                0f,
                false))
            .ToArray();
    }

    public static bool SetPinned(string nameOrCommand, bool pinned) => Plugin.Behaviour?.SetPinnedFromCompanion(nameOrCommand, pinned) ?? false;
    public static bool Refresh(string nameOrCommand) => Plugin.Behaviour?.ForceRefreshFromCompanion(nameOrCommand) ?? false;
}

[HarmonyPatch(typeof(ClientChatSystem), nameof(ClientChatSystem.OnUpdate))]
internal static class ClientChatSystemPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(ClientChatSystem __instance)
    {
        BossRespawnOverlayBehaviour.Instance?.HandleChatUpdate(__instance);
    }
}

[HarmonyPatch(typeof(ClientChatSystem), "ParseCommand")]
internal static class ClientChatCommandPatch
{
    [HarmonyPrefix]
    private static void Prefix(string text)
    {
        BossRespawnOverlayBehaviour.Instance?.NotifyManualChatCommand(text);
    }
}
