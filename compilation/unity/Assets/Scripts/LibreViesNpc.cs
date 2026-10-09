using System;

// npc.id est du texte et n'est jamais un identifiant de compte joueur.
[Serializable]
public sealed class LibreViesNpc
{
    public string id;
    public string sexe;
    public float tete, yeux, nez, bouche, oreilles, seins, volume, hanche, ventre;
    public float largeur_bras, longueur_bras, hauteur_jambe, pieds;
    public string teinte_peau, coiffure, chaussures, chapeau, tenue, objets;

    public LibreViesPersonnage ProfilHumain()
    {
        return new LibreViesPersonnage
        {
            @default = 0, sexe = sexe, tete = tete, yeux = yeux, nez = nez,
            bouche = bouche, oreilles = oreilles, seins = seins, volume = volume,
            hanche = hanche, ventre = ventre, largeur_bras = largeur_bras,
            longueur_bras = longueur_bras, hauteur_jambe = hauteur_jambe, pieds = pieds,
            teinte_peau = teinte_peau, coiffure = coiffure, chaussures = chaussures,
            chapeau = chapeau, tenue = tenue, objets = objets
        };
    }

    public bool EstValide(out string erreur)
    {
        erreur = "Identifiant NPC inconnu.";
        if (id != "maire" && id != "forgeron" && id != "marchand" && id != "esthetique"
            && id != "garde_nord" && id != "garde_sud") return false;
        return ProfilHumain().EstValide(out erreur);
    }
}
