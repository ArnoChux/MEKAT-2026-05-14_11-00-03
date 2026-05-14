# Système de combat basic - Document de design

Version : 0.1  
Statut : base de travail avant implémentation

## Objectif

Créer un système de combat simple, lisible et extensible, qui permette de programmer rapidement une première version jouable sans enfermer le projet dans des règles trop complexes.

Le système doit d'abord servir un combat clair :

- le joueur comprend ce qui se passe ;
- chaque action produit un retour visible ;
- les règles sont assez stables pour être testées ;
- l'architecture pourra accueillir plus tard des compétences, objets, statuts ou ennemis multiples.

## Décision de base proposée

Le combat de départ est un combat au tour par tour.

Cette approche est pertinente pour un premier système parce qu'elle rend les règles faciles à isoler, tester et équilibrer. Elle laisse aussi le temps de construire une bonne boucle de feedback avant d'ajouter de la vitesse, des animations ou des mécaniques plus avancées.

## Périmètre du MVP

Le MVP couvre :

- un combat joueur contre ennemi ;
- des combattants avec points de vie, attaque, défense et vitesse ;
- une action d'attaque simple ;
- une action de défense ;
- une condition de victoire et de défaite ;
- un journal d'événements de combat ;
- une logique séparée de l'interface pour faciliter les tests.

Hors MVP, mais à prévoir dans la structure :

- compétences spéciales ;
- objets consommables ;
- effets de statut ;
- plusieurs ennemis ou alliés ;
- chances de critique, esquive ou précision ;
- fuite du combat.

## Boucle de combat

1. Initialiser le combat avec les participants.
2. Déterminer l'ordre d'action avec la vitesse.
3. Demander ou choisir l'action du combattant actif.
4. Résoudre l'action.
5. Ajouter les événements au journal de combat.
6. Vérifier si une équipe est vaincue.
7. Passer au tour suivant si le combat continue.

## Combattant

Un combattant représente le joueur, un ennemi ou plus tard un allié.

Champs recommandés :

- `id` : identifiant stable ;
- `name` : nom affiché ;
- `team` : `player` ou `enemy` ;
- `maxHp` : points de vie maximum ;
- `hp` : points de vie actuels ;
- `attack` : puissance offensive ;
- `defense` : réduction des dégâts ;
- `speed` : ordre d'action ;
- `isDefending` : bonus défensif temporaire ;
- `statusEffects` : liste prévue pour une extension future.

## Actions de base

### Attaquer

Inflige des dégâts à une cible ennemie.

Formule MVP proposée :

```text
dégâts = max(1, attaque_attaquant - défense_cible)
```

Si la cible se défend :

```text
dégâts = max(1, round(dégâts * 0.5))
```

Règle importante : les points de vie ne descendent jamais sous `0`.

### Défendre

Le combattant ne fait pas de dégâts pendant son action, mais réduit les dégâts reçus jusqu'à son prochain tour.

Effet proposé :

- active `isDefending` ;
- réduit de 50 % la prochaine attaque reçue ;
- se désactive après réduction ou au début du prochain tour du combattant.

## Journal de combat

Chaque action produit un événement exploitable par l'interface.

Exemples :

- `combat_started`
- `turn_started`
- `attack_resolved`
- `defense_started`
- `combatant_defeated`
- `combat_ended`

Le journal doit permettre d'afficher des phrases comme :

- "Héros attaque Slime et inflige 5 dégâts."
- "Slime se défend."
- "Slime est vaincu."

## Conditions de fin

Victoire :

- tous les combattants de l'équipe ennemie sont à `0` PV.

Défaite :

- tous les combattants de l'équipe joueur sont à `0` PV.

Le combat se termine immédiatement lorsqu'une de ces conditions est vraie.

## Équilibrage initial

Valeurs de départ proposées pour tester la boucle :

| Combattant | PV | Attaque | Défense | Vitesse |
| --- | ---: | ---: | ---: | ---: |
| Héros | 30 | 7 | 2 | 5 |
| Slime | 18 | 5 | 1 | 3 |
| Bandit | 24 | 6 | 2 | 5 |

Objectif de rythme :

- un ennemi faible doit durer environ 3 à 5 tours ;
- une erreur du joueur doit être visible mais pas immédiatement punitive ;
- la défense doit être utile, sans être meilleure que l'attaque à chaque tour.

## IA ennemie MVP

L'ennemi choisit une action simple :

- si ses PV sont bas, il peut se défendre ;
- sinon, il attaque.

Règle initiale proposée :

```text
si hp <= 30 % de maxHp : 35 % de chance de défendre
sinon : attaquer
```

Pour les premiers tests automatisés, l'IA peut être rendue déterministe afin de vérifier facilement les résultats.

## Architecture recommandée

Séparer la logique de combat de l'affichage.

Modules ou responsabilités possibles :

- `CombatState` : état complet du combat ;
- `Combatant` : données d'un combattant ;
- `CombatAction` : description d'une action ;
- `resolveAction` : applique une action et retourne le nouvel état ;
- `getCombatResult` : indique si le combat continue, est gagné ou perdu ;
- `combatLog` : liste d'événements à afficher.

Principe important : les fonctions de règles doivent pouvoir être testées sans interface graphique.

## Critères d'acceptation pour la première implémentation

- Un combat peut démarrer avec un joueur et un ennemi.
- Le système sait quel combattant agit.
- Le joueur peut attaquer.
- L'ennemi peut attaquer automatiquement.
- L'action défendre réduit les dégâts.
- Les PV sont mis à jour correctement.
- Le combat s'arrête sur victoire ou défaite.
- Un journal décrit les événements principaux.
- Les règles principales sont couvertes par des tests.

## Questions à trancher avant ou pendant le développement

- Le jeu vise-t-il plutôt un rythme tactique lent ou rapide ?
- Le joueur aura-t-il plusieurs actions dès le MVP, ou seulement attaque/défense ?
- Les ennemis doivent-ils avoir des comportements très différents dès le début ?
- Les dégâts doivent-ils être entièrement prévisibles ou inclure une variance aléatoire ?
- Le combat doit-il être pensé dès maintenant pour plusieurs personnages ?

## Prochaine étape recommandée

Implémenter le noyau de combat sans interface complexe :

1. créer les structures de données ;
2. coder la résolution attaque/défense ;
3. ajouter la boucle de tours ;
4. ajouter les conditions de fin ;
5. écrire quelques tests ;
6. brancher ensuite une interface simple ou une scène de jeu.
