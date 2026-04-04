using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Buckle.CodeAnalysis.Emitting;

internal static class DotnetReferenceResolver {
    internal static string ResolveNetCoreAppRefPath(string tfm, out string version) {
        var runtimeVersions = GetInstalledRuntimeVersions();

        if (runtimeVersions.Count == 0) {
            version = null;
            return null;
        }

        version = runtimeVersions
            .Where(v => v.StartsWith($"Microsoft.NETCore.App {tfm}."))
            .OrderByDescending(v => v)
            .FirstOrDefault()
            .Substring(22);

        if (version is null)
            return null;

        var dotnetRoot = GetDotnetRoot();

        if (dotnetRoot is null)
            return null;

        var refPath = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref", version, "ref", $"net{tfm}");
        return Directory.Exists(refPath) ? refPath : null;
    }

    internal static string ResolveSystemRuntimeDll(string tfm) {
        var refPath = ResolveNetCoreAppRefPath(tfm, out _);

        if (refPath is null)
            return null;

        var dllPath = Path.Combine(refPath, "System.Runtime.dll");
        return File.Exists(dllPath) ? dllPath : null;
    }

    private static List<string> GetInstalledRuntimeVersions() {
        return GetInstalledVersionsCore("--list-runtimes");
    }

    private static List<string> GetInstalledVersionsCore(string filterCommand) {
        try {
            var psi = new ProcessStartInfo("dotnet", filterCommand) {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);

            if (proc is null)
                return [];

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            return output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split('[')[0].Trim())
                .ToList();
        } catch {
            return [];
        }
    }

    private static string GetDotnetRoot() {
        var env = Environment.GetEnvironmentVariable("DOTNET_ROOT");

        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return env;

        if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");

            if (Directory.Exists(path))
                return path;
        } else {
            var fallback = "/usr/share/dotnet";

            if (Directory.Exists(fallback))
                return fallback;
        }

        return null;
    }
}
