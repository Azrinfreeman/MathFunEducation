using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class PortfolioWebBuild
{
    [MenuItem("Tools/Portfolio/Build Web Demo")]
    public static void Build()
    {
        EnsureVuforiaConfigurationHasNoLicense();

        const string fallbackDirectory = "Build/PortfolioWeb";
        string output = Environment.GetEnvironmentVariable("PORTFOLIO_WEB_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            output = fallbackDirectory;
        }

        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled && !scene.path.StartsWith("Assets/Scenes/AR/", StringComparison.Ordinal))
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
        {
            throw new BuildFailedException("Enable at least one non-AR scene in Build Profiles.");
        }

        Directory.CreateDirectory(output);
        var originalCompression = PlayerSettings.WebGL.compressionFormat;
        bool originalFallback = PlayerSettings.WebGL.decompressionFallback;
        bool originalDataCaching = PlayerSettings.WebGL.dataCaching;
        try
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            // The portfolio's script-only iframe cannot access the browser cache.
            PlayerSettings.WebGL.dataCaching = false;
            var report = BuildPipeline.BuildPlayer(scenes, output, BuildTarget.WebGL, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"WebGL build ended with {report.summary.result}.");
            }
        }
        finally
        {
            PlayerSettings.WebGL.compressionFormat = originalCompression;
            PlayerSettings.WebGL.decompressionFallback = originalFallback;
            PlayerSettings.WebGL.dataCaching = originalDataCaching;
        }
    }

    private static void EnsureVuforiaConfigurationHasNoLicense()
    {
        if (File.Exists("Assets/VuforiaLicense.cs"))
        {
            throw new BuildFailedException("Remove the generated Assets/VuforiaLicense.cs from the separate WebGL checkout before building.");
        }

        const string configurationPath = "Assets/Resources/VuforiaConfiguration.asset";
        if (!File.Exists(configurationPath))
        {
            return;
        }

        var asset = AssetDatabase.LoadMainAssetAtPath(configurationPath);
        if (asset == null)
        {
            throw new BuildFailedException("Unity could not inspect the Vuforia configuration before the WebGL build.");
        }

        using var configuration = new SerializedObject(asset);
        var vuforiaLicense = configuration.FindProperty("vuforia.vuforiaLicenseKey");
        var legacyLicense = configuration.FindProperty("vuforia.ufoLicenseKey");
        if (!string.IsNullOrWhiteSpace(vuforiaLicense?.stringValue) || !string.IsNullOrWhiteSpace(legacyLicense?.stringValue))
        {
            throw new BuildFailedException("Build WebGL from a separate checkout with a blank Vuforia configuration. The browser build includes assets under Resources.");
        }
    }
}
