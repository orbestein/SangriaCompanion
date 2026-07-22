using UnityEngine;

namespace SangriaCompanion;

internal static class SCTheme
{
    // Tema Sangria com vermelho vinho menos saturado e maior contraste de leitura.
    internal static readonly Color Backdrop = new(0.020f, 0.013f, 0.017f, 1f);
    internal static readonly Color Header = new(0.028f, 0.016f, 0.021f, 1f);
    internal static readonly Color Sidebar = new(0.033f, 0.021f, 0.026f, 1f);
    internal static readonly Color Panel = new(0.058f, 0.036f, 0.043f, 1f);
    internal static readonly Color PanelAlt = new(0.082f, 0.049f, 0.058f, 1f);
    internal static readonly Color PanelHover = new(0.125f, 0.066f, 0.078f, 1f);
    internal static readonly Color Border = new(0.34f, 0.15f, 0.18f, 1f);
    internal static readonly Color BorderSoft = new(0.22f, 0.11f, 0.13f, 1f);
    internal static readonly Color Gold = new(0.90f, 0.66f, 0.48f, 1f);
    internal static readonly Color GoldSoft = new(0.50f, 0.25f, 0.25f, 1f);
    internal static readonly Color Blood = new(0.72f, 0.18f, 0.25f, 1f);
    internal static readonly Color Green = new(0.34f, 0.76f, 0.50f, 1f);
    internal static readonly Color Blue = new(0.38f, 0.62f, 0.84f, 1f);
    internal static readonly Color Purple = new(0.56f, 0.43f, 0.76f, 1f);
    internal static readonly Color Text = new(0.94f, 0.91f, 0.91f, 1f);
    internal static readonly Color Muted = new(0.72f, 0.66f, 0.67f, 1f);
    internal static readonly Color Dim = new(0.48f, 0.42f, 0.43f, 1f);

    internal static void Fill(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    internal static void BorderRect(Rect rect, Color color, float thickness = 1f)
    {
        Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
        Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
        Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }
}
