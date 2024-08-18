using System.Text.Json;

namespace dependency_overrider;

internal static class Serialization 
{
	private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

	internal static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _options);
}

// DTOs

internal record Config(string CommonRoot, string[] ProjectPaths, Override[] Overrides);
internal record Override(string PackageId, HashSet<string> OldVersions, string NewVersion)
{
	internal bool IsValid => !string.IsNullOrWhiteSpace(PackageId) && OldVersions.Count > 0 && !string.IsNullOrWhiteSpace(NewVersion);
}