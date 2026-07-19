using Bayou.Player;
using UnityEngine;

namespace Bayou.Audio
{
    /// <summary>
    /// Footstep / wade / swim loops driven by motor + water sensor. Drag clips in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLocomotionAudio : MonoBehaviour
    {
        [SerializeField] private BayouCharacterMotor motor;
        [SerializeField] private BayouWaterSensor waterSensor;
        [SerializeField] private SfxPlayer sfx;

        [Header("Dry land (marsh)")]
        [SerializeField] private AudioClip[] walkingInMarsh;

        [Header("Shallow water")]
        [SerializeField] private AudioClip[] wadingInWater;

        [Header("Deep / sustained water move")]
        [SerializeField] private AudioClip swimming;

        [Header("Timing")]
        [SerializeField] private float stepInterval = 0.42f;
        [SerializeField] private float moveSpeedThreshold = 0.35f;
        [SerializeField] private float swimLoopVolume = 0.55f;

        private float _nextStepTime;

        private void Awake()
        {
            if (motor == null)
                motor = GetComponent<BayouCharacterMotor>();
            if (waterSensor == null)
                waterSensor = GetComponent<BayouWaterSensor>();
            if (sfx == null)
                sfx = GetComponent<SfxPlayer>() ?? gameObject.AddComponent<SfxPlayer>();
            sfx.EnsureSource();
        }

        private void Update()
        {
            if (motor == null) return;

            var moving = motor.PlanarSpeed >= moveSpeedThreshold && motor.HasMoveInput;
            var inWater = waterSensor != null && waterSensor.InWater;

            if (!moving)
            {
                sfx.StopLoop();
                return;
            }

            if (inWater)
            {
                // Sustained swim bed under discrete wade steps.
                if (swimming != null)
                    sfx.PlayLoop(swimming, swimLoopVolume);

                if (Time.time >= _nextStepTime)
                {
                    PlayRandom(wadingInWater, 0.85f);
                    _nextStepTime = Time.time + stepInterval;
                }
            }
            else
            {
                if (sfx.IsPlayingClip(swimming))
                    sfx.StopLoop();

                if (Time.time >= _nextStepTime)
                {
                    PlayRandom(walkingInMarsh, 1f);
                    _nextStepTime = Time.time + stepInterval;
                }
            }
        }

        private void PlayRandom(AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            sfx.PlayOneShot(clip, volume);
        }
    }
}
