using System;
using System.Collections.Generic;

namespace TheTower
{
    [Serializable]
    public class Stats
    {
        public int health;
        public int attack;
        public int defense;
        public int magic;

        public Stats Clone()
        {
            return new Stats
            {
                health = health,
                attack = attack,
                defense = defense,
                magic = magic
            };
        }
    }

    [Serializable]
    public class StatModifier
    {
        public string stat;
        public int value;
        public int durationTurns;
    }

    [Serializable]
    public class Move
    {
        public string id;
        public string name;
        public string description;
        public string spriteKey;
        public string type;
        public string effect;
        public string target;
        public int basePower;
        public float statMultiplier;
        public StatModifier statModifier;
        public int? hpCost;
    }

    [Serializable]
    public class Monster
    {
        public string id;
        public string name;
        public string description;
        public Stats stats;
        public List<string> moves;
        public List<string> learnableMoves;
        public int xpReward;
        public int coinReward;
        public string spriteKey;
    }

    [Serializable]
    public class CoinRewardScaling
    {
        public float multiplierPerKill;
        public int minimumReward;
    }

    [Serializable]
    public class XpRewardScaling
    {
        public float multiplierPerKill;
        public int minimumReward;
    }

    [Serializable]
    public class HeroDefaults
    {
        public string id;
        public string name;
        public string description;
        public string spriteKey;
        public Stats baseStats;
        public Stats statsPerLevel;
        public List<string> moves;
    }

    [Serializable]
    public class HeroDefinition
    {
        public string id;
        public string name;
        public string description;
        public string spriteKey;
        public Stats baseStats;
        public Stats statsPerLevel;
        public List<string> moves;
        public string portraitKey;
    }

    [Serializable]
    public class RunConfig
    {
        public string runId;
        public List<Monster> encounters;
        public List<HeroDefinition> heroes;
        public HeroDefaults heroDefaults;
        public List<int> xpTable;
        public CoinRewardScaling coinRewardScaling;
        public XpRewardScaling xpRewardScaling;
        public Dictionary<string, Move> moveRegistry;
    }

    [Serializable]
    public class BattleState
    {
        public string monsterId;
        public int monsterCurrentHp;
        public int heroCurrentHp;
        public int heroMaxHp;
        public Stats heroStats;
        public int turnNumber;
        public string heroLastMoveId;
        public List<string> monsterMoveHistory;
    }

    [Serializable]
    public class MonsterMoveResponse
    {
        public string moveId;
        public Move move;
    }
}
