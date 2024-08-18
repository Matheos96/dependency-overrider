using System.Text.Json;
using dependency_overrider;

// Ensure dotnet CLI is installed
if (!Dotnet.IsInstalled()) 
{
	Console.Error.WriteLine($"dotnet CLI is not installed or not in PATH. Please install it or configure your PATH.");
	return;
}

var assemblyDir = AppContext.BaseDirectory;
var configFilePath = Path.Combine(assemblyDir, "config.json");
var runRestore = !args.Contains("--no-restore");
Config? config;
try
{
	config = Serialization.Deserialize<Config>(File.ReadAllText(configFilePath));
}
catch (FileNotFoundException)
{
	Console.Error.WriteLine($"{configFilePath} was not found!");
	return;
}
catch (JsonException)
{
	Console.Error.WriteLine($"{configFilePath} is not a valid JSON file!");
	return;
}
if (config is null) 
{
	Console.Error.WriteLine($"There was an issue reading and/or parsing {configFilePath}!");
	return;
}

// Only consider valid overrides and create dictionary for easy lookup
var overridesDict = config.Overrides.Where(o => o.IsValid).ToDictionary(o => o.PackageId, o => o);
if (overridesDict is null || overridesDict.Count == 0) 
{
	Console.WriteLine("Config contains no overrides. Nothing to do. Exiting...");
	return;
}

// Loop through all projects to handle
var commonRoot = Path.Combine(assemblyDir, config.CommonRoot);
foreach (var path in config.ProjectPaths) 
{
	var targetProjectPath = Path.Combine(commonRoot, path);
	if (runRestore) Dotnet.Restore(targetProjectPath); // dotnet restore

	// dotnet list ... package
	var listJsonStr = Dotnet.List(targetProjectPath);

	// Parse dotnet list standard output JSON to JsonDocument
	using var dotnetList = JsonDocument.Parse(listJsonStr);
	var dotnetListElement = dotnetList.RootElement;
	if (!dotnetListElement.TryGetProperty("projects", out var listProjects)) continue; // Skip if somehow there are no projects...
	if (dotnetListElement.TryGetProperty("problems", out var problems)) 
	{
		foreach (var problem in problems.EnumerateArray())
		{
			Console.Error.WriteLine($"Problem occurred: {problem.GetProperty("text").GetString()}.\nSkipping...");
			continue;
		}
	}

	foreach (var project in listProjects.EnumerateArray())
	{
		if (!project.TryGetProperty("path", out var projectPathProp) || projectPathProp.GetString() is not { } projectPath) continue;
		if (!project.TryGetProperty("frameworks", out var frameworksProp)) continue;
		foreach (var framework in frameworksProp.EnumerateArray())
		{
			if (!framework.TryGetProperty("transitivePackages", out var transPackProp)) continue;
			foreach (var transPackage in transPackProp.EnumerateArray())
			{
				if (!transPackage.TryGetProperty("id", out var packageIdProp) || packageIdProp.GetString() is not { } packageId) continue;
				if (!transPackage.TryGetProperty("resolvedVersion", out var resVerProp) || resVerProp.GetString() is not { } resolvedVersion) continue;
				if (!overridesDict.TryGetValue(packageId, out var overrideObj) || !overrideObj.OldVersions.Contains(resolvedVersion)) continue;

				// dotnet add package
				Dotnet.AddPackage(projectPath, packageId, overrideObj.NewVersion);
				Console.WriteLine($"Added a direct dependency to {projectPath} for package {packageId}:{overrideObj.NewVersion} overriding version {resolvedVersion}");
			}
		}
	}
}
