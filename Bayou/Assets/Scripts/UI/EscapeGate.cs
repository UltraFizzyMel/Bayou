using UnityEngine;

/// <summary>
/// Menus that already use Esc mark the key consumed so the pause screen
/// does not open on the same press that closed them.
/// </summary>
public static class EscapeGate
{
    public static bool Consumed { get; private set; }

    public static void Consume()
    {
        Consumed = true;
        EscapeGateRunner.Ensure();
    }

    public static void Reset() => Consumed = false;

    private sealed class EscapeGateRunner : MonoBehaviour
    {
        private static EscapeGateRunner _runner;

        public static void Ensure()
        {
            if (_runner != null) return;
            var go = new GameObject(nameof(EscapeGate));
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<EscapeGateRunner>();
        }

        private void LateUpdate() => Reset();
    }
}
