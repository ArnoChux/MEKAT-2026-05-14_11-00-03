# Noyau prototype combat

Statut : prototype jouable sans interface.

## Objectif

Ce noyau implémente les règles V1 du système de combat sous forme de logique pure et testable. Il ne dépend pas d'une interface graphique et peut être branché plus tard sur une scène, un HUD ou un outil de debug.

## Fichiers principaux

- `src/combat/core.mjs` : règles de combat, actions, initiative, dégâts, états et IA.
- `src/combat/prototype-fixtures.mjs` : robot joueur et drone ennemi de test.
- `test/combat-core.test.mjs` : tests du noyau.
- `scripts/run-combat-prototype.mjs` : simulation simple d'un round 1v1.

## Règles implémentées

- Combat au tour par tour avec résolution par roue d'initiative.
- Initiative : `AGI`, puis `DEX`, puis pile ou face.
- Recharge de l'`ENERGY` à chaque round.
- Consommation de l'`ENERGY` au choix de l'action.
- Actions de base : `Attaque`, `Défense`, `Taunt`.
- `Taunt` mono-cible pendant 2 tours.
- `Défense` active jusqu'à la fin du round.
- Esquive totale : annule dégâts et effets associés.
- Critique x2 avant blindage.
- Blindage consommé avant les PV.
- KO à 0 PV.
- IA prototype : agressive, défensive, opportuniste.

## Formules V1

```text
PV max = 20 + VIT * 5
BLINDAGE max = DEF
Dégâts bruts = ATK + puissance_compétence

Si critique :
dégâts bruts = dégâts bruts * 2

Réduction DEF = floor(DEF / 2)
Dégâts après DEF = max(1, dégâts bruts - réduction DEF)

Si Défense active :
dégâts après défense = max(1, ceil(dégâts après DEF * 0.5))

Puis :
blindage -> PV
```

## Lancer les tests

```bash
node --test test/combat-core.test.mjs
```

## Lancer une simulation

```bash
node scripts/run-combat-prototype.mjs
```

La simulation affiche l'état final du round et le journal complet des événements.
