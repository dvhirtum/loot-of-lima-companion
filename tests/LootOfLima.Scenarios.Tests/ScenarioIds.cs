namespace LootOfLima.Scenarios.Tests;

/// <summary>Every scenario id in the library, as theory data. Shared by the per-scenario theories.</summary>
public static class ScenarioIds
{
    public static TheoryData<string> All
    {
        get
        {
            TheoryData<string> data = new();
            foreach (string id in ScenarioLibrary.Ids)
            {
                data.Add(id);
            }

            return data;
        }
    }
}
