using System.Text.RegularExpressions;

namespace Maliev.Aspire.ServiceDefaults.Tests.Publishing;

/// <summary>
/// Executable contracts for pull-request package authentication.
/// </summary>
public sealed class PrValidationWorkflowContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    /// <summary>
    /// Dependabot cannot access repository secrets, so package restore must fall back to the
    /// pull-request job token with explicit package-read permission.
    /// </summary>
    [Fact]
    public void PackageRestore_UsesDependabotSafeTokenFallback()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            ".github",
            "workflows",
            "pr-validation.yml"));

        Assert.Contains("permissions:\n  contents: read\n  packages: read", source, StringComparison.Ordinal);
        Assert.Equal(3, CountOccurrences(source, "NUGET_PASSWORD: ${{ secrets.GITOPS_PAT || github.token }}"));
        Assert.DoesNotContain("NUGET_PASSWORD: ${{ secrets.GITOPS_PAT }}", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// First-party actions upgraded by Dependabot must remain pinned to immutable commits.
    /// </summary>
    [Fact]
    public void UpgradedFirstPartyActions_ArePinnedAcrossWorkflows()
    {
        var workflowDirectory = Path.Combine(RepositoryRoot, ".github", "workflows");
        var references = Directory.EnumerateFiles(workflowDirectory, "*.yml")
            .SelectMany(path => Regex.Matches(
                File.ReadAllText(path),
                "uses: actions/(checkout|setup-dotnet|upload-artifact)@([^\\s#]+)"))
            .Select(match => match.Groups[2].Value)
            .ToArray();

        Assert.NotEmpty(references);
        Assert.All(references, reference => Assert.Matches("^[0-9a-f]{40}$", reference));
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var offset = 0;

        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Maliev.Aspire.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Maliev.Aspire repository root.");
    }
}
