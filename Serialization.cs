using System.Text.Json;

namespace dependency_overrider;

internal static class Serialization 
{
	private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

	internal static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _options);
}

// DTOs

internal record Config(string CommonRoot, string[] ProjectPaths, Override[] Overrides);
internal record Override(string PackageId, HashSet<string> OldVersions, string NewVersion, string? Reason)
{
	internal bool IsValid => !string.IsNullOrWhiteSpace(PackageId) && OldVersions.Count > 0 && !string.IsNullOrWhiteSpace(NewVersion);
}

internal record PackageList(Problem[]? Problems, Project[] Projects)
{
	internal bool HasProblems => Problems?.Length > 0;
}
internal record Problem(string Project, string Level, string Text);
internal record Project(string Path, TargetFramework[]? Frameworks) 
{
	internal bool IsValid => !string.IsNullOrWhiteSpace(Path) && Frameworks?.Length > 0;
}
internal record TargetFramework(string Framework, TransitivePackage[]? TransitivePackages) 
{
	internal bool HasTransitivePackages => TransitivePackages?.Length > 0;
}
internal record TransitivePackage(string Id, string ResolvedVersion) 
{
	internal bool IsValid => !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(ResolvedVersion);
}