using UnityEngine;
using UnityEngine.Audio;

namespace Bayou.Audio
{
    /// <summary>
    /// Church and graveyard clips for <see cref="TransitionMusicDirector"/>.
    /// The old generated church/graveyard boxes are not created: they overlapped the
    /// spawn road and the graveyard, and the swamp director then replaced them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayMusicBootstrap : MonoBehaviour
    {
        [SerializeField] private AudioClip churchMusic;
        [SerializeField] private AudioClip churchAmbient;
        [SerializeField] private AudioClip graveyardMusic;
        [SerializeField] private AudioClip graveyardAmbient;
        [SerializeField] private AudioMixerGroup musicGroup;

        // Kept so the scene serialization stays put. These boxes are not spawned.
#pragma warning disable CS0414
        [SerializeField] private Vector3 churchZoneCenter = new(0f, 8f, -20f);
        [SerializeField] private Vector3 churchZoneSize = new(140f, 50f, 140f);
        [SerializeField] private Vector3 graveyardZoneCenter = new(-70f, 8f, 80f);
        [SerializeField] private Vector3 graveyardZoneSize = new(110f, 50f, 110f);
#pragma warning restore CS0414

        public AudioClip ChurchMusic => churchMusic;
        public AudioClip ChurchAmbient => churchAmbient;
        public AudioClip GraveyardMusic => graveyardMusic;
        public AudioClip GraveyardAmbient => graveyardAmbient;
        public AudioMixerGroup MusicGroup => musicGroup;
    }
}
