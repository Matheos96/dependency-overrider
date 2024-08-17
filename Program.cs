using System.Diagnostics;
using System.Text.Json;

// Ensure dotnet CLI is installed
if (!DotnetIsInstalled()) 
{
    Console.Error.WriteLine($"dotnet CLI is not installed or not in PATH. Please install it or configure your PATH.");
    return;
}

var assemblyDir = AppContext.BaseDirectory;
var configFilePath = Path.Combine(assemblyDir, "config.json");
var runRestore = !args.Contains("--no-restore");
JsonDocument? config = null;
try 
{
    config = JsonDocument.Parse(File.ReadAllText(configFilePath));
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

var jsonRoot = config.RootElement;
if (!jsonRoot.TryGetProperty("overrides", out var overridesElement) 
    || !jsonRoot.TryGetProperty("commonRoot", out var commonRootElement)
    || !jsonRoot.TryGetProperty("projectPaths", out var projectsElement))
{
    Console.Error.WriteLine("Invalid config file. Exiting...");
    return;
}

var commonRoot = commonRootElement.GetString();

// Create an array out of project paths defined in the config. The package updates should be apply to each of these projects
var targetProjectPaths = projectsElement.EnumerateArray()
    .Select(x => x.GetString())
    .Where(x => !string.IsNullOrEmpty(x))
    .Select(x => Path.Combine(assemblyDir, commonRoot ?? "", x!)).ToArray();

// Create a dictionary of Override objects based on the overrides list in the config
var overrides = overridesElement.EnumerateArray()
    .Select(o => o.ToOverride())
    .Where(o => !o.IsEmpty).ToDictionary(o => o.PackageId, o => o);

config.Dispose(); //Manually dispose the config JsonDocument already as it is no longer needed

if (overrides is null || overrides.Count == 0) {
    Console.WriteLine("Config contains no overrides. Nothing to do. Exiting...");
    return;
}

// Loop through all projects to handle
foreach (var targetProjectPath in targetProjectPaths) 
{
    if (runRestore) DotnetRestore(targetProjectPath); // dotnet restore

    // dotnet list ... package
    var listJsonStr = DotnetList(targetProjectPath);

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
                if (!overrides.TryGetValue(packageId, out var overrideObj) || !overrideObj.OldVersions.Contains(resolvedVersion)) continue;

                // dotnet add package
                DotnetAddPackage(projectPath, packageId, overrideObj.NewVersion);
                Console.WriteLine($"Added a direct dependency to {projectPath} for package {packageId}:{overrideObj.NewVersion} overriding version {resolvedVersion}");
            }
        }
    }
}

static bool DotnetIsInstalled()
{
    try {
        var dotnet = new Process { StartInfo = new ProcessStartInfo { FileName = "dotnet.exe", RedirectStandardOutput = true } };
        dotnet.Start();
        dotnet.WaitForExit();
        return true;
    }
    catch {
        return false;
    }
}

static void DotnetRestore(string projectPath) 
{
    // Make sure that project is restored before running list to ensure project.assets.json and project file are in sync
    var dotnetRestoreProcess = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet.exe",
            Arguments = $"restore {projectPath}",
            RedirectStandardOutput = true, // Don't output to console
        }
    };
    dotnetRestoreProcess.Start();
    dotnetRestoreProcess.WaitForExit();
}

static string DotnetList(string projectPath)
{
    // Run dotnet list to get all the needed info for the current project
    var dotnetListProcess = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet.exe",
            Arguments = $"list {projectPath} package --include-transitive --format json",
            RedirectStandardOutput = true,
        }
    };
    dotnetListProcess.Start();
    var listJsonStr = dotnetListProcess.StandardOutput.ReadToEnd();
    dotnetListProcess.WaitForExit();
    return listJsonStr;
}

static void DotnetAddPackage(string projectPath, string packageId, string newVersion)
{
    var updateProcess = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet.exe",
            Arguments = $"add {projectPath} package {packageId} -v {newVersion}",
            RedirectStandardOutput = true // Don't output to console
        }
    };
    updateProcess.Start();
    updateProcess.WaitForExit();
}


internal record Override {
    public required string PackageId { get; init; }
    public required HashSet<string> OldVersions { get; init; }
    public required string NewVersion { get; init; }
    internal bool IsEmpty => this == Empty;
    internal static readonly Override Empty = new() { PackageId = "", OldVersions = [], NewVersion = "" };
}

internal static class Extensions {
    internal static Override ToOverride(this JsonElement jsonElement) {
        if (!jsonElement.TryGetProperty("packageId", out var packageIdProp) || packageIdProp.GetString() is not string packageId || string.IsNullOrEmpty(packageId)
        || !jsonElement.TryGetProperty("oldVersions", out var oldVersionsProp) || oldVersionsProp.Deserialize<HashSet<string>?>() is not { } oldVersions
        || !jsonElement.TryGetProperty("newVersion", out var newVersionProp) || newVersionProp.GetString() is not string newVersion || string.IsNullOrEmpty(newVersion))
        {
            return Override.Empty;
        }

        return new Override
        {
            PackageId = packageId,
            OldVersions = oldVersions,
            NewVersion = newVersion
        };
    }
}