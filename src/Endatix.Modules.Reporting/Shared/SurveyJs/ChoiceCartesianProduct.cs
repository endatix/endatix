namespace Endatix.Modules.Reporting.Shared.SurveyJs;

/// <summary>
/// Iterative Cartesian product over choice value levels (nested-loop export paths).
/// </summary>
internal static class ChoiceCartesianProduct
{
    internal static long EstimateCombinationCount(IReadOnlyList<int> levelSizes)
    {
        if (levelSizes.Count == 0)
        {
            return 1;
        }

        long count = 1;
        foreach (var size in levelSizes)
        {
            if (size == 0)
            {
                return 0;
            }

            count *= size;
        }

        return count;
    }

    internal static IEnumerable<string[]> Enumerate(IReadOnlyList<IReadOnlyList<string>> levels)
    {
        if (levels.Count == 0)
        {
            yield return [];
            yield break;
        }

        var levelCount = levels.Count;
        var indices = new int[levelCount];

        while (true)
        {
            var combination = new string[levelCount];
            for (var i = 0; i < levelCount; i++)
            {
                combination[i] = levels[i][indices[i]];
            }

            yield return combination;

            var level = levelCount - 1;
            while (level >= 0)
            {
                indices[level]++;
                if (indices[level] < levels[level].Count)
                {
                    break;
                }

                indices[level] = 0;
                level--;
            }

            if (level < 0)
            {
                break;
            }
        }
    }
}
