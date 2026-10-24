using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MenuButtonStyler : MonoBehaviour
{
    [SerializeField] private Shader buttonShader;
    [SerializeField] private Shader textShader;

    private static readonly Color Blue = new Color(0.10f, 0.68f, 0.92f);
    private static readonly Color Orange = new Color(1f, 0.46f, 0.15f);

    private void Start()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            bool isPlay = button.name == "CampaingButton";
            Image image = button.GetComponent<Image>();
            if (image == null) continue;

            image.color = isPlay ? Orange : Blue;
            if (buttonShader != null)
            {
                Material material = new Material(buttonShader);
                material.SetColor("_GlowColor", isPlay
                    ? new Color(1f, 0.62f, 0.34f, 0.55f)
                    : new Color(0.34f, 0.78f, 0.96f, 0.48f));
                material.SetFloat("_GlowStrength", 0.12f);
                material.SetFloat("_EdgeWidth", 0.025f);
                material.SetFloat("_GlossStrength", 0.08f);
                image.material = material;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null && textShader != null && label.fontSharedMaterial != null)
            {
                Material material = new Material(label.fontSharedMaterial);
                material.shader = textShader;
                material.EnableKeyword("GLOW_ON");
                material.SetColor("_GlowColor", isPlay
                    ? new Color(1f, 0.62f, 0.34f, 0.32f)
                    : new Color(0.34f, 0.78f, 0.96f, 0.30f));
                material.SetFloat("_GlowOuter", 0.08f);
                material.SetFloat("_GlowInner", 0.025f);
                material.SetFloat("_GlowOffset", -0.08f);
                material.SetColor("_OutlineColor", isPlay
                    ? new Color(0.45f, 0.14f, 0.02f)
                    : new Color(0.03f, 0.16f, 0.28f));
                material.SetFloat("_OutlineWidth", 0.08f);
                material.SetFloat("_OutlineSoftness", 0.06f);
                label.fontSharedMaterial = material;
            }
        }
    }
}
