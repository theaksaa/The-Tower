using System;
using System.Collections.Generic;
using TheTower;

[Serializable]
public sealed class PersistedRunData
{
    public string SaveId;
    public string Mode;
    public long CreatedAtUtcTicks;
    public long UpdatedAtUtcTicks;
    public bool EndSceneShown;
    public RunConfig RunConfig;
    public HeroRuntimeState Hero;
    public HeroDefinition SelectedHeroDefinition;
    public List<bool> CompletedEncounters = new();
    public int SelectedEncounterIndex = -1;
    public bool UsingFallbackData;
    public bool IsDefeated;
    public string StatusMessage;
    public string PendingLearnedMoveId;
}

public sealed class RunSaveSummary
{
    public string SaveId;
    public string Mode;
    public string HeroName;
    public string HeroSpriteKey;
    public int CompletedEncounterCount;
    public int TotalEncounterCount;
    public bool IsComplete;
    public bool IsDefeated;
    public DateTime LastUpdatedUtc;
}
