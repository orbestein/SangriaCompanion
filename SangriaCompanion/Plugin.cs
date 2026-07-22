using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SangriaCompanion;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("lucas.vrising.bossrespawnoverlay", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("lucas.vrising.sangriaeventalerts", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "alvaro.vrising.sangriacompanion";
    public const string PluginName = "Sangria Companion";
    public const string PluginVersion = "2.2.0";

    internal static Plugin Instance { get; private set; } = null!;
    internal static CompanionBehaviour Behaviour { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> PanelVisible { get; private set; } = null!;
    internal static ConfigEntry<float> PanelX { get; private set; } = null!;
    internal static ConfigEntry<float> PanelY { get; private set; } = null!;
    internal static ConfigEntry<int> FontSize { get; private set; } = null!;
    internal static ConfigEntry<float> UiScale { get; private set; } = null!;
    internal static ConfigEntry<float> UiOpacity { get; private set; } = null!;

    internal static ConfigEntry<string> FavoriteBosses { get; private set; } = null!;
    internal static ConfigEntry<string> AlertBosses { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowOnlyFavorites { get; private set; } = null!;
    internal static ConfigEntry<bool> BossAlertsEnabled { get; private set; } = null!;
    internal static ConfigEntry<int> BossAlertSeconds { get; private set; } = null!;
    internal static ConfigEntry<float> BossAlertDuration { get; private set; } = null!;
    internal static ConfigEntry<bool> FavoriteBossHudEnabled { get; private set; } = null!;
    internal static ConfigEntry<float> FavoriteBossHudX { get; private set; } = null!;
    internal static ConfigEntry<float> FavoriteBossHudY { get; private set; } = null!;
    internal static ConfigEntry<string> TrackedBossCommand { get; private set; } = null!;
    internal static ConfigEntry<bool> TrackerHudEnabled { get; private set; } = null!;
    internal static ConfigEntry<float> TrackerHudX { get; private set; } = null!;
    internal static ConfigEntry<float> TrackerHudY { get; private set; } = null!;
    internal static ConfigEntry<float> TrackerRefreshInterval { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> TrackerRefreshKey { get; private set; } = null!;

    internal static ConfigEntry<bool> EventAlertsEnabled { get; private set; } = null!;
    internal static ConfigEntry<string> EventAlertsSelected { get; private set; } = null!;
    internal static ConfigEntry<float> EventAlertDuration { get; private set; } = null!;
    internal static ConfigEntry<bool> AlwaysShowNotifications { get; private set; } = null!;

    internal static ConfigEntry<bool> DashboardAlive { get; private set; } = null!;
    internal static ConfigEntry<bool> DashboardDead { get; private set; } = null!;
    internal static ConfigEntry<bool> DashboardFavorites { get; private set; } = null!;
    internal static ConfigEntry<bool> DashboardEventsActive { get; private set; } = null!;
    internal static ConfigEntry<bool> DashboardNextEvent { get; private set; } = null!;
    internal static ConfigEntry<bool> DashboardFavoriteBosses { get; private set; } = null!;

    internal static ConfigEntry<string> CollectionQuantities { get; private set; } = null!;
    internal static ConfigEntry<string> CollectionHistory { get; private set; } = null!;
    internal static ConfigEntry<string> SelectedRecipe { get; private set; } = null!;
    internal static ConfigEntry<int> DesiredRecipeQuantity { get; private set; } = null!;
    internal static ConfigEntry<bool> CollectionHudEnabled { get; private set; } = null!;
    internal static ConfigEntry<float> CollectionHudX { get; private set; } = null!;
    internal static ConfigEntry<float> CollectionHudY { get; private set; } = null!;

    private Harmony? _harmony;
    private GameObject? _host;
    private EmergencyLauncherBehaviour? _emergencyLauncher;

    public override void Load()
    {
        Instance = this;

        Enabled = Config.Bind("General", "Enabled", true, "Ativa o Sangria Companion.");
        PanelVisible = Config.Bind("UI", "PanelVisible", true, "Define se o painel inicia aberto.");
        PanelX = Config.Bind("UI", "PanelX", 28f, "Posição horizontal do painel.");
        PanelY = Config.Bind("UI", "PanelY", 92f, "Posição vertical do painel.");
        FontSize = Config.Bind("UI", "FontSize", 14, "Tamanho-base da fonte.");
        UiScale = Config.Bind("UI", "Scale", 1f, "Escala da interface entre 0.8 e 1.25.");
        UiOpacity = Config.Bind("UI", "Opacity", 0.94f, "Opacidade do painel entre 0.55 e 1.0.");

        FavoriteBosses = Config.Bind("Bosses", "Favorites", string.Empty, "Bosses favoritos, separados por vírgula.");
        AlertBosses = Config.Bind("Bosses", "Alerts", string.Empty, "Bosses com aviso individual ativado, separados por vírgula.");
        ShowOnlyFavorites = Config.Bind("Bosses", "ShowOnlyFavorites", false, "Mostra somente bosses favoritos.");
        BossAlertsEnabled = Config.Bind("Bosses", "AlertsEnabled", true, "Ativa os avisos globais de bosses.");
        BossAlertSeconds = Config.Bind("Bosses", "AlertAtSeconds", 20, "Segundos restantes para exibir o aviso.");
        BossAlertDuration = Config.Bind("Bosses", "AlertDurationSeconds", 5f, "Duração do aviso na tela.");
        FavoriteBossHudEnabled = Config.Bind("Bosses", "FavoriteHudEnabled", true, "Exibe uma HUD pequena somente com os bosses favoritos.");
        FavoriteBossHudX = Config.Bind("Bosses", "FavoriteHudX", 72f, "Posição horizontal da HUD de bosses favoritos.");
        FavoriteBossHudY = Config.Bind("Bosses", "FavoriteHudY", 118f, "Posição vertical da HUD de bosses favoritos.");
        TrackedBossCommand = Config.Bind("Bosses", "TrackedBoss", string.Empty, "Boss selecionado no rastreador do Companion.");
        TrackerHudEnabled = Config.Bind("Tracker", "CompactHudEnabled", true, "Exibe a HUD compacta do boss acompanhado fora da janela principal.");
        TrackerHudX = Config.Bind("Tracker", "CompactHudX", 74f, "Posição horizontal da HUD compacta.");
        TrackerHudY = Config.Bind("Tracker", "CompactHudY", 116f, "Posição vertical da HUD compacta.");
        TrackerRefreshInterval = Config.Bind("Tracker", "RefreshIntervalSeconds", 20f, "Intervalo automático de atualização do boss acompanhado, entre 10 e 120 segundos.");
        TrackerRefreshKey = Config.Bind("Tracker", "RefreshKey", KeyCode.F8, "Tecla para consultar imediatamente o boss acompanhado.");

        EventAlertsEnabled = Config.Bind("Events", "AlertsEnabled", true, "Ativa avisos de eventos na tela.");
        EventAlertsSelected = Config.Bind("Events", "SelectedAlerts", "Chefe Supremo,Dantos Sangrentum,Piracema,Investida Cientifica / Invasao", "Eventos com alertas ativos, separados por vírgula.");
        EventAlertDuration = Config.Bind("Events", "AlertDurationSeconds", 6f, "Duração do aviso de evento na tela.");
        AlwaysShowNotifications = Config.Bind("Notifications", "AlwaysShow", true, "Mantém os avisos visíveis mesmo com o painel e as HUDs compactas fechados.");

        DashboardAlive = Config.Bind("Dashboard", "ShowAliveBosses", true, "Exibe bosses vivos.");
        DashboardDead = Config.Bind("Dashboard", "ShowDeadBosses", true, "Exibe bosses mortos.");
        DashboardFavorites = Config.Bind("Dashboard", "ShowFavoriteCount", true, "Exibe quantidade de favoritos.");
        DashboardEventsActive = Config.Bind("Dashboard", "ShowActiveEvents", true, "Exibe eventos ativos.");
        DashboardNextEvent = Config.Bind("Dashboard", "ShowNextEvent", true, "Exibe o próximo evento.");
        DashboardFavoriteBosses = Config.Bind("Dashboard", "ShowFavoriteBosses", true, "Exibe a lista de bosses favoritos.");

        CollectionQuantities = Config.Bind("Collection", "Quantities", string.Empty, "Quantidades registradas por item.");
        CollectionHistory = Config.Bind("Collection", "History", string.Empty, "Histórico recente de coleta.");
        SelectedRecipe = Config.Bind("Collection", "SelectedRecipe", "Núcleo de Energia", "Receita selecionada no planejador.");
        DesiredRecipeQuantity = Config.Bind("Collection", "DesiredQuantity", 1, "Quantidade desejada da receita selecionada.");
        CollectionHudEnabled = Config.Bind("Collection", "CompactHudEnabled", true, "Exibe o progresso da receita fora da janela principal.");
        CollectionHudX = Config.Bind("Collection", "CompactHudX", 430f, "Posição horizontal da HUD de coleta.");
        CollectionHudY = Config.Bind("Collection", "CompactHudY", 116f, "Posição vertical da HUD de coleta.");

        ClassInjector.RegisterTypeInIl2Cpp<CompanionBehaviour>();
        ClassInjector.RegisterTypeInIl2Cpp<EmergencyLauncherBehaviour>();

        _harmony = new Harmony(PluginGuid);
        try
        {
            _harmony.PatchAll(typeof(Plugin).Assembly);
        }
        catch (Exception ex)
        {
            // Um patch opcional nunca deve impedir a criação da HUD.
            Log.LogWarning("Alguns patches opcionais não foram aplicados: " + ex.Message);
        }

        try
        {
            InputSystemSuppressionPatches.Apply(_harmony);
        }
        catch (Exception ex)
        {
            Log.LogWarning("O bloqueio adicional do novo Input System não pôde ser aplicado: " + ex.Message);
        }

        try
        {
            GameplayInputSuppressionPatches.Apply(_harmony);
        }
        catch (Exception ex)
        {
            Log.LogWarning("O bloqueio interno de gameplay não pôde ser aplicado: " + ex.Message);
        }

        _host = new GameObject("SangriaCompanionHost");
        _host.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(_host);

        // O launcher de emergência é criado primeiro e usa somente GUI nativa.
        // Assim o botão SC continua disponível mesmo que algum módulo novo falhe.
        _emergencyLauncher = _host.AddComponent<EmergencyLauncherBehaviour>();
        TryCreateBehaviour();

        Log.LogInfo($"{PluginName} {PluginVersion} carregado. Host={_host != null}, HUD={Behaviour != null}.");
    }

    internal static bool HasBehaviour => Behaviour != null;

    internal void TryCreateBehaviour()
    {
        if (_host == null || Behaviour != null) return;

        try
        {
            Behaviour = _host.AddComponent<CompanionBehaviour>();
            Log.LogInfo("CompanionBehaviour criado com sucesso.");
        }
        catch (Exception ex)
        {
            Behaviour = null!;
            Log.LogError("Falha ao criar a HUD principal: " + ex);
        }
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        ClientWorldService.Clear();

        if (_host != null)
        {
            Object.Destroy(_host);
            _host = null;
        }

        Behaviour = null!;
        return true;
    }

    internal static void SaveState() => Instance.Config.Save();
}
