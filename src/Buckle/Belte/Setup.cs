using System;
using System.Configuration;
using System.IO;
using System.Reflection;

namespace Belte;

public static class Setup {
    /// <summary>
    /// Sets up the program configuration by loading any settings in the App.config.
    /// Currently only adds a specified probing path for assemblies.
    /// </summary>
    public static void SetupConfiguration() {
        var currentDomain = AppDomain.CurrentDomain;
        var executingPath = AppDomain.CurrentDomain.BaseDirectory;
        var appProbingPath = ConfigurationManager.AppSettings["probing"];

        // In case the App.config cannot be found automatically
        if (appProbingPath == null) {
            var configMap = new ExeConfigurationFileMap();
            configMap.ExeConfigFilename = Path.Combine(executingPath, "App.config");
            var config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
            appProbingPath = config.AppSettings.Settings["probing"].Value;
        }

        Assembly assemblyResolveEventHandler(object sender, ResolveEventArgs args) {
            //This handler is called only when the common language runtime tries to bind to the assembly and fails.
            var assemblyName = args.Name.Split(',')[0] + ".dll";
            var assemblyPath = Path.Combine(executingPath, appProbingPath, assemblyName);
            var assembly = Assembly.LoadFrom(assemblyPath);

            return assembly;
        }

        currentDomain.AssemblyResolve += new ResolveEventHandler(assemblyResolveEventHandler);
    }
}
