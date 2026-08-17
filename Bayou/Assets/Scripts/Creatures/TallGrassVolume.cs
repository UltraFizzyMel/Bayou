using System.Collections.Generic;
using UnityEngine;

namespace Bayou.Creatures
{
    /// <summary>
    /// Trigger volume: while the player is inside, creatures treat them as hidden
    /// (unless within minimum sense range — see CreatureSense).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class TallGrassVolume : MonoBehaviour
    {
        private static readonly Dictionary<Transform, int> HiddenRefCounts = new();

        [SerializeField] private string playerTag = "Player";

        private readonly HashSet<Transform> _inside = new();

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryGetPlayer(other, out var t)) return;
            if (!_inside.Add(t)) return;
            if (!HiddenRefCounts.TryGetValue(t, out var n))
                n = 0;
            HiddenRefCounts[t] = n + 1;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!TryGetPlayer(other, out var t)) return;
            if (!_inside.Remove(t)) return;
            Decrement(t);
        }

        private void OnDisable()
        {
            foreach (var t in _inside)
                Decrement(t);
            _inside.Clear();
        }

        private static void Decrement(Transform t)
        {
            if (t == null) return;
            if (!HiddenRefCounts.TryGetValue(t, out var n)) return;
            n--;
            if (n <= 0)
                HiddenRefCounts.Remove(t);
            else
                HiddenRefCounts[t] = n;
        }

        public static bool IsPlayerHidden(Transform player)
        {
            if (player == null) return false;
            if (HiddenRefCounts.ContainsKey(player)) return true;
            return HiddenRefCounts.ContainsKey(player.root);
        }

        private bool TryGetPlayer(Collider other, out Transform player)
        {
            player = null;
            if (other == null) return false;
            if (!(other.CompareTag(playerTag) ||
                  other.GetComponentInParent<Bayou.Player.BayouCharacterMotor>() != null))
                return false;

            player = other.attachedRigidbody != null
                ? other.attachedRigidbody.transform
                : other.transform.root;
            return player != null;
        }
    }
}
