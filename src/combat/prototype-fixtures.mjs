import {
  AI_STYLES,
  TEAMS,
  createCombatState,
  createCombatant,
} from "./core.mjs";

export const PROTOTYPE_PLAYER_STATS = Object.freeze({
  ATK: 6,
  VIT: 4,
  DEF: 5,
  ENERGY: 1,
  AGI: 5,
  DEX: 3,
  LUK: 3,
});

export const PROTOTYPE_DRONE_STATS = Object.freeze({
  ATK: 5,
  VIT: 3,
  DEF: 2,
  ENERGY: 1,
  AGI: 4,
  DEX: 2,
  LUK: 2,
});

export function createPrototypePlayer(overrides = {}) {
  return createCombatant({
    ...overrides,
    id: overrides.id ?? "robot-player",
    name: overrides.name ?? "Robot joueur",
    team: TEAMS.PLAYER,
    stats: {
      ...PROTOTYPE_PLAYER_STATS,
      ...(overrides.stats ?? {}),
    },
  });
}

export function createPrototypeDrone(overrides = {}) {
  return createCombatant({
    ...overrides,
    id: overrides.id ?? "drone-enemy",
    name: overrides.name ?? "Drone ennemi",
    team: TEAMS.ENEMY,
    aiStyle: overrides.aiStyle ?? AI_STYLES.AGGRESSIVE,
    stats: {
      ...PROTOTYPE_DRONE_STATS,
      ...(overrides.stats ?? {}),
    },
  });
}

export function createPrototypeCombatState(options = {}) {
  return createCombatState(
    [
      createPrototypePlayer(options.player),
      createPrototypeDrone(options.enemy),
    ],
    options,
  );
}
