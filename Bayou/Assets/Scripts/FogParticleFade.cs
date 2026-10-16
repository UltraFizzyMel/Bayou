using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class FogParticleFade : MonoBehaviour
{
    [SerializeField] private float remaininglife;
    [SerializeField] private float fadeSpeed = 2f;
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
            Color32 col = p.GetCurrentColor(ps);
            if (col.a > 0)
            { 
                float newAlpha = col.a - (fadeSpeed * Time.deltaTime * 255);
                col.a = (byte)Mathf.Max(newAlpha, 0);
                p.color = col;
                //p.GetCurrentColor(ps) = col;

            }
            else if (col.a == 0)
            {
                p.remainingLifetime = remaininglife;
            }
            

            enter[i] = p;
            
        }

        
        ps.SetTriggerParticles(ParticleSystemTriggerEventType.Enter, enter);
    }
}
