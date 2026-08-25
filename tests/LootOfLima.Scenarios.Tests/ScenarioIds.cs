// The ScenarioLibrary/ test folder shadows the type of the same name in this root namespace, so it
// is aliased under a name no folder claims.
using Library = LootOfLima.Scenarios.ScenarioLibrary;

namespace LootOfLima.Scenarios.Tests;

/// <summary>Every scenario id in the library, as theory data. Shared by the per-scenario theories.</summary>
public static class ScenarioIds
{
    public static TheoryData<string> All
    {
        get
        {
            TheoryData<string> data = new();
            foreach (string id in Library.Ids)
            {
                data.Add(id);
            }

            return data;
        }
    }
}
