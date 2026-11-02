using System.Reflection;

namespace CleanSlices.Cli;

internal static class CliInfo
{
    public static string Version { get; } =
        typeof(CliInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+')[0]
        ?? "0.0.0";
}
