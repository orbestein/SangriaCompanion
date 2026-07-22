using UnityEngine;

namespace SangriaCompanion;

/// <summary>
/// Launcher mínimo e independente dos módulos do Companion.
/// Só aparece quando a HUD principal não está executando OnGUI.
/// </summary>
internal sealed class EmergencyLauncherBehaviour : MonoBehaviour
{
    private GUIStyle? _buttonStyle;
    private GUIStyle? _statusStyle;
    private string _lastError = string.Empty;

    private void Awake()
    {
        Plugin.Instance.Log.LogInfo("Launcher de emergência inicializado.");
    }

    private void OnGUI()
    {
        var mainAlive = Plugin.HasBehaviour &&
                        Plugin.Behaviour.IsInitialized &&
                        Time.unscaledTime - CompanionBehaviour.LastGuiHeartbeat < 2f;
        if (mainAlive) return;

        EnsureStyles();
        var rect = new Rect(12f, 70f, 54f, 32f);
        GUI.Box(rect, GUIContent.none);
        if (GUI.Button(rect, "SC", _buttonStyle))
        {
            try
            {
                if (!Plugin.HasBehaviour)
                    Plugin.Instance.TryCreateBehaviour();

                if (Plugin.HasBehaviour)
                    Plugin.Behaviour.SetVisibility(true);
                else
                    _lastError = "HUD não iniciou";
            }
            catch (Exception ex)
            {
                _lastError = ex.GetType().Name;
                Plugin.Instance.Log.LogError("Falha ao reabrir a HUD: " + ex);
            }
        }

        if (!string.IsNullOrEmpty(_lastError))
            GUI.Label(new Rect(72f, 73f, 220f, 26f), _lastError, _statusStyle);
    }

    private void EnsureStyles()
    {
        if (_buttonStyle != null) return;
        _buttonStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13
        };
        _buttonStyle.normal.textColor = Color.white;
        _buttonStyle.hover.textColor = new Color(1f, 0.18f, 0.36f, 1f);
        _statusStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 12
        };
        _statusStyle.normal.textColor = new Color(1f, 0.35f, 0.4f, 1f);
    }
}
