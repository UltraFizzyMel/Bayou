using UnityEngine;

namespace Bayou.World
{
    /// <summary>Scene-persistent host for sunken-grove ending gates (build-safe lifecycle).</summary>
    [DisallowMultipleComponent]
    public sealed class SunkenGroveEndingHost : MonoBehaviour
    {
        private Transform _root;
        private Transform _ringTrigger;
        private Transform _endTrigger;

        public void Install(Transform sigilRoot, Transform ring, Transform ending)
        {
            _root = sigilRoot;
            _ringTrigger = ring;
            _endTrigger = ending;
            RefreshGates();
        }

        private void OnEnable()
        {
            SpiritSigilDispel.SigilStateChanged -= RefreshGates;
            SpiritSigilDispel.SigilStateChanged += RefreshGates;
        }

        private void OnDisable()
        {
            SpiritSigilDispel.SigilStateChanged -= RefreshGates;
        }

        private void Start()
        {
            // SigilDispel components register on Enable after WireSigils — refresh next frame.
            RefreshGates();
            Invoke(nameof(RefreshGates), 0.05f);
        }

        public void RefreshGates()
        {
            var allGone = SpiritSigilDispel.AllDispelled;
            var hasRing = WorldFlags.Has(WorldFlags.WeddingRingFound);

            var wall = SunkenGroveEndingBootstrap.FindWallCollider(_root);
            if (wall != null)
            {
                var col = wall.GetComponent<Collider>();
                if (col != null)
                    col.enabled = !allGone && !hasRing;
            }

            if (_ringTrigger != null)
            {
                var col = _ringTrigger.GetComponent<Collider>();
                if (col != null)
                    col.enabled = allGone || hasRing;
            }

            if (_endTrigger != null)
            {
                var col = _endTrigger.GetComponent<Collider>();
                if (col != null)
                    col.enabled = hasRing;
            }
        }
    }
}