using System.Text.RegularExpressions;

namespace Maliev.Aspire.ServiceDefaults.Tests.Publishing;

/// <summary>
/// Executable contracts for pull-request package authentication.
/// </summary>
public sealed class PrValidationWorkflowContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    /// <summary>
    /// Dependabot validation must build an immutable public shared-source revision without credentials.
    /// </summary>
    [Fact]
    public void PackageRestore_UsesCredentialFreePinnedSharedSource()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            ".github",
            "workflows",
            "pr-validation.yml"));

        Assert.Contains("repository: MALIEV-Co-Ltd/Maliev.MessagingContracts", source, StringComparison.Ordinal);
        Assert.Contains("ref: 53173003ba9ab72d2ea140fbe30d71ae0885f8b7", source, StringComparison.Ordinal);
        Assert.Contains("-p:UsePackageReferences=false", source, StringComparison.Ordinal);
        Assert.Contains("-p:SharedSourceRoot=${{ github.workspace }}/shared", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NUGET_PASSWORD", source, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.GITOPS_PAT", source, StringComparison.Ordinal);
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
