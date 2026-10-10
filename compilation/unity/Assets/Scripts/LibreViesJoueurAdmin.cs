using System;

// Consultation reservee a ADMIN : aucun hash de mot de passe ni jeton.
[Serializable]
public sealed class LibreViesJoueurAdmin
{
    [Serializable]
    public sealed class Classement
    {
        // Texte pour ne pas arrondir un BIGINT UNSIGNED provenant de MySQL.
        public string experience;
        public string chasse;
        public string territoire;
    }

    public LibreViesCompte.Membre membre;
    public LibreViesPersonnage personnage;
    public Classement classement;
    public string erreur_personnage;
    public LibreViesPosition position;
}
