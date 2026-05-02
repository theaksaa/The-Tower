using System.Collections.Generic;

[System.Serializable]
public sealed class HeroRuntimeState
{
    public string HeroId;
    public string HeroName;
    public int Level;
    public int Xp;
    public int Coins;
    public int CurrentHp;
    public int BonusHealth;
    public int BonusAttack;
    public int BonusDefense;
    public int BonusMagic;
    public List<string> EquippedMoves = new();
    public List<string> EquippedItems = new();
    public List<string> InventoryItems = new();
    public HashSet<string> KnownMoves = new();
    public Dictionary<string, int> MonsterKillCounts = new();
}
