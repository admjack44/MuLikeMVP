using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MuLike.EditorTools
{
    /// <summary>
    /// Professional Unity build pipeline for Android/iOS with scene bootstrap setup,
    /// automatic versioning, split APK per architecture and separated debug symbols.
    /// </summary>
    public static class ProjectBuildPipeline
    {
        private const string ProjectScenesRoot = "Assets/_Project/Scenes";
        private const string BuildRoot = "Builds";
        private const string AndroidBuildRoot = BuildRoot + "/Android";
        private const string IOSBuildRoot = BuildRoot + "/iOS";
        private const string VersionFilePath = "Assets/_Project/Settings/build_version.json";

        private static readonly string[] RequiredScenePaths =
        {
            "Assets/_Project/Scenes/Bootstrap/Bootstrap.unity",
            "Assets/_Project/Scenes/Login/Login.unity",
            "Assets/_Project/Scenes/CharacterSelect/CharacterSelect.unity",
            "Assets/_Project/Scenes/Loading/Loading.unity",
            "Assets/_Project/Scenes/Main/Main.unity"
        };

        [Serializable]
        private struct BuildVersionData
        {
            public int major;
            public int minor;
            public int build;
        }

        [MenuItem("MuLike/Build/Setup Required Scene Structure")]
        public static void SetupRequiredScenes()
        {
            EnsureProjectFolderLayout();

            for (int i = 0; i < RequiredScenePaths.Length; i++)
                EnsureSceneExists(RequiredScenePaths[i]);

            ApplyScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ProjectBuildPipeline] Scene structure prepared and Build Settings updated.");
        }

        [MenuItem("MuLike/Build/Bump Version (major.minor.build)")]
        public static void BumpBuildVersion()
        {
            EnsureProjectFolderLayout();
            BuildVersionData version = ReadVersion();
            version.build++;
            WriteVersion(version);
            ApplyPlayerSettingsVersion(version);
            Debug.Log($"[ProjectBuildPipeline] Version bumped to {version.major}.{version.minor}.{version.build}");
        }

        [MenuItem("MuLike/Build/Android/Build AAB (Release)")]
        public static void BuildAndroidAab()
        {
            SetupRequiredScenes();
            BuildVersionData version = ReadVersion();
            version.build++;
            WriteVersion(version);
            ApplyPlayerSettingsVersion(version);

            EnsureDirectory(AndroidBuildRoot);
            string outputPath = Path.Combine(AndroidBuildRoot, $"MuLikeMVP_{VersionString(version)}.aab");

            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            SetAndroidDebugSymbolsSeparated();
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = RequiredScenePaths,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = UnityEditor.BuildPipeline.BuildPlayer(options);
            ValidateReport(report, "Android AAB");
        }

        [MenuItem("MuLike/Build/Android/Build APK ARMv7")]
        public static void BuildAndroidApkArmv7()
        {
            BuildAndroidApkByArchitecture(AndroidArchitecture.ARMv7, "armv7");
        }

        [MenuItem("MuLike/Build/Android/Build APK ARM64")]
        public static void BuildAndroidApkArm64()
        {
            BuildAndroidApkByArchitecture(AndroidArchitecture.ARM64, "arm64");
        }

        [MenuItem("MuLike/Build/iOS/Build Xcode Project")]
        public static void BuildIosXcodeProject()
        {
            SetupRequiredScenes();
            BuildVersionData version = ReadVersion();
            version.build++;
            WriteVersion(version);
            ApplyPlayerSettingsVersion(version);

            EnsureDirectory(IOSBuildRoot);
            string outputPath = Path.Combine(IOSBuildRoot, $"MuLikeMVP_{VersionString(version)}");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = RequiredScenePaths,
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            BuildReport report = UnityEditor.BuildPipeline.BuildPlayer(options);
            ValidateReport(report, "iOS Xcode Project");
        }

        private static void BuildAndroidApkByArchitecture(AndroidArchitecture architecture, string suffix)
        {
            SetupRequiredScenes();
            BuildVersionData version = ReadVersion();
            version.build++;
            WriteVersion(version);
            ApplyPlayerSettingsVersion(version);

            EnsureDirectory(AndroidBuildRoot);
            string outputPath = Path.Combine(AndroidBuildRoot, $"MuLikeMVP_{VersionString(version)}_{suffix}.apk");

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            SetAndroidDebugSymbolsSeparated();
            PlayerSettings.Android.targetArchitectures = architecture;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = RequiredScenePaths,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = UnityEditor.BuildPipeline.BuildPlayer(options);
            ValidateReport(report, $"Android APK ({suffix})");
        }

        private static void ValidateReport(BuildReport report, string label)
        {
            if (report == null)
                throw new Exception($"[ProjectBuildPipeline] {label}: build report is null.");

            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"[ProjectBuildPipeline] {label}: build failed with {report.summary.result}.");

            Debug.Log($"[ProjectBuildPipeline] {label}: success. Size={report.summary.totalSize / (1024f * 1024f):F1}MB");
        }

        private static void EnsureProjectFolderLayout()
        {
            EnsureDirectory("Assets/_Project/Animations");
            EnsureDirectory("Assets/_Project/Audio");
            EnsureDirectory("Assets/_Project/Editor");
            EnsureDirectory("Assets/_Project/Materials");
            EnsureDirectory("Assets/_Project/Models");
            EnsureDirectory("Assets/_Project/Plugins");
            EnsureDirectory("Assets/_Project/Prefabs");
            EnsureDirectory("Assets/_Project/Resources");
            EnsureDirectory("Assets/_Project/Scenes");
            EnsureDirectory("Assets/_Project/ScriptableObjects");
            EnsureDirectory("Assets/_Project/Scripts");
            EnsureDirectory("Assets/_Project/Scripts/Core");
            EnsureDirectory("Assets/_Project/Scripts/Networking");
            EnsureDirectory("Assets/_Project/Scripts/Gameplay");
            EnsureDirectory("Assets/_Project/Scripts/UI");
            EnsureDirectory("Assets/_Project/Scripts/Services");
            EnsureDirectory("Assets/_Project/Settings");
            EnsureDirectory("Assets/_Project/Shaders");
            EnsureDirectory("Assets/_Project/Sprites");
            EnsureDirectory("Assets/_Project/Textures");
            EnsureDirectory("Assets/ThirdParty");
        }

        private static void EnsureSceneExists(string scenePath)
        {
            if (File.Exists(scenePath))
                return;

            string directory = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(directory))
                EnsureDirectory(directory);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = Path.GetFileNameWithoutExtension(scenePath);

            if (scene.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase))
                EnsureBootstrapMarker();
            if (scene.name.Equals("Loading", StringComparison.OrdinalIgnoreCase))
                EnsureLoadingSceneUi();

            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[ProjectBuildPipeline] Created scene: {scenePath}");
        }

        private static void ApplyScenesToBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = RequiredScenePaths
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();
            EditorBuildSettings.scenes = scenes;
        }

        private static BuildVersionData ReadVersion()
        {
            EnsureDirectory("Assets/_Project/Settings");

            if (!File.Exists(VersionFilePath))
            {
                BuildVersionData initial = new BuildVersionData { major = 1, minor = 0, build = 0 };
                WriteVersion(initial);
                return initial;
            }

            string json = File.ReadAllText(VersionFilePath);
            BuildVersionData version = JsonUtility.FromJson<BuildVersionData>(json);
            if (version.major <= 0)
                version.major = 1;
            return version;
        }

        private static void WriteVersion(BuildVersionData version)
        {
            string json = JsonUtility.ToJson(version, true);
            File.WriteAllText(VersionFilePath, json);
            AssetDatabase.ImportAsset(VersionFilePath);
        }

        private static void ApplyPlayerSettingsVersion(BuildVersionData version)
        {
            PlayerSettings.bundleVersion = VersionString(version);

            int androidCode = Math.Max(1, version.major * 1000000 + version.minor * 10000 + version.build);
            PlayerSettings.Android.bundleVersionCode = androidCode;

            string iosBuildNumber = Math.Max(1, version.build).ToString();
            PlayerSettings.iOS.buildNumber = iosBuildNumber;
        }

        private static string VersionString(BuildVersionData version)
        {
            return $"{version.major}.{version.minor}.{version.build}";
        }

        private static void EnsureDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                if (AssetDatabase.IsValidFolder(path))
                    return;

                string[] parts = path.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = $"{current}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }

                return;
            }

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static void EnsureBootstrapMarker()
        {
            GameObject marker = GameObject.Find("BootstrapRoot");
            if (marker == null)
                marker = new GameObject("BootstrapRoot");

            if (marker.GetComponent<BootstrapSceneMarker>() == null)
                marker.AddComponent<BootstrapSceneMarker>();
        }

        private static void EnsureLoadingSceneUi()
        {
            GameObject canvasGo = GameObject.Find("LoadingCanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("LoadingCanvas");
                Canvas canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            GameObject tips = GameObject.Find("LoadingTips");
            if (tips == null)
            {
                tips = new GameObject("LoadingTips");
                tips.transform.SetParent(canvasGo.transform, false);
                RectTransform rt = tips.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 64f);
                rt.sizeDelta = new Vector2(1200f, 120f);
            }

            if (tips.GetComponent<MuLike.UI.LoadingTipsRotator>() == null)
                tips.AddComponent<MuLike.UI.LoadingTipsRotator>();
        }

        private static void SetAndroidDebugSymbolsSeparated()
        {
            // Uses reflection to remain compatible with multiple Unity versions.
            Type androidType = typeof(PlayerSettings).GetNestedType("Android", BindingFlags.Public);
            if (androidType == null)
                return;

            PropertyInfo createSymbolsProperty = androidType.GetProperty("createSymbols", BindingFlags.Public | BindingFlags.Static);
            if (createSymbolsProperty == null)
                return;

            Type enumType = createSymbolsProperty.PropertyType;
            Array values = Enum.GetValues(enumType);
            object chosen = null;

            for (int i = 0; i < values.Length; i++)
            {
                string name = values.GetValue(i).ToString();
                if (name.Equals("Public", StringComparison.OrdinalIgnoreCase) || name.Equals("Debugging", StringComparison.OrdinalIgnoreCase))
                {
                    chosen = values.GetValue(i);
                    break;
                }
            }

            if (chosen != null)
                createSymbolsProperty.SetValue(null, chosen, null);
        }
    }

    /// <summary>
    /// Marker component for bootstrap scene service initialization root.
    /// </summary>
    public sealed class BootstrapSceneMarker : MonoBehaviour
    {
    }
}
