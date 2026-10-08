using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Le jeu appelle PHP, jamais MySQL. Le launcher transmet uniquement une
// session temporaire en memoire ; aucun mot de passe ni fichier de session.
public sealed class LibreViesCompte
{
    [Serializable]
    public sealed class Membre
    {
        public int id;
        public string pseudo;
        public int valider;
    }

    [Serializable]
    private sealed class ReponseApi
    {
        public bool ok;
        public string message;
        public Membre membre;
        public LibreViesPersonnage personnage;
    }

    [Serializable]
    private sealed class ConfigurationApi
    {
        public string api_url;
    }

    private static LibreViesCompte instance;
    private string jeton;
    private string url = "http://localhost/serveur/api.php";
    // RAZ Ville recharge la scene, pas le compte : garder la session en RAM.
    public static LibreViesCompte Courant
    {
        get { return instance ?? (instance = new LibreViesCompte()); }
    }

    public bool LanceParLauncher { get; private set; }
    public Membre Joueur { get; private set; }
    public LibreViesPersonnage Personnage { get; private set; }
    public string Erreur { get; private set; }

    public bool SessionPresente
    {
        get { return !String.IsNullOrEmpty(jeton); }
    }

    public bool Authentifie
    {
        get { return SessionPresente && Joueur != null && Joueur.id > 0; }
    }

    private LibreViesCompte()
    {
        jeton = Environment.GetEnvironmentVariable("LIBREVIES_SESSION_TOKEN");
        LanceParLauncher = !String.IsNullOrEmpty(jeton);
        // Eviter que d'autres processus lances plus tard par le jeu l'heritent.
        Environment.SetEnvironmentVariable("LIBREVIES_SESSION_TOKEN", null);
        string urlLauncher = Environment.GetEnvironmentVariable("LIBREVIES_API_URL");
        if (UrlValide(urlLauncher)) url = urlLauncher;
        else ChargerConfigurationApi();
    }

    public IEnumerator Charger()
    {
        yield return Envoyer("get_character", null);
    }

    public IEnumerator Sauvegarder(LibreViesPersonnage profil)
    {
        yield return Envoyer("save_character", profil);
    }

    private IEnumerator Envoyer(string action, LibreViesPersonnage profil)
    {
        Erreur = "";
        if (!SessionPresente)
        {
            Erreur = "Reconnectez-vous depuis le launcher pour enregistrer votre personnage.";
            yield break;
        }
        // Formulaire classique : evite $HTTP_RAW_POST_DATA sur PHP 5.6.
        // Seul le profil est encode en JSON pour son transport, jamais en SQL.
        string formulaire = "action=" + action + "&session_token=" + UnityWebRequest.EscapeURL(jeton);
        if (profil != null)
            formulaire += "&personnage=" + UnityWebRequest.EscapeURL(JsonUtility.ToJson(profil));
        byte[] corps = Encoding.UTF8.GetBytes(formulaire);
        using (UnityWebRequest requete = new UnityWebRequest(url, "POST"))
        {
            requete.uploadHandler = new UploadHandlerRaw(corps);
            requete.downloadHandler = new DownloadHandlerBuffer();
            requete.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded; charset=utf-8");
            requete.SetRequestHeader("Accept", "application/json");
            requete.timeout = 20;
            yield return requete.SendWebRequest();
            ReponseApi reponse = null;
            try
            {
                reponse = JsonUtility.FromJson<ReponseApi>(requete.downloadHandler.text);
            }
            catch (Exception) { }
            if (requete.result != UnityWebRequest.Result.Success || reponse == null || !reponse.ok)
            {
                Erreur = reponse != null && !String.IsNullOrEmpty(reponse.message)
                    ? reponse.message : "Serveur inaccessible. Reessayez sans fermer le panel.";
                if (requete.responseCode == 401)
                {
                    jeton = null;
                    Joueur = null;
                }
                // Ne jamais journaliser le corps POST, le jeton ou la reponse.
                yield break;
            }
            string erreurProfil = "";
            if (reponse.membre == null || reponse.membre.id <= 0
                || String.IsNullOrEmpty(reponse.membre.pseudo) || reponse.personnage == null
                || reponse.personnage.id != reponse.membre.id
                || (action == "save_character" && reponse.personnage.EstPrimitif)
                || !reponse.personnage.EstValide(out erreurProfil))
            {
                Erreur = String.IsNullOrEmpty(erreurProfil)
                    ? "Reponse du compte invalide." : erreurProfil;
                yield break;
            }
            Joueur = reponse.membre;
            Personnage = reponse.personnage;
        }
    }

    private static bool UrlValide(string valeur)
    {
        Uri adresse;
        return Uri.TryCreate(valeur, UriKind.Absolute, out adresse)
            && (adresse.Scheme == Uri.UriSchemeHttp || adresse.Scheme == Uri.UriSchemeHttps);
    }

    private void ChargerConfigurationApi()
    {
        try
        {
            string dossierJeu = Path.GetDirectoryName(Application.dataPath);
            string dossierInstallation = dossierJeu == null ? null : Path.GetDirectoryName(dossierJeu);
            string chemin = Path.Combine(dossierInstallation ?? dossierJeu ?? Application.dataPath,
                "auth_config.json");
            if (!File.Exists(chemin)) return;
            ConfigurationApi configuration = JsonUtility.FromJson<ConfigurationApi>(File.ReadAllText(chemin));
            if (configuration != null && UrlValide(configuration.api_url)) url = configuration.api_url;
        }
        catch (Exception)
        {
            Debug.LogWarning("[LV] Configuration API illisible : adresse locale conservee.");
        }
    }
}
