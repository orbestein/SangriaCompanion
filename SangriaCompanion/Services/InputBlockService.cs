using UnityEngine;

namespace SangriaCompanion;

/// <summary>
/// Centraliza a captura de mouse e teclado da interface. Mantém as áreas de HUD
/// do frame atual e do anterior para que os patches consigam bloquear o primeiro
/// clique antes mesmo do OnGUI do novo frame ser processado.
/// </summary>
internal static class InputBlockService
{
    private static readonly List<Rect> CurrentAreas = new(16);
    private static readonly List<Rect> PreviousAreas = new(16);

    private static float _blockedUntil;
    private static float _pointerCaptureUntil;
    private static bool _textEntryActive;
    private static bool _panelOpen;
    private static int _registeredFrame = -1;

    internal static bool IsBlocked => Time.unscaledTime < _blockedUntil;

    internal static bool ShouldSuppressGameplayInput =>
        IsBlocked ||
        _panelOpen ||
        _textEntryActive ||
        Time.unscaledTime < _pointerCaptureUntil ||
        IsPointerOverCompanion();

    internal static void BeginGuiFrame()
    {
        if (_registeredFrame == Time.frameCount) return;

        PreviousAreas.Clear();
        PreviousAreas.AddRange(CurrentAreas);
        CurrentAreas.Clear();
        _registeredFrame = Time.frameCount;
    }

    internal static void RegisterScreenArea(Rect rect)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        CurrentAreas.Add(rect);
    }

    internal static void SetPanelOpen(bool open)
    {
        if (_panelOpen == open) return;
        _panelOpen = open;
        if (open) BlockFor(0.25f);
        else BlockFor(0.12f);
    }

    internal static void SetTextEntryActive(bool active)
    {
        _textEntryActive = active;
        if (active) BlockFor(0.18f);
    }

    internal static void BlockFor(float seconds = 0.45f)
    {
        _blockedUntil = Mathf.Max(_blockedUntil, Time.unscaledTime + seconds);
        _pointerCaptureUntil = Mathf.Max(_pointerCaptureUntil, Time.unscaledTime + seconds);
        Input.ResetInputAxes();
    }

    internal static void Update()
    {
        if (!ShouldSuppressGameplayInput) return;
        Input.ResetInputAxes();
    }

    internal static void ObservePointer(Rect rect)
    {
        var current = Event.current;
        if (current == null || !rect.Contains(current.mousePosition)) return;

        _pointerCaptureUntil = Mathf.Max(_pointerCaptureUntil, Time.unscaledTime + 0.35f);

        if (current.type is EventType.MouseDown or EventType.MouseUp or EventType.MouseDrag or EventType.ScrollWheel)
            BlockFor(0.65f);
    }

    internal static void ConsumeCurrentMouseEvent(Rect rect)
    {
        var current = Event.current;
        if (current == null || !rect.Contains(current.mousePosition)) return;

        ObservePointer(rect);
        if (current.type is EventType.MouseDown or EventType.MouseUp or EventType.MouseDrag or EventType.ScrollWheel)
            current.Use();
    }

    private static bool IsPointerOverCompanion()
    {
        // Input.mousePosition usa origem no canto inferior esquerdo; IMGUI usa o
        // canto superior esquerdo.
        var raw = Input.mousePosition;
        var point = new Vector2(raw.x, Screen.height - raw.y);

        return Contains(CurrentAreas, point) || Contains(PreviousAreas, point);
    }

    private static bool Contains(List<Rect> areas, Vector2 point)
    {
        for (var i = 0; i < areas.Count; i++)
        {
            if (areas[i].Contains(point)) return true;
        }

        return false;
    }
}
