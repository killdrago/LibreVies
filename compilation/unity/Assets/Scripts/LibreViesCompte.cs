using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Le jeu appelle PHP, jamais MySQL. La session reste en RAM, jamais un mot de passe.
public sealed class LibreViesCompte
{
    [Serializable]
    public sealed class Membre
    {
        public int id;
        public string pseudo;
        public string valider;
        public int droit;
        public string bani;
        // Renseigne seulement dans la consultation admin du joueur choisi.
        public string email;
    }

    [Serializable]
    private sealed class ReponseApi
    {
        public bool ok;
        public string message;
        public string code;
        public Membre membre;
        public LibreViesPersonnage personnage;
        public LibreViesNpc[] npcs;
        public Membre[] players;
        public LibreViesJoueurAdmin player;
        public LibreViesPosition position;
        public int id;
        public LibreViesElementMonde[] elements;
        public LibreViesMaisonMonde[] maisons;
    }

    // Monde partage (tables monde_element et monde_maison, voir monde.sql).
    [Serializable]
    public sealed class LibreViesElementMonde
    {
        public int id;
        public string type;
        public float x;
        public float z;
        public float rotation;
        public float taille;
    }

    [Serializable]
    public sealed class LibreViesMaisonMonde
    {
        public string id;
        public string nom;
        public float x;
        public float z;
        public float rotation;
        public string donnees;
    }

    [Serializable]
    private sealed class ConfigurationApi { public string api_url; }

    private static LibreViesCompte instance;
    private string jeton;
    private string url = "http://localhost/serveur/api.php";
    public static LibreViesCompte Courant
    {
        get { return instance ?? (instance = new LibreViesCompte()); }
    }

    public bool LanceParLauncher { get; private set; }
    public Membre Joueur { get; private set; }
    public LibreViesPersonnage Personnage { get; private set; }
    public string Erreur { get; private set; }
    public string ErreurPosition { get; private set; }
    public LibreViesPosition Position { get; private set; }
    public LibreViesNpc[] Npcs { get; private set; }
    public Membre[] JoueursTrouves { get; private set; }
    public LibreViesJoueurAdmin JoueurAdministratif { get; private set; }
    public LibreViesElementMonde[] ElementsMonde { get; private set; }
    public LibreViesMaisonMonde[] MaisonsMonde { get; private set; }
    public int DernierIdMonde { get; private set; }

    public bool EstAdministrateur
    {
        get { return Authentifie && Joueur.droit == 1; }
    }
    public bool SessionPresente { get { return !String.IsNullOrEmpty(jeton); } }
    public bool Authentifie
    {
        get { return SessionPresente && Joueur != null && Joueur.id > 0 && Joueur.bani == "non"; }
    }

    private LibreViesCompte()
    {
        jeton = Environment.GetEnvironmentVariable("LIBREVIES_SESSION_TOKEN");
        LanceParLauncher = !String.IsNullOrEmpty(jeton);
        Environment.SetEnvironmentVariable("LIBREVIES_SESSION_TOKEN", null);
        string urlLauncher = Environment.GetEnvironmentVariable("LIBREVIES_API_URL");
        if (UrlValide(urlLauncher)) url = urlLauncher;
        else ChargerConfigurationApi();
    }

    public IEnumerator Charger() { yield return Envoyer("get_character", null); }
    public IEnumerator ChargerNpcs()
    {
        Npcs = null;
        yield return Envoyer("get_npcs", null);
    }
    public IEnumerator Sauvegarder(LibreViesPersonnage profil)
    {
        yield return Envoyer("save_character", profil);
    }
    public IEnumerator SauvegarderNpc(string id, LibreViesPersonnage profil)
    {
        if (!EstAdministrateur) { Erreur = "Edition NPC reservee aux administrateurs."; yield break; }
        yield return Envoyer("save_npc", profil, id);
    }
    public IEnumerator ChargerMonde() { yield return Envoyer("get_monde", null); }

    public IEnumerator AjouterElementMonde(LibreViesElementMonde element)
    {
        yield return Envoyer("ajouter_element_monde", null, null, ChampsElementMonde(element));
    }

    public IEnumerator ModifierElementMonde(LibreViesElementMonde element)
    {
        var champs = ChampsElementMonde(element);
        champs["id"] = element.id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        yield return Envoyer("modifier_element_monde", null, null, champs);
    }

    public IEnumerator SupprimerElementMonde(int id)
    {
        yield return Envoyer("supprimer_element_monde", null, null,
            new Dictionary<string, string> { { "id", id.ToString(System.Globalization.CultureInfo.InvariantCulture) } });
    }

    public IEnumerator ViderMonde() { yield return Envoyer("vider_monde", null); }

    private static Dictionary<string, string> ChampsElementMonde(LibreViesElementMonde element)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        return new Dictionary<string, string>
        {
            { "type", element.type ?? "" },
            { "x", element.x.ToString("0.###", culture) },
            { "z", element.z.ToString("0.###", culture) },
            { "rotation", element.rotation.ToString("0.###", culture) },
            { "taille", element.taille.ToString("0.###", culture) }
        };
    }

    public IEnumerator RechercherJoueurs(string recherche)
    {
        JoueursTrouves = new Membre[0];
        if (!EstAdministrateur) { Erreur = "Recherche joueurs reservee aux administrateurs."; yield break; }
        yield return Envoyer("search_players", null, null,
            new Dictionary<string, string> { { "search", recherche ?? "" } });
    }
    public IEnumerator ChargerJoueurAdministration(int id)
    {
        JoueurAdministratif = null;
        if (!EstAdministrateur) { Erreur = "Consultation reservee aux administrateurs."; yield break; }
        yield return Envoyer("get_player", null, null,
            new Dictionary<string, string> { { "player_id", id.ToString(CultureInfo.InvariantCulture) } });
    }
    public IEnumerator SauvegarderBannissement(int id, bool bani)
    {
        if (!EstAdministrateur) { Erreur = "Bannissement reserve aux administrateurs."; yield break; }
        yield return Envoyer("save_player_ban", null, null, new Dictionary<string, string>
        {
            { "player_id", id.ToString(CultureInfo.InvariantCulture) }, { "bani", bani ? "oui" : "non" }
        });
    }

    public System.Collections.IEnumerator SauvegarderPosition(LibreViesPosition position)
    {
        // Erreur distincte : les autosaves ne doivent pas effacer le retour du panel ADMIN.
        ErreurPosition = "";
        if (!Authentifie || position == null || !position.EstValide)
        { ErreurPosition = "Position/compte indisponible."; yield break; }
        string tokenRequete = jeton;
        int idRequete = Joueur.id;
        string formulaire = "action=save_position&session_token=" + UnityWebRequest.EscapeURL(tokenRequete)
            + "&position=" + UnityWebRequest.EscapeURL(JsonUtility.ToJson(position));
        using (UnityWebRequest requete = new UnityWebRequest(url, "POST"))
        {
            requete.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(formulaire));
            requete.downloadHandler = new DownloadHandlerBuffer();
            requete.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded; charset=utf-8");
            requete.SetRequestHeader("Accept", "application/json");
            requete.timeout = 5; // La fermeture ne doit pas attendre un serveur en panne.
            yield return requete.SendWebRequest();
            ReponseApi reponse = null;
            try { reponse = JsonUtility.FromJson<ReponseApi>(requete.downloadHandler.text); }
            catch (Exception) { }
            if (jeton != tokenRequete) { ErreurPosition = "Connexion changee pendant la sauvegarde."; yield break; }
            if (requete.result != UnityWebRequest.Result.Success || reponse == null || !reponse.ok)
            {
                ErreurPosition = reponse != null && !String.IsNullOrEmpty(reponse.message)
                    ? reponse.message : "Sauvegarde position inaccessible.";
                if (requete.responseCode == 401 || (reponse != null && reponse.code == "membre_bani"))
                { jeton = null; Joueur = null; }
                yield break;
            }
            if (reponse.membre == null || reponse.membre.id != idRequete || reponse.membre.bani != "non"
                || reponse.position == null || !reponse.position.EstValide)
            { ErreurPosition = "Reponse position invalide."; yield break; }
            // Une reponse d'autosave ne doit pas reaccorder un ancien droit client.
            Position = reponse.position;
        }
    }

    private IEnumerator Envoyer(string action, LibreViesPersonnage profil, string idNpc = null,
        Dictionary<string, string> champs = null)
    {
        Erreur = "";
        if (!SessionPresente)
        {
            Erreur = "Reconnectez-vous depuis le launcher pour enregistrer votre personnage.";
            yield break;
        }
        string formulaire = "action=" + action + "&session_token=" + UnityWebRequest.EscapeURL(jeton);
        if (idNpc != null) formulaire += "&npc_id=" + UnityWebRequest.EscapeURL(idNpc);
        if (profil != null) formulaire += "&personnage=" + UnityWebRequest.EscapeURL(JsonUtility.ToJson(profil));
        if (champs != null)
            foreach (KeyValuePair<string, string> champ in champs)
                formulaire += "&" + UnityWebRequest.EscapeURL(champ.Key) + "=" + UnityWebRequest.EscapeURL(champ.Value);
        using (UnityWebRequest requete = new UnityWebRequest(url, "POST"))
        {
            requete.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(formulaire));
            requete.downloadHandler = new DownloadHandlerBuffer();
            requete.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded; charset=utf-8");
            requete.SetRequestHeader("Accept", "application/json");
            requete.timeout = 20;
            yield return requete.SendWebRequest();
            ReponseApi reponse = null;
            try { reponse = JsonUtility.FromJson<ReponseApi>(requete.downloadHandler.text); }
            catch (Exception) { }
            if (requete.result != UnityWebRequest.Result.Success || reponse == null || !reponse.ok)
            {
                Erreur = reponse != null && !String.IsNullOrEmpty(reponse.message)
                    ? reponse.message : "Serveur inaccessible. Reessayez sans fermer le panel.";
                if (requete.responseCode == 403 && Joueur != null) Joueur.droit = 0;
                if (requete.responseCode == 401 || (reponse != null && reponse.code == "membre_bani"))
                {
                    jeton = null;
                    Joueur = null;
                    JoueursTrouves = null;
                    JoueurAdministratif = null;
                }
                // Ne jamais journaliser le corps POST, le jeton ou la reponse.
                yield break;
            }
            if (reponse.membre == null || reponse.membre.id <= 0
                || String.IsNullOrEmpty(reponse.membre.pseudo)
                || (reponse.membre.valider != "non" && reponse.membre.valider != "oui")
                || (reponse.membre.bani != "non" && reponse.membre.bani != "oui"))
            {
                Erreur = "Reponse du compte invalide : statut bani requis, mettez l'API a jour.";
                yield break;
            }
            if (action == "get_monde")
            {
                ElementsMonde = reponse.elements ?? new LibreViesElementMonde[0];
                MaisonsMonde = reponse.maisons ?? new LibreViesMaisonMonde[0];
            }
            if (action == "ajouter_element_monde") DernierIdMonde = reponse.id;
            if (action == "search_players" || action == "get_player" || action == "save_player_ban")
            {
                if (Joueur == null || reponse.membre.id != Joueur.id)
                {
                    Erreur = "Identite administrateur invalide.";
                    yield break;
                }
                if (action == "search_players")
                {
                    if (reponse.players == null || reponse.players.Length > 30)
                    { Erreur = "Liste de joueurs invalide."; yield break; }
                    HashSet<int> ids = new HashSet<int>();
                    for (int i = 0; i < reponse.players.Length; i++)
                    {
                        Membre trouve = reponse.players[i];
                        if (trouve == null || trouve.id <= 0 || String.IsNullOrEmpty(trouve.pseudo)
                            || (trouve.bani != "non" && trouve.bani != "oui") || !ids.Add(trouve.id))
                        { Erreur = "Resultat joueur invalide ou duplique."; yield break; }
                    }
                    JoueursTrouves = reponse.players;
                }
                else
                {
                    int attendu;
                    if (champs == null || !Int32.TryParse(champs["player_id"], out attendu)
                        || reponse.player == null || reponse.player.membre == null
                        || reponse.player.membre.id != attendu || attendu <= 0
                        || (reponse.player.membre.bani != "non" && reponse.player.membre.bani != "oui")
                        || (reponse.player.personnage != null && reponse.player.personnage.id != attendu))
                    { Erreur = "Fiche du joueur invalide."; yield break; }
                    JoueurAdministratif = reponse.player;
                }
                Joueur = reponse.membre; // Toujours l'acteur, jamais le joueur recherche.
                yield break;
            }
            if (reponse.membre.bani != "non")
            { Erreur = "Joueur bani veuillez contacter l'administrateur"; jeton = null; Joueur = null; yield break; }
            if (action == "get_npcs" || action == "save_npc")
            {
                if (Joueur == null || reponse.membre.id != Joueur.id
                    || reponse.npcs == null || reponse.npcs.Length != 6)
                { Erreur = "Reponse NPC incomplete : six profils complets sont attendus dans la table npc."; yield break; }
                HashSet<string> identifiants = new HashSet<string>();
                for (int i = 0; i < reponse.npcs.Length; i++)
                {
                    string erreurNpc;
                    if (reponse.npcs[i] == null || !reponse.npcs[i].EstValide(out erreurNpc)
                        || !identifiants.Add(reponse.npcs[i].id))
                    { Erreur = "Profil NPC invalide ou duplique."; yield break; }
                }
                Npcs = reponse.npcs;
                Joueur = reponse.membre;
                yield break;
            }
            string erreurProfil = "";
            if (reponse.personnage == null || reponse.personnage.id != reponse.membre.id
                || (action == "save_character" && reponse.personnage.EstPrimitif)
                || !reponse.personnage.EstValide(out erreurProfil))
            {
                Erreur = String.IsNullOrEmpty(erreurProfil) ? "Reponse du compte invalide." : erreurProfil;
                yield break;
            }
            if (reponse.position != null && !reponse.position.EstValide)
            { Erreur = "Position du compte invalide."; yield break; }
            Joueur = reponse.membre;
            Personnage = reponse.personnage;
            Position = reponse.position;
        }
    }

    // Reponse d'un PNJ (action npc_chat) : passe par l'API, comme les autres actions.
    public string ReponseNpc { get; private set; }

    public IEnumerator DemanderReponseNpc(string requeteJson)
    {
        ReponseNpc = null;
        Erreur = "";
        if (!SessionPresente)
        {
            Erreur = "Reconnectez-vous depuis le launcher pour parler aux PNJ.";
            yield break;
        }
        string formulaire = "action=npc_chat&session_token=" + UnityWebRequest.EscapeURL(jeton)
            + "&requete=" + UnityWebRequest.EscapeURL(requeteJson);
        using (UnityWebRequest requete = new UnityWebRequest(url, "POST"))
        {
            requete.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(formulaire));
            requete.downloadHandler = new DownloadHandlerBuffer();
            requete.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded; charset=utf-8");
            requete.SetRequestHeader("Accept", "application/json");
            requete.timeout = 90;
            yield return requete.SendWebRequest();
            ReponseIaApi reponse = null;
            try { reponse = JsonUtility.FromJson<ReponseIaApi>(requete.downloadHandler.text); }
            catch (Exception) { }
            if (requete.result != UnityWebRequest.Result.Success || reponse == null || !reponse.ok
                || String.IsNullOrEmpty(reponse.reponse))
            {
                Erreur = reponse != null && !String.IsNullOrEmpty(reponse.message)
                    ? reponse.message : "Le serveur IA ne répond pas.";
                yield break;
            }
            ReponseNpc = reponse.reponse;
        }
    }

    private sealed class ReponseIaApi
    {
        public bool ok;
        public string message;
        public string reponse;
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
            string chemin = Path.Combine(dossierInstallation ?? dossierJeu ?? Application.dataPath, "auth_config.json");
            if (!File.Exists(chemin)) return;
            ConfigurationApi configuration = JsonUtility.FromJson<ConfigurationApi>(File.ReadAllText(chemin));
            if (configuration != null && UrlValide(configuration.api_url)) url = configuration.api_url;
        }
        catch (Exception) { Debug.LogWarning("[LV] Configuration API illisible : adresse locale conservee."); }
    }
}
