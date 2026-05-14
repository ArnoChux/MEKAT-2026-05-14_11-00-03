using System;
using System.Collections.Generic;

namespace Mekat.Combat
{
    public enum CombatTeam
    {
        Player,
        Enemy
    }

    public enum SkillId
    {
        Attack,
        Defend,
        Taunt
    }

    public enum AiStyle
    {
        Aggressive,
        Defensive,
        Opportunistic
    }

    public enum CombatPhase
    {
        Ready,
        Planning,
        Resolving,
        Ended
    }

    public enum CombatResultStatus
    {
        Ongoing,
        Victory,
        Defeat,
        Draw
    }

    public enum CombatTarget
    {
        Self,
        Enemy,
        Ally
    }

    public enum CombatSkillKind
    {
        Damage,
        Defend,
        Taunt
    }

    public enum CombatActionStatus
    {
        Queued,
        Resolved,
        Cancelled
    }

    public enum StatusEffectType
    {
        Defending,
        Taunted
    }

    public enum StatusExpiry
    {
        None,
        EndRound
    }

    [Serializable]
    public sealed class CombatConfig
    {
        public int BaseHp = 20;
        public int HpPerVit = 5;
        public int ArmorPerDef = 1;
        public int DefenseReductionDivisor = 2;
        public float DefendDamageMultiplier = 0.5f;
        public float DodgeChancePerDex = 0.03f;
        public float CritChancePerLuk = 0.04f;
        public int MinDamage = 1;
        public int TauntDurationTurns = 2;

        public CombatConfig Clone()
        {
            return (CombatConfig)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class CombatStats
    {
        public int ATK;
        public int VIT;
        public int DEF;
        public int ENERGY = 1;
        public int AGI;
        public int DEX;
        public int LUK;

        public CombatStats Clone()
        {
            return (CombatStats)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class CombatSkill
    {
        public SkillId Id;
        public string Name;
        public int EnergyCost;
        public CombatTarget Target;
        public CombatSkillKind Kind;
        public int Power;
        public bool CanDodge;
        public bool CanCrit;

        public CombatSkill Clone()
        {
            return (CombatSkill)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class CombatStatusEffect
    {
        public StatusEffectType Type;
        public string SourceId;
        public int RemainingTurns;
        public StatusExpiry Expires;

        public CombatStatusEffect Clone()
        {
            return (CombatStatusEffect)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class Combatant
    {
        public string Id;
        public string Name;
        public CombatTeam Team;
        public CombatStats Stats = new CombatStats();
        public int MaxHp;
        public int Hp;
        public int MaxArmor;
        public int Armor;
        public int Energy;
        public List<SkillId> Skills = new List<SkillId>();
        public AiStyle AiStyle = AiStyle.Aggressive;
        public List<CombatStatusEffect> StatusEffects = new List<CombatStatusEffect>();
        public bool IsAlive = true;

        public Combatant Clone()
        {
            var clone = (Combatant)MemberwiseClone();
            clone.Stats = Stats.Clone();
            clone.Skills = new List<SkillId>(Skills);
            clone.StatusEffects = new List<CombatStatusEffect>();

            foreach (var status in StatusEffects)
            {
                clone.StatusEffects.Add(status.Clone());
            }

            return clone;
        }
    }

    [Serializable]
    public sealed class CombatAction
    {
        public string Id;
        public int Round;
        public int Order;
        public string ActorId;
        public SkillId SkillId;
        public string TargetId;
        public int EnergyCost;
        public CombatActionStatus Status = CombatActionStatus.Queued;

        public CombatAction Clone()
        {
            return (CombatAction)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class CombatEvent
    {
        public string Type;
        public int Round;
        public string Message;

        public CombatEvent Clone()
        {
            return (CombatEvent)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class CombatResult
    {
        public CombatResultStatus Status = CombatResultStatus.Ongoing;
        public bool HasWinningTeam;
        public CombatTeam WinningTeam;

        public CombatResult Clone()
        {
            return (CombatResult)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class DamagePreview
    {
        public int RawDamage;
        public int DamageAfterCrit;
        public int DefenseReduction;
        public int DamageAfterDefenseStat;
        public int FinalDamage;
    }

    [Serializable]
    public sealed class CombatState
    {
        public int Round;
        public CombatPhase Phase = CombatPhase.Ready;
        public CombatConfig Config = new CombatConfig();
        public List<Combatant> Combatants = new List<Combatant>();
        public List<string> InitiativeOrder = new List<string>();
        public List<CombatAction> ActionQueue = new List<CombatAction>();
        public List<CombatEvent> Log = new List<CombatEvent>();
        public CombatResult Result = new CombatResult();

        public CombatState Clone()
        {
            var clone = (CombatState)MemberwiseClone();
            clone.Config = Config.Clone();
            clone.Combatants = new List<Combatant>();
            clone.InitiativeOrder = new List<string>(InitiativeOrder);
            clone.ActionQueue = new List<CombatAction>();
            clone.Log = new List<CombatEvent>();
            clone.Result = Result.Clone();

            foreach (var combatant in Combatants)
            {
                clone.Combatants.Add(combatant.Clone());
            }

            foreach (var action in ActionQueue)
            {
                clone.ActionQueue.Add(action.Clone());
            }

            foreach (var logEvent in Log)
            {
                clone.Log.Add(logEvent.Clone());
            }

            return clone;
        }
    }
}
