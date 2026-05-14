import assert from "node:assert/strict";
import test from "node:test";

import {
  AI_STYLES,
  SKILL_IDS,
  TEAMS,
  calculateDamagePreview,
  calculateInitiativeOrder,
  createCombatState,
  createCombatant,
  createSeededRandom,
  queueAction,
  queueEnemyActions,
  resolveRound,
  startRound,
} from "../src/combat/core.mjs";

test("creates combatants from temporary V1 stats", () => {
  const robot = createCombatant({
    id: "robot",
    team: TEAMS.PLAYER,
    stats: {
      ATK: 6,
      VIT: 4,
      DEF: 5,
      ENERGY: 1,
      AGI: 5,
      DEX: 3,
      LUK: 3,
    },
  });

  assert.equal(robot.maxHp, 40);
  assert.equal(robot.hp, 40);
  assert.equal(robot.maxArmor, 5);
  assert.equal(robot.armor, 5);
  assert.equal(robot.energy, 1);
});

test("orders initiative by AGI, then DEX, then coin flip", () => {
  const slow = createCombatant({
    id: "slow",
    team: TEAMS.PLAYER,
    stats: { AGI: 2, DEX: 9 },
  });
  const agile = createCombatant({
    id: "agile",
    team: TEAMS.ENEMY,
    stats: { AGI: 5, DEX: 1 },
  });
  const dexterous = createCombatant({
    id: "dexterous",
    team: TEAMS.PLAYER,
    stats: { AGI: 5, DEX: 8 },
  });
  const coinWinner = createCombatant({
    id: "coin-winner",
    team: TEAMS.ENEMY,
    stats: { AGI: 5, DEX: 8 },
  });

  const rolls = [0.1, 0.1, 0.2, 0.9];
  const order = calculateInitiativeOrder(
    [slow, agile, dexterous, coinWinner],
    { random: () => rolls.shift() },
  );

  assert.deepEqual(order, ["coin-winner", "dexterous", "agile", "slow"]);
});

test("applies DEF reduction before armor and HP damage", () => {
  let state = createCombatState([
    createCombatant({
      id: "robot",
      team: TEAMS.PLAYER,
      stats: { ATK: 8, AGI: 5 },
    }),
    createCombatant({
      id: "drone",
      team: TEAMS.ENEMY,
      stats: { VIT: 3, DEF: 5, AGI: 1, DEX: 0, LUK: 0 },
    }),
  ]);

  state = startRound(state, { random: () => 0.5 });
  state = queueAction(state, "robot", SKILL_IDS.ATTACK, "drone");
  state = resolveRound(state, { random: () => 0.99 });

  const drone = state.combatants.find((combatant) => combatant.id === "drone");

  assert.equal(drone.armor, 0);
  assert.equal(drone.hp, 34);
  assert.equal(
    state.log.some((entry) => entry.type === "armor_damaged"),
    true,
  );
  assert.equal(
    state.log.some((entry) => entry.type === "hp_damaged"),
    true,
  );
});

test("applies critical hit before DEF and armor", () => {
  const attacker = createCombatant({
    id: "robot",
    team: TEAMS.PLAYER,
    stats: { ATK: 8, LUK: 100 },
  });
  const target = createCombatant({
    id: "drone",
    team: TEAMS.ENEMY,
    stats: { VIT: 3, DEF: 5 },
  });

  const preview = calculateDamagePreview(attacker, target, {
    isCritical: true,
  });

  assert.deepEqual(preview, {
    rawDamage: 8,
    damageAfterCrit: 16,
    defenseReduction: 2,
    damageAfterDefenseStat: 14,
    finalDamage: 14,
  });
});

test("defense halves damage after DEF until the end of the round", () => {
  let state = createCombatState([
    createCombatant({
      id: "robot",
      team: TEAMS.PLAYER,
      stats: { ATK: 8, AGI: 1 },
    }),
    createCombatant({
      id: "drone",
      team: TEAMS.ENEMY,
      stats: { VIT: 3, DEF: 5, AGI: 5, DEX: 0, LUK: 0 },
    }),
  ]);

  state = startRound(state, { random: () => 0.5 });
  state = queueAction(state, "robot", SKILL_IDS.ATTACK, "drone");
  state = queueAction(state, "drone", SKILL_IDS.DEFEND, "drone");
  state = resolveRound(state, { random: () => 0.99 });

  const drone = state.combatants.find((combatant) => combatant.id === "drone");

  assert.equal(drone.armor, 2);
  assert.equal(drone.hp, 35);
  assert.equal(
    drone.statusEffects.some((status) => status.type === "defending"),
    false,
  );
});

test("taunt redirects enemy target for two enemy turns", () => {
  let state = createCombatState([
    createCombatant({
      id: "tank",
      team: TEAMS.PLAYER,
      stats: { ATK: 1, VIT: 10, DEF: 5, AGI: 10 },
    }),
    createCombatant({
      id: "ally",
      team: TEAMS.PLAYER,
      stats: { VIT: 4, DEF: 0, AGI: 1 },
    }),
    createCombatant({
      id: "drone",
      team: TEAMS.ENEMY,
      stats: { ATK: 4, AGI: 5, DEX: 0, LUK: 0 },
    }),
  ]);

  state = startRound(state, { random: () => 0.5 });
  state = queueAction(state, "tank", SKILL_IDS.TAUNT, "drone");
  state = queueAction(state, "drone", SKILL_IDS.ATTACK, "ally");
  state = resolveRound(state, { random: () => 0.99 });

  let tank = state.combatants.find((combatant) => combatant.id === "tank");
  let ally = state.combatants.find((combatant) => combatant.id === "ally");
  let drone = state.combatants.find((combatant) => combatant.id === "drone");

  assert.equal(tank.armor, 3);
  assert.equal(ally.hp, ally.maxHp);
  assert.equal(drone.statusEffects[0].remainingTurns, 1);

  state = startRound(state, { random: () => 0.5 });
  state = queueAction(state, "drone", SKILL_IDS.ATTACK, "ally");
  state = resolveRound(state, { random: () => 0.99 });

  tank = state.combatants.find((combatant) => combatant.id === "tank");
  ally = state.combatants.find((combatant) => combatant.id === "ally");
  drone = state.combatants.find((combatant) => combatant.id === "drone");

  assert.equal(tank.armor, 1);
  assert.equal(ally.hp, ally.maxHp);
  assert.equal(
    drone.statusEffects.some((status) => status.type === "taunted"),
    false,
  );
});

test("enemy defensive style can choose defense when exposed", () => {
  let state = createCombatState([
    createCombatant({
      id: "robot",
      team: TEAMS.PLAYER,
      stats: { ATK: 6, VIT: 4, DEF: 5, AGI: 5 },
    }),
    createCombatant({
      id: "drone",
      team: TEAMS.ENEMY,
      aiStyle: AI_STYLES.DEFENSIVE,
      hp: 5,
      armor: 0,
      stats: { ATK: 5, VIT: 3, DEF: 2, ENERGY: 1, AGI: 4 },
    }),
  ]);

  state = startRound(state, { random: () => 0.5 });
  state = queueEnemyActions(state, { random: () => 0.1 });

  assert.equal(state.actionQueue.length, 1);
  assert.equal(state.actionQueue[0].actorId, "drone");
  assert.equal(state.actionQueue[0].skillId, SKILL_IDS.DEFEND);
});

test("combat ends with victory when all enemies are defeated", () => {
  let state = createCombatState([
    createCombatant({
      id: "robot",
      team: TEAMS.PLAYER,
      stats: { ATK: 100, AGI: 10 },
    }),
    createCombatant({
      id: "drone",
      team: TEAMS.ENEMY,
      stats: { VIT: 0, DEF: 0, AGI: 1, DEX: 0, LUK: 0 },
    }),
  ]);

  state = startRound(state, { random: createSeededRandom(1) });
  state = queueAction(state, "robot", SKILL_IDS.ATTACK, "drone");
  state = resolveRound(state, { random: () => 0.99 });

  assert.equal(state.result.status, "victory");
  assert.equal(
    state.log.some((entry) => entry.type === "combat_ended"),
    true,
  );
});
