using System;
using System.Configuration;
using System.IO;
using System.Reflection;

namespace Belte;

/// <summary>
/// Prepares the program for execution by setting up events and collecting info
/// from the App.Config.
/// </summary>
public static class Setup {
    /// <summary>
    /// Prepares the program for execution finding and collecting information
    /// from the App.Config, add then setting up events.
    /// </summary>
    /// <returns>Information from the App.config.</returns>
    public static AppSettings SetupProgram() {
        var executingPath = AppDomain.CurrentDomain.BaseDirectory;
        var defaultAppConfig = ConfigurationManager.AppSettings;
        KeyValueConfigurationCollection manualAppConfig = null;
        // Used as a test key to check if the App.config was found
        var testKey = defaultAppConfig["probing"];

        if (testKey == null) {
            var configMap = new ExeConfigurationFileMap();
            configMap.ExeConfigFilename = Path.Combine(executingPath, "App.config");
            var config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
            manualAppConfig = config.AppSettings.Settings;
        }

        var appSettings = new AppSettings();
        appSettings.executingPath = executingPath;
        appSettings.probingPath = GetAppSetting("probing");
        appSettings.resourcesPath = Path.Combine(appSettings.executingPath, GetAppSetting("resources"));

        SetupConfiguration(appSettings);

        return appSettings;

        string GetAppSetting(string key) {
            return defaultAppConfig[key] ?? manualAppConfig?[key]?.Value;
        }
    }

    private static void SetupConfiguration(AppSettings appSettings) {
        var currentDomain = AppDomain.CurrentDomain;
        currentDomain.AssemblyResolve += new ResolveEventHandler(assemblyResolveEventHandler);

        Assembly assemblyResolveEventHandler(object sender, ResolveEventArgs args) {
            // This handler is called only when the common language runtime tries to bind to the assembly and fails
            var assemblyName = args.Name.Split(',')[0] + ".dll";
            var assemblyPath = Path.Combine(appSettings.executingPath, appSettings.probingPath, assemblyName);
            var assembly = Assembly.LoadFrom(assemblyPath);

            return assembly;
        }
    }
}
