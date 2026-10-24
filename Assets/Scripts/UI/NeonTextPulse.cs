using UnityEngine;
using TMPro;

public class NeonTextPulse : MonoBehaviour
{
    [SerializeField] private TMP_Text textLabel;
    [SerializeField] private float speed = 2f;
    [SerializeField] private float minAlpha = 0.65f;
    [SerializeField] private float maxAlpha = 1f;

    private Color baseColor;

    void Awake()
    {
        if (textLabel == null)
            textLabel = GetComponent<TMP_Text>();

        baseColor = textLabel.color;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.unscaledTime * speed) + 1f) / 2f;
        Color c = baseColor;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        textLabel.color = c;
    }
}
