using Bayou.Player;
using UnityEngine;

namespace Bayou.World
{
    /// <summary>
    /// Walking into Foggy Marsh or Brackish Shore counts as finding Caliste's stall there.
    /// That flag is what opens the next row of her shop.
    /// </summary>
    public sealed class CalisteLocationDiscovery : MonoBehaviour
    {
        public static void Ensure()
        {
            if (Object.FindFirstObjectByType<CalisteLocationDiscovery>() != null) return;
            var go = new GameObject(nameof(CalisteLocationDiscovery));
            go.AddComponent<CalisteLocationDiscovery>();
        }

        private void Update()
        {
            var player = PlayerLocator.Transform;
            if (player == null)
            {
                var motor = Object.FindFirstObjectByType<BayouCharacterMotor>();
                if (motor == null) return;
                player = motor.transform;
            }

            var pos = player.position;
            if (!WorldFlags.Has(WorldFlags.FoundCalisteFoggyMarsh) &&
                Near(pos, "Foggy Marsh Area", new Vector3(4.56f, 0f, 164.4f), 55f))
                WorldFlags.Set(WorldFlags.FoundCalisteFoggyMarsh, true);

            if (!WorldFlags.Has(WorldFlags.FoundCalisteBrackishShore) &&
                Near(pos, "Brackish Shore Area", new Vector3(76.2f, 0f, -59.6f), 48f))
                WorldFlags.Set(WorldFlags.FoundCalisteBrackishShore, true);
        }

        private static bool Near(Vector3 player, string anchorName, Vector3 fallback, float radius)
        {
            var anchor = GameObject.Find(anchorName);
            var center = anchor != null ? anchor.transform.position : fallback;
            player.y = 0f;
            center.y = 0f;
            return (player - center).sqrMagnitude <= radius * radius;
        }
    }
}
