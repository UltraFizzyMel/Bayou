using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using System.Collections;

public class ObjectBobbing : MonoBehaviour
{
    public float bobHeight = 0.5f;
    public float duration = 1.0f;
    public float offSet = 0f;

    private void Start()
    {
        float startY = transform.position.y;
        StartCoroutine(offsetTime(offSet));
        transform.DOMoveY(startY + bobHeight, duration).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }

    IEnumerator  offsetTime(float offset)
    {
        yield return new WaitForSeconds(offset);
    }

}
