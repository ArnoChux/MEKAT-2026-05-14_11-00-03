import {
  SKILL_IDS,
  createSeededRandom,
  queueAction,
  queueEnemyActions,
  resolveRound,
  startRound,
} from "../src/combat/core.mjs";
import { createPrototypeCombatState } from "../src/combat/prototype-fixtures.mjs";

const random = createSeededRandom(42);

let state = createPrototypeCombatState();
const maxRounds = 20;

while (state.result.status === "ongoing" && state.round < maxRounds) {
  state = startRound(state, { random });
  state = queueAction(state, "robot-player", SKILL_IDS.ATTACK, "drone-enemy");
  state = queueEnemyActions(state, { random });
  state = resolveRound(state, { random });
}

console.log(JSON.stringify({
  round: state.round,
  stoppedByRoundLimit: state.result.status === "ongoing" && state.round >= maxRounds,
  result: state.result,
  combatants: state.combatants.map((combatant) => ({
    id: combatant.id,
    name: combatant.name,
    hp: combatant.hp,
    armor: combatant.armor,
    energy: combatant.energy,
    isAlive: combatant.isAlive,
    statusEffects: combatant.statusEffects,
  })),
  log: state.log,
}, null, 2));
