using UnityEngine;

namespace SangriaCompanion;

internal sealed class CompanionBehaviour : MonoBehaviour
{
    internal static float LastGuiHeartbeat { get; private set; } = -999f;
    internal bool IsInitialized { get; private set; }
    private const float BaseWidth = 820f;
    private const float StandardBaseHeight = 580f;
    private const float CompactDashboardHeight = 410f;
    private float _effectiveScale = 1f;
    private readonly CompanionState _state = new();
    private readonly SCStyles _styles = new();
    private readonly DashboardModule _dashboard = new();
    private readonly BossModule _bosses = new();
    private readonly EventModule _events = new();
    private readonly SessionDropsModule _sessionDrops = new();
    private readonly CollectionModule _collection = new();
    private readonly SettingsModule _settings = new();

    private bool _dragging;
    private Vector2 _dragOffset;
    private float _smoothedFps = 60f;
    private float _nextBossRefresh;
    private bool _collectionHudDragging;
    private Vector2 _collectionHudDragOffset;
    private bool _favoriteBossHudDragging;
    private Vector2 _favoriteBossHudDragOffset;
    private bool _eventHudDragging;
    private Vector2 _eventHudDragOffset;

    private void Awake()
    {
        try
        {
        _state.PanelVisible = Plugin.PanelVisible.Value;
        var scale = Mathf.Clamp(Plugin.UiScale.Value, 0.8f, 1.25f);
        _effectiveScale = CalculateEffectiveScale(scale);
        _state.PanelRect = new Rect(Plugin.PanelX.Value, Plugin.PanelY.Value, BaseWidth * _effectiveScale, GetBaseHeight() * _effectiveScale);
        BossPreferenceService.Load();
        CollectionService.Load();
        SessionDropService.StartNewSession();
        Plugin.Instance.Log.LogInfo("Sangria Companion 2.6.1 inicializado: pets e almas da sessão separados por atos.");
                IsInitialized = true;
            Plugin.Instance.Log.LogInfo("CompanionBehaviour.Awake concluído.");
        }
        catch (Exception ex)
        {
            IsInitialized = false;
            Plugin.Instance.Log.LogError("Falha durante CompanionBehaviour.Awake: " + ex);
        }
    }

    private void Update()
    {
        InputBlockService.SetPanelOpen(_state.PanelVisible);
        InputBlockService.SetTextEntryActive(_state.BossSearchFocused || _state.ItemSearchFocused || _state.RecipeSearchFocused);
        ModuleRuntime.Run("Entrada", InputBlockService.Update);
        ModuleRuntime.Run("Mundo do cliente", ClientWorldService.Update);

        var current = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        _smoothedFps = Mathf.Lerp(_smoothedFps, current, 0.08f);
        if (Time.unscaledTime >= _nextBossRefresh)
        {
            ModuleRuntime.Run("Bosses", BossCatalog.Refresh);
            _nextBossRefresh = Time.unscaledTime + 0.5f;
        }

        ModuleRuntime.Run("Alertas de eventos", EventNotificationService.Update);
        ModuleRuntime.Run("Alertas de bosses", BossNotificationService.Update);
        ModuleRuntime.Run("Receitas reais", RecipeDiscoveryService.Update);
        ModuleRuntime.Run("Inventário", InventorySyncService.Update);
        ModuleRuntime.Run("Notificações", NotificationCenter.Update);
    }

    private void OnGUI()
    {
        InputBlockService.BeginGuiFrame();
        LastGuiHeartbeat = Time.unscaledTime;
        ApplyScaleImmediately();
        _styles.EnsureCreated(1f);
        DrawLauncher();
        if (_state.PanelVisible)
        {
            InputBlockService.RegisterScreenArea(_state.PanelRect);
            InputBlockService.ObservePointer(_state.PanelRect);
        }
        if (Plugin.AlwaysShowNotifications.Value || _state.PanelVisible || Plugin.CollectionHudEnabled.Value)
            ModuleRuntime.Run("Notificações visuais", () => NotificationCenter.Draw(_styles));
        ModuleRuntime.Run("HUD de bosses favoritos", DrawFavoriteBossHud);
        ModuleRuntime.Run("HUD de eventos", DrawEventHud);
        ModuleRuntime.Run("HUD de coleta", DrawCollectionHud);
        if (!_state.PanelVisible)
        {
            return;
        }

        KeepInsideScreen();
        HandleDragging();
        DrawWindow();
    }

    private void ApplyScaleImmediately()
    {
        var requested = Mathf.Clamp(Plugin.UiScale.Value, 0.8f, 1.25f);
        _effectiveScale = CalculateEffectiveScale(requested);
        var panel = _state.PanelRect;
        panel.width = BaseWidth * _effectiveScale;
        panel.height = GetBaseHeight() * _effectiveScale;
        _state.PanelRect = panel;
    }

    private static float CalculateEffectiveScale(float requested)
    {
        var fitWidth = Mathf.Max(0.5f, (Screen.width - 20f) / BaseWidth);
        var fitHeight = Mathf.Max(0.5f, (Screen.height - 20f) / StandardBaseHeight);
        return Mathf.Min(requested, fitWidth, fitHeight);
    }

    private float GetBaseHeight()
    {
        if (_state.Page != CompanionPage.Dashboard) return StandardBaseHeight;

        var metricCount = 0;
        if (Plugin.DashboardAlive.Value) metricCount++;
        if (Plugin.DashboardDead.Value) metricCount++;
        if (Plugin.DashboardFavorites.Value) metricCount++;
        if (Plugin.DashboardEventsActive.Value) metricCount++;

        var onlyFavoriteList = metricCount == 0 &&
                               !Plugin.DashboardNextEvent.Value &&
                               Plugin.DashboardFavoriteBosses.Value;

        return onlyFavoriteList ? CompactDashboardHeight : StandardBaseHeight;
    }

    private void DrawLauncher()
    {
        var rect = new Rect(12f, 70f, 46f, 30f);
        InputBlockService.RegisterScreenArea(rect);
        InputBlockService.ObservePointer(rect);
        SCUI.Panel(rect, SCTheme.Backdrop, SCTheme.GoldSoft);
        if (SCUI.Button(rect, _state.PanelVisible ? "SC ×" : "SC", _styles.Button))
        {
            SetVisibility(!_state.PanelVisible);
        }
    }

    private void DrawWindow()
    {
        var screenPanel = _state.PanelRect;
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(
            new Vector3(screenPanel.x, screenPanel.y, 0f),
            Quaternion.identity,
            new Vector3(_effectiveScale, _effectiveScale, 1f));

        try
        {
            var panel = new Rect(0f, 0f, BaseWidth, GetBaseHeight());
            var opacity = Mathf.Clamp(Plugin.UiOpacity.Value, 0.55f, 1f);
            var background = SCTheme.Backdrop;
            background.a = opacity;
            SCUI.Panel(panel, background, SCTheme.Border, 2f);

            var header = new Rect(0f, 0f, panel.width, 58f);
            SCTheme.Fill(header, new Color(SCTheme.Header.r, SCTheme.Header.g, SCTheme.Header.b, opacity));
            SCTheme.Fill(new Rect(header.x, header.yMax - 1f, header.width, 1f), SCTheme.GoldSoft);
            SCUI.Label(new Rect(22f, 10f, 390f, 36f), "SANGRIA COMPANION", _styles.Title);
    
            if (SCUI.Button(new Rect(panel.xMax - 42f, 14f, 26f, 26f), "×", _styles.Button, true))
            {
                SetVisibility(false);
                return;
            }

            var sidebar = new Rect(0f, 58f, 176f, panel.height - 58f);
            SCTheme.Fill(sidebar, new Color(SCTheme.Sidebar.r, SCTheme.Sidebar.g, SCTheme.Sidebar.b, opacity));
            SCTheme.Fill(new Rect(sidebar.xMax - 1f, sidebar.y, 1f, sidebar.height), SCTheme.Border);
            DrawSidebar(sidebar);

            var mainX = sidebar.xMax;
            var statusBar = new Rect(mainX, 58f, panel.width - sidebar.width, 38f);
            DrawStatusBar(statusBar);

            var content = new Rect(mainX + 18f, statusBar.yMax + 10f, panel.width - sidebar.width - 36f, panel.height - 116f);
            switch (_state.Page)
            {
                case CompanionPage.Dashboard: _dashboard.Draw(content, _styles); break;
                case CompanionPage.Bosses: _bosses.Draw(content, _state, _styles); break;
                case CompanionPage.Events: _events.Draw(content, _styles); break;
                case CompanionPage.SessionDrops: _sessionDrops.Draw(content, _state, _styles); break;
                case CompanionPage.Collection: _collection.Draw(content, _state, _styles); break;
                case CompanionPage.Settings: _settings.Draw(content, _styles); break;
            }
        }
        finally
        {
            GUI.matrix = oldMatrix;
        }
    }

    private void DrawStatusBar(Rect rect)
    {
        SCTheme.Fill(rect, SCTheme.Panel);
        SCTheme.Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), SCTheme.BorderSoft);

        DrawStatusItem(rect.x + 18f, rect.y, 155f, "SERVIDOR", EventScheduleService.IsSynchronized ? $"Sangria {EventScheduleService.ServerTime.ToString(@"hh\:mm\:ss")}" : "Sincronizando", SCTheme.Gold);
        DrawStatusItem(rect.x + 180f, rect.y, 105f, "PING", "-- ms", SCTheme.Muted);
        DrawStatusItem(rect.x + 292f, rect.y, 105f, "FPS", Mathf.RoundToInt(_smoothedFps).ToString(), SCTheme.Green);
        DrawStatusItem(rect.x + 404f, rect.y, rect.width - 414f, "STATUS", "Conectado", SCTheme.Green);
    }

    private void DrawStatusItem(float x, float y, float width, string caption, string value, Color valueColor)
    {
        SCUI.Label(new Rect(x, y + 3f, width, 14f), caption, _styles.Tiny);
        var old = _styles.Label.normal.textColor;
        _styles.Label.normal.textColor = valueColor;
        SCUI.Label(new Rect(x, y + 15f, width, 20f), value, _styles.Label);
        _styles.Label.normal.textColor = old;
    }

    private void DrawSidebar(Rect rect)
    {
        var compact = _state.Page == CompanionPage.Dashboard && GetBaseHeight() < StandardBaseHeight;
        var y = rect.y + (compact ? 14f : 22f);
        var itemHeight = compact ? 31f : 40f;
        var step = compact ? 35f : 47f;

        DrawSidebarButton(new Rect(rect.x + 10f, y, rect.width - 20f, itemHeight), CompanionPage.Dashboard, "◆   Dashboard"); y += step;
        DrawSidebarButton(new Rect(rect.x + 10f, y, rect.width - 20f, itemHeight), CompanionPage.Bosses, "◇   Bosses"); y += step;
        DrawSidebarButton(new Rect(rect.x + 10f, y, rect.width - 20f, itemHeight), CompanionPage.Events, "◇   Eventos"); y += step;
        DrawSidebarButton(new Rect(rect.x + 10f, y, rect.width - 20f, itemHeight), CompanionPage.SessionDrops, "◇   Pets e Almas"); y += step;
        DrawSidebarButton(new Rect(rect.x + 10f, y, rect.width - 20f, itemHeight), CompanionPage.Collection, "◇   Coleta e Receitas"); y += step;
        DrawSidebarButton(new Rect(rect.x + 10f, y, rect.width - 20f, itemHeight), CompanionPage.Settings, "◇   Configurações");

        var footerHeight = compact ? 50f : 66f;
        var footer = new Rect(rect.x + 12f, rect.yMax - footerHeight - 10f, rect.width - 24f, footerHeight);
        SCTheme.Fill(new Rect(footer.x, footer.y, footer.width, 1f), SCTheme.BorderSoft);
        SCTheme.Fill(new Rect(footer.x, footer.y + 17f, 7f, 7f), SCTheme.Green);
        SCUI.Label(new Rect(footer.x + 15f, footer.y + 9f, footer.width - 15f, 22f), "Conectado", _styles.Green);
        SCUI.Label(new Rect(footer.x, footer.y + 31f, footer.width, 18f), "Sangria Falls", _styles.Tiny);
    }

    private void DrawSidebarButton(Rect rect, CompanionPage page, string text)
    {
        var active = _state.Page == page;
        var hovered = rect.Contains(Event.current.mousePosition);
        if (active || hovered)
        {
            SCTheme.Fill(rect, active ? SCTheme.PanelAlt : SCTheme.Panel);
        }
        if (active)
        {
            SCTheme.Fill(new Rect(rect.x, rect.y + 5f, 3f, rect.height - 10f), SCTheme.Blood);
            SCTheme.BorderRect(rect, SCTheme.BorderSoft);
        }

        if (SCUI.Button(rect, text, active ? _styles.SidebarActive : _styles.SidebarButton))
        {
            _state.Page = page;
        }
    }


    private void DrawFavoriteBossHud()
    {
        if (!Plugin.FavoriteBossHudEnabled.Value) return;

        BossCatalog.Refresh();
        var favorites = BossCatalog.All
            .Where(x => x.IsFavorite)
            .ToList();
        if (favorites.Count == 0) return;

        const float baseWidth = 300f;
        const int maxVisible = 8;
        var visibleCount = Mathf.Min(maxVisible, favorites.Count);
        var hasMore = favorites.Count > maxVisible;
        var baseHeight = 38f + visibleCount * 27f + (hasMore ? 23f : 8f);

        var requestedScale = Mathf.Clamp(Plugin.UiScale.Value, 0.8f, 1.25f);
        var fitScale = Mathf.Min(
            Mathf.Max(0.62f, (Screen.width - 12f) / baseWidth),
            Mathf.Max(0.62f, (Screen.height - 12f) / baseHeight));
        var scale = Mathf.Min(requestedScale, fitScale);
        var width = baseWidth * scale;
        var height = baseHeight * scale;

        var rect = new Rect(Plugin.FavoriteBossHudX.Value, Plugin.FavoriteBossHudY.Value, width, height);
        rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
        rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
        Plugin.FavoriteBossHudX.Value = rect.x;
        Plugin.FavoriteBossHudY.Value = rect.y;

        InputBlockService.RegisterScreenArea(rect);
        InputBlockService.ObservePointer(rect);
        var current = Event.current;
        var closeRect = new Rect(rect.xMax - 30f * scale, rect.y + 3f * scale, 24f * scale, 24f * scale);
        if (current.type == EventType.MouseDown && current.button == 0 && closeRect.Contains(current.mousePosition))
        {
            Plugin.FavoriteBossHudEnabled.Value = false;
            Plugin.SaveState();
            current.Use();
            return;
        }

        var dragBar = new Rect(rect.x, rect.y, rect.width - 38f * scale, 31f * scale);
        if (current.type == EventType.MouseDown && current.button == 0 && dragBar.Contains(current.mousePosition))
        {
            _favoriteBossHudDragging = true;
            _favoriteBossHudDragOffset = current.mousePosition - new Vector2(rect.x, rect.y);
            current.Use();
        }
        else if (_favoriteBossHudDragging && current.type == EventType.MouseDrag)
        {
            Plugin.FavoriteBossHudX.Value = Mathf.Clamp(current.mousePosition.x - _favoriteBossHudDragOffset.x, 0f, Mathf.Max(0f, Screen.width - width));
            Plugin.FavoriteBossHudY.Value = Mathf.Clamp(current.mousePosition.y - _favoriteBossHudDragOffset.y, 0f, Mathf.Max(0f, Screen.height - height));
            current.Use();
        }
        else if (_favoriteBossHudDragging && current.type == EventType.MouseUp)
        {
            _favoriteBossHudDragging = false;
            Plugin.SaveState();
            current.Use();
        }

        rect.x = Plugin.FavoriteBossHudX.Value;
        rect.y = Plugin.FavoriteBossHudY.Value;
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3(rect.x, rect.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        try
        {
            var local = new Rect(0f, 0f, baseWidth, baseHeight);
            var opacity = Mathf.Clamp(Plugin.UiOpacity.Value, 0.55f, 1f);
            var background = SCTheme.Backdrop;
            background.a = opacity;
            SCUI.Panel(local, background, SCTheme.BorderSoft, 1f);
            SCTheme.Fill(new Rect(0f, 0f, baseWidth, 31f), new Color(SCTheme.Header.r, SCTheme.Header.g, SCTheme.Header.b, opacity));
            SCTheme.Fill(new Rect(0f, 30f, baseWidth, 1f), SCTheme.BorderSoft);
            SCUI.Label(new Rect(11f, 4f, baseWidth - 48f, 23f), "BOSSES FAVORITOS", _styles.Gold);
            SCUI.Button(new Rect(baseWidth - 30f, 4f, 24f, 23f), "×", _styles.Button, true);

            var y = 36f;
            foreach (var boss in favorites.Take(maxVisible))
            {
                var row = new Rect(8f, y, baseWidth - 16f, 23f);
                if (((int)((y - 36f) / 27f)) % 2 == 1) SCTheme.Fill(row, SCTheme.Panel);

                SCUI.Label(new Rect(13f, y + 1f, 164f, 21f), "★ " + boss.Name, _styles.Label);

                string status;
                GUIStyle statusStyle;
                if (!boss.TryGetConfirmedDisplay(out var confirmedStatus, out var confirmedRemaining))
                {
                    status = "AGUARDANDO";
                    statusStyle = _styles.Muted;
                }
                else if (confirmedStatus == CompanionBossStatus.Alive)
                {
                    status = "VIVO";
                    statusStyle = _styles.Green;
                }
                else
                {
                    status = "MORTO • " + SCUI.FormatTime(confirmedRemaining);
                    statusStyle = _styles.Blood;
                }

                statusStyle.alignment = TextAnchor.MiddleRight;
                SCUI.Label(new Rect(176f, y + 1f, 109f, 21f), status, statusStyle);
                statusStyle.alignment = TextAnchor.MiddleLeft;
                y += 27f;
            }

            if (hasMore)
                SCUI.Label(new Rect(12f, y, baseWidth - 24f, 20f), "+ " + (favorites.Count - maxVisible) + " favorito(s) no painel principal", _styles.Tiny);
        }
        finally
        {
            GUI.matrix = oldMatrix;
        }
    }


    private void DrawEventHud()
    {
        if (!Plugin.EventHudEnabled.Value) return;

        var upcoming = EventScheduleService.GetUpcoming(3);
        if (upcoming.Count == 0) return;

        const float baseWidth = 340f;
        const float baseHeight = 132f;
        var requestedScale = Mathf.Clamp(Plugin.UiScale.Value, 0.8f, 1.25f);
        var fitScale = Mathf.Min(
            Mathf.Max(0.62f, (Screen.width - 12f) / baseWidth),
            Mathf.Max(0.62f, (Screen.height - 12f) / baseHeight));
        var scale = Mathf.Min(requestedScale, fitScale);
        var width = baseWidth * scale;
        var height = baseHeight * scale;

        var rect = new Rect(Plugin.EventHudX.Value, Plugin.EventHudY.Value, width, height);
        rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
        rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
        Plugin.EventHudX.Value = rect.x;
        Plugin.EventHudY.Value = rect.y;

        InputBlockService.RegisterScreenArea(rect);
        InputBlockService.ObservePointer(rect);
        var current = Event.current;
        var closeRect = new Rect(rect.xMax - 30f * scale, rect.y + 3f * scale, 24f * scale, 24f * scale);
        if (current.type == EventType.MouseDown && current.button == 0 && closeRect.Contains(current.mousePosition))
        {
            Plugin.EventHudEnabled.Value = false;
            Plugin.SaveState();
            current.Use();
            return;
        }

        var dragBar = new Rect(rect.x, rect.y, rect.width - 38f * scale, 31f * scale);
        if (current.type == EventType.MouseDown && current.button == 0 && dragBar.Contains(current.mousePosition))
        {
            _eventHudDragging = true;
            _eventHudDragOffset = current.mousePosition - new Vector2(rect.x, rect.y);
            current.Use();
        }
        else if (_eventHudDragging && current.type == EventType.MouseDrag)
        {
            Plugin.EventHudX.Value = Mathf.Clamp(current.mousePosition.x - _eventHudDragOffset.x, 0f, Mathf.Max(0f, Screen.width - width));
            Plugin.EventHudY.Value = Mathf.Clamp(current.mousePosition.y - _eventHudDragOffset.y, 0f, Mathf.Max(0f, Screen.height - height));
            current.Use();
        }
        else if (_eventHudDragging && current.type == EventType.MouseUp)
        {
            _eventHudDragging = false;
            Plugin.SaveState();
            current.Use();
        }

        rect.x = Plugin.EventHudX.Value;
        rect.y = Plugin.EventHudY.Value;
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3(rect.x, rect.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        try
        {
            var local = new Rect(0f, 0f, baseWidth, baseHeight);
            var opacity = Mathf.Clamp(Plugin.UiOpacity.Value, 0.55f, 1f);
            var background = SCTheme.Backdrop;
            background.a = opacity;
            SCUI.Panel(local, background, SCTheme.BorderSoft, 1f);
            SCTheme.Fill(new Rect(0f, 0f, baseWidth, 31f), new Color(SCTheme.Header.r, SCTheme.Header.g, SCTheme.Header.b, opacity));
            SCTheme.Fill(new Rect(0f, 30f, baseWidth, 1f), SCTheme.BorderSoft);
            SCUI.Label(new Rect(11f, 4f, baseWidth - 48f, 23f), "EVENTOS", _styles.Gold);
            SCUI.Button(new Rect(baseWidth - 30f, 4f, 24f, 23f), "×", _styles.Button, true);

            var y = 37f;
            foreach (var item in upcoming.Take(3))
            {
                var active = item.IsActive;
                var nameStyle = active ? _styles.Green : _styles.Label;
                var countdown = active
                    ? "ATIVO • " + EventScheduleService.FormatCountdown(item.UntilEnd)
                    : EventScheduleService.FormatCountdown(item.UntilStart);
                SCUI.Label(new Rect(12f, y, 210f, 24f), item.Name, nameStyle);
                var countdownStyle = active ? _styles.Green : _styles.Gold;
                countdownStyle.alignment = TextAnchor.MiddleRight;
                SCUI.Label(new Rect(220f, y, 106f, 24f), countdown, countdownStyle);
                countdownStyle.alignment = TextAnchor.MiddleLeft;
                y += 29f;
            }
        }
        finally
        {
            GUI.matrix = oldMatrix;
        }
    }

    private void DrawCollectionHud()
    {
        if (!Plugin.CollectionHudEnabled.Value) return;

        var collectRequirements = CollectionService.GetBaseRequirements();
        var craftRequirements = CollectionService.GetCraftRequirements();
        if (collectRequirements.Count == 0 && craftRequirements.Count == 0) return;

        const float baseWidth = 360f;
        var visibleCollect = Math.Min(5, collectRequirements.Count);
        var visibleCraft = Math.Min(4, craftRequirements.Count);
        var collectSectionHeight = visibleCollect > 0 ? 24f + visibleCollect * 25f : 0f;
        var craftSectionHeight = visibleCraft > 0 ? 24f + visibleCraft * 25f : 0f;
        var baseHeight = 64f + collectSectionHeight + craftSectionHeight;

        var requestedScale = Mathf.Clamp(Plugin.UiScale.Value, 0.8f, 1.25f);
        var fitScale = Mathf.Min(
            Mathf.Max(0.62f, (Screen.width - 12f) / baseWidth),
            Mathf.Max(0.62f, (Screen.height - 12f) / baseHeight));
        var scale = Mathf.Min(requestedScale, fitScale);
        var width = baseWidth * scale;
        var height = baseHeight * scale;

        var rect = new Rect(Plugin.CollectionHudX.Value, Plugin.CollectionHudY.Value, width, height);
        rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
        rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
        Plugin.CollectionHudX.Value = rect.x;
        Plugin.CollectionHudY.Value = rect.y;

        InputBlockService.RegisterScreenArea(rect);
        InputBlockService.ObservePointer(rect);
        var current = Event.current;
        var closeRect = new Rect(rect.xMax - 30f * scale, rect.y + 3f * scale, 24f * scale, 24f * scale);
        if (current.type == EventType.MouseDown && current.button == 0 && closeRect.Contains(current.mousePosition))
        {
            Plugin.CollectionHudEnabled.Value = false;
            Plugin.SaveState();
            current.Use();
            return;
        }

        var dragBar = new Rect(rect.x, rect.y, rect.width - 38f * scale, 30f * scale);
        if (current.type == EventType.MouseDown && current.button == 0 && dragBar.Contains(current.mousePosition))
        {
            _collectionHudDragging = true;
            _collectionHudDragOffset = current.mousePosition - new Vector2(rect.x, rect.y);
            current.Use();
        }
        else if (_collectionHudDragging && current.type == EventType.MouseDrag)
        {
            Plugin.CollectionHudX.Value = Mathf.Clamp(current.mousePosition.x - _collectionHudDragOffset.x, 0f, Mathf.Max(0f, Screen.width - width));
            Plugin.CollectionHudY.Value = Mathf.Clamp(current.mousePosition.y - _collectionHudDragOffset.y, 0f, Mathf.Max(0f, Screen.height - height));
            current.Use();
        }
        else if (_collectionHudDragging && current.type == EventType.MouseUp)
        {
            _collectionHudDragging = false;
            Plugin.SaveState();
            current.Use();
        }

        rect.x = Plugin.CollectionHudX.Value;
        rect.y = Plugin.CollectionHudY.Value;
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3(rect.x, rect.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        try
        {
            var local = new Rect(0f, 0f, baseWidth, baseHeight);
            var opacity = Mathf.Clamp(Plugin.UiOpacity.Value, 0.55f, 1f);
            var background = SCTheme.Backdrop; background.a = opacity;
            SCUI.Panel(local, background, SCTheme.GoldSoft, 1f);
            SCTheme.Fill(new Rect(0f, 0f, baseWidth, 30f), new Color(SCTheme.Header.r, SCTheme.Header.g, SCTheme.Header.b, opacity));
            SCUI.Label(new Rect(10f, 3f, baseWidth - 48f, 24f),
                "PROJETO: " + CollectionService.SelectedRecipeName + " × " + CollectionService.DesiredQuantity, _styles.Gold);
            SCUI.Button(new Rect(baseWidth - 30f, 3f, 24f, 23f), "×", _styles.Button, true);

            var y = 35f;
            if (visibleCollect > 0)
            {
                SCUI.Label(new Rect(12f, y, baseWidth - 24f, 20f), "COLETAR", _styles.Gold);
                y += 22f;
                foreach (var item in collectRequirements.Take(visibleCollect))
                    DrawCollectionHudRow(local, ref y, item, false);
            }

            if (visibleCraft > 0)
            {
                SCUI.Label(new Rect(12f, y + 1f, baseWidth - 24f, 20f), "FABRICAR ANTES", _styles.Gold);
                y += 23f;
                foreach (var item in craftRequirements.Take(visibleCraft))
                    DrawCollectionHudRow(local, ref y, item, true);
            }
        }
        finally
        {
            GUI.matrix = oldMatrix;
        }
    }

    private void DrawCollectionHudRow(Rect rect, ref float y, MaterialRequirement item, bool crafted)
    {
        var style = item.Complete ? _styles.Green : _styles.Label;
        var prefix = crafted ? "◆ " : "• ";
        SCUI.Label(new Rect(rect.x + 12f, y, rect.width - 154f, 21f), prefix + item.ItemName, style);
        SCUI.Label(new Rect(rect.xMax - 140f, y, 82f, 21f), item.Collected + " / " + item.Required,
            item.Complete ? _styles.Green : _styles.Gold);
        SCUI.Label(new Rect(rect.xMax - 54f, y, 42f, 21f), item.Complete ? "OK" : "−" + item.Missing,
            item.Complete ? _styles.Green : _styles.Blood);
        y += 25f;
    }

    internal void SetVisibility(bool visible)
    {
        _state.PanelVisible = visible;
        Plugin.PanelVisible.Value = visible;
        Plugin.SaveState();
    }

    private void HandleDragging()
    {
        var panel = _state.PanelRect;
        var header = new Rect(panel.x, panel.y, panel.width - 48f * _effectiveScale, 58f * _effectiveScale);
        var current = Event.current;

        if (current.type == EventType.MouseDown && current.button == 0 && header.Contains(current.mousePosition))
        {
            _dragging = true;
            _dragOffset = current.mousePosition - new Vector2(panel.x, panel.y);
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && _dragging)
        {
            panel.x = current.mousePosition.x - _dragOffset.x;
            panel.y = current.mousePosition.y - _dragOffset.y;
            _state.PanelRect = panel;
            KeepInsideScreen();
            current.Use();
        }
        else if (current.type == EventType.MouseUp && _dragging)
        {
            _dragging = false;
            Plugin.PanelX.Value = _state.PanelRect.x;
            Plugin.PanelY.Value = _state.PanelRect.y;
            Plugin.SaveState();
            current.Use();
        }
    }

    private void KeepInsideScreen()
    {
        var panel = _state.PanelRect;
        panel.x = Mathf.Clamp(panel.x, 0f, Mathf.Max(0f, Screen.width - panel.width));
        panel.y = Mathf.Clamp(panel.y, 0f, Mathf.Max(0f, Screen.height - panel.height));
        _state.PanelRect = panel;
    }
}
