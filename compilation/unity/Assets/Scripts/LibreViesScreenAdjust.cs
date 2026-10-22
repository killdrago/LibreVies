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
    private float halo = 0f;
    private float etalonnage = 0f;
    private float vignettage = 0f;

    // q entre 0 (faible) et 1 (maximum).
    public void SetQualite(float q)
    {
        q = Mathf.Clamp01(q);
        nettete = q * 0.6f;
        vivacite = q * 0.15f;
        etalonnage = q * 0.5f;
        vignettage = q * 0.25f;
    }

    // intensite 0..1 (curseur « Halo des lumieres » des options). 100 % = 0,5 d'ajout.
    public void SetHalo(float intensite)
    {
        halo = Mathf.Clamp01(intensite) * 0.5f;
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
        material.SetFloat("_Etalonnage", etalonnage);
        material.SetFloat("_Vignettage", vignettage);
        material.SetFloat("_HaloIntensite", halo);

        if (halo > 0.001f)
        {
            // Halo : seuil puis flou sur un quart de la resolution (peu couteux).
            int largeur = Mathf.Max(1, source.width / 4);
            int hauteur = Mathf.Max(1, source.height / 4);
            RenderTexture a = RenderTexture.GetTemporary(largeur, hauteur, 0, source.format);
            RenderTexture b = RenderTexture.GetTemporary(largeur, hauteur, 0, source.format);
            Graphics.Blit(source, a, material, 1);
            Graphics.Blit(a, b, material, 2);
            Graphics.Blit(b, a, material, 3);
            material.SetTexture("_Halo", a);
            Graphics.Blit(source, destination, material, 0);
            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
        }
        else
        {
            material.SetTexture("_Halo", Texture2D.blackTexture);
            Graphics.Blit(source, destination, material, 0);
        }
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
