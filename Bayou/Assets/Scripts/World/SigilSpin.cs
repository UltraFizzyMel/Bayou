using UnityEngine;

public class SigilSpin : MonoBehaviour
{
    public float degreesPerSecond = -24f;   // 15 s per full turn

    void Update()
    {
        transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}