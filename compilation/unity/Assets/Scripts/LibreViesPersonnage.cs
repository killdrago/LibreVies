using System;
using System.Globalization;

// DTO HTTP, aligne sur les colonnes de personnage. Le JSON est un format de
// transport uniquement : chaque reglage reste dans sa propre colonne SQL.
[Serializable]
public sealed class LibreViesPersonnage
{
    public int id;
    public int @default = 1;
    public string sexe;
    public float tete;
    public float yeux;
    public float nez;
    public float bouche;
    public float oreilles;
    public float seins;
    // Le slider existant "Jambes largeur" regle leur volume.
    public float volume;
    public float hanche;
    public float ventre;
    public float largeur_bras;
    public float longueur_bras;
    public float hauteur_jambe;
    public float pieds;
    public string teinte_peau;
    public string coiffure;
    public string chaussures;
    public string chapeau;
    public string tenue;
    public string objets;

    public bool EstPrimitif
    {
        get { return @default == 1; }
    }

    public bool EstValide(out string erreur)
    {
        erreur = "";
        if (@default == 1) return true;
        if (@default != 0 || (sexe != "homme" && sexe != "femme"))
        {
            erreur = "Etat ou sexe du personnage invalide.";
            return false;
        }
        float[] valeurs = { tete, yeux, nez, bouche, oreilles, seins, volume,
            hanche, ventre, largeur_bras, longueur_bras, hauteur_jambe, pieds };
        for (int i = 0; i < valeurs.Length; i++)
        {
            if (float.IsNaN(valeurs[i]) || float.IsInfinity(valeurs[i])
                || valeurs[i] < 0f || valeurs[i] > 5f)
            {
                erreur = "Reglage du personnage hors limites.";
                return false;
            }
        }
        int teinte;
        bool femme = sexe == "femme";
        if (!int.TryParse(teinte_peau, NumberStyles.None, CultureInfo.InvariantCulture, out teinte)
            || teinte < 0 || teinte > 6
            || IndexOption(coiffure, MakeHumanClothingFactory.HairOptions(), false) < 0
            || IndexOption(tenue, MakeHumanClothingFactory.ClothingOptions(femme), false) < 0
            || IndexOption(chapeau, MakeHumanClothingFactory.HatOptions(femme), true) < -1
            || IndexOption(chaussures, MakeHumanClothingFactory.ShoeOptions(femme), true) < -1)
        {
            erreur = "Peau, coiffure ou equipement du personnage inconnu.";
            return false;
        }
        return true;
    }

    public void ChargerDans(AdminHumanCreator createur)
    {
        createur.female = sexe == "femme";
        createur.headShape = tete;
        createur.eyesShape = yeux;
        createur.noseShape = nez;
        createur.mouthShape = bouche;
        createur.earsShape = oreilles;
        createur.chestShape = seins;
        createur.legThickness = volume;
        createur.hipShape = hanche;
        createur.belly = ventre;
        createur.armThickness = largeur_bras;
        createur.armLength = longueur_bras;
        createur.legLength = hauteur_jambe;
        createur.feetSize = pieds;
        createur.skinTone = int.Parse(teinte_peau, CultureInfo.InvariantCulture);
        createur.hairStyle = IndexOption(coiffure, MakeHumanClothingFactory.HairOptions(), false);
        createur.clothingStyle = IndexOption(tenue,
            MakeHumanClothingFactory.ClothingOptions(createur.female), false);
        createur.hatStyle = IndexOption(chapeau,
            MakeHumanClothingFactory.HatOptions(createur.female), true);
        createur.shoeStyle = IndexOption(chaussures,
            MakeHumanClothingFactory.ShoeOptions(createur.female), true);
    }

    public static LibreViesPersonnage DepuisCreateur(AdminHumanCreator createur, int idMembre)
    {
        return new LibreViesPersonnage
        {
            id = idMembre,
            @default = 0,
            sexe = createur.female ? "femme" : "homme",
            tete = createur.headShape,
            yeux = createur.eyesShape,
            nez = createur.noseShape,
            bouche = createur.mouthShape,
            oreilles = createur.earsShape,
            seins = createur.chestShape,
            volume = createur.legThickness,
            hanche = createur.hipShape,
            ventre = createur.belly,
            largeur_bras = createur.armThickness,
            longueur_bras = createur.armLength,
            hauteur_jambe = createur.legLength,
            pieds = createur.feetSize,
            teinte_peau = createur.skinTone.ToString(CultureInfo.InvariantCulture),
            coiffure = IdOption(createur.hairStyle, MakeHumanClothingFactory.HairOptions()),
            tenue = IdOption(createur.clothingStyle,
                MakeHumanClothingFactory.ClothingOptions(createur.female)),
            chapeau = IdOption(createur.hatStyle,
                MakeHumanClothingFactory.HatOptions(createur.female)),
            chaussures = IdOption(createur.shoeStyle,
                MakeHumanClothingFactory.ShoeOptions(createur.female))
        };
    }

    private static string IdOption(int index, MakeHumanClothingFactory.Option[] options)
    {
        return index < 0 || index >= options.Length ? null : options[index].id;
    }

    private static int IndexOption(string identifiant, MakeHumanClothingFactory.Option[] options,
        bool optionnel)
    {
        if (String.IsNullOrEmpty(identifiant) && optionnel) return -1;
        for (int i = 0; i < options.Length; i++)
            if (options[i].id == identifiant) return i;
        return -2;
    }
}
