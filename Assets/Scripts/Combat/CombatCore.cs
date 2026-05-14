using System;
using System.Collections.Generic;

namespace Mekat.Combat
{
    public static class CombatCore
    {
        public static readonly Dictionary<SkillId, CombatSkill> BaseSkills =
            new Dictionary<SkillId, CombatSkill>
            {
                {
                    SkillId.Attack,
                    new CombatSkill
                    {
                        Id = SkillId.Attack,
                        Name = "Attaque",
                        EnergyCost = 1,
                        Target = CombatTarget.Enemy,
                        Kind = CombatSkillKind.Damage,
                        Power = 0,
                        CanDodge = true,
                        CanCrit = true
                    }
                },
                {
                    SkillId.Defend,
                    new CombatSkill
                    {
                        Id = SkillId.Defend,
                        Name = "Défense",
                        EnergyCost = 1,
                        Target = CombatTarget.Self,
                        Kind = CombatSkillKind.Defend,
                        CanDodge = false,
                        CanCrit = false
                    }
                },
                {
                    SkillId.Taunt,
                    new CombatSkill
                    {
                        Id = SkillId.Taunt,
                        Name = "Taunt",
                        EnergyCost = 1,
                        Target = CombatTarget.Enemy,
                        Kind = CombatSkillKind.Taunt,
                        CanDodge = true,
                        CanCrit = false
                    }
                }
            };

        private static readonly SkillId[] DefaultSkillLoadout =
        {
            SkillId.Attack,
            SkillId.Defend,
            SkillId.Taunt
        };

        private static readonly Random DefaultRandom = new Random();

        public static Combatant CreateCombatant(
            string id,
            string name,
            CombatTeam team,
            CombatStats stats,
            AiStyle aiStyle = AiStyle.Aggressive,
            IEnumerable<SkillId> skills = null,
            CombatConfig config = null,
            int? hp = null,
            int? armor = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Combatant id must not be empty.", nameof(id));
            }

            var resolvedConfig = CloneConfig(config);
            var resolvedStats = stats != null ? stats.Clone() : new CombatStats();
            var maxHp = CalculateMaxHp(resolvedStats, resolvedConfig);
            var maxArmor = CalculateMaxArmor(resolvedStats, resolvedConfig);
            var combatant = new Combatant
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(name) ? id : name,
                Team = team,
                Stats = resolvedStats,
                MaxHp = maxHp,
                Hp = hp ?? maxHp,
                MaxArmor = maxArmor,
                Armor = armor ?? maxArmor,
                Energy = resolvedStats.ENERGY,
                AiStyle = aiStyle,
                IsAlive = (hp ?? maxHp) > 0
            };

            combatant.Skills.AddRange(skills ?? DefaultSkillLoadout);
            return combatant;
        }

        public static CombatState CreateCombatState(IEnumerable<Combatant> combatants, CombatConfig config = null)
        {
            var state = new CombatState
            {
                Config = CloneConfig(config),
                Result = new CombatResult()
            };

            foreach (var combatant in combatants)
            {
                state.Combatants.Add(HydrateInitialCombatant(combatant, state.Config));
            }

            state.Result = GetCombatResult(state);
            AddEvent(state, "combat_started", "Combat started.");
            return state;
        }

        public static CombatState StartRound(CombatState state, Func<float> random = null)
        {
            var next = state.Clone();

            if (GetCombatResult(next).Status != CombatResultStatus.Ongoing)
            {
                return next;
            }

            next.Round += 1;
            next.Phase = CombatPhase.Planning;
            next.ActionQueue.Clear();

            foreach (var combatant in next.Combatants)
            {
                if (!combatant.IsAlive)
                {
                    continue;
                }

                combatant.Energy = combatant.Stats.ENERGY;
            }

            next.InitiativeOrder = CalculateInitiativeOrder(next.Combatants, random);

            AddEvent(next, "round_started", "Round " + next.Round + " started.");
            AddEvent(next, "initiative_calculated", "Initiative: " + string.Join(" > ", next.InitiativeOrder));

            return next;
        }

        public static CombatState QueueAction(CombatState state, string actorId, SkillId skillId, string targetId = null)
        {
            var next = state.Clone();
            var actor = GetLivingCombatant(next, actorId);
            var skill = GetSkill(actor, skillId);
            var resolvedTargetId = ResolveActionTarget(next, actor, skill, targetId, false);

            if (actor.Energy < skill.EnergyCost)
            {
                throw new InvalidOperationException(actor.Name + " does not have enough ENERGY for " + skill.Name + ".");
            }

            actor.Energy -= skill.EnergyCost;

            var action = new CombatAction
            {
                Id = next.Round + ":" + (next.ActionQueue.Count + 1) + ":" + actor.Id + ":" + skill.Id,
                Round = next.Round,
                Order = next.ActionQueue.Count,
                ActorId = actor.Id,
                SkillId = skill.Id,
                TargetId = resolvedTargetId,
                EnergyCost = skill.EnergyCost,
                Status = CombatActionStatus.Queued
            };

            next.ActionQueue.Add(action);
            AddEvent(
                next,
                "action_chosen",
                actor.Name + " chose " + skill.Name + " on " + resolvedTargetId + ". ENERGY left: " + actor.Energy + ".");

            return next;
        }

        public static CombatState QueueEnemyActions(CombatState state, Func<float> random = null)
        {
            var next = state.Clone();
            var enemyIds = new List<string>();

            foreach (var combatant in next.Combatants)
            {
                if (combatant.Team == CombatTeam.Enemy && combatant.IsAlive)
                {
                    enemyIds.Add(combatant.Id);
                }
            }

            foreach (var enemyId in enemyIds)
            {
                while (GetCombatResult(next).Status == CombatResultStatus.Ongoing)
                {
                    var enemy = FindCombatant(next, enemyId);
                    if (enemy == null || !enemy.IsAlive || enemy.Energy <= 0)
                    {
                        break;
                    }

                    var plannedAction = ChooseEnemyAction(next, enemy.Id, random);
                    if (plannedAction == null)
                    {
                        break;
                    }

                    next = QueueAction(next, plannedAction.ActorId, plannedAction.SkillId, plannedAction.TargetId);
                }
            }

            return next;
        }

        public static CombatState ResolveRound(CombatState state, Func<float> random = null)
        {
            var next = state.Clone();
            var rng = random ?? NextDefaultRandom;

            next.Phase = CombatPhase.Resolving;

            foreach (var actorId in next.InitiativeOrder)
            {
                if (GetCombatResult(next).Status != CombatResultStatus.Ongoing)
                {
                    break;
                }

                var actor = FindCombatant(next, actorId);
                if (actor == null || !actor.IsAlive)
                {
                    continue;
                }

                var actions = GetQueuedActionsForActor(next, actorId);
                foreach (var action in actions)
                {
                    if (GetCombatResult(next).Status != CombatResultStatus.Ongoing)
                    {
                        break;
                    }

                    action.Status = CombatActionStatus.Resolved;
                    ResolveAction(next, action, rng);
                    next.Result = GetCombatResult(next);
                }

                DecrementTurnStatuses(next, actor.Id);
            }

            ClearEndOfRoundStatuses(next);
            next.ActionQueue.Clear();
            next.Result = GetCombatResult(next);
            next.Phase = next.Result.Status == CombatResultStatus.Ongoing ? CombatPhase.Ready : CombatPhase.Ended;

            AddEvent(next, "round_ended", "Round " + next.Round + " ended. Result: " + next.Result.Status + ".");

            if (next.Result.Status != CombatResultStatus.Ongoing)
            {
                AddEvent(next, "combat_ended", "Combat ended: " + next.Result.Status + ".");
            }

            return next;
        }

        public static PlannedCombatAction ChooseEnemyAction(CombatState state, string actorId, Func<float> random = null)
        {
            var rng = random ?? NextDefaultRandom;
            var enemy = GetLivingCombatant(state, actorId);

            if (enemy.Team != CombatTeam.Enemy)
            {
                throw new InvalidOperationException(enemy.Name + " is not an enemy combatant.");
            }

            var forcedTargetId = GetForcedTargetId(state, enemy);
            var defaultTarget = !string.IsNullOrEmpty(forcedTargetId)
                ? FindCombatant(state, forcedTargetId)
                : FindFirstLivingEnemyOf(state, enemy);

            if (defaultTarget == null)
            {
                return null;
            }

            if (enemy.AiStyle == AiStyle.Defensive && ShouldDefensiveAiDefend(enemy, rng))
            {
                return new PlannedCombatAction(enemy.Id, SkillId.Defend, enemy.Id);
            }

            if (enemy.AiStyle == AiStyle.Opportunistic && ShouldOpportunisticAiDefend(enemy, defaultTarget, rng))
            {
                return new PlannedCombatAction(enemy.Id, SkillId.Defend, enemy.Id);
            }

            if (enemy.AiStyle == AiStyle.Aggressive && ShouldAggressiveAiDefend(enemy, rng))
            {
                return new PlannedCombatAction(enemy.Id, SkillId.Defend, enemy.Id);
            }

            return new PlannedCombatAction(enemy.Id, SkillId.Attack, defaultTarget.Id);
        }

        public static DamagePreview CalculateDamagePreview(
            Combatant attacker,
            Combatant target,
            CombatConfig config = null,
            int skillPower = 0,
            bool isCritical = false,
            bool isDefending = false)
        {
            var resolvedConfig = CloneConfig(config);
            var rawDamage = Math.Max(resolvedConfig.MinDamage, attacker.Stats.ATK + skillPower);
            var damageAfterCrit = isCritical ? rawDamage * 2 : rawDamage;
            var defenseReduction = target.Stats.DEF / resolvedConfig.DefenseReductionDivisor;
            var damageAfterDefenseStat = Math.Max(
                resolvedConfig.MinDamage,
                damageAfterCrit - defenseReduction);
            var finalDamage = isDefending
                ? Math.Max(
                    resolvedConfig.MinDamage,
                    (int)Math.Ceiling(damageAfterDefenseStat * resolvedConfig.DefendDamageMultiplier))
                : damageAfterDefenseStat;

            return new DamagePreview
            {
                RawDamage = rawDamage,
                DamageAfterCrit = damageAfterCrit,
                DefenseReduction = defenseReduction,
                DamageAfterDefenseStat = damageAfterDefenseStat,
                FinalDamage = finalDamage
            };
        }

        public static CombatResult GetCombatResult(CombatState state)
        {
            var playersAlive = false;
            var enemiesAlive = false;

            foreach (var combatant in state.Combatants)
            {
                if (!combatant.IsAlive)
                {
                    continue;
                }

                if (combatant.Team == CombatTeam.Player)
                {
                    playersAlive = true;
                }
                else if (combatant.Team == CombatTeam.Enemy)
                {
                    enemiesAlive = true;
                }
            }

            if (!playersAlive && !enemiesAlive)
            {
                return new CombatResult { Status = CombatResultStatus.Draw, HasWinningTeam = false };
            }

            if (!playersAlive)
            {
                return new CombatResult
                {
                    Status = CombatResultStatus.Defeat,
                    HasWinningTeam = true,
                    WinningTeam = CombatTeam.Enemy
                };
            }

            if (!enemiesAlive)
            {
                return new CombatResult
                {
                    Status = CombatResultStatus.Victory,
                    HasWinningTeam = true,
                    WinningTeam = CombatTeam.Player
                };
            }

            return new CombatResult { Status = CombatResultStatus.Ongoing, HasWinningTeam = false };
        }

        public static int CalculateMaxHp(CombatStats stats, CombatConfig config = null)
        {
            var resolvedConfig = CloneConfig(config);
            return resolvedConfig.BaseHp + stats.VIT * resolvedConfig.HpPerVit;
        }

        public static int CalculateMaxArmor(CombatStats stats, CombatConfig config = null)
        {
            var resolvedConfig = CloneConfig(config);
            return stats.DEF * resolvedConfig.ArmorPerDef;
        }

        public static float CalculateDodgeChance(CombatStats stats, CombatConfig config = null)
        {
            var resolvedConfig = CloneConfig(config);
            return stats.DEX * resolvedConfig.DodgeChancePerDex;
        }

        public static float CalculateCritChance(CombatStats stats, CombatConfig config = null)
        {
            var resolvedConfig = CloneConfig(config);
            return stats.LUK * resolvedConfig.CritChancePerLuk;
        }

        public static List<string> CalculateInitiativeOrder(IEnumerable<Combatant> combatants, Func<float> random = null)
        {
            var rng = random ?? NextDefaultRandom;
            var entries = new List<InitiativeEntry>();

            foreach (var combatant in combatants)
            {
                if (!combatant.IsAlive)
                {
                    continue;
                }

                entries.Add(new InitiativeEntry
                {
                    Id = combatant.Id,
                    AGI = combatant.Stats.AGI,
                    DEX = combatant.Stats.DEX,
                    CoinFlip = rng()
                });
            }

            entries.Sort((left, right) =>
            {
                var agiCompare = right.AGI.CompareTo(left.AGI);
                if (agiCompare != 0)
                {
                    return agiCompare;
                }

                var dexCompare = right.DEX.CompareTo(left.DEX);
                if (dexCompare != 0)
                {
                    return dexCompare;
                }

                return right.CoinFlip.CompareTo(left.CoinFlip);
            });

            var order = new List<string>();
            foreach (var entry in entries)
            {
                order.Add(entry.Id);
            }

            return order;
        }

        public static Combatant FindCombatant(CombatState state, string combatantId)
        {
            foreach (var combatant in state.Combatants)
            {
                if (combatant.Id == combatantId)
                {
                    return combatant;
                }
            }

            return null;
        }

        private static void ResolveAction(CombatState state, CombatAction action, Func<float> random)
        {
            var actor = FindCombatant(state, action.ActorId);
            if (actor == null || !actor.IsAlive)
            {
                action.Status = CombatActionStatus.Cancelled;
                AddEvent(state, "action_cancelled", "Action cancelled: actor is not alive.");
                return;
            }

            var skill = GetSkill(actor, action.SkillId);
            var targetId = ResolveActionTarget(state, actor, skill, action.TargetId, true);
            var target = FindCombatant(state, targetId);

            if (target == null || !target.IsAlive)
            {
                action.Status = CombatActionStatus.Cancelled;
                AddEvent(state, "action_cancelled", actor.Name + "'s action was cancelled: target is not alive.");
                return;
            }

            if (skill.Kind == CombatSkillKind.Damage)
            {
                ResolveDamageAction(state, actor, target, skill, random);
                return;
            }

            if (skill.Kind == CombatSkillKind.Defend)
            {
                UpsertStatus(actor, new CombatStatusEffect
                {
                    Type = StatusEffectType.Defending,
                    SourceId = actor.Id,
                    Expires = StatusExpiry.EndRound
                });
                AddEvent(state, "defense_started", actor.Name + " is defending until the end of the round.");
                return;
            }

            if (skill.Kind == CombatSkillKind.Taunt)
            {
                if (RollDodge(state, actor, target, skill, random))
                {
                    return;
                }

                UpsertStatus(target, new CombatStatusEffect
                {
                    Type = StatusEffectType.Taunted,
                    SourceId = actor.Id,
                    RemainingTurns = state.Config.TauntDurationTurns,
                    Expires = StatusExpiry.None
                });
                AddEvent(
                    state,
                    "taunt_applied",
                    actor.Name + " taunted " + target.Name + " for " + state.Config.TauntDurationTurns + " turns.");
            }
        }

        private static void ResolveDamageAction(
            CombatState state,
            Combatant actor,
            Combatant target,
            CombatSkill skill,
            Func<float> random)
        {
            if (RollDodge(state, actor, target, skill, random))
            {
                return;
            }

            var critChance = CalculateCritChance(actor.Stats, state.Config);
            var critRoll = random();
            var isCritical = skill.CanCrit && critRoll < critChance;
            var isDefending = HasStatus(target, StatusEffectType.Defending);
            var damage = CalculateDamagePreview(
                actor,
                target,
                state.Config,
                skill.Power,
                isCritical,
                isDefending);
            var armorDamage = Math.Min(target.Armor, damage.FinalDamage);
            var hpDamage = damage.FinalDamage - armorDamage;

            target.Armor = Math.Max(0, target.Armor - armorDamage);
            target.Hp = Math.Max(0, target.Hp - hpDamage);

            if (isCritical)
            {
                AddEvent(state, "critical_hit", actor.Name + " landed a critical hit on " + target.Name + ".");
            }

            if (armorDamage > 0)
            {
                AddEvent(
                    state,
                    "armor_damaged",
                    target.Name + " lost " + armorDamage + " armor. Remaining armor: " + target.Armor + ".");
            }

            if (hpDamage > 0)
            {
                AddEvent(
                    state,
                    "hp_damaged",
                    target.Name + " lost " + hpDamage + " HP. Remaining HP: " + target.Hp + ".");
            }

            AddEvent(
                state,
                "attack_resolved",
                actor.Name + " attacked " + target.Name + " for " + damage.FinalDamage
                + " final damage. DEF reduction: " + damage.DefenseReduction
                + ". Armor damage: " + armorDamage + ". HP damage: " + hpDamage + ".");

            if (target.Hp <= 0 && target.IsAlive)
            {
                target.IsAlive = false;
                target.Energy = 0;
                AddEvent(state, "combatant_defeated", target.Name + " is defeated.");
            }
        }

        private static bool RollDodge(
            CombatState state,
            Combatant actor,
            Combatant target,
            CombatSkill skill,
            Func<float> random)
        {
            if (!skill.CanDodge)
            {
                return false;
            }

            var dodgeChance = CalculateDodgeChance(target.Stats, state.Config);
            var dodgeRoll = random();
            var dodged = dodgeRoll < dodgeChance;

            if (dodged)
            {
                AddEvent(
                    state,
                    "action_dodged",
                    target.Name + " dodged " + actor.Name + "'s " + skill.Name + ".");
            }

            return dodged;
        }

        private static string ResolveActionTarget(
            CombatState state,
            Combatant actor,
            CombatSkill skill,
            string requestedTargetId,
            bool allowRedirectLog)
        {
            if (skill.Target == CombatTarget.Self)
            {
                return actor.Id;
            }

            var forcedTargetId = skill.Target == CombatTarget.Enemy
                ? GetForcedTargetId(state, actor)
                : null;
            var targetId = !string.IsNullOrEmpty(forcedTargetId) ? forcedTargetId : requestedTargetId;

            if (string.IsNullOrEmpty(targetId))
            {
                throw new InvalidOperationException(skill.Name + " requires a target.");
            }

            var target = GetLivingCombatant(state, targetId);

            if (skill.Target == CombatTarget.Enemy && target.Team == actor.Team)
            {
                throw new InvalidOperationException(skill.Name + " must target an enemy.");
            }

            if (skill.Target == CombatTarget.Ally && target.Team != actor.Team)
            {
                throw new InvalidOperationException(skill.Name + " must target an ally.");
            }

            if (!string.IsNullOrEmpty(forcedTargetId)
                && requestedTargetId != forcedTargetId
                && allowRedirectLog)
            {
                AddEvent(
                    state,
                    "target_forced",
                    actor.Name + " is taunted and must target " + target.Name + ".");
            }

            return target.Id;
        }

        private static string GetForcedTargetId(CombatState state, Combatant actor)
        {
            foreach (var status in actor.StatusEffects)
            {
                if (status.Type != StatusEffectType.Taunted)
                {
                    continue;
                }

                var source = FindCombatant(state, status.SourceId);
                if (source != null && source.IsAlive && source.Team != actor.Team)
                {
                    return source.Id;
                }
            }

            return null;
        }

        private static void DecrementTurnStatuses(CombatState state, string actorId)
        {
            var actor = FindCombatant(state, actorId);
            if (actor == null || !actor.IsAlive)
            {
                return;
            }

            var nextStatuses = new List<CombatStatusEffect>();
            foreach (var status in actor.StatusEffects)
            {
                if (status.Type != StatusEffectType.Taunted)
                {
                    nextStatuses.Add(status);
                    continue;
                }

                var remainingTurns = status.RemainingTurns - 1;
                if (remainingTurns > 0)
                {
                    var nextStatus = status.Clone();
                    nextStatus.RemainingTurns = remainingTurns;
                    nextStatuses.Add(nextStatus);
                    continue;
                }

                AddEvent(state, "status_expired", actor.Name + "'s taunt expired.");
            }

            actor.StatusEffects = nextStatuses;
        }

        private static void ClearEndOfRoundStatuses(CombatState state)
        {
            foreach (var combatant in state.Combatants)
            {
                var nextStatuses = new List<CombatStatusEffect>();
                foreach (var status in combatant.StatusEffects)
                {
                    if (status.Expires == StatusExpiry.EndRound)
                    {
                        AddEvent(state, "status_expired", combatant.Name + "'s " + status.Type + " expired.");
                        continue;
                    }

                    nextStatuses.Add(status);
                }

                combatant.StatusEffects = nextStatuses;
            }
        }

        private static bool ShouldDefensiveAiDefend(Combatant enemy, Func<float> random)
        {
            var lowHp = enemy.Hp <= enemy.MaxHp * 0.35f;
            var lowArmor = enemy.MaxArmor > 0 && enemy.Armor <= enemy.MaxArmor * 0.25f;
            return (lowHp || lowArmor) && random() < 0.8f;
        }

        private static bool ShouldOpportunisticAiDefend(Combatant enemy, Combatant target, Func<float> random)
        {
            var targetArmorThreshold = target.MaxArmor * 0.2f;
            if (targetArmorThreshold < 1f)
            {
                targetArmorThreshold = 1f;
            }

            var selfArmorThreshold = enemy.MaxArmor * 0.2f;
            if (selfArmorThreshold < 0f)
            {
                selfArmorThreshold = 0f;
            }

            var targetExposed = target.Armor <= targetArmorThreshold;
            var selfExposed = enemy.Armor <= selfArmorThreshold;
            return !targetExposed && selfExposed && random() < 0.35f;
        }

        private static bool ShouldAggressiveAiDefend(Combatant enemy, Func<float> random)
        {
            var nearlyDefeated = enemy.Hp <= enemy.MaxHp * 0.2f;
            return nearlyDefeated && random() < 0.2f;
        }

        private static List<CombatAction> GetQueuedActionsForActor(CombatState state, string actorId)
        {
            var actions = new List<CombatAction>();
            foreach (var action in state.ActionQueue)
            {
                if (action.ActorId == actorId && action.Status == CombatActionStatus.Queued)
                {
                    actions.Add(action);
                }
            }

            actions.Sort((left, right) => left.Order.CompareTo(right.Order));
            return actions;
        }

        private static Combatant HydrateInitialCombatant(Combatant combatant, CombatConfig config)
        {
            var clone = combatant.Clone();
            clone.Stats = clone.Stats ?? new CombatStats();

            if (clone.MaxHp <= 0)
            {
                clone.MaxHp = CalculateMaxHp(clone.Stats, config);
            }

            if (clone.Hp <= 0)
            {
                clone.Hp = clone.MaxHp;
            }

            if (clone.MaxArmor <= 0)
            {
                clone.MaxArmor = CalculateMaxArmor(clone.Stats, config);
            }

            if (clone.Armor < 0)
            {
                clone.Armor = 0;
            }
            else if (clone.Armor == 0 && clone.MaxArmor > 0)
            {
                clone.Armor = clone.MaxArmor;
            }

            if (clone.Energy <= 0)
            {
                clone.Energy = clone.Stats.ENERGY;
            }

            if (clone.Skills == null || clone.Skills.Count == 0)
            {
                clone.Skills = new List<SkillId>(DefaultSkillLoadout);
            }

            clone.IsAlive = clone.Hp > 0;
            return clone;
        }

        private static CombatSkill GetSkill(Combatant actor, SkillId skillId)
        {
            if (!actor.Skills.Contains(skillId))
            {
                throw new InvalidOperationException(actor.Name + " does not know skill: " + skillId + ".");
            }

            if (!BaseSkills.TryGetValue(skillId, out var skill))
            {
                throw new InvalidOperationException("Unknown skill: " + skillId + ".");
            }

            return skill;
        }

        private static Combatant GetLivingCombatant(CombatState state, string combatantId)
        {
            var combatant = FindCombatant(state, combatantId);

            if (combatant == null)
            {
                throw new InvalidOperationException("Unknown combatant: " + combatantId + ".");
            }

            if (!combatant.IsAlive)
            {
                throw new InvalidOperationException(combatant.Name + " is not alive.");
            }

            return combatant;
        }

        private static Combatant FindFirstLivingEnemyOf(CombatState state, Combatant actor)
        {
            foreach (var combatant in state.Combatants)
            {
                if (combatant.Team != actor.Team && combatant.IsAlive)
                {
                    return combatant;
                }
            }

            return null;
        }

        private static bool HasStatus(Combatant combatant, StatusEffectType statusType)
        {
            foreach (var status in combatant.StatusEffects)
            {
                if (status.Type == statusType)
                {
                    return true;
                }
            }

            return false;
        }

        private static void UpsertStatus(Combatant combatant, CombatStatusEffect status)
        {
            combatant.StatusEffects.RemoveAll(existingStatus => existingStatus.Type == status.Type);
            combatant.StatusEffects.Add(status);
        }

        private static void AddEvent(CombatState state, string type, string message)
        {
            state.Log.Add(new CombatEvent
            {
                Type = type,
                Round = state.Round,
                Message = message
            });
        }

        private static CombatConfig CloneConfig(CombatConfig config)
        {
            return config != null ? config.Clone() : new CombatConfig();
        }

        private static float NextDefaultRandom()
        {
            return (float)DefaultRandom.NextDouble();
        }

        private sealed class InitiativeEntry
        {
            public string Id;
            public int AGI;
            public int DEX;
            public float CoinFlip;
        }
    }

    public sealed class PlannedCombatAction
    {
        public readonly string ActorId;
        public readonly SkillId SkillId;
        public readonly string TargetId;

        public PlannedCombatAction(string actorId, SkillId skillId, string targetId)
        {
            ActorId = actorId;
            SkillId = skillId;
            TargetId = targetId;
        }
    }
}
