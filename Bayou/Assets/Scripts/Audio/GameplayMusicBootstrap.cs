using UnityEngine;
using UnityEngine.Audio;

namespace Bayou.Audio
{
    /// <summary>
    /// Ensures church/graveyard music zones exist in player builds (no playtest harness required).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayMusicBootstrap : MonoBehaviour
    {
        [SerializeField] private AudioClip churchMusic;
        [SerializeField] private AudioClip churchAmbient;
        [SerializeField] private AudioClip graveyardMusic;
        [SerializeField] private AudioClip graveyardAmbient;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private Vector3 churchZoneCenter = new(0f, 8f, -20f);
        [SerializeField] private Vector3 churchZoneSize = new(140f, 50f, 140f);
        [SerializeField] private Vector3 graveyardZoneCenter = new(-70f, 8f, 80f);
        [SerializeField] private Vector3 graveyardZoneSize = new(110f, 50f, 110f);

        private void Awake()
        {
            if (FindFirstObjectByType<AreaMusicZone>() != null)
                return;

            EnsureZone("ChurchAreaMusicZone", churchZoneCenter, churchZoneSize, churchMusic, churchAmbient);
            EnsureZone("GraveyardAreaMusicZone", graveyardZoneCenter, graveyardZoneSize, graveyardMusic, graveyardAmbient);
        }

        private void EnsureZone(
            string name,
            Vector3 center,
            Vector3 size,
            AudioClip music,
            AudioClip ambient)
        {
            if (music == null && ambient == null) return;

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = center;

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;

            var zone = go.AddComponent<AreaMusicZone>();
            zone.Configure(name, music, ambient, musicGroup);
        }
    }
}
