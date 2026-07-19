using UnityEngine;

namespace Bayou.Audio
{
    /// <summary>
    /// Drag fishing SFX here. Place on the player (or a scene Audio object).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingAudio : MonoBehaviour
    {
        public static FishingAudio Instance { get; private set; }

        [Header("Player")]
        [SerializeField] private SfxPlayer sfx;

        [Header("Cast / throw")]
        [SerializeField] private AudioClip castConfirm;
        [SerializeField] private AudioClip throwNet;
        [SerializeField] private AudioClip rodCasting;
        [SerializeField] private AudioClip handNetScoop;

        [Header("Landing / bite / catch")]
        [SerializeField] private AudioClip rodLanding;
        [SerializeField] private AudioClip fishOnLine;
        [SerializeField] private AudioClip reelingInFish;
        [SerializeField] private AudioClip manSnaggingFish;

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

        public void PlayCastConfirm() => sfx.PlayOneShot(castConfirm);
        public void PlayThrowNet() => sfx.PlayOneShot(throwNet != null ? throwNet : rodCasting);
        public void PlayRodCasting() => sfx.PlayOneShot(rodCasting != null ? rodCasting : throwNet);
        public void PlayHandNetScoop() => sfx.PlayOneShot(handNetScoop != null ? handNetScoop : throwNet);
        public void PlayLanding() => sfx.PlayOneShot(rodLanding);
        public void PlayFishOnLine() => sfx.PlayOneShot(fishOnLine);
        public void PlaySnagCatch() => sfx.PlayOneShot(manSnaggingFish);

        public void StartReelingLoop() => sfx.PlayLoop(reelingInFish);
        public void StopReelingLoop() => sfx.StopLoop();

        public static FishingAudio Resolve()
        {
            if (Instance != null) return Instance;
            return FindFirstObjectByType<FishingAudio>();
        }
    }
}
