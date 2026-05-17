using UnityEngine;

namespace Bayou.Environment
{
    /// <summary>
    /// Marks a collider as water for gameplay.
    /// For surface tiles: prefer Is Trigger so the player does not snag on vertical collider edges at the shore.
    /// Put ground/terrain underneath (or a continuous floor) so the character does not fall through.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaterVolume : MonoBehaviour
    {
        [Tooltip("Optional: require this GameObject's tag. Leave empty to accept any.")]
        [SerializeField] private string requiredTag = "Water";

        public bool Matches(GameObject other)
        {
            if (string.IsNullOrWhiteSpace(requiredTag)) return true;
            return other != null && other.CompareTag(requiredTag);
        }
    }
}
