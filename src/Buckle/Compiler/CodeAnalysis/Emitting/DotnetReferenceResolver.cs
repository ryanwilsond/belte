using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Buckle.CodeAnalysis.Emitting;

internal static class DotnetReferenceResolver {
    internal static string ResolveReferenceAssemblyPath(string tfm) {
        var sdkVersions = GetInstalledSdkVersions();

        if (sdkVersions.Count == 0)
            return null;

        var matchingVersion = sdkVersions
            .Where(v => v.StartsWith(tfm + "."))
            .OrderByDescending(v => v)
            .FirstOrDefault();

        if (matchingVersion is null)
            return null;

        var dotnetRoot = GetDotnetRoot();

        if (dotnetRoot is null)
            return null;

        // var refPath = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref", matchingVersion, "ref", tfm);
        var refPath = Path.Combine(dotnetRoot, "sdk", matchingVersion, "ref");
        return Directory.Exists(refPath) ? refPath : null;
    }

    internal static string ResolveSystemRuntimeDll(string tfm) {
        var refPath = ResolveReferenceAssemblyPath(tfm);

        if (refPath is null)
            return null;

        // var dllPath = Path.Combine(refPath, "System.Runtime.dll");
        var dllPath = Path.Combine(refPath, "netstandard.dll");
        return File.Exists(dllPath) ? dllPath : null;
    }

    private static List<string> GetInstalledSdkVersions() {
        try {
            var psi = new ProcessStartInfo("dotnet", "--list-sdks") {
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
