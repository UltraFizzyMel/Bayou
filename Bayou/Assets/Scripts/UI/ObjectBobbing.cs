using UnityEngine;
using DG.Tweening;

public class ObjectBobbing : MonoBehaviour
{
    public float bobHeight = 0.5f;
    public float duration = 1.0f;

    private void Start()
    {
        float startY = transform.position.y;

        transform.DOMoveY(startY + bobHeight, duration).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }
}
