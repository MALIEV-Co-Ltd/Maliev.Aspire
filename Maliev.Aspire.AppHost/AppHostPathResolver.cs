namespace Maliev.Aspire.AppHost;

internal static class AppHostPathResolver
{
    public static string ResolveRequiredDirectoryPath(string sourcePath)
    {
        foreach (var candidate in EnumerateCandidates(sourcePath))
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"Unable to locate Docker container file source directory '{sourcePath}'.");
    }

    public static string ResolveRequiredFilePath(string sourcePath)
    {
        foreach (var candidate in EnumerateCandidates(sourcePath))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Unable to locate required AppHost file '{sourcePath}'.", sourcePath);
    }

    private static IEnumerable<string> EnumerateCandidates(string sourcePath)
    {
        if (Path.IsPathFullyQualified(sourcePath))
        {
            yield return Path.GetFullPath(sourcePath);
            yield break;
        }

        foreach (var startDirectory in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(startDirectory);
            while (directory is not null)
            {
                yield return Path.GetFullPath(Path.Combine(directory.FullName, sourcePath));
                yield return Path.GetFullPath(Path.Combine(directory.FullName, "Maliev.Aspire.AppHost", sourcePath));
                yield return Path.GetFullPath(Path.Combine(directory.FullName, "Maliev.Aspire", "Maliev.Aspire.AppHost", sourcePath));

                directory = directory.Parent;
            }
        }
    }
}
