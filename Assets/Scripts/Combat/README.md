# Combat Unity Prototype

Ce dossier contient le port C# du noyau de combat V1 pour Unity.

## Fichiers

- `CombatModels.cs` : modèles de données sérialisables pour Unity.
- `CombatCore.cs` : règles de combat pures, sans dépendance à `MonoBehaviour`.
- `CombatPrototypeFixtures.cs` : robot joueur et drone ennemi de test.
- `CombatDebugRunner.cs` : composant Unity pour lancer un round prototype et afficher les logs.

## Voir le prototype dans Unity

1. Ouvrir le projet dans Unity.
2. Dans la fenêtre `Project`, vérifier que `Assets/Scripts/Combat` apparaît.
3. Créer un GameObject vide dans la scène.
4. Ajouter le composant `CombatDebugRunner`.
5. Lancer Play.
6. Lire le rapport de combat complet dans la Console Unity.

Par défaut, le runner lance plusieurs rounds jusqu'à victoire/défaite ou jusqu'à `Max Rounds`.

Le menu contextuel du composant permet aussi de lancer :

- `Run One Prototype Round` pour inspecter un seul round.
- `Run Full Prototype Combat` pour simuler tout le combat.

## Règles portées

- `ENERGY` se recharge à chaque round.
- `ENERGY` est consommée au choix de l'action.
- Initiative : `AGI`, puis `DEX`, puis pile ou face.
- Actions : attaque, défense, taunt.
- Taunt : 2 tours.
- Défense : jusqu'à la fin du round.
- Esquive : annule toute l'action.
- Critique : x2 avant blindage.
- Dégâts : brut -> critique -> réduction DEF -> défense active -> blindage -> PV.
