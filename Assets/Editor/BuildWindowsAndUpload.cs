using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

internal static class BuildWindowsAndUpload
{
    private const string MenuPath = "Tools/Build Windows & Upload";

    [MenuItem(MenuPath, false, 1000)]
    private static void BuildAndUpload()
    {
        string previousVersion = PlayerSettings.bundleVersion;
        bool buildSucceeded = false;

        try
        {
            EnsureReadyToBuild();

            string version = IncrementPatchVersion(previousVersion);
            string tag = "v" + version;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string productName = MakeFileNameSafe(PlayerSettings.productName);
            string buildDirectory = Path.Combine(projectRoot, "Builds", "Windows", productName + "-" + tag);
            string executablePath = Path.Combine(buildDirectory, productName + ".exe");
            string releaseDirectory = Path.Combine(projectRoot, "Builds", "Releases");
            string zipPath = Path.Combine(releaseDirectory, productName + "-Windows-" + tag + ".zip");

            EnsureReleasePrerequisites(projectRoot);

            PlayerSettings.bundleVersion = version;
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayProgressBar("Windows Release", "Building " + productName + " " + tag + "...", 0.15f);
            BuildWindowsPlayer(executablePath);
            buildSucceeded = true;

            EditorUtility.DisplayProgressBar("Windows Release", "Compressing the Windows build...", 0.55f);
            CreateReleaseZip(buildDirectory, releaseDirectory, zipPath);

            EditorUtility.DisplayProgressBar("Windows Release", "Committing and pushing " + tag + "...", 0.72f);
            CommitAndPushVersion(projectRoot, tag);

            EditorUtility.DisplayProgressBar("Windows Release", "Uploading " + Path.GetFileName(zipPath) + " to GitHub Releases...", 0.88f);
            UploadGitHubRelease(projectRoot, productName, tag, zipPath);

            EditorUtility.ClearProgressBar();
            EditorUtility.RevealInFinder(zipPath);
            EditorUtility.DisplayDialog(
                "Release uploaded",
                productName + " " + tag + " was built, zipped, committed, pushed, and uploaded to GitHub Releases.\n\n" + zipPath,
                "OK");

            Debug.Log("Windows release " + tag + " uploaded successfully: " + zipPath);
        }
        catch (Exception exception)
        {
            EditorUtility.ClearProgressBar();

            // A failed player build should not consume a version number. Once a
            // build succeeds, keep its version so the artifact and repository agree.
            if (!buildSucceeded)
            {
                PlayerSettings.bundleVersion = previousVersion;
                AssetDatabase.SaveAssets();
            }

            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Windows release failed", exception.Message, "OK");
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateBuildAndUpload()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling
            && !BuildPipeline.isBuildingPlayer;
    }

    private static void EnsureReadyToBuild()
    {
        if (!EditorSceneManager.SaveOpenScenes())
        {
            throw new InvalidOperationException("Open scenes must be saved before creating a release.");
        }

        AssetDatabase.SaveAssets();

        string[] scenes = GetEnabledScenes();
        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes were found in File > Build Profiles.");
        }

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
        {
            throw new InvalidOperationException("Unity could not switch to the Windows 64-bit build target.");
        }
    }

    private static void BuildWindowsPlayer(string executablePath)
    {
        string buildDirectory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(buildDirectory))
        {
            throw new InvalidOperationException("The Windows build directory is invalid.");
        }

        Directory.CreateDirectory(buildDirectory);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = executablePath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Windows build " + summary.result + " with " + summary.totalErrors + " error(s). Check the Unity Console for details.");
        }

        Debug.Log("Windows build completed: " + executablePath + " (" + FormatBytes(summary.totalSize) + ")");
    }

    private static void CreateReleaseZip(string buildDirectory, string releaseDirectory, string zipPath)
    {
        if (!Directory.Exists(buildDirectory))
        {
            throw new DirectoryNotFoundException("Windows build output was not found: " + buildDirectory);
        }

        Directory.CreateDirectory(releaseDirectory);
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        ZipFile.CreateFromDirectory(buildDirectory, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);
        if (!File.Exists(zipPath) || new FileInfo(zipPath).Length == 0)
        {
            throw new InvalidOperationException("The release ZIP was not created correctly.");
        }

        Debug.Log("Windows build compressed: " + zipPath + " (" + FormatBytes(new FileInfo(zipPath).Length) + ")");
    }

    private static void CommitAndPushVersion(string projectRoot, string tag)
    {
        Run("git", "add -A", projectRoot);
        Run("git", "commit -m " + Quote("Release " + tag), projectRoot);
        Run("git", "push", projectRoot);
    }

    private static void EnsureReleasePrerequisites(string projectRoot)
    {
        Run("git", "rev-parse --is-inside-work-tree", projectRoot);
        Run("git", "remote get-url origin", projectRoot);

        string branch = Run("git", "branch --show-current", projectRoot).Trim();
        if (string.IsNullOrWhiteSpace(branch))
        {
            throw new InvalidOperationException("Check out a Git branch before creating a release.");
        }

        Run(FindGitHubCli(), "auth status", projectRoot);
    }

    private static void UploadGitHubRelease(string projectRoot, string productName, string tag, string zipPath)
    {
        string gh = FindGitHubCli();
        Run(gh, "auth status", projectRoot);

        string branch = Run("git", "branch --show-current", projectRoot).Trim();
        if (string.IsNullOrWhiteSpace(branch))
        {
            throw new InvalidOperationException("The current Git branch could not be determined.");
        }

        string arguments = "release create " + Quote(tag)
            + " " + Quote(zipPath + "#Windows x64 build")
            + " --target " + Quote(branch)
            + " --title " + Quote(productName + " " + tag)
            + " --generate-notes --latest";

        Run(gh, arguments, projectRoot);
    }

    private static string Run(string executable, string arguments, string workingDirectory)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        StringBuilder standardOutput = new StringBuilder();
        StringBuilder standardError = new StringBuilder();

        using (Process process = new Process { StartInfo = startInfo })
        {
            process.OutputDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data != null) standardOutput.AppendLine(eventArgs.Data);
            };
            process.ErrorDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data != null) standardError.AppendLine(eventArgs.Data);
            };

            try
            {
                process.Start();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("Could not start " + executable + ". " + exception.Message, exception);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            string output = standardOutput.ToString().Trim();
            string error = standardError.ToString().Trim();
            if (process.ExitCode != 0)
            {
                string details = string.IsNullOrWhiteSpace(error) ? output : error;
                throw new InvalidOperationException(executable + " failed with exit code " + process.ExitCode + ".\n\n" + details);
            }

            if (!string.IsNullOrWhiteSpace(output))
            {
                Debug.Log(output);
            }

            return output;
        }
    }

    private static string[] GetEnabledScenes()
    {
        return EditorBuildSettings.scenes
            .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
            .Select(scene => scene.path)
            .ToArray();
    }

    private static string IncrementPatchVersion(string currentVersion)
    {
        Version parsed;
        if (!Version.TryParse(currentVersion, out parsed))
        {
            throw new InvalidOperationException(
                "Player Settings version must use major.minor.patch format before it can be bumped automatically. Current value: " + currentVersion);
        }

        int patch = Math.Max(parsed.Build, 0) + 1;
        return parsed.Major + "." + parsed.Minor + "." + patch;
    }

    private static string FindGitHubCli()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string installedPath = Path.Combine(programFiles, "GitHub CLI", "gh.exe");
        return File.Exists(installedPath) ? installedPath : "gh";
    }

    private static string MakeFileNameSafe(string value)
    {
        HashSet<char> invalidCharacters = new HashSet<char>(Path.GetInvalidFileNameChars());
        string safe = new string(value.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "WindowsGame" : safe;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static string FormatBytes(ulong bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }
        return value.ToString("0.##") + " " + units[unit];
    }

    private static string FormatBytes(long bytes)
    {
        return FormatBytes((ulong)Math.Max(bytes, 0));
    }
}
