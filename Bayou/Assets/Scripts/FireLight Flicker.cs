using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class FireLightFlicker : MonoBehaviour
{
    Light myLight;
    float baseIntensity;

    [SerializeField]
    private int min = -20;
    [SerializeField]
    private int max = 20;

    void Start()
    {
        myLight = GetComponent<Light>(); //get the light component of the light object
        baseIntensity = myLight.intensity;
    }

    void Update()
    {
        
        int randomNumberInRange = Random.Range(min, max);
        myLight.intensity = baseIntensity + randomNumberInRange;

    }
}
