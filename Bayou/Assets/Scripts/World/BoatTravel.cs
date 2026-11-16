using Bayou.Inventory;
using Bayou.Player;
using UnityEngine;

namespace Bayou.World
{
    /// <summary>
    /// Marie and Olivier boat choices. Routes stay locked until the matching quest flag is set.
    /// Payment for a Ride unlocks church-side brackish ↔ foggy marsh.
    /// Expanded Horizons unlocks the sunken-grove pier.
    /// Fish cost follows <see cref="FastTravelState"/> (free, 1, or 2).
    /// </summary>
    public static class BoatTravel
    {
        public const string Church = "church";
        public const string Foggy = "foggy";
        public const string Grove = "grove";

        private static string _pending;

        public static bool CanTravel(string destination)
        {
            destination = Normalize(destination);
            if (!RouteUnlocked(destination))
                return false;

            var cost = FishCost();
            if (cost <= 0)
                return true;

            var inv = InventoryController.Instance;
            return inv != null && inv.CountFish() >= cost;
        }

        /// <summary>Charges fish and moves the player when the conversation closes.</summary>
        public static bool Queue(string destination)
        {
            destination = Normalize(destination);
            if (!CanTravel(destination))
                return false;

            var cost = FishCost();
            if (cost > 0)
            {
                var inv = InventoryController.Instance;
                if (inv == null || inv.RemoveFish(cost) < cost)
                    return false;
            }

            _pending = destination;
            return true;
        }

        public static void CommitPending()
        {
            if (string.IsNullOrEmpty(_pending))
                return;

            var destination = _pending;
            _pending = null;
            if (!TryDestination(destination, out var position))
            {
                Debug.LogWarning("[Boat] No landing for '" + destination + "'.");
                return;
            }

            var motor = Object.FindFirstObjectByType<BayouCharacterMotor>();
            var player = motor != null ? motor.transform : PlayerLocator.Transform;
            if (player == null)
                return;

            SnapOntoGround(ref position);
            var rotation = Quaternion.LookRotation(FlatFacing(destination), Vector3.up);
            if (motor != null)
                motor.Teleport(position, rotation);
            else
                player.SetPositionAndRotation(position, rotation);

            Debug.Log("[Boat] Landed at " + destination + " " + position);
        }

        public static int FishCost()
        {
            if (FastTravelState.Free)
                return 0;
            return Mathf.Max(0, FastTravelState.CostInFish);
        }

        private static bool RouteUnlocked(string destination)
        {
            switch (destination)
            {
                case Church:
                case Foggy:
                    return FastTravelState.ChurchToFoggyMarsh;
                case Grove:
                    return FastTravelState.ToBrackishShore;
                default:
                    return false;
            }
        }

        private static bool TryDestination(string destination, out Vector3 position)
        {
            position = default;
            if (TryNearMarie(out position))
                return true;

            switch (destination)
            {
                case Church:
                    return Anchor("Piers for Brackish Shore", new Vector3(123.03f, -4.5f, -48.6f), out position);
                case Foggy:
                    return Anchor("Foggy Marsh Dock", new Vector3(4.56f, 1.2f, 164.4f), out position);
                case Grove:
                    return Anchor("Brackish Shore Pier 2", new Vector3(256.01f, -4.5f, 46.69f), out position);
                default:
                    return false;
            }
        }

        private static bool TryNearMarie(out Vector3 position)
        {
            position = default;
            string[] names = { "Marie Claire Baptiste", "Marie-Claire", "Marie", "NPC_Marie" };
            Transform best = null;
            var bestSq = float.MaxValue;
            for (var i = 0; i < names.Length; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go == null) continue;
                var sq = go.transform.position.sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = go.transform;
                }
            }

            if (best == null)
            {
                var houseboat = GameObject.Find("Houseboat") ?? GameObject.Find("Church Houseboat");
                if (houseboat != null)
                    best = houseboat.transform;
            }

            if (best == null) return false;
            var forward = best.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward.Normalize();
            position = best.position - forward * 2.8f + Vector3.up * 1.15f;
            return true;
        }

        private static bool Anchor(string objectName, Vector3 fallback, out Vector3 position)
        {
            var anchor = GameObject.Find(objectName);
            position = anchor != null ? anchor.transform.position : fallback;
            position.y = Mathf.Max(position.y, fallback.y);
            return true;
        }

        private static void SnapOntoGround(ref Vector3 position)
        {
            var terrains = Terrain.activeTerrains;
            for (var i = 0; i < terrains.Length; i++)
            {
                var terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null) continue;
                var local = position - terrain.transform.position;
                var size = terrain.terrainData.size;
                if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z)
                    continue;

                var ground = terrain.SampleHeight(position) + terrain.transform.position.y;
                position.y = ground + 1.15f;
                return;
            }

            position.y += 1.15f;
        }

        private static Vector3 FlatFacing(string destination)
        {
            switch (destination)
            {
                case Foggy: return Vector3.forward;
                case Grove: return Vector3.back;
                default: return Vector3.left;
            }
        }

        private static string Normalize(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination))
                return "";
            destination = destination.Trim().ToLowerInvariant();
            if (destination.Contains("grove") || destination.Contains("sunken"))
                return Grove;
            if (destination.Contains("fog") || destination.Contains("marsh"))
                return Foggy;
            if (destination.Contains("church") || destination.Contains("brackish"))
                return Church;
            return destination;
        }
    }
}
