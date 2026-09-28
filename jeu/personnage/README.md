# Createur de personnage Unreal

Le createur n'est plus compile comme un executable separe. Il est maintenant
integre dans le projet Unreal :

```text
compilation\unreal\Source\LibreVies\
compilation\unreal\Content\Characters\MakeHuman\
```

La distribution finale se trouve dans `jeu\game\`. Plus tard, le meme
`ALVCharacterGenerator` sera appele par l'ecran Nouvelle partie pour creer le
personnage du joueur directement dans le monde.
