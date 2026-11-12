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
    public sealed class LibreViesTypeMonstre
    {
        public int id;
        public string nom;
        public int pv;
        public int agressif;
        public int xp;
        public int loot;
        public int coup_min;
        public int coup_max;
        public int attparmin;   // attaques par minute (0 = n'attaque jamais)
        public int regen;       // regeneration des PV, en points par minute
    }

    [Serializable]
    public sealed class LibreViesReglageMonstres
    {
        public int id;
        public string lieu;
        public int nombre;
    }

    private sealed class ReponseApi
    {
        public bool ok;
        public string message;
        public string code;
        public Membre membre;
        public LibreViesPersonnage personnage;
        public LibreViesNpc[] npcs;
        public LibreViesReglageMonstres reglage_monstres;
        public LibreViesTypeMonstre[] types_monstres;
        public Membre[] players;
        public LibreViesJoueurAdmin player;
        public LibreViesPosition position;
        public int id;
        public LibreViesElementMonde[] elements;
        public LibreViesMaisonMonde[] maisons;
        public LibreViesJoueurPresent[] joueurs;
        public int revision;
        public bool complet;
        public int[] supprimes;
        public string[] maisons_supprimees;
        public LibreViesMonstre[] monstres;
        public string[] pilotes;
    }

    // Monstre partage (table monde_monstre). mort = 1 ou 0 ; age = secondes depuis la mort.
    [Serializable]
    public sealed class LibreViesMonstre
    {
        public string cle;
        public float x;
        public float z;
        public int pv;
        public int pv_max;
        public int mort;
        public int age;
        public int proprietaire;
        public float rot;
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

    // Joueur connecte vu par le serveur (position envoyee toutes les secondes).
    [Serializable]
    public sealed class LibreViesJoueurPresent
    {
        public int membre_id;
        public string pseudo;
        public float position_x;
        public float position_z;
        public float rotation_y;
        public string animation;
        // Apparence du joueur (meme profil que son personnage). Absent : avatar simple.
        public LibreViesPersonnage personnage;
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
    // Depuis le dernier chargement : ElementsMonde = seulement ce qui a change.
    public LibreViesElementMonde[] ElementsMonde { get; private set; }
    public LibreViesMaisonMonde[] MaisonsMonde { get; private set; }
    public int[] IdsElementsSupprimes { get; private set; }
    public LibreViesMonstre[] Monstres { get; private set; }
    // Monstres que ce joueur pilote (accepte par le serveur) lors du dernier envoi.
    public string[] PilotesAcceptes { get; private set; }
    public LibreViesJoueurPresent[] JoueursPresents { get; private set; }
    public int RevisionMonde { get; private set; }
    public bool MondeComplet { get; private set; }
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
    // Stats des types de monstres (table monstre). Null = pas encore lues.
    public LibreViesTypeMonstre[] TypesMonstres { get; private set; }
    public IEnumerator ChargerTypesMonstres()
    {
        TypesMonstres = null;
        yield return Envoyer("get_types_monstres", null);
    }
    // Modifie une ligne de la table monstre (admin) ; la reponse renvoie toute la table.
    public IEnumerator SauvegarderTypeMonstre(LibreViesTypeMonstre t)
    {
        if (!EstAdministrateur) { Erreur = "Modification des monstres reservee aux administrateurs."; yield break; }
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var champs = new Dictionary<string, string>
        {
            { "id", t.id.ToString(ci) }, { "nom", t.nom ?? "" }, { "pv", t.pv.ToString(ci) },
            { "agressif", t.agressif.ToString(ci) }, { "xp", t.xp.ToString(ci) },
            { "loot", t.loot.ToString(ci) }, { "coup_min", t.coup_min.ToString(ci) },
            { "coup_max", t.coup_max.ToString(ci) }, { "attparmin", t.attparmin.ToString(ci) }, { "regen", t.regen.ToString(ci) }
        };
        yield return Envoyer("save_type_monstre", null, null, champs);
    }
    // Quantite de monstres enregistree dans la table monstre (-1 = pas encore lue).
    public int NombreMonstres { get; private set; }
    public IEnumerator ChargerMonstres()
    {
        NombreMonstres = -1;
        yield return Envoyer("get_monstres", null);
    }
    public IEnumerator SauvegarderMonstres(int nombre)
    {
        if (!EstAdministrateur) { Erreur = "Quantite de monstres reservee aux administrateurs."; yield break; }
        var champs = new Dictionary<string, string>
        {
            { "nombre", nombre.ToString(System.Globalization.CultureInfo.InvariantCulture) }
        };
        yield return Envoyer("save_monstres", null, null, champs);
    }
    public IEnumerator SauvegarderNpc(string id, LibreViesPersonnage profil)
    {
        if (!EstAdministrateur) { Erreur = "Edition NPC reservee aux administrateurs."; yield break; }
        yield return Envoyer("save_npc", profil, id);
    }
    // depuis = 0 : tout le monde. Sinon : seulement les changements depuis cette revision.
    public IEnumerator ChargerMonde(int depuis = 0)
    {
        var champs = new Dictionary<string, string>
        {
            { "depuis", depuis.ToString(System.Globalization.CultureInfo.InvariantCulture) }
        };
        yield return Envoyer("get_monde", null, null, champs);
    }

    // Declare les monstres du client (JSON : cle, type, x, z, pv_max). Le serveur
    // garde la premiere position connue et renvoie l'etat de chaque monstre.
    public IEnumerator DeclarerMonstres(string monstresJson)
    {
        yield return Envoyer("declarer_monstres", null, null,
            new Dictionary<string, string> { { "monstres", monstresJson ?? "[]" } });
    }

    // Envoie les positions des monstres pilotes ; le serveur renvoie ceux qu'il accepte.
    public IEnumerator PiloterMonstres(string monstresJson)
    {
        PilotesAcceptes = null;
        yield return Envoyer("piloter_monstres", null, null,
            new Dictionary<string, string> { { "monstres", monstresJson ?? "[]" } });
    }

    // Fait revivre des monstres morts depuis 10 s, a des points donnes.
    public IEnumerator RessusciterMonstres(string monstresJson)
    {
        yield return Envoyer("ressusciter_monstres", null, null,
            new Dictionary<string, string> { { "monstres", monstresJson ?? "[]" } });
    }

    // Un coup porte sur un monstre : les pv baissent pour tous les joueurs.
    public IEnumerator FrapperMonstre(string cle, int degats)
    {
        yield return Envoyer("frapper_monstre", null, null, new Dictionary<string, string>
        {
            { "cle", cle ?? "" },
            { "degats", degats.ToString(System.Globalization.CultureInfo.InvariantCulture) }
        });
    }

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

    // Enregistre une maison (identifiant lisible, position, reglages en JSON) dans la base partagee.
    public IEnumerator EnregistrerMaisonMonde(string cle, string nom, float x, float z, float rotation, string donnees,
        string genre = "maison")
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var champs = new Dictionary<string, string>
        {
            { "genre", genre ?? "maison" },
            { "id", cle ?? "" },
            { "nom", nom ?? "" },
            { "x", x.ToString("0.###", culture) },
            { "z", z.ToString("0.###", culture) },
            { "rotation", rotation.ToString("0.###", culture) },
            { "donnees", donnees ?? "{}" }
        };
        yield return Envoyer("enregistrer_maison", null, null, champs);
    }

    // Renomme une maison deja enregistree dans la base partagee.
    public IEnumerator RenommerMaisonMonde(string cle, string nom)
    {
        var champs = new Dictionary<string, string>
        {
            { "id", cle ?? "" },
            { "nom", nom ?? "" }
        };
        yield return Envoyer("renommer_maison", null, null, champs);
    }

    // Envoie la position du joueur local et recoit celles des autres joueurs.
    public IEnumerator EnvoyerPresence(float x, float z, float rotation, string animation)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var champs = new Dictionary<string, string>
        {
            { "x", x.ToString("0.###", culture) },
            { "z", z.ToString("0.###", culture) },
            { "rotation", rotation.ToString("0.###", culture) },
            { "animation", animation ?? "Idle" }
        };
        yield return Envoyer("presence", null, null, champs);
    }

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

    public IEnumerator SauvegarderCaracteristiques(int id, string pseudo, int droit,
        string experience, string chasse, string territoire)
    {
        if (!EstAdministrateur) { Erreur = "Modification reservee aux administrateurs."; yield break; }
        yield return Envoyer("save_player_stats", null, null, new Dictionary<string, string>
        {
            { "player_id", id.ToString(CultureInfo.InvariantCulture) },
            { "pseudo", pseudo }, { "droit", droit.ToString(CultureInfo.InvariantCulture) },
            { "experience", experience }, { "chasse", chasse }, { "territoire", territoire }
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
                IdsElementsSupprimes = reponse.supprimes ?? new int[0];
                RevisionMonde = reponse.revision;
                MondeComplet = reponse.complet;
            }
            if (action == "get_monde" || action == "declarer_monstres" || action == "ressusciter_monstres")
                Monstres = reponse.monstres ?? new LibreViesMonstre[0];
            if (action == "piloter_monstres")
                PilotesAcceptes = reponse.pilotes ?? new string[0];
            if (action == "ajouter_element_monde") DernierIdMonde = reponse.id;
            if (action == "presence") JoueursPresents = reponse.joueurs ?? new LibreViesJoueurPresent[0];
            if (action == "search_players" || action == "get_player" || action == "save_player_ban"
                || action == "save_player_stats")
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
            // Actions du monde et de la presence : leur reponse ne contient pas de personnage.
            // Elles ne doivent pas passer par le controle du personnage (sinon : « Reponse du compte invalide »).
            if (action == "get_types_monstres" || action == "save_type_monstre")
            {
                LibreViesTypeMonstre[] types = reponse.types_monstres;
                if (types == null || types.Length == 0) { Erreur = "Reponse types de monstres invalide."; yield break; }
                HashSet<int> identifiants = new HashSet<int>();
                for (int i = 0; i < types.Length; i++)
                {
                    LibreViesTypeMonstre t = types[i];
                    if (t == null || t.id < 1 || !identifiants.Add(t.id) || string.IsNullOrEmpty(t.nom)
                        || t.pv <= 0 || (t.agressif != 0 && t.agressif != 1) || t.xp < 0 || t.loot < 0
                        || t.coup_min < 0 || t.coup_min > t.coup_max || t.attparmin < 0 || t.regen < 0)
                    { Erreur = "Type de monstre invalide dans la table monstre."; yield break; }
                }
                TypesMonstres = types;
                yield break;
            }
            if (action == "get_monstres" || action == "save_monstres")
            {
                if (reponse.reglage_monstres == null || reponse.reglage_monstres.lieu != "village depart"
                    || reponse.reglage_monstres.nombre < 0)
                { Erreur = "Reponse monstres invalide."; yield break; }
                NombreMonstres = reponse.reglage_monstres.nombre;
                yield break;
            }
            if (action == "get_monde" || action == "presence" || action == "ajouter_element_monde"
                || action == "modifier_element_monde" || action == "supprimer_element_monde"
                || action == "vider_monde" || action == "enregistrer_maison" || action == "renommer_maison"
                || action == "declarer_monstres" || action == "piloter_monstres"
                || action == "ressusciter_monstres" || action == "frapper_monstre")
                yield break;
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
