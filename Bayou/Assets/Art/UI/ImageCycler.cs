using UnityEngine;
using UnityEngine.UI;

public class ImageCycler : MonoBehaviour
{
    public Image targetImage;
    public Sprite[] images;
    public float interval = 0.5f;
    public float fadeSpeed = 3f;

    int index = 0;
    float timer;
    bool fadingOut;

    void Update()
    {
        timer += Time.deltaTime;

        Color color = targetImage.color;

        if (timer >= interval && !fadingOut)
            fadingOut = true;

        color.a = Mathf.MoveTowards(color.a, fadingOut ? 0 : 1, Time.deltaTime * fadeSpeed);
        targetImage.color = color;

        if (fadingOut && color.a == 0)
        {
            index = (index + 1) % images.Length;
            targetImage.sprite = images[index];
            fadingOut = false;
            timer = 0;
        }
    }
}
