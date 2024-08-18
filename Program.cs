using System.Text.Json;
using dependency_overrider;

// Ensure dotnet CLI is installed
if (!Dotnet.IsInstalled()) 
{
	Console.Error.WriteLine($"dotnet CLI is not installed or not in PATH. Please install it or configure your PATH.");
	return 1;
}

var assemblyDir = AppContext.BaseDirectory;
var configFilePath = Path.Combine(assemblyDir, "config.json");
Config? config;
try
{
	config = Serialization.Deserialize<Config>(File.ReadAllText(configFilePath));
}
catch (FileNotFoundException)
{
	Console.Error.WriteLine($"{configFilePath} was not found!");
	return 1;
}
catch (JsonException)
{
	Console.Error.WriteLine($"{configFilePath} is not a valid JSON file!");
	return 1;
}
if (config is null) 
{
	Console.Error.WriteLine($"There was an issue reading and/or parsing {configFilePath}!");
	return 1;
}

// Only consider valid overrides and create dictionary for easy lookup
var overridesDict = config.Overrides.Where(o => o.IsValid).ToDictionary(o => o.PackageId, o => o);
if (overridesDict is null || overridesDict.Count == 0) 
{
	Console.WriteLine("Config contains no overrides. Nothing to do. Exiting...");
	return 0; // Successful run, we just did not need to do anything
}

// Loop through all projects to handle
var runRestore = !args.Contains("--no-restore");
var commonRoot = Path.Combine(assemblyDir, config.CommonRoot);
foreach (var path in config.ProjectPaths) 
{
	var targetProjectPath = Path.Combine(commonRoot, path);
	if (runRestore) Dotnet.Restore(targetProjectPath); // dotnet restore

	// dotnet list ... package
	var listJsonStr = Dotnet.List(targetProjectPath);
	var packageList = Serialization.Deserialize<PackageList>(listJsonStr);
	if (packageList is null) 
	{
		Console.Error.WriteLine($"Could not deserialize dotnet list output.. Skipping project {targetProjectPath}");
		continue;
	}
	
	// If dotnet list returned problems, print them and skip this project
	if (packageList.HasProblems)
	{
		Console.Error.WriteLine($"Encountered problems with dotnet list for {targetProjectPath}:");
		foreach (var problem in packageList.Problems!) Console.Error.WriteLine($"    - {problem.Text}");
		Console.Error.WriteLine("Skipping...");
		continue;
	}

	// Loop over all projects (if the target was a solution, there could be multiple)
	foreach (var project in packageList.Projects.Where(p => p.IsValid))
	{
		// Loop over all frameworks (Framworks is not null has project is valid)
		foreach (var framework in project.Frameworks!.Where(f => f.HasTransitivePackages))
		{
			// Loop over all transitive packages (TransitivePackages is not null as framework is valid)
			foreach (var package in framework.TransitivePackages!.Where(p => p.IsValid))
			{
				// If there is no matching entry in the overrides dict, there is nothing to do
				if (!overridesDict.TryGetValue(package.Id, out var @override) 
					|| !@override.OldVersions.Contains(package.ResolvedVersion)
					|| (@override.Framework is not null && @override.Framework != framework.Framework)) continue; // consider framework if present

				// dotnet add package
				Dotnet.AddPackage(project.Path, package.Id, @override.NewVersion);
				Console.WriteLine($"Added a direct dependency to {project.Path} for package "
				+ $"{package.Id}:{@override.NewVersion} overriding version {package.ResolvedVersion} "
				+ (!string.IsNullOrWhiteSpace(@override.Reason) ? $"to remedy {@override.Reason}" : string.Empty));
			}
		}
	}
}

return 0;