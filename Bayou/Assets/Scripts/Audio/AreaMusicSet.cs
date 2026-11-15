using UnityEngine;

namespace Bayou.Audio
{
    /// <summary>Foggy marsh and brackish shore songs, with and without the accordion.</summary>
    [CreateAssetMenu(menuName = "Bayou/Audio/Area Music Set", fileName = "AreaMusicSet")]
    public sealed class AreaMusicSet : ScriptableObject
    {
        public AudioClip foggyWithout;
        public AudioClip foggyWith;
        public AudioClip brackishWithout;
        public AudioClip brackishWith;
    }
}
