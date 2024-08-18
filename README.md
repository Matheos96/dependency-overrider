# Dependency Overrider for .NET
_A simple CLI tool for overriding transitive .NET dependency versions._  
_Inspired by a tool by [@hoerup](https://github.com/hoerup) mentioned in [this issue](https://github.com/dotnet/runtime/issues/105028#issuecomment-2277941066)._

## What is it
This tool is mainly meant to be used in CI/CD integrations where you may want to override the versions of certain transitive dependencies **manually** before building/publishing your application. This keeps the project files under source control free from manual transitive dependency version override clutter, which also need to be maintained in the long run

## Why do I need it
Ever stumbled upon a scenario where even the latest version of a direct project dependency results in the usage of a transitive dependency with a known vulnerability, even when there would be a newer version available fixing the issue? I have, and it is annoying to sit around and wait for every package maintainer in the "dependency chain" to bump the versions of their dependencies in their NuGet packages so that I, eventually, get the transitive dependency version I desire (without vulnerabilities)...   

The only real fix for this is for you to override these transitive dependencies manually in your project files, through the use of direct project references. Maintaining these is tedious and annoying in itself. This tool will allow you to defined "version bumps" to be applied automatically on demand, most likely in a CI/CD environment, just before you publish your application. This way you can keep your source code clean from this clutter, but you still get the patched versions you define, of your transitive dependencies in your final published application.

## Usage
1. Make sure you have `dotnet` CLI installed
2. Configure `config.json` to your needs. 
    - `commonRoot` is relative to the executable. 
    - `commonRoot` is prepended to the project paths in `projectPaths`.
    - The `"reason"` and `"framework"` properties for override objects are optional.
        - The reason will be printed if given
        - The framework will be used to for filtering if present. If omitted, the override will be applied to all frameworks (for projects with multiple target framworks)
    - The `config.json` **must** be in the same directory as the executable
3. Run the tool either by building it and running the executable directly, or run the project using `dotnet run`

## Additional CLI parameters

- `--no-restore` - When present, prevents `dotnet restore` being run for each project (a restore is needed for `dotnet list` to work correctly in case project packages have changed since last restore)

## Contributions
Contributions are always welcome.  
The tool is probably filled with bugs... If you want to fix them, be my guest :)   Open a PR and I will make sure to get it merged. Alternatively, open an issue and tell me everything that is wrong with it :D