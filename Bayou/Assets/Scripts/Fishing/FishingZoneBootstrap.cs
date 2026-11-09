using UnityEngine;

namespace Bayou.Fishing
{
    /// <summary>
    /// Fishing water for the two outer regions. Church and pond spots stay as they are.
    /// </summary>
    public static class FishingZoneBootstrap
    {
        public static void EnsureInScene()
        {
            if (Object.FindFirstObjectByType<FishingZoneManager>() == null)
                new GameObject("FishingZones").AddComponent<FishingZoneManager>();

            EnsureZone(
                "FishZone_FoggyMarsh",
                "Foggy Marsh Area",
                new Vector3(4.56f, 1f, 164.4f),
                new Vector3(120f, 24f, 120f));

            EnsureZone(
                "FishZone_BrackishShore",
                "Brackish Shore Area",
                new Vector3(76.2f, 1f, -59.6f),
                new Vector3(110f, 24f, 110f));
        }

        private static void EnsureZone(string zoneName, string anchorName, Vector3 fallback, Vector3 size)
        {
            if (GameObject.Find(zoneName) != null) return;

            var anchor = GameObject.Find(anchorName);
            var position = anchor != null ? anchor.transform.position : fallback;
            position.y += 1f;

            var go = new GameObject(zoneName);
            go.transform.position = position;
            var zone = go.AddComponent<FishingZone>();
            zone.ConfigureBox(size);
        }
    }
}
