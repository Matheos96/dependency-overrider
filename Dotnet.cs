using System.Diagnostics;

namespace dependency_overrider;

internal static class Dotnet
{
	internal static bool IsInstalled()
	{
		try { RunCmd(); }
		catch { return false; }
		return true;
	}

	internal static void Restore(string projectPath) => RunCmd($"restore {projectPath}");
	internal static string List(string projectPath) => RunCmd($"list {projectPath} package --include-transitive --format json");
	internal static void AddPackage(string projectPath, string packageId, string newVersion) => RunCmd($"add {projectPath} package {packageId} -v {newVersion}");

	private static string RunCmd(string arguments = "")
	{
		var process = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = arguments,
				RedirectStandardOutput = true // Don't output to console
			}
		};
		process.Start();
		var stdOutput = process.StandardOutput.ReadToEnd();
		process.WaitForExit();
		return stdOutput;
	}
}
