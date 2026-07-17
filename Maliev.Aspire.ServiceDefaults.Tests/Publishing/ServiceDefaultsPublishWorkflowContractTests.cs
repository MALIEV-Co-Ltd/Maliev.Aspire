using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Maliev.Aspire.ServiceDefaults.Tests.Publishing;

/// <summary>
/// Executable contracts for the ServiceDefaults package publication boundary.
/// </summary>
public sealed class ServiceDefaultsPublishWorkflowContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string WorkflowPath = Path.Combine(
        RepositoryRoot,
        ".github",
        "workflows",
        "publish-nuget.yml");
    private static readonly string VersionResolverPath = Path.Combine(
        RepositoryRoot,
        ".github",
        "scripts",
        "resolve-servicedefaults-version.sh");

    /// <summary>
    /// Push publication must react only to inputs that can change the ServiceDefaults package.
    /// </summary>
    [Fact]
    public void PushPaths_ContainOnlyAuthoritativePackageInputs()
    {
        var source = ReadWorkflow();
        var pushStart = source.IndexOf("\n  push:\n", StringComparison.Ordinal);
        var releaseStart = source.IndexOf("\n  release:\n", pushStart, StringComparison.Ordinal);
        Assert.True(pushStart >= 0 && releaseStart > pushStart);
        var pushBlock = source[pushStart..releaseStart];
        var paths = Regex.Matches(pushBlock, "^      - '([^']+)'$", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Equal(
            [
                "Maliev.Aspire.ServiceDefaults/**",
                "Directory.Build.props",
                "README.md",
                "nuget.config"
            ],
            paths);
        Assert.DoesNotContain(paths, path => path.Contains(".github/workflows", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, path => path.Contains("AppHost", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, path => path.Contains("Tests", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, path => path.EndsWith(".slnx", StringComparison.Ordinal));
    }

    /// <summary>
    /// PRs must never publish, and the workflow must delegate all version decisions to the tested resolver.
    /// </summary>
    [Fact]
    public void Triggers_ExcludePullRequestsAndUseTheVersionResolver()
    {
        var source = ReadWorkflow();

        Assert.DoesNotContain("\n  pull_request:", source, StringComparison.Ordinal);
        Assert.Contains("types: [published]", source, StringComparison.Ordinal);
        Assert.Contains("required: true", source, StringComparison.Ordinal);
        Assert.Contains("resolve-servicedefaults-version.sh", source, StringComparison.Ordinal);
        Assert.Contains("github.event.release.tag_name", source, StringComparison.Ordinal);
        Assert.Contains("github.event.inputs.version", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Publication must use pinned actions, read-only repository access, serialized runs, and scoped credentials.
    /// </summary>
    [Fact]
    public void SecurityContract_UsesPinnedActionsMinimalPermissionsConcurrencyAndScopedCredentials()
    {
        var source = ReadWorkflow();
        var actionReferences = Regex.Matches(source, "uses: ([^@\\s]+)@([^\\s#]+)")
            .Select(match => (Action: match.Groups[1].Value, Version: match.Groups[2].Value))
            .ToArray();

        Assert.NotEmpty(actionReferences);
        Assert.All(actionReferences, reference =>
            Assert.Matches("^[0-9a-f]{40}$", reference.Version));
        Assert.Contains("permissions:\n  contents: read", source, StringComparison.Ordinal);
        Assert.Contains("packages: write", source, StringComparison.Ordinal);
        Assert.Contains("concurrency:", source, StringComparison.Ordinal);
        Assert.Contains("cancel-in-progress: false", source, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(source, "secrets.GITOPS_PAT"));
        Assert.Equal(1, CountOccurrences(source, "secrets.GITHUB_TOKEN"));
        Assert.DoesNotContain("--skip-duplicate", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Package-mode restore must be explicit and must not rewrite the checked-out project file.
    /// </summary>
    [Fact]
    public void PackageMode_IsExplicitAndDoesNotMutateSource()
    {
        var workflow = ReadWorkflow();
        var project = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Maliev.Aspire.ServiceDefaults",
            "Maliev.Aspire.ServiceDefaults.csproj"));

        Assert.DoesNotContain("sed ", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-p:UsePackageReferences=true", workflow, StringComparison.Ordinal);
        Assert.Contains("<UsePackageReferences", project, StringComparison.Ordinal);
        Assert.Contains("'$(UsePackageReferences)' == 'true'", project, StringComparison.Ordinal);
        Assert.Contains("'$(UsePackageReferences)' != 'true'", project, StringComparison.Ordinal);
    }

    /// <summary>
    /// Supported push, release, and manual inputs must normalize to exact package versions.
    /// </summary>
    [Theory]
    [InlineData("push", "develop", "", "", "91", "1", "0123456789abcdef0123456789abcdef01234567", "1.0.91-alpha.1.01234567")]
    [InlineData("push", "staging", "", "", "91", "2", "abcdef0123456789abcdef0123456789abcdef01", "1.0.91-beta.2.abcdef01")]
    [InlineData("push", "main", "", "", "91", "1", "abcdef0123456789abcdef0123456789abcdef01", "1.0.91")]
    [InlineData("release", "", "release/v2.3.4", "", "91", "1", "abcdef0123456789abcdef0123456789abcdef01", "2.3.4")]
    [InlineData("workflow_dispatch", "", "", "2.3.4", "91", "1", "abcdef0123456789abcdef0123456789abcdef01", "2.3.4")]
    [InlineData("workflow_dispatch", "", "", "v2.3.4", "91", "1", "abcdef0123456789abcdef0123456789abcdef01", "2.3.4")]
    public void VersionResolver_ValidInputsReturnExactVersion(
        string eventName,
        string refName,
        string releaseTag,
        string manualVersion,
        string runNumber,
        string runAttempt,
        string commitSha,
        string expectedVersion)
    {
        var result = RunVersionResolver(
            eventName,
            refName,
            releaseTag,
            manualVersion,
            runNumber,
            runAttempt,
            commitSha);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(expectedVersion, result.StandardOutput.Trim());
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
    }

    /// <summary>
    /// Malformed tags, manual versions, unsupported branches, and stable reruns must fail closed.
    /// </summary>
    [Theory]
    [InlineData("release", "", "v2.3.4", "", "91", "1", "abcdef0123456789abcdef0123456789abcdef01")]
    [InlineData("release", "", "release/v02.3.4", "", "91", "1", "abcdef0123456789abcdef0123456789abcdef01")]
    [InlineData("workflow_dispatch", "", "", "", "91", "1", "abcdef0123456789abcdef0123456789abcdef01")]
    [InlineData("workflow_dispatch", "", "", "release/v2.3.4", "91", "1", "abcdef0123456789abcdef0123456789abcdef01")]
    [InlineData("workflow_dispatch", "", "", "2.3.4-alpha", "91", "1", "abcdef0123456789abcdef0123456789abcdef01")]
    [InlineData("push", "feature/test", "", "", "91", "1", "abcdef0123456789abcdef0123456789abcdef01")]
    [InlineData("push", "main", "", "", "91", "2", "abcdef0123456789abcdef0123456789abcdef01")]
    [InlineData("push", "develop", "", "", "91", "1", "not-a-commit-sha")]
    public void VersionResolver_InvalidInputsFailClosed(
        string eventName,
        string refName,
        string releaseTag,
        string manualVersion,
        string runNumber,
        string runAttempt,
        string commitSha)
    {
        var result = RunVersionResolver(
            eventName,
            refName,
            releaseTag,
            manualVersion,
            runNumber,
            runAttempt,
            commitSha);

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.StandardError));
    }

    /// <summary>
    /// A development rerun must produce a different version while retaining commit traceability.
    /// </summary>
    [Fact]
    public void VersionResolver_DevelopmentRerunIsUniqueAndTraceable()
    {
        const string sha = "abcdef0123456789abcdef0123456789abcdef01";
        var first = RunVersionResolver("push", "develop", "", "", "91", "1", sha);
        var rerun = RunVersionResolver("push", "develop", "", "", "91", "2", sha);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, rerun.ExitCode);
        Assert.NotEqual(first.StandardOutput, rerun.StandardOutput);
        Assert.Contains("abcdef01", first.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("abcdef01", rerun.StandardOutput, StringComparison.Ordinal);
    }

    private static ProcessResult RunVersionResolver(
        string eventName,
        string refName,
        string releaseTag,
        string manualVersion,
        string runNumber,
        string runAttempt,
        string commitSha)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FindBash(),
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(VersionResolverPath);
        startInfo.ArgumentList.Add(eventName);
        startInfo.ArgumentList.Add(refName);
        startInfo.ArgumentList.Add(releaseTag);
        startInfo.ArgumentList.Add(manualVersion);
        startInfo.ArgumentList.Add(runNumber);
        startInfo.ArgumentList.Add(runAttempt);
        startInfo.ArgumentList.Add(commitSha);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start the version resolver.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string ReadWorkflow() =>
        File.ReadAllText(WorkflowPath).ReplaceLineEndings("\n");

    private static string FindBash()
    {
        const string gitBash = @"C:\Program Files\Git\bin\bash.exe";
        return OperatingSystem.IsWindows() && File.Exists(gitBash) ? gitBash : "bash";
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startDirectory in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(startDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Maliev.Aspire.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate the Maliev.Aspire repository root.");
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
