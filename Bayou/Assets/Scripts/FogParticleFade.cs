using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class FogParticleFade : MonoBehaviour
{
    [SerializeField] private float remaininglife;
    ParticleSystem ps;

    // these lists are used to contain the particles which match
    // the trigger conditions each frame.
    List<ParticleSystem.Particle> enter = new List<ParticleSystem.Particle>();

    void OnEnable()
    {
        ps = GetComponent<ParticleSystem>();
    }

    private void OnParticleTrigger()
    {
        // get the particles which matched the trigger conditions this frame
        int numEnter = ps.GetTriggerParticles(ParticleSystemTriggerEventType.Enter, enter);

        for (int i = 0; i < numEnter; i++)
        {
            ParticleSystem.Particle p = enter[i];
            p.remainingLifetime = remaininglife;
            p.color = p.GetCurrentColor.DOFade(0f, 2f);
            
            enter[i] = p;
            
        }

        
        ps.SetTriggerParticles(ParticleSystemTriggerEventType.Enter, enter);
    }
}
