using System;
using System.Collections;
using Newtonsoft.Json;
using TheTower;
using UnityEngine;
using UnityEngine.Networking;

public static class RunDataService
{
    public static IEnumerator LoadRunConfig(string baseUrl, bool useLocalFallbackIfApiFails, Action<RunConfig, bool> onLoaded)
    {
        RunConfig runConfig = null;
        var usingFallbackData = false;

        using (var request = UnityWebRequest.Get($"{baseUrl}/run/config"))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    runConfig = JsonConvert.DeserializeObject<RunConfig>(request.downloadHandler.text);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Failed to deserialize run config: {exception}");
                }
            }
        }

        if (runConfig == null && useLocalFallbackIfApiFails)
        {
            usingFallbackData = true;
            runConfig = BuildFallbackRunConfig();
        }

        onLoaded?.Invoke(runConfig, usingFallbackData);
    }

    public static RunConfig BuildFallbackRunConfig()
    {
        const string fallbackJson = @"
{
  ""runId"": ""offline-fallback"",
  ""encounters"": [
    {
      ""id"": ""training_dummy"",
      ""name"": ""Training Dummy"",
      ""description"": ""A stand-in opponent used when the local API is unavailable."",
      ""stats"": { ""health"": 60, ""attack"": 10, ""defense"": 6, ""magic"": 4 },
      ""moves"": [""rusty_blade"", ""dirty_kick""],
      ""learnableMoves"": [""rusty_blade"", ""dirty_kick""],
      ""xpReward"": 60,
      ""spriteKey"": ""training_dummy""
    },
    {
      ""id"": ""emberscale"",
      ""name"": ""Emberscale"",
      ""description"": ""A young drake with a nasty flame breath."",
      ""stats"": { ""health"": 110, ""attack"": 16, ""defense"": 12, ""magic"": 16 },
      ""moves"": [""flame_breath"", ""claw_swipe"", ""dragon_scales""],
      ""learnableMoves"": [""flame_breath"", ""dragon_scales""],
      ""xpReward"": 140,
      ""spriteKey"": ""emberscale""
    }
  ],
  ""heroDefaults"": {
    ""baseStats"": { ""health"": 100, ""attack"": 18, ""defense"": 10, ""magic"": 12 },
    ""statsPerLevel"": { ""health"": 18, ""attack"": 4, ""defense"": 3, ""magic"": 3 },
    ""moves"": [""slash"", ""shield_up"", ""battle_cry"", ""second_wind""]
  },
  ""xpTable"": [0, 100, 250, 450, 700],
  ""moveRegistry"": {
    ""slash"": { ""id"": ""slash"", ""name"": ""Slash"", ""description"": ""Moderate physical damage."", ""type"": ""physical"", ""effect"": ""damage"", ""target"": ""opponent"", ""basePower"": 15, ""statMultiplier"": 1.0, ""statModifier"": null, ""hpCost"": null },
    ""shield_up"": { ""id"": ""shield_up"", ""name"": ""Shield Up"", ""description"": ""Raises the user's Defense for two turns."", ""type"": ""status"", ""effect"": ""stat_modifier"", ""target"": ""self"", ""basePower"": 0, ""statMultiplier"": 0, ""statModifier"": { ""stat"": ""defense"", ""value"": 6, ""durationTurns"": 2 }, ""hpCost"": null },
    ""battle_cry"": { ""id"": ""battle_cry"", ""name"": ""Battle Cry"", ""description"": ""Raises the user's Attack for two turns."", ""type"": ""status"", ""effect"": ""stat_modifier"", ""target"": ""self"", ""basePower"": 0, ""statMultiplier"": 0, ""statModifier"": { ""stat"": ""attack"", ""value"": 6, ""durationTurns"": 2 }, ""hpCost"": null },
    ""second_wind"": { ""id"": ""second_wind"", ""name"": ""Second Wind"", ""description"": ""Moderate heal that scales with Magic."", ""type"": ""magic"", ""effect"": ""heal"", ""target"": ""self"", ""basePower"": 18, ""statMultiplier"": 0.8, ""statModifier"": null, ""hpCost"": null },
    ""rusty_blade"": { ""id"": ""rusty_blade"", ""name"": ""Rusty Blade"", ""description"": ""Moderate physical damage."", ""type"": ""physical"", ""effect"": ""damage"", ""target"": ""opponent"", ""basePower"": 14, ""statMultiplier"": 1, ""statModifier"": null, ""hpCost"": null },
    ""dirty_kick"": { ""id"": ""dirty_kick"", ""name"": ""Dirty Kick"", ""description"": ""Light damage and lowers the target's Defense."", ""type"": ""physical"", ""effect"": ""damage_and_stat_modifier"", ""target"": ""opponent"", ""basePower"": 8, ""statMultiplier"": 0.7, ""statModifier"": { ""stat"": ""defense"", ""value"": -5, ""durationTurns"": 2 }, ""hpCost"": null },
    ""flame_breath"": { ""id"": ""flame_breath"", ""name"": ""Flame Breath"", ""description"": ""Heavy magic damage."", ""type"": ""magic"", ""effect"": ""damage"", ""target"": ""opponent"", ""basePower"": 30, ""statMultiplier"": 1.3, ""statModifier"": null, ""hpCost"": null },
    ""claw_swipe"": { ""id"": ""claw_swipe"", ""name"": ""Claw Swipe"", ""description"": ""Moderate physical damage."", ""type"": ""physical"", ""effect"": ""damage"", ""target"": ""opponent"", ""basePower"": 18, ""statMultiplier"": 1, ""statModifier"": null, ""hpCost"": null },
    ""dragon_scales"": { ""id"": ""dragon_scales"", ""name"": ""Dragon Scales"", ""description"": ""Raises the user's Defense for two turns."", ""type"": ""status"", ""effect"": ""stat_modifier"", ""target"": ""self"", ""basePower"": 0, ""statMultiplier"": 0, ""statModifier"": { ""stat"": ""defense"", ""value"": 8, ""durationTurns"": 2 }, ""hpCost"": null }
  }
}";

        return JsonConvert.DeserializeObject<RunConfig>(fallbackJson);
    }
}
