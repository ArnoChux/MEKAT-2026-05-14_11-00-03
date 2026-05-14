using System.Text;
using UnityEngine;

namespace Mekat.Combat
{
    public sealed class CombatDebugRunner : MonoBehaviour
    {
        [Header("Prototype")]
        [SerializeField] private AiStyle enemyStyle = AiStyle.Aggressive;
        [SerializeField] private SkillId playerAction = SkillId.Attack;
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private int seed = 42;

        private CombatState state;

        private void Start()
        {
            if (runOnStart)
            {
                RunOneRound();
            }
        }

        [ContextMenu("Run One Prototype Round")]
        public void RunOneRound()
        {
            var random = new SeededCombatRandom((uint)seed);

            state = CombatPrototypeFixtures.CreatePrototypeCombatState(enemyStyle);
            state = CombatCore.StartRound(state, random.Next01);
            state = CombatCore.QueueAction(state, "robot-player", playerAction, "drone-enemy");
            state = CombatCore.QueueEnemyActions(state, random.Next01);
            state = CombatCore.ResolveRound(state, random.Next01);

            Debug.Log(BuildCombatReport(state));
        }

        private static string BuildCombatReport(CombatState combatState)
        {
            var builder = new StringBuilder();
            builder.AppendLine("=== Mekat Combat Prototype ===");
            builder.AppendLine("Round: " + combatState.Round);
            builder.AppendLine("Result: " + combatState.Result.Status);
            builder.AppendLine();
            builder.AppendLine("Combatants");

            foreach (var combatant in combatState.Combatants)
            {
                builder.Append("- ");
                builder.Append(combatant.Name);
                builder.Append(" | HP ");
                builder.Append(combatant.Hp);
                builder.Append("/");
                builder.Append(combatant.MaxHp);
                builder.Append(" | Armor ");
                builder.Append(combatant.Armor);
                builder.Append("/");
                builder.Append(combatant.MaxArmor);
                builder.Append(" | Energy ");
                builder.Append(combatant.Energy);
                builder.Append(" | Alive ");
                builder.AppendLine(combatant.IsAlive ? "yes" : "no");

                if (combatant.StatusEffects.Count > 0)
                {
                    foreach (var status in combatant.StatusEffects)
                    {
                        builder.Append("  status: ");
                        builder.Append(status.Type);
                        if (status.RemainingTurns > 0)
                        {
                            builder.Append(" (");
                            builder.Append(status.RemainingTurns);
                            builder.Append(" turns)");
                        }
                        builder.AppendLine();
                    }
                }
            }

            builder.AppendLine();
            builder.AppendLine("Log");
            foreach (var logEvent in combatState.Log)
            {
                builder.Append("[R");
                builder.Append(logEvent.Round);
                builder.Append("] ");
                builder.Append(logEvent.Type);
                builder.Append(": ");
                builder.AppendLine(logEvent.Message);
            }

            return builder.ToString();
        }
    }
}
