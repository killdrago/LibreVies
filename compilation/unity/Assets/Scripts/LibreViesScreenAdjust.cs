using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Reglages d'image (luminosite, contraste, vivacite, halo, etalonnage, vignettage)
// appliques en URP par un Volume global cree au demarrage.
// En URP, OnRenderImage n'est plus appele : on passe par les effets de post-traitement.
// Le post-traitement doit rester active dans le URP Asset et sur la camera (par defaut : oui).
public sealed class LibreViesScreenAdjust : MonoBehaviour
{
    private GameObject volumeObjet;
    private ColorAdjustments couleurs;
    private Bloom bloom;
    private Vignette vignette;

    private float brightness = 0.5f;
    private float contrast = 1f;
    private float vivacite = 0f;
    private float haloIntensite = 0f;
    private float etalonnage = 0f;
    private float vignettage = 0f;

    private void Awake()
    {
        CreerVolume();
    }

    // q entre 0 (faible) et 1 (maximum).
    public void SetQualite(float q)
    {
        q = Mathf.Clamp01(q);
        vivacite = q * 0.15f;
        etalonnage = q * 0.5f;
        vignettage = q * 0.25f;
        Appliquer();
    }

    // intensite 0..1 (curseur « Halo des lumieres » des options).
    public void SetHalo(float intensite)
    {
        haloIntensite = Mathf.Clamp01(intensite) * 0.5f;
        Appliquer();
    }

    public void SetAdjustments(float newBrightness, float newContrast)
    {
        brightness = Mathf.Clamp01(newBrightness);
        contrast = Mathf.Clamp(newContrast, 0.5f, 1.5f);
        Appliquer();
    }

    private void CreerVolume()
    {
        if (volumeObjet != null) return;
        var profil = ScriptableObject.CreateInstance<VolumeProfile>();
        couleurs = profil.Add<ColorAdjustments>(true);
        couleurs.postExposure.overrideState = true;
        couleurs.contrast.overrideState = true;
        couleurs.saturation.overrideState = true;
        couleurs.colorFilter.overrideState = true;
        bloom = profil.Add<Bloom>(true);
        bloom.intensity.overrideState = true;
        bloom.threshold.overrideState = true;
        vignette = profil.Add<Vignette>(true);
        vignette.intensity.overrideState = true;
        vignette.smoothness.overrideState = true;

        volumeObjet = new GameObject("LibreViesReglagesEcran");
        var volume = volumeObjet.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.profile = profil;
        Appliquer();
    }

    private void Appliquer()
    {
        if (couleurs == null) return;
        // 0,5 = image neutre ; luminosite en paliers d'exposition (EV).
        couleurs.postExposure.value = Mathf.Log(Mathf.Max(0.05f, 1f + (brightness - 0.5f) * 0.9f), 2f);
        // Contraste : 1 = neutre dans les options, 0 = neutre dans URP.
        couleurs.contrast.value = Mathf.Clamp((contrast - 1f) * 100f, -100f, 100f);
        couleurs.saturation.value = Mathf.Clamp(vivacite * 100f, -100f, 100f);
        // Etalonnage chaud, en approximation (URP n'a pas le meme reglage ombres/lumieres).
        couleurs.colorFilter.value = Color.Lerp(Color.white, new Color(1.04f, 1.0f, 0.95f), etalonnage);

        bloom.intensity.value = haloIntensite * 2f;
        bloom.threshold.value = 0.85f;

        vignette.intensity.value = vignettage * 0.5f;
        vignette.smoothness.value = 0.4f;
    }

    private void OnDestroy()
    {
        if (volumeObjet != null) Destroy(volumeObjet);
    }
}
