#!/usr/bin/env python3
"""Verifications statiques du mode EDITION et de la publication pre-build."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CS = (ROOT / "compilation/unity/Assets/Scripts/LibreViesGame.cs").read_text(encoding="utf-8")
BAT = (ROOT / "compilation/build_launcher.bat").read_text(encoding="ascii")


def section(source: str, start: str, end: str) -> str:
    assert start in source, f"section absente: {start}"
    return source.split(start, 1)[1].split(end, 1)[0]


def test_bat_publishes_before_sync():
    publish = section(BAT, "\n:etape_projet", "\n:etape_python")
    assert "call :publier_edition" in publish
    assert publish.index("call :publier_edition") < publish.index("powershell")
    publisher = section(BAT, ":publier_edition", ":etape_projet")
    assert '"%GIT%" -C' in publisher and " add -- jeu/edition" in publisher
    assert "commit" in publisher and "push origin" in publisher
    assert "if errorlevel 1" in publisher
    assert "preparer_depot_git" in publisher
    assert "reset --mixed FETCH_HEAD" in BAT
    assert 'fetch --no-tags origin "%BRANCHE%"' in BAT
    assert "diff --cached --quiet -- jeu\\edition" in publisher or "diff --cached --quiet -- jeu/edition" in publisher


def test_edition_keeps_horizontal_drag_and_single_selection():
    assert "HauteurSoulevementMaisonEdition" in CS
    assert "Input.GetMouseButton(0)" in CS
    assert "Input.GetMouseButtonUp(0)" in CS
    assert "elementEditionSelectionne" in CS
    assert "objetsEdition" in CS
    assert "element.Root.position = new Vector3(position.x" in CS
    assert "sol + HauteurSoulevementMaisonEdition" in CS
    assert "Input.GetKeyDown(KeyCode.Escape)" in CS
    assert "AnnulerDeplacementEdition" in CS
    assert "PositionAvant" in CS
    assert "Grillage" not in CS
    assert "MaisonParent" in CS
    assert "HauteurLocale" in CS
    assert "element.BloqueHauteur ? element.HauteurLocale" in CS
    assert "Mathf.Clamp(local.y" in CS
    assert "PointSourisSurPlanMaison" in CS
    assert "ProfondeurLocale" in CS
    assert 'BloqueHauteur = objet.name == "Porte"' in CS
    assert "TryTrouverCoteRedimensionnement" in CS
    assert "ActualiserDimensionsBatiment" in CS
    assert "nouvelleDimension" in CS
    assert "ModifierTailleMur" in CS
    assert "ModifierTailleMur(elementEditionDernierSelectionne, cotes[i], inverse)" in CS
    assert "ModifierTailleMur(elementEditionDernierSelectionne, cotes[i], !inverse)" in CS
    assert "CadreEditionMaison" in CS
    assert "editionOutilMaison" in CS
    assert "MODIFIER LES FENETRES" in CS
    assert "MODIFIER LA PORTE" in CS
    assert "MODIFIER LE TOIT" in CS
    assert "AppliquerMateriauMaison" in CS
    assert "MaisonSousToitSouris" in CS
    assert "DessinerEditionMaison" in CS
    assert 'GUI.Button(new Rect(cadre.x + 242f' in CS
    assert 'GUI.Button(new Rect(cadre.x + 304f' in CS
    assert "editionOutilMaison" in CS
    assert "MODIFIER LES FENETRES" in CS
    assert "MODIFIER LA PORTE" in CS
    assert "MODIFIER LE TOIT" in CS
    assert "AppliquerMateriauMaison" in CS
    assert "MaisonSousToitSouris" in CS
    assert "Stack<HistoriqueEdition>" in CS
    assert "AjouterHistoriqueEdition" in CS
    assert "SupprimerFichierHistoriqueEdition" in CS
    assert "historiqueEdition.Clear()" in CS


def test_requested_material_choices_and_special_shaders():
    panel = section(CS, "private void DessinerEditionMaison", "private bool TryZoneEcranElementEdition")
    assert 'new[] { "Wall", "Bois_Clair", "Brique" }' in panel
    assert 'new[] { "GlassBleu" }' in panel
    assert 'new[] { "Bois_Clair", "MetalAluminium" }' in panel
    assert 'new[] { "RoofRed", "Bois_Clair" }' in panel
    assert "MOQUET" not in panel
    assert "CLAIR" not in panel
    assert "TUILE" not in panel
    assert '"MetalAluminium"' in CS
    assert '"LVShaders/LVMetalAluminium"' in CS
    assert '"LVShaders/LVGlassBleu"' in CS
    assert (ROOT / "compilation/unity/Assets/Resources/LVShaders/LVMetalAluminium.shader").exists()
    assert (ROOT / "compilation/unity/Assets/Resources/LVShaders/LVGlassBleu.shader").exists()


def test_requested_objects_are_registered():
    registry = section(CS, "private bool EstObjetDeplacableEdition", "private void ConstruireObjetsEdition")
    for name in ("Porte", "Fenetre", "Sapin", "Asset_CC0_", "Caisse", "Baril"):
        assert name in registry, f"objet non enregistrable: {name}"
    assert "FindObjectsByType<Transform>()" in CS


def test_signs_follow_buildings_and_facade_visibility_is_dynamic():
    assert "texte.transform.SetParent(panneau.transform, true)" in CS
    visibility = section(CS, "private void MettreAJourVisibiliteAffiches", "private GameObject CreerTexte3D")
    assert "affiche.Position = affiche.Root.transform.position" in visibility
    assert "parentFacade.TransformDirection" in visibility


def test_blacksmith_and_mayor_accessories_follow_animated_arms():
    update = section(CS, "private void UpdatePnj", "private void MettreAJourVisibiliteAffiches")
    assert 'pnj.Metier == "Forgeron" || pnj.Metier == "Maire"' in update
    assert "accessoireBrasDroit" in update
    assert "Mathf.Clamp(angleBrasDroit" in update
    assert "Quaternion.identity" in update
    assert "TryDirectionProlongementBras" in update
    assert "pnj.Marteau" in update
    assert "pnj.Feuille" in update
    assert "Enclume_Forgeron" not in update


def test_admin_character_appearance_editor_exists():
    assert "APPARENCE DU PERSONNAGE" in CS
    assert "DessinerAdminApparence" in CS
    assert "AppliquerApparencePersonnage" in CS
    assert "NomSexeApparence" in CS
    assert '"Femme"' in CS and '"Homme"' in CS
    assert '"Tete : type "' in CS
    assert '"Bras : type "' in CS
    assert '"Corps : type "' in CS
    assert '"Jambes : type "' in CS
    assert '"Coupe de cheveux : type "' in CS
    assert 'string[] choix = { "1", "2", "3", "4", "5" };' in CS
    for field in ("TypeTete", "TypeBras", "TypeCorps", "TypeJambes", "TypeCheveux"):
        assert field in CS
    assert "objets et accessoires seront ajoutes plus tard." in CS


def test_sign_text_style_and_persistence_controls_exist():
    panel = section(CS, "private void DessinerEditionPancarte", "private void CreerToitTriangle")
    assert "GUI.TextField" in CS
    assert "FontStyle.Bold" in CS
    assert "FontStyle.Italic" in CS
    assert "Souligne" in CS  # champ conserve pour migrer les anciennes sauvegardes
    assert "LineRenderer" in CS  # ancien type reconnu sans etre recree
    assert "TextePancarte" in CS
    assert "CouleurPancarte" in CS
    assert '"SOULIGNE' not in panel
    assert '"PANNEAU DE LA MAISON"' not in panel
    assert "Les changements sont sauvegardes" not in panel
    assert '"TAILLE :"' in panel
    assert "tailleChampStyle" in panel
    for label in ("BLANC", "JAUNE", "ROUGE", "BLEU", "VERT", "BRUN"):
        assert label in CS


def test_encrypted_coordinate_file_and_path_metadata_remain_in_place():
    assert '"edition", "coordonee"' in CS
    assert "ChiffrerCoordonneesEdition" in CS
    assert "DechiffrerCoordonneesEdition" in CS
    assert "CheminObjetEdition(objet)" in CS


if __name__ == "__main__":
    tests = [value for name, value in globals().items() if name.startswith("test_")]
    for test in tests:
        test()
        print(f"OK    {test.__name__}")
    print(f"OK : {len(tests)} verifications EDITION")
