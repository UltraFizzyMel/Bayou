using UnityEngine;

namespace Bayou.Audio
{
    /// <summary>
    /// Lives on an existing transition trigger. The area comes from the parent
    /// object name (Into Church, Into Graveyard, and the rest), not the child name.
    /// </summary>
    public sealed class TransitionMusicGate : MonoBehaviour
    {
        private TransitionMusicDirector _director;
        private TransitionMusicDirector.Area _area;
        private bool _listen;

        public void MarkZoneRoot()
        {
            _listen = false;
        }

        public void Arm(TransitionMusicDirector director, TransitionMusicDirector.Area area)
        {
            _director = director;
            _area = area;
            _listen = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_listen || _director == null || !IsPlayer(other)) return;
            _director.NotifyEnter(_area);
        }

        private static bool IsPlayer(Collider other)
        {
            return other != null &&
                   (other.CompareTag("Player") ||
                    other.GetComponentInParent<Bayou.Player.BayouCharacterMotor>() != null);
        }
    }
}
