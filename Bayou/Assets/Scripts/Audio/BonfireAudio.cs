using UnityEngine;

namespace Bayou.Audio
{
    /// <summary>
    /// Drag match / fire SFX. Place on the bonfire interactable (or UI root).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BonfireAudio : MonoBehaviour
    {
        public static BonfireAudio Instance { get; private set; }

        [SerializeField] private SfxPlayer sfx;
        [SerializeField] private AudioClip strikingMatch;
        [SerializeField] private AudioClip matchBurning;

        private void Awake()
        {
            Instance = this;
            if (sfx == null)
                sfx = GetComponent<SfxPlayer>() ?? gameObject.AddComponent<SfxPlayer>();
            sfx.EnsureSource();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void PlayStrikeMatch() => sfx.PlayOneShot(strikingMatch);

        public void StartBurningLoop() => sfx.PlayLoop(matchBurning, 0.7f);

        public void StopBurningLoop() => sfx.StopLoop();

        public static BonfireAudio Resolve()
        {
            if (Instance != null) return Instance;
            return FindFirstObjectByType<BonfireAudio>();
        }
    }
}
