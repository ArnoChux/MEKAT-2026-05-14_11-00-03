using System;

namespace Mekat.Combat
{
    public static class CombatPrototypeFixtures
    {
        public static CombatStats CreatePlayerStats()
        {
            return new CombatStats
            {
                ATK = 6,
                VIT = 4,
                DEF = 5,
                ENERGY = 1,
                AGI = 5,
                DEX = 3,
                LUK = 3
            };
        }

        public static CombatStats CreateDroneStats()
        {
            return new CombatStats
            {
                ATK = 5,
                VIT = 3,
                DEF = 2,
                ENERGY = 1,
                AGI = 4,
                DEX = 2,
                LUK = 2
            };
        }

        public static Combatant CreatePlayer(string id = "robot-player")
        {
            return CombatCore.CreateCombatant(
                id,
                "Robot joueur",
                CombatTeam.Player,
                CreatePlayerStats());
        }

        public static Combatant CreateDrone(string id = "drone-enemy", AiStyle aiStyle = AiStyle.Aggressive)
        {
            return CombatCore.CreateCombatant(
                id,
                "Drone ennemi",
                CombatTeam.Enemy,
                CreateDroneStats(),
                aiStyle);
        }

        public static CombatState CreatePrototypeCombatState(AiStyle enemyStyle = AiStyle.Aggressive)
        {
            return CombatCore.CreateCombatState(new[]
            {
                CreatePlayer(),
                CreateDrone("drone-enemy", enemyStyle)
            });
        }
    }

    public sealed class SeededCombatRandom
    {
        private uint value;

        public SeededCombatRandom(uint seed)
        {
            value = seed;
        }

        public float Next01()
        {
            value = value * 1664525u + 1013904223u;
            return value / (float)uint.MaxValue;
        }
    }
}
