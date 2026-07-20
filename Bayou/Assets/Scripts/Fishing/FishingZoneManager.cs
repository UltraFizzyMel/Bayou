using UnityEngine;
using System.Collections.Generic;

namespace Bayou.Fishing
{
    public class FishingZoneManager : MonoBehaviour
    {
        public static FishingZoneManager Instance { get; private set; }

        [SerializeField] private List<FishingZone> fishingZones = new List<FishingZone>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public static bool IsInFishingZone(Vector3 position)
        {
            if (Instance == null) return true; // Allow fishing if no zones exist

            foreach (var zone in Instance.fishingZones)
            {
                if (zone != null && zone.IsPositionInZone(position))
                    return true;
            }

            return false;
        }

        public void RegisterZone(FishingZone zone)
        {
            if (!fishingZones.Contains(zone))
                fishingZones.Add(zone);
        }

        public void UnregisterZone(FishingZone zone)
        {
            fishingZones.Remove(zone);
        }
    }

    public class FishingZone : MonoBehaviour
    {
        public enum ZoneShape { Sphere, Box }

        [SerializeField] private ZoneShape shape = ZoneShape.Sphere;
        [SerializeField] private float radius = 10f;
        [SerializeField] private Vector3 boxSize = new Vector3(10f, 5f, 10f);

        private void Start()
        {
            if (FishingZoneManager.Instance != null)
                FishingZoneManager.Instance.RegisterZone(this);
        }

        private void OnDestroy()
        {
            if (FishingZoneManager.Instance != null)
                FishingZoneManager.Instance.UnregisterZone(this);
        }

        public bool IsPositionInZone(Vector3 position)
        {
            switch (shape)
            {
                case ZoneShape.Sphere:
                    return Vector3.Distance(transform.position, position) <= radius;

                case ZoneShape.Box:
                    var localPos = transform.InverseTransformPoint(position);
                    return Mathf.Abs(localPos.x) <= boxSize.x * 0.5f &&
                           Mathf.Abs(localPos.y) <= boxSize.y * 0.5f &&
                           Mathf.Abs(localPos.z) <= boxSize.z * 0.5f;

                default:
                    return false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);

            switch (shape)
            {
                case ZoneShape.Sphere:
                    Gizmos.DrawWireSphere(transform.position, radius);
                    break;

                case ZoneShape.Box:
                    Gizmos.DrawWireCube(transform.position, boxSize);
                    break;
            }
        }
    }
}
