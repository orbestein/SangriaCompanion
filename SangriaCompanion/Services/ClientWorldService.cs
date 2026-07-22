using ProjectM.UI;
using Unity.Entities;

namespace SangriaCompanion;

/// <summary>
/// Localiza de forma segura o World real do cliente sem depender de um patch
/// Harmony em ClientChatSystem. Isso evita que uma mudança no método OnUpdate
/// impeça o plugin inteiro de carregar e deixe a HUD invisível.
/// </summary>
internal static class ClientWorldService
{
    internal static World? World { get; private set; }
    private static float _nextLookupAt;

    internal static void Update()
    {
        if (World != null && World.IsCreated) return;
        if (UnityEngine.Time.unscaledTime < _nextLookupAt) return;
        _nextLookupAt = UnityEngine.Time.unscaledTime + 2f;

        try
        {
            var worlds = Unity.Entities.World.All;
            for (var i = 0; i < worlds.Count; i++)
            {
                var candidate = worlds[i];
                if (candidate == null || !candidate.IsCreated) continue;

                try
                {
                    var chat = candidate.GetExistingSystemManaged<ClientChatSystem>();
                    if (chat == null) continue;
                    World = candidate;
                    Plugin.Instance.Log.LogInfo("World do cliente localizado: " + candidate.Name);
                    return;
                }
                catch
                {
                    // Este World não possui ClientChatSystem.
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Instance.Log.LogDebug("World do cliente ainda indisponível: " + ex.Message);
        }
    }

    internal static void Clear()
    {
        World = null;
        _nextLookupAt = 0f;
    }
}
