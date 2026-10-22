using UnityEngine;

// Effet d'image porte par la camera principale. LibreViesGame est un objet
// gestionnaire et ne recoit donc pas OnRenderImage directement.
public sealed class LibreViesScreenAdjust : MonoBehaviour
{
    private Material material;
    private float brightness = 0.5f;
    private float contrast = 1f;
    private float nettete = 0f;
    private float vivacite = 0f;

    // q entre 0 (faible) et 1 (maximum) : netteté et couleurs vives.
    public void SetQualite(float q)
    {
        q = Mathf.Clamp01(q);
        nettete = q * 0.6f;
        vivacite = q * 0.15f;
    }

    public void SetAdjustments(float newBrightness, float newContrast)
    {
        brightness = Mathf.Clamp01(newBrightness);
        contrast = Mathf.Clamp(newContrast, 0.5f, 1.5f);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (material == null)
        {
            Shader shader = Resources.Load<Shader>("LVShaders/LVScreenAdjust");
            if (shader != null) material = new Material(shader);
        }
        if (material == null)
        {
            Graphics.Blit(source, destination);
            return;
        }
        material.SetFloat("_Brightness", brightness);
        material.SetFloat("_Contrast", contrast);
        material.SetFloat("_Nettete", nettete);
        material.SetFloat("_Vivacite", vivacite);
        Graphics.Blit(source, destination, material);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
