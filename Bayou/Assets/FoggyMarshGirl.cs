using System.Collections;
using UnityEngine;

public class FoggyMarshGirl : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float waitTime;

    // Update is called once per frame
    void Start()
    {
        StartCoroutine(GirlAnimationSwitch(waitTime));
    }

    IEnumerator GirlAnimationSwitch(float waitTime)
        {
        yield return new WaitForSeconds(waitTime);
        bool isClicking = animator.GetBool("IsHeelClicking");
        if (isClicking)
        { animator.SetBool("IsHeelClicking", false); }
        else { animator.SetBool("IsHeelClicking", true); }
            

    }
}
