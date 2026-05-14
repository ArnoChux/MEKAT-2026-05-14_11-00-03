export const TEAMS = Object.freeze({
  PLAYER: "player",
  ENEMY: "enemy",
});

export const SKILL_IDS = Object.freeze({
  ATTACK: "attack",
  DEFEND: "defend",
  TAUNT: "taunt",
});

export const AI_STYLES = Object.freeze({
  AGGRESSIVE: "aggressive",
  DEFENSIVE: "defensive",
  OPPORTUNISTIC: "opportunistic",
});

export const DEFAULT_COMBAT_CONFIG = Object.freeze({
  baseHp: 20,
  hpPerVit: 5,
  armorPerDef: 1,
  defenseReductionDivisor: 2,
  defendDamageMultiplier: 0.5,
  dodgeChancePerDex: 0.03,
  critChancePerLuk: 0.04,
  minDamage: 1,
  tauntDurationTurns: 2,
});

export const BASE_SKILLS = Object.freeze({
  [SKILL_IDS.ATTACK]: Object.freeze({
    id: SKILL_IDS.ATTACK,
    name: "Attaque",
    energyCost: 1,
    target: "enemy",
    kind: "damage",
    power: 0,
    canDodge: true,
    canCrit: true,
  }),
  [SKILL_IDS.DEFEND]: Object.freeze({
    id: SKILL_IDS.DEFEND,
    name: "Défense",
    energyCost: 1,
    target: "self",
    kind: "defend",
    canDodge: false,
    canCrit: false,
  }),
  [SKILL_IDS.TAUNT]: Object.freeze({
    id: SKILL_IDS.TAUNT,
    name: "Taunt",
    energyCost: 1,
    target: "enemy",
    kind: "taunt",
    canDodge: true,
    canCrit: false,
  }),
});

const DEFAULT_SKILL_LOADOUT = Object.freeze([
  SKILL_IDS.ATTACK,
  SKILL_IDS.DEFEND,
  SKILL_IDS.TAUNT,
]);

export function createSeededRandom(seed = 1) {
  let value = seed >>> 0;

  return function seededRandom() {
    value = (value * 1664525 + 1013904223) >>> 0;
    return value / 0x100000000;
  };
}

export function createCombatant(input, options = {}) {
  const config = mergeConfig(options.config);
  const stats = normalizeStats(input.stats);
  const maxHp = calculateMaxHp(stats, config);
  const maxArmor = calculateMaxArmor(stats, config);

  return {
    id: requireString(input.id, "combatant.id"),
    name: input.name ?? input.id,
    team: normalizeTeam(input.team),
    stats,
    maxHp,
    hp: input.hp ?? maxHp,
    maxArmor,
    armor: input.armor ?? maxArmor,
    energy: input.energy ?? stats.energy,
    skills: [...(input.skills ?? DEFAULT_SKILL_LOADOUT)],
    aiStyle: input.aiStyle ?? AI_STYLES.AGGRESSIVE,
    statusEffects: [...(input.statusEffects ?? [])],
    isAlive: input.isAlive ?? (input.hp ?? maxHp) > 0,
  };
}

export function createCombatState(combatants, options = {}) {
  const config = mergeConfig(options.config);
  const normalizedCombatants = combatants.map((combatant) =>
    isHydratedCombatant(combatant)
      ? cloneCombatant(combatant, config)
      : createCombatant(combatant, { config }),
  );

  const state = {
    round: 0,
    phase: "ready",
    config,
    combatants: normalizedCombatants,
    initiativeOrder: [],
    actionQueue: [],
    log: [],
    result: { status: "ongoing", winningTeam: null },
  };

  state.result = getCombatResult(state);
  addEvent(state, "combat_started", {
    combatantIds: normalizedCombatants.map((combatant) => combatant.id),
  });

  return state;
}

export function startRound(state, options = {}) {
  const next = cloneState(state);

  if (getCombatResult(next).status !== "ongoing") {
    return next;
  }

  next.round += 1;
  next.phase = "planning";
  next.actionQueue = [];

  for (const combatant of next.combatants) {
    if (!combatant.isAlive) {
      continue;
    }

    combatant.energy = combatant.stats.energy;
  }

  next.initiativeOrder = calculateInitiativeOrder(next.combatants, {
    random: options.random,
  });

  addEvent(next, "round_started", {
    round: next.round,
  });
  addEvent(next, "initiative_calculated", {
    order: [...next.initiativeOrder],
  });

  return next;
}

export function queueAction(state, actorId, skillId, targetId = null) {
  const next = cloneState(state);
  const actor = getLivingCombatant(next, actorId);
  const skill = getSkill(actor, skillId);
  const resolvedTargetId = resolveActionTarget(next, actor, skill, targetId);

  if (actor.energy < skill.energyCost) {
    throw new Error(`${actor.name} does not have enough ENERGY for ${skill.name}.`);
  }

  actor.energy -= skill.energyCost;

  const action = {
    id: `${next.round}:${next.actionQueue.length + 1}:${actor.id}:${skill.id}`,
    round: next.round,
    order: next.actionQueue.length,
    actorId: actor.id,
    skillId: skill.id,
    targetId: resolvedTargetId,
    energyCost: skill.energyCost,
    status: "queued",
  };

  next.actionQueue.push(action);
  addEvent(next, "action_chosen", {
    actorId: actor.id,
    skillId: skill.id,
    targetId: resolvedTargetId,
    energySpent: skill.energyCost,
    remainingEnergy: actor.energy,
  });

  return next;
}

export function queueEnemyActions(state, options = {}) {
  let next = cloneState(state);
  const enemies = next.combatants.filter(
    (combatant) => combatant.team === TEAMS.ENEMY && combatant.isAlive,
  );

  for (const enemy of enemies) {
    while (enemy.energy > 0 && getCombatResult(next).status === "ongoing") {
      const plannedAction = chooseEnemyAction(next, enemy.id, options);

      if (!plannedAction) {
        break;
      }

      next = queueAction(
        next,
        plannedAction.actorId,
        plannedAction.skillId,
        plannedAction.targetId,
      );

      const updatedEnemy = findCombatant(next, enemy.id);
      if (!updatedEnemy || updatedEnemy.energy < 1) {
        break;
      }
    }
  }

  return next;
}

export function resolveRound(state, options = {}) {
  const next = cloneState(state);
  const random = options.random ?? Math.random;

  next.phase = "resolving";

  for (const actorId of next.initiativeOrder) {
    if (getCombatResult(next).status !== "ongoing") {
      break;
    }

    const actor = findCombatant(next, actorId);
    if (!actor || !actor.isAlive) {
      continue;
    }

    const actions = next.actionQueue
      .filter((action) => action.actorId === actorId && action.status === "queued")
      .sort((left, right) => left.order - right.order);

    for (const action of actions) {
      if (getCombatResult(next).status !== "ongoing") {
        break;
      }

      action.status = "resolved";
      resolveAction(next, action, { random });
      next.result = getCombatResult(next);
    }

    decrementTurnStatuses(next, actor.id);
  }

  clearEndOfRoundStatuses(next);
  next.actionQueue = [];
  next.result = getCombatResult(next);
  next.phase = next.result.status === "ongoing" ? "ready" : "ended";

  addEvent(next, "round_ended", {
    round: next.round,
    result: next.result.status,
  });

  if (next.result.status !== "ongoing") {
    addEvent(next, "combat_ended", next.result);
  }

  return next;
}

export function chooseEnemyAction(state, actorId, options = {}) {
  const random = options.random ?? Math.random;
  const enemy = getLivingCombatant(state, actorId);

  if (enemy.team !== TEAMS.ENEMY) {
    throw new Error(`${enemy.name} is not an enemy combatant.`);
  }

  const forcedTargetId = getForcedTargetId(state, enemy);
  const defaultTarget = forcedTargetId
    ? findCombatant(state, forcedTargetId)
    : findFirstLivingEnemyOf(state, enemy);

  if (!defaultTarget) {
    return null;
  }

  if (enemy.aiStyle === AI_STYLES.DEFENSIVE && shouldDefensiveAiDefend(enemy, random)) {
    return {
      actorId: enemy.id,
      skillId: SKILL_IDS.DEFEND,
      targetId: enemy.id,
    };
  }

  if (
    enemy.aiStyle === AI_STYLES.OPPORTUNISTIC &&
    shouldOpportunisticAiDefend(enemy, defaultTarget, random)
  ) {
    return {
      actorId: enemy.id,
      skillId: SKILL_IDS.DEFEND,
      targetId: enemy.id,
    };
  }

  if (enemy.aiStyle === AI_STYLES.AGGRESSIVE && shouldAggressiveAiDefend(enemy, random)) {
    return {
      actorId: enemy.id,
      skillId: SKILL_IDS.DEFEND,
      targetId: enemy.id,
    };
  }

  return {
    actorId: enemy.id,
    skillId: SKILL_IDS.ATTACK,
    targetId: defaultTarget.id,
  };
}

export function calculateDamagePreview(attacker, target, options = {}) {
  const config = mergeConfig(options.config);
  const rawDamage = Math.max(
    config.minDamage,
    attacker.stats.atk + (options.skillPower ?? 0),
  );
  const damageAfterCrit = options.isCritical ? rawDamage * 2 : rawDamage;
  const defenseReduction = Math.floor(target.stats.def / config.defenseReductionDivisor);
  const damageAfterDefenseStat = Math.max(
    config.minDamage,
    damageAfterCrit - defenseReduction,
  );
  const finalDamage = options.isDefending
    ? Math.max(
        config.minDamage,
        Math.ceil(damageAfterDefenseStat * config.defendDamageMultiplier),
      )
    : damageAfterDefenseStat;

  return {
    rawDamage,
    damageAfterCrit,
    defenseReduction,
    damageAfterDefenseStat,
    finalDamage,
  };
}

export function getCombatResult(state) {
  const playersAlive = state.combatants.some(
    (combatant) => combatant.team === TEAMS.PLAYER && combatant.isAlive,
  );
  const enemiesAlive = state.combatants.some(
    (combatant) => combatant.team === TEAMS.ENEMY && combatant.isAlive,
  );

  if (!playersAlive && !enemiesAlive) {
    return { status: "draw", winningTeam: null };
  }

  if (!playersAlive) {
    return { status: "defeat", winningTeam: TEAMS.ENEMY };
  }

  if (!enemiesAlive) {
    return { status: "victory", winningTeam: TEAMS.PLAYER };
  }

  return { status: "ongoing", winningTeam: null };
}

export function calculateMaxHp(stats, config = DEFAULT_COMBAT_CONFIG) {
  return config.baseHp + stats.vit * config.hpPerVit;
}

export function calculateMaxArmor(stats, config = DEFAULT_COMBAT_CONFIG) {
  return stats.def * config.armorPerDef;
}

export function calculateDodgeChance(stats, config = DEFAULT_COMBAT_CONFIG) {
  return stats.dex * config.dodgeChancePerDex;
}

export function calculateCritChance(stats, config = DEFAULT_COMBAT_CONFIG) {
  return stats.luk * config.critChancePerLuk;
}

export function calculateInitiativeOrder(combatants, options = {}) {
  const random = options.random ?? Math.random;

  return combatants
    .filter((combatant) => combatant.isAlive)
    .map((combatant) => ({
      id: combatant.id,
      agi: combatant.stats.agi,
      dex: combatant.stats.dex,
      coinFlip: random(),
    }))
    .sort((left, right) => {
      if (right.agi !== left.agi) {
        return right.agi - left.agi;
      }

      if (right.dex !== left.dex) {
        return right.dex - left.dex;
      }

      return right.coinFlip - left.coinFlip;
    })
    .map((entry) => entry.id);
}

function resolveAction(state, action, options) {
  const actor = findCombatant(state, action.actorId);

  if (!actor || !actor.isAlive) {
    addEvent(state, "action_cancelled", {
      actorId: action.actorId,
      reason: "actor_not_alive",
    });
    return;
  }

  const skill = getSkill(actor, action.skillId);
  const targetId = resolveActionTarget(state, actor, skill, action.targetId, {
    allowRedirectLog: true,
  });
  const target = findCombatant(state, targetId);

  if (!target || !target.isAlive) {
    addEvent(state, "action_cancelled", {
      actorId: actor.id,
      skillId: skill.id,
      targetId,
      reason: "target_not_alive",
    });
    return;
  }

  if (skill.kind === "damage") {
    resolveDamageAction(state, actor, target, skill, options);
    return;
  }

  if (skill.kind === "defend") {
    upsertStatus(actor, {
      type: "defending",
      sourceId: actor.id,
      expires: "end_round",
    });
    addEvent(state, "defense_started", {
      actorId: actor.id,
      expires: "end_round",
    });
    return;
  }

  if (skill.kind === "taunt") {
    if (rollDodge(state, actor, target, skill, options)) {
      return;
    }

    upsertStatus(target, {
      type: "taunted",
      sourceId: actor.id,
      remainingTurns: state.config.tauntDurationTurns,
    });
    addEvent(state, "taunt_applied", {
      actorId: actor.id,
      targetId: target.id,
      remainingTurns: state.config.tauntDurationTurns,
    });
    return;
  }

  throw new Error(`Unsupported skill kind: ${skill.kind}`);
}

function resolveDamageAction(state, actor, target, skill, options) {
  if (rollDodge(state, actor, target, skill, options)) {
    return;
  }

  const critChance = calculateCritChance(actor.stats, state.config);
  const critRoll = options.random();
  const isCritical = skill.canCrit && critRoll < critChance;
  const isDefending = hasStatus(target, "defending");
  const damage = calculateDamagePreview(actor, target, {
    config: state.config,
    skillPower: skill.power,
    isCritical,
    isDefending,
  });
  const armorDamage = Math.min(target.armor, damage.finalDamage);
  const hpDamage = damage.finalDamage - armorDamage;

  target.armor = Math.max(0, target.armor - armorDamage);
  target.hp = Math.max(0, target.hp - hpDamage);

  if (isCritical) {
    addEvent(state, "critical_hit", {
      actorId: actor.id,
      targetId: target.id,
      chance: critChance,
      roll: critRoll,
    });
  }

  if (armorDamage > 0) {
    addEvent(state, "armor_damaged", {
      targetId: target.id,
      amount: armorDamage,
      remainingArmor: target.armor,
    });
  }

  if (hpDamage > 0) {
    addEvent(state, "hp_damaged", {
      targetId: target.id,
      amount: hpDamage,
      remainingHp: target.hp,
    });
  }

  addEvent(state, "attack_resolved", {
    actorId: actor.id,
    targetId: target.id,
    rawDamage: damage.rawDamage,
    damageAfterCrit: damage.damageAfterCrit,
    defenseReduction: damage.defenseReduction,
    damageAfterDefenseStat: damage.damageAfterDefenseStat,
    defenseActive: isDefending,
    finalDamage: damage.finalDamage,
    armorDamage,
    hpDamage,
  });

  if (target.hp <= 0 && target.isAlive) {
    target.isAlive = false;
    target.energy = 0;
    addEvent(state, "combatant_defeated", {
      combatantId: target.id,
    });
  }
}

function rollDodge(state, actor, target, skill, options) {
  if (!skill.canDodge) {
    return false;
  }

  const dodgeChance = calculateDodgeChance(target.stats, state.config);
  const dodgeRoll = options.random();
  const dodged = dodgeRoll < dodgeChance;

  if (dodged) {
    addEvent(state, "action_dodged", {
      actorId: actor.id,
      targetId: target.id,
      skillId: skill.id,
      chance: dodgeChance,
      roll: dodgeRoll,
    });
  }

  return dodged;
}

function resolveActionTarget(state, actor, skill, requestedTargetId, options = {}) {
  if (skill.target === "self") {
    return actor.id;
  }

  const forcedTargetId = skill.target === "enemy" ? getForcedTargetId(state, actor) : null;
  const targetId = forcedTargetId ?? requestedTargetId;

  if (!targetId) {
    throw new Error(`${skill.name} requires a target.`);
  }

  const target = getLivingCombatant(state, targetId);

  if (skill.target === "enemy" && target.team === actor.team) {
    throw new Error(`${skill.name} must target an enemy.`);
  }

  if (skill.target === "ally" && target.team !== actor.team) {
    throw new Error(`${skill.name} must target an ally.`);
  }

  if (forcedTargetId && requestedTargetId !== forcedTargetId && options.allowRedirectLog) {
    addEvent(state, "target_forced", {
      actorId: actor.id,
      requestedTargetId,
      forcedTargetId,
      reason: "taunted",
    });
  }

  return target.id;
}

function getForcedTargetId(state, actor) {
  const taunt = actor.statusEffects.find((status) => status.type === "taunted");

  if (!taunt) {
    return null;
  }

  const source = findCombatant(state, taunt.sourceId);
  if (!source || !source.isAlive || source.team === actor.team) {
    return null;
  }

  return source.id;
}

function decrementTurnStatuses(state, actorId) {
  const actor = findCombatant(state, actorId);
  if (!actor || !actor.isAlive) {
    return;
  }

  const nextStatuses = [];

  for (const status of actor.statusEffects) {
    if (status.type !== "taunted") {
      nextStatuses.push(status);
      continue;
    }

    const remainingTurns = status.remainingTurns - 1;
    if (remainingTurns > 0) {
      nextStatuses.push({
        ...status,
        remainingTurns,
      });
      continue;
    }

    addEvent(state, "status_expired", {
      combatantId: actor.id,
      statusType: status.type,
    });
  }

  actor.statusEffects = nextStatuses;
}

function clearEndOfRoundStatuses(state) {
  for (const combatant of state.combatants) {
    const nextStatuses = [];

    for (const status of combatant.statusEffects) {
      if (status.expires === "end_round") {
        addEvent(state, "status_expired", {
          combatantId: combatant.id,
          statusType: status.type,
        });
        continue;
      }

      nextStatuses.push(status);
    }

    combatant.statusEffects = nextStatuses;
  }
}

function shouldDefensiveAiDefend(enemy, random) {
  const lowHp = enemy.hp <= enemy.maxHp * 0.35;
  const lowArmor = enemy.maxArmor > 0 && enemy.armor <= enemy.maxArmor * 0.25;
  return (lowHp || lowArmor) && random() < 0.8;
}

function shouldOpportunisticAiDefend(enemy, target, random) {
  const targetExposed = target.armor <= Math.max(1, target.maxArmor * 0.2);
  const selfExposed = enemy.armor <= Math.max(0, enemy.maxArmor * 0.2);
  return !targetExposed && selfExposed && random() < 0.35;
}

function shouldAggressiveAiDefend(enemy, random) {
  const nearlyDefeated = enemy.hp <= enemy.maxHp * 0.2;
  return nearlyDefeated && random() < 0.2;
}

function normalizeStats(stats = {}) {
  return {
    atk: toNumber(stats.atk ?? stats.ATK, 0),
    vit: toNumber(stats.vit ?? stats.VIT, 0),
    def: toNumber(stats.def ?? stats.DEF, 0),
    energy: toNumber(stats.energy ?? stats.ENERGY, 1),
    agi: toNumber(stats.agi ?? stats.AGI, 0),
    dex: toNumber(stats.dex ?? stats.DEX, 0),
    luk: toNumber(stats.luk ?? stats.LUK, 0),
  };
}

function normalizeTeam(team) {
  if (team === TEAMS.PLAYER || team === TEAMS.ENEMY) {
    return team;
  }

  throw new Error(`Unsupported team: ${team}`);
}

function cloneState(state) {
  return {
    ...state,
    config: { ...state.config },
    combatants: state.combatants.map((combatant) => cloneCombatant(combatant)),
    initiativeOrder: [...state.initiativeOrder],
    actionQueue: state.actionQueue.map((action) => ({ ...action })),
    log: state.log.map((entry) => ({
      ...entry,
      payload: { ...entry.payload },
    })),
    result: { ...state.result },
  };
}

function cloneCombatant(combatant, config = null) {
  const stats = normalizeStats(combatant.stats);
  const resolvedConfig = mergeConfig(config);
  const maxHp = combatant.maxHp ?? calculateMaxHp(stats, resolvedConfig);
  const maxArmor = combatant.maxArmor ?? calculateMaxArmor(stats, resolvedConfig);

  return {
    ...combatant,
    stats,
    maxHp,
    hp: combatant.hp ?? maxHp,
    maxArmor,
    armor: combatant.armor ?? maxArmor,
    energy: combatant.energy ?? stats.energy,
    skills: [...(combatant.skills ?? DEFAULT_SKILL_LOADOUT)],
    statusEffects: (combatant.statusEffects ?? []).map((status) => ({ ...status })),
    isAlive: combatant.isAlive ?? (combatant.hp ?? maxHp) > 0,
  };
}

function isHydratedCombatant(combatant) {
  return Boolean(combatant.maxHp || combatant.maxArmor || combatant.statusEffects);
}

function mergeConfig(config = null) {
  return {
    ...DEFAULT_COMBAT_CONFIG,
    ...(config ?? {}),
  };
}

function getSkill(actor, skillId) {
  if (!actor.skills.includes(skillId)) {
    throw new Error(`${actor.name} does not know skill: ${skillId}`);
  }

  const skill = BASE_SKILLS[skillId];
  if (!skill) {
    throw new Error(`Unknown skill: ${skillId}`);
  }

  return skill;
}

function findCombatant(state, combatantId) {
  return state.combatants.find((combatant) => combatant.id === combatantId) ?? null;
}

function getLivingCombatant(state, combatantId) {
  const combatant = findCombatant(state, combatantId);

  if (!combatant) {
    throw new Error(`Unknown combatant: ${combatantId}`);
  }

  if (!combatant.isAlive) {
    throw new Error(`${combatant.name} is not alive.`);
  }

  return combatant;
}

function findFirstLivingEnemyOf(state, actor) {
  return (
    state.combatants.find(
      (combatant) => combatant.team !== actor.team && combatant.isAlive,
    ) ?? null
  );
}

function hasStatus(combatant, statusType) {
  return combatant.statusEffects.some((status) => status.type === statusType);
}

function upsertStatus(combatant, status) {
  combatant.statusEffects = combatant.statusEffects.filter(
    (existingStatus) => existingStatus.type !== status.type,
  );
  combatant.statusEffects.push({ ...status });
}

function addEvent(state, type, payload = {}) {
  state.log.push({
    type,
    round: state.round,
    payload,
  });
}

function toNumber(value, fallback) {
  if (value === undefined || value === null) {
    return fallback;
  }

  const number = Number(value);
  if (!Number.isFinite(number)) {
    throw new Error(`Expected a finite number, received: ${value}`);
  }

  return number;
}

function requireString(value, label) {
  if (typeof value !== "string" || value.length === 0) {
    throw new Error(`${label} must be a non-empty string.`);
  }

  return value;
}
