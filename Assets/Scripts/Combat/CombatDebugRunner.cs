using System.Text;
using UnityEngine;

namespace Mekat.Combat
{
    public enum CombatDebugRunMode
    {
        PlayerChoice,
        FullAuto,
        OneRound
    }

    public sealed class CombatDebugRunner : MonoBehaviour
    {
        [Header("Prototype")]
        [SerializeField] private AiStyle enemyStyle = AiStyle.Aggressive;
        [SerializeField] private SkillId playerAction = SkillId.Attack;
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private CombatDebugRunMode runMode = CombatDebugRunMode.PlayerChoice;
        [SerializeField] private int maxRounds = 20;
        [SerializeField] private int seed = 42;

        [Header("Debug UI")]
        [SerializeField] private bool showDebugGui = true;
        [SerializeField] private int maxVisibleLogLines = 18;

        private CombatState state;
        private SeededCombatRandom interactiveRandom;
        private string statusMessage = "Ready.";
        private Vector2 logScroll;

        private void Start()
        {
            if (runOnStart)
            {
                if (runMode == CombatDebugRunMode.PlayerChoice)
                {
                    StartPlayerChoiceCombat();
                }
                else if (runMode == CombatDebugRunMode.FullAuto)
                {
                    RunFullPrototypeCombat();
                }
                else
                {
                    RunOneRound();
                }
            }
        }

        private void OnGUI()
        {
            if (!showDebugGui || state == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 460f, Screen.height - 32f), GUI.skin.box);
            GUILayout.Label("Mekat Combat Prototype");
            GUILayout.Label(statusMessage);
            GUILayout.Space(8f);
            DrawCombatantState();
            GUILayout.Space(8f);
            DrawPlayerActions();
            GUILayout.Space(8f);
            DrawCombatLog();
            GUILayout.EndArea();
        }

        [ContextMenu("Start Player Choice Combat")]
        public void StartPlayerChoiceCombat()
        {
            interactiveRandom = new SeededCombatRandom((uint)seed);
            state = CombatPrototypeFixtures.CreatePrototypeCombatState(enemyStyle);
            StartNextInteractiveRound();
        }

        [ContextMenu("Run One Prototype Round")]
        public void RunOneRound()
        {
            var random = new SeededCombatRandom((uint)seed);

            state = CombatPrototypeFixtures.CreatePrototypeCombatState(enemyStyle);
            state = CombatCore.StartRound(state, random.Next01);
            state = CombatCore.QueueAction(state, "robot-player", playerAction, GetPlayerTargetId(playerAction));
            state = CombatCore.QueueEnemyActions(state, random.Next01);
            state = CombatCore.ResolveRound(state, random.Next01);

            Debug.Log(BuildCombatReport(state));
        }

        [ContextMenu("Run Full Prototype Combat")]
        public void RunFullPrototypeCombat()
        {
            var random = new SeededCombatRandom((uint)seed);
            var roundLimit = maxRounds < 1 ? 1 : maxRounds;

            state = CombatPrototypeFixtures.CreatePrototypeCombatState(enemyStyle);

            while (state.Result.Status == CombatResultStatus.Ongoing && state.Round < roundLimit)
            {
                state = CombatCore.StartRound(state, random.Next01);

                if (CombatCore.FindCombatant(state, "robot-player").IsAlive
                    && CombatCore.FindCombatant(state, "drone-enemy").IsAlive)
                {
                    state = CombatCore.QueueAction(
                        state,
                        "robot-player",
                        playerAction,
                        GetPlayerTargetId(playerAction));
                }

                state = CombatCore.QueueEnemyActions(state, random.Next01);
                state = CombatCore.ResolveRound(state, random.Next01);
            }

            Debug.Log(BuildCombatReport(state, roundLimit));
        }

        public void ChooseAttack()
        {
            ChoosePlayerAction(SkillId.Attack);
        }

        public void ChooseDefense()
        {
            ChoosePlayerAction(SkillId.Defend);
        }

        public void ChooseTaunt()
        {
            ChoosePlayerAction(SkillId.Taunt);
        }

        private void StartNextInteractiveRound()
        {
            var roundLimit = maxRounds < 1 ? 1 : maxRounds;

            if (state.Result.Status != CombatResultStatus.Ongoing)
            {
                statusMessage = "Combat ended: " + state.Result.Status + ".";
                Debug.Log(BuildCombatReport(state));
                return;
            }

            if (state.Round >= roundLimit)
            {
                statusMessage = "Max round limit reached.";
                Debug.Log(BuildCombatReport(state, roundLimit));
                return;
            }

            state = CombatCore.StartRound(state, interactiveRandom.Next01);
            statusMessage = "Round " + state.Round + ": choose player action.";
        }

        private void ChoosePlayerAction(SkillId skillId)
        {
            if (state == null || state.Result.Status != CombatResultStatus.Ongoing)
            {
                return;
            }

            var player = CombatCore.FindCombatant(state, "robot-player");
            if (player == null || !player.IsAlive)
            {
                statusMessage = "Player is not able to act.";
                return;
            }

            if (state.Phase != CombatPhase.Planning)
            {
                statusMessage = "Action unavailable outside planning phase.";
                return;
            }

            if (player.Energy <= 0)
            {
                statusMessage = "No ENERGY left.";
                return;
            }

            state = CombatCore.QueueAction(
                state,
                "robot-player",
                skillId,
                GetPlayerTargetId(skillId));

            player = CombatCore.FindCombatant(state, "robot-player");
            if (player != null && player.Energy > 0)
            {
                statusMessage = "Action queued. ENERGY left: " + player.Energy + ".";
                return;
            }

            ResolveInteractiveRound();
        }

        private void ResolveInteractiveRound()
        {
            state = CombatCore.QueueEnemyActions(state, interactiveRandom.Next01);
            state = CombatCore.ResolveRound(state, interactiveRandom.Next01);

            Debug.Log(BuildRoundReport(state, state.Round));

            if (state.Result.Status == CombatResultStatus.Ongoing)
            {
                StartNextInteractiveRound();
                return;
            }

            statusMessage = "Combat ended: " + state.Result.Status + ".";
            Debug.Log(BuildCombatReport(state));
        }

        private void DrawCombatantState()
        {
            GUILayout.Label("Combatants");

            foreach (var combatant in state.Combatants)
            {
                GUILayout.Label(
                    combatant.Name
                    + " | HP " + combatant.Hp + "/" + combatant.MaxHp
                    + " | Armor " + combatant.Armor + "/" + combatant.MaxArmor
                    + " | Energy " + combatant.Energy
                    + " | " + (combatant.IsAlive ? "Alive" : "KO"));

                foreach (var status in combatant.StatusEffects)
                {
                    var suffix = status.RemainingTurns > 0
                        ? " (" + status.RemainingTurns + " turns)"
                        : string.Empty;
                    GUILayout.Label("  " + status.Type + suffix);
                }
            }
        }

        private void DrawPlayerActions()
        {
            var player = CombatCore.FindCombatant(state, "robot-player");
            var enemy = CombatCore.FindCombatant(state, "drone-enemy");
            var canAct = state.Result.Status == CombatResultStatus.Ongoing
                && state.Phase == CombatPhase.Planning
                && player != null
                && player.IsAlive
                && player.Energy > 0
                && enemy != null
                && enemy.IsAlive;

            GUILayout.Label("Player Actions");
            GUI.enabled = canAct;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Attaque"))
            {
                ChooseAttack();
            }

            if (GUILayout.Button("Défense"))
            {
                ChooseDefense();
            }

            if (GUILayout.Button("Taunt"))
            {
                ChooseTaunt();
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            if (GUILayout.Button("Restart Combat"))
            {
                StartPlayerChoiceCombat();
            }
        }

        private void DrawCombatLog()
        {
            GUILayout.Label("Log");
            logScroll = GUILayout.BeginScrollView(logScroll, GUILayout.Height(240f));

            var visibleLogLines = maxVisibleLogLines < 1 ? 1 : maxVisibleLogLines;
            var start = state.Log.Count - visibleLogLines;
            if (start < 0)
            {
                start = 0;
            }

            for (var index = start; index < state.Log.Count; index += 1)
            {
                var logEvent = state.Log[index];
                GUILayout.Label("[R" + logEvent.Round + "] " + logEvent.Type + ": " + logEvent.Message);
            }

            GUILayout.EndScrollView();
        }

        private static string GetPlayerTargetId(SkillId skillId)
        {
            return skillId == SkillId.Defend ? "robot-player" : "drone-enemy";
        }

        private static string BuildCombatReport(CombatState combatState, int? roundLimit = null)
        {
            var builder = new StringBuilder();
            builder.AppendLine("=== Mekat Combat Prototype ===");
            builder.AppendLine("Round: " + combatState.Round);
            builder.AppendLine("Result: " + combatState.Result.Status);

            if (roundLimit.HasValue
                && combatState.Result.Status == CombatResultStatus.Ongoing
                && combatState.Round >= roundLimit.Value)
            {
                builder.AppendLine("Stopped: max round limit reached.");
            }

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

        private static string BuildRoundReport(CombatState combatState, int round)
        {
            var builder = new StringBuilder();
            builder.AppendLine("=== Mekat Combat Round " + round + " ===");
            builder.AppendLine("Result: " + combatState.Result.Status);

            foreach (var logEvent in combatState.Log)
            {
                if (logEvent.Round != round)
                {
                    continue;
                }

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
