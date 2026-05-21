using System.Reflection;
using System.Text.RegularExpressions;

namespace Maliev.Aspire.Tests.E2E;

/// <summary>
/// Traceability checks for the production-gate E2E story catalog.
/// </summary>
public sealed partial class E2EStoryCatalogTraceabilityTests
{
    private static readonly string SpecsDirectory = Path.Combine(
        FindRepositoryRoot(),
        "Maliev.Aspire.Tests",
        "specs");

    /// <summary>
    /// Verifies every story id in the catalog has a row in the execution results file.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    public void E2EStoryRunResults_CoverEveryCatalogStoryId()
    {
        var storyIds = ExtractStoryIds(Path.Combine(SpecsDirectory, "E2E_USER_JOURNEY_STORIES.md"));
        var resultIds = ExtractStoryIds(Path.Combine(SpecsDirectory, "E2E_USER_JOURNEY_RUN_RESULTS.md"));

        var missing = storyIds.Except(resultIds, StringComparer.Ordinal).ToArray();
        var extra = resultIds.Except(storyIds, StringComparer.Ordinal).ToArray();

        Assert.True(
            missing.Length == 0,
            $"Missing run-result rows for story ids: {string.Join(", ", missing)}");
        Assert.True(
            extra.Length == 0,
            $"Run-result file contains ids that are not in the story catalog: {string.Join(", ", extra)}");
        Assert.Equal(102, storyIds.Count);
    }

    /// <summary>
    /// Verifies the automated browser tests advertise the story ids they cover.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    public void BrowserE2ETests_DeclareStoryCoverage()
    {
        var automatedStoryIds = typeof(BrowserJourneyGateTests)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(method => method.GetCustomAttributesData())
            .Where(attribute => attribute.AttributeType == typeof(TraitAttribute))
            .Select(attribute => new
            {
                Name = attribute.ConstructorArguments.ElementAtOrDefault(0).Value as string,
                Value = attribute.ConstructorArguments.ElementAtOrDefault(1).Value as string
            })
            .Where(trait => string.Equals(trait.Name, "Stories", StringComparison.Ordinal))
            .SelectMany(trait => (trait.Value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("WEB-001", automatedStoryIds);
        Assert.Contains("WEB-014", automatedStoryIds);
        Assert.Contains("QUOTE-024", automatedStoryIds);
        Assert.Contains("INT-001", automatedStoryIds);
    }

    private static IReadOnlySet<string> ExtractStoryIds(string path)
    {
        var text = File.ReadAllText(path);
        return StoryIdRegex()
            .Matches(text)
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startDirectory in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(startDirectory);
            while (directory is not null)
            {
                var candidates = new[]
                {
                    directory.FullName,
                    Path.Combine(directory.FullName, "Maliev.Aspire")
                };

                foreach (var candidate in candidates)
                {
                    if (File.Exists(Path.Combine(candidate, "Maliev.Aspire.slnx")))
                    {
                        return candidate;
                    }
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate Maliev.Aspire repository root.");
    }

    [GeneratedRegex("[A-Z]+-[0-9]{3}", RegexOptions.CultureInvariant)]
    private static partial Regex StoryIdRegex();
}
