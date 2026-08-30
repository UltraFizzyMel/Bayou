using System;
using System.Collections.Generic;
using Bayou.Fish;
using Bayou.Inventory;
using Bayou.Rendering;
using Bayou.UI;
using UnityEngine;

namespace Bayou.Fishing
{
    /// <summary>
    /// Authoring volume for a fishing hole: required tool (Net or Rod) + fish / one-time loot spawns.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingSpot : MonoBehaviour, IInteractionPromptSource
    {
        [Serializable]
        public sealed class FishSpawn
        {
            public ItemDefinition item;
            public FishCatchTool tool = FishCatchTool.Net;
            [Min(1)] public int count = 1;
            public bool moving = true;
            public Color tint = new(0.85f, 0.35f, 0.25f, 1f);
        }

        [Serializable]
        public sealed class LootSpawn
        {
            public ItemDefinition item;
            public Color glowColor = new(0.85f, 0.75f, 0.2f, 1f);
            public Vector3 localOffset;
        }

        [Header("Spot")]
        [SerializeField] private string spotName = "Fishing Spot";
        [SerializeField] private FishCatchTool requiredTool = FishCatchTool.Net;
        [SerializeField] private float radius = 8f;
        [SerializeField] private float spawnY = 0.12f;
        [Tooltip("Optional pond collider — fish are hard-clamped inside its XZ bounds.")]
        [SerializeField] private Collider waterBounds;
        [SerializeField] private float shoreMargin = 0.45f;
        [Tooltip("Move this hole onto its water mesh at play so terrain edits keep fish in the pond.")]
        [SerializeField] private bool snapToWaterOnPlay = true;

        [Header("Spawns")]
        [SerializeField] private List<FishSpawn> fishSpawns = new();
        [SerializeField] private List<LootSpawn> oneTimeLoot = new();
        [SerializeField] private BayouFish fishPrefab;

        [Header("Runtime")]
        [SerializeField] private bool spawnOnStart = true;

        private readonly List<BayouFish> _spawned = new();
        private static readonly List<FishingSpot> All = new();
        private static Transform _cachedPlayer;
        private static BayouFishingEquipment _cachedEquipment;

        public string SpotName => spotName;
        public FishCatchTool RequiredTool => requiredTool;
        public float Radius => radius;
        public Collider WaterBounds => waterBounds;
        public IReadOnlyList<BayouFish> SpawnedFish => _spawned;

        public static IReadOnlyList<FishingSpot> AllSpots => All;

        private void Awake()
        {
            if (snapToWaterOnPlay)
                AlignToWater();
            if (waterBounds != null)
                radius = FitRadiusToWater(radius);
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
            InteractionPromptBroker.Register(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
            InteractionPromptBroker.Unregister(this);
        }

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            if (_cachedPlayer == null)
            {
                _cachedPlayer = Bayou.Player.PlayerLocator.Transform;
                _cachedEquipment = Bayou.Player.PlayerLocator.Equipment;
            }

            if (_cachedPlayer == null) return false;

            var bag = InventoryDisplayUI.Active;
            if (bag != null && bag.IsOpen) return false;

            var d = _cachedPlayer.position - transform.position;
            d.y = 0f;
            var reach = radius + 2.5f;
            if (d.sqrMagnitude > reach * reach) return false;

            var equipment = _cachedEquipment;
            var held = equipment != null ? equipment.CurrentItem : BayouHeldItem.None;
            var distSq = d.sqrMagnitude;

            if (requiredTool == FishCatchTool.Rod)
            {
                if (held == BayouHeldItem.Rod)
                    prompt = new InteractionPrompt("Hold LMB", $"Cast into {spotName}", 42, distSq);
                else if (equipment != null && equipment.CanHold(BayouHeldItem.Rod))
                    prompt = new InteractionPrompt("1", $"{spotName} needs a rod", 41, distSq);
                else
                    return false;
                return true;
            }

            if (held == BayouHeldItem.Net)
                prompt = new InteractionPrompt("Hold LMB", $"Throw net into {spotName}", 42, distSq);
            else if (equipment != null && equipment.CanHold(BayouHeldItem.Net))
                prompt = new InteractionPrompt("2", $"{spotName} needs a hand net", 41, distSq);
            else
                return false;
            return true;
        }

        private void Start()
        {
            if (spawnOnStart)
                SpawnContents();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (waterBounds != null)
                radius = FitRadiusToWater(radius);
        }
#endif

        /// <summary>Places the hole on the current water mesh (after terrain / pond moves).</summary>
        public void AlignToWater()
        {
            if (waterBounds == null) return;
            var b = waterBounds.bounds;
            transform.position = new Vector3(b.center.x, b.max.y, b.center.z);
            radius = FitRadiusToWater(radius);
        }

        public float SurfaceYAt(Vector3 worldPos)
        {
            var waterY = waterBounds != null ? waterBounds.bounds.max.y : transform.position.y;
            var swimY = waterY - Mathf.Clamp(spawnY, 0.02f, 0.35f);
            if (TryGetGroundHeight(worldPos, out var ground))
                swimY = Mathf.Max(swimY, ground + 0.06f);
            return swimY;
        }

        public bool Contains(Vector3 worldPos)
        {
            var flat = worldPos - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > radius * radius)
                return false;
            if (!IsInsideWaterBounds(worldPos))
                return false;
            return !IsLand(worldPos);
        }

        /// <summary>Keeps a world point inside the spot circle and (if set) the water mesh AABB.</summary>
        public Vector3 ClampInside(Vector3 worldPos)
        {
            var pos = worldPos;
            var center = transform.position;
            var flat = new Vector3(pos.x - center.x, 0f, pos.z - center.z);
            var maxR = Mathf.Max(0.5f, radius - shoreMargin);
            if (flat.sqrMagnitude > maxR * maxR)
            {
                flat = flat.normalized * maxR;
                pos.x = center.x + flat.x;
                pos.z = center.z + flat.z;
            }

            if (waterBounds != null)
            {
                var b = waterBounds.bounds;
                var m = shoreMargin;
                var minX = b.min.x + m;
                var maxX = b.max.x - m;
                var minZ = b.min.z + m;
                var maxZ = b.max.z - m;
                if (minX <= maxX) pos.x = Mathf.Clamp(pos.x, minX, maxX);
                if (minZ <= maxZ) pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
            }

            if (IsLand(pos))
            {
                var inward = new Vector3(center.x - pos.x, 0f, center.z - pos.z);
                if (inward.sqrMagnitude > 0.0001f)
                {
                    inward.Normalize();
                    for (var step = 0; step < 8; step++)
                    {
                        pos.x += inward.x * 0.45f;
                        pos.z += inward.z * 0.45f;
                        if (!IsLand(pos) && IsInsideWaterBounds(pos))
                            break;
                    }
                }
            }

            pos.y = SurfaceYAt(pos);
            return pos;
        }

        public Vector3 SwimCenter
        {
            get
            {
                if (waterBounds != null)
                {
                    var c = waterBounds.bounds.center;
                    return new Vector3(c.x, SurfaceYAt(c), c.z);
                }

                var p = transform.position;
                return new Vector3(p.x, SurfaceYAt(p), p.z);
            }
        }

        public bool IsNearShore(Vector3 worldPos, float edgeFraction = 0.85f)
        {
            var center = SwimCenter;
            var flat = worldPos - center;
            flat.y = 0f;
            var limit = Mathf.Max(0.5f, radius - shoreMargin) * Mathf.Clamp01(edgeFraction);
            if (flat.sqrMagnitude >= limit * limit)
                return true;

            if (IsLand(worldPos))
                return true;

            if (waterBounds == null) return false;
            var b = waterBounds.bounds;
            var m = shoreMargin;
            return worldPos.x <= b.min.x + m * 1.5f || worldPos.x >= b.max.x - m * 1.5f ||
                   worldPos.z <= b.min.z + m * 1.5f || worldPos.z >= b.max.z - m * 1.5f;
        }

        public static FishingSpot FindContaining(Vector3 worldPos)
        {
            foreach (var spot in All)
            {
                if (spot != null && spot.isActiveAndEnabled && spot.Contains(worldPos))
                    return spot;
            }

            return null;
        }

        public static FishingSpot FindNearby(Vector3 worldPos, float extraReach = 3.5f)
        {
            FishingSpot best = null;
            var bestSq = float.MaxValue;
            foreach (var spot in All)
            {
                if (spot == null || !spot.isActiveAndEnabled) continue;
                var d = worldPos - spot.transform.position;
                d.y = 0f;
                var reach = spot.Radius + extraReach;
                var sq = d.sqrMagnitude;
                if (sq > reach * reach) continue;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = spot;
                }
            }

            return best;
        }

        public static bool AnySpotsExist() => All.Count > 0;

        /// <summary>Runtime / bootstrap setup before <see cref="Start"/> spawns contents.</summary>
        public void Configure(
            string name,
            FishCatchTool primaryTool,
            float spotRadius,
            IList<FishSpawn> fish,
            IList<LootSpawn> loot = null,
            bool autoSpawnOnStart = true,
            Collider water = null)
        {
            spotName = string.IsNullOrWhiteSpace(name) ? "Fishing Spot" : name;
            requiredTool = primaryTool;
            waterBounds = water;
            radius = FitRadiusToWater(Mathf.Max(1f, spotRadius));
            fishSpawns = fish != null ? new List<FishSpawn>(fish) : new List<FishSpawn>();
            oneTimeLoot = loot != null ? new List<LootSpawn>(loot) : new List<LootSpawn>();
            spawnOnStart = autoSpawnOnStart;
        }

        public void SetWaterBounds(Collider water)
        {
            waterBounds = water;
            radius = FitRadiusToWater(radius);
        }

        private float FitRadiusToWater(float requested)
        {
            if (waterBounds == null) return requested;
            var e = waterBounds.bounds.extents;
            var fit = Mathf.Min(e.x, e.z) - shoreMargin;
            if (fit < 1f) fit = Mathf.Max(0.75f, Mathf.Min(e.x, e.z) * 0.85f);
            return Mathf.Min(requested, fit);
        }

        private bool IsInsideWaterBounds(Vector3 worldPos)
        {
            if (waterBounds == null) return true;
            var b = waterBounds.bounds;
            var m = shoreMargin * 0.5f;
            return worldPos.x >= b.min.x - m && worldPos.x <= b.max.x + m &&
                   worldPos.z >= b.min.z - m && worldPos.z <= b.max.z + m;
        }

        public bool IsLand(Vector3 worldPos)
        {
            if (!TryGetGroundHeight(worldPos, out var ground))
                return false;
            var waterY = waterBounds != null ? waterBounds.bounds.max.y : transform.position.y;
            return ground > waterY - 0.04f;
        }

        public static bool TryGetGroundHeight(Vector3 worldPos, out float y)
        {
            var terrains = Terrain.activeTerrains;
            for (var i = 0; i < terrains.Length; i++)
            {
                var terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null) continue;
                var local = worldPos - terrain.transform.position;
                var size = terrain.terrainData.size;
                if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z)
                    continue;
                y = terrain.SampleHeight(worldPos) + terrain.transform.position.y;
                return true;
            }

            var origin = new Vector3(worldPos.x, worldPos.y + 40f, worldPos.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore) &&
                !IsWaterCollider(hit.collider))
            {
                y = hit.point.y;
                return true;
            }

            y = 0f;
            return false;
        }

        private static bool IsWaterCollider(Collider col)
        {
            if (col == null) return false;
            if (col.CompareTag("Water")) return true;
            return col.GetComponent<Bayou.Environment.WaterVolume>() != null ||
                   col.GetComponentInParent<Bayou.Environment.WaterVolume>() != null;
        }

        public void SpawnContents()
        {
            ClearSpawned();

            foreach (var entry in fishSpawns)
            {
                if (entry == null || entry.item == null || entry.count <= 0) continue;
                for (var i = 0; i < entry.count; i++)
                    SpawnFish(entry);
            }

            foreach (var loot in oneTimeLoot)
            {
                if (loot?.item == null) continue;
                SpawnLoot(loot);
            }
        }

        private void SpawnFish(FishSpawn entry)
        {
            var pos = RandomPointInSpot();
            BayouFish fish;

            if (fishPrefab != null)
            {
                fish = Instantiate(fishPrefab, pos, Quaternion.identity, transform);
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = entry.item.displayName;
                go.transform.SetParent(transform, false);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 0.35f;
                var col = go.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;
                fish = go.GetComponent<BayouFish>() ?? go.AddComponent<BayouFish>();
                Tint(go, entry.tint);
            }

            fish.Configure(entry.item, entry.tool, this, moving: entry.moving);
            _spawned.Add(fish);
        }

        private void SpawnLoot(LootSpawn loot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = loot.item.displayName;
            go.transform.SetParent(transform, false);
            go.transform.position = ClampInside(transform.position + loot.localOffset + Vector3.up * spawnY);
            go.transform.localScale = Vector3.one * 0.3f;
            var col = go.GetComponent<SphereCollider>();
            if (col != null)
            {
                col.isTrigger = true;
                col.radius = 1.2f;
            }

            var scoop = go.AddComponent<NetScoopLoot>();
            scoop.Configure(loot.item, loot.glowColor);
        }

        private Vector3 RandomPointInSpot()
        {
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var r = radius * 0.65f;
                var offset = UnityEngine.Random.insideUnitCircle * r;
                var candidate = new Vector3(
                    transform.position.x + offset.x,
                    transform.position.y,
                    transform.position.z + offset.y);
                candidate = ClampInside(candidate);
                if (!IsLand(candidate) && (Contains(candidate) || attempt == 15))
                    return candidate;
            }

            return ClampInside(SwimCenter);
        }

        private void ClearSpawned()
        {
            for (var i = _spawned.Count - 1; i >= 0; i--)
            {
                if (_spawned[i] != null)
                    Destroy(_spawned[i].gameObject);
            }

            _spawned.Clear();

            foreach (var loot in GetComponentsInChildren<NetScoopLoot>(true))
            {
                if (loot != null)
                    Destroy(loot.gameObject);
            }
        }

        private static void Tint(GameObject go, Color color)
        {
            var rend = go.GetComponent<MeshRenderer>();
            if (rend == null) return;
            rend.sharedMaterial = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(color);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = requiredTool == FishCatchTool.Rod
                ? new Color(0.3f, 0.55f, 1f, 0.35f)
                : new Color(0.2f, 0.85f, 0.45f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up, $"{spotName} ({requiredTool})");
#endif
        }
    }
}
