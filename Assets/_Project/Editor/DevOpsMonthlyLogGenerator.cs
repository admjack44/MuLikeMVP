using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MuLike.EditorTools
{
    /// <summary>
    /// Generates monthly CI run logs from template and updates DevOps index.
    /// Supports manual execution and automatic generation once per month on editor load.
    /// </summary>
    public static class DevOpsMonthlyLogGenerator
    {
        private const string DevOpsFolder = "Assets/_Project/Docs/DevOps";
        private const string TemplatePath = DevOpsFolder + "/CI_Run_Log_YYYY_MM.md";
        private const string IndexPath = DevOpsFolder + "/INDEX.md";
        private const string AutoEnabledKey = "MuLike.DevOps.AutoMonthlyLog.Enabled";
        private const string LastGeneratedMonthKey = "MuLike.DevOps.AutoMonthlyLog.LastGeneratedMonth";

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            EditorApplication.delayCall += TryAutoGenerateCurrentMonth;
        }

        [MenuItem("MuLike/DevOps/Generate Current Month CI Log")]
        public static void GenerateCurrentMonthLog()
        {
            GenerateMonthlyLog(DateTime.UtcNow, triggeredByAuto: false);
        }

        [MenuItem("MuLike/DevOps/Generate Next Month CI Log")]
        public static void GenerateNextMonthLog()
        {
            GenerateMonthlyLog(DateTime.UtcNow.AddMonths(1), triggeredByAuto: false);
        }

        [MenuItem("MuLike/DevOps/Toggle Auto Monthly CI Log Generation")]
        public static void ToggleAutoGeneration()
        {
            bool enabled = IsAutoEnabled();
            EditorPrefs.SetBool(AutoEnabledKey, !enabled);
            Debug.Log($"[DevOpsMonthlyLogGenerator] Auto generation {(!enabled ? "ENABLED" : "DISABLED")}");
        }

        [MenuItem("MuLike/DevOps/Toggle Auto Monthly CI Log Generation", true)]
        private static bool ToggleAutoGenerationValidate()
        {
            Menu.SetChecked("MuLike/DevOps/Toggle Auto Monthly CI Log Generation", IsAutoEnabled());
            return true;
        }

        private static void TryAutoGenerateCurrentMonth()
        {
            if (!IsAutoEnabled())
                return;

            string currentMonthKey = ToMonthKey(DateTime.UtcNow);
            string expectedLogPath = $"{DevOpsFolder}/CI_Run_Log_{currentMonthKey}.md";
            string lastGenerated = EditorPrefs.GetString(LastGeneratedMonthKey, string.Empty);

            if (lastGenerated == currentMonthKey && File.Exists(expectedLogPath))
                return;

            GenerateMonthlyLog(DateTime.UtcNow, triggeredByAuto: true);
            EditorPrefs.SetString(LastGeneratedMonthKey, currentMonthKey);
        }

        private static void GenerateMonthlyLog(DateTime monthDateUtc, bool triggeredByAuto)
        {
            EnsureFolder(DevOpsFolder);
            EnsureTemplateExists();

            string monthKey = ToMonthKey(monthDateUtc);
            string monthFileName = $"CI_Run_Log_{monthKey}.md";
            string monthFilePath = $"{DevOpsFolder}/{monthFileName}";

            if (!File.Exists(monthFilePath))
            {
                string template = File.ReadAllText(TemplatePath);
                string content = template
                    .Replace("YYYY_MM", monthKey)
                    .Replace("YYYY-MM-DD", monthDateUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

                File.WriteAllText(monthFilePath, content);
                Debug.Log($"[DevOpsMonthlyLogGenerator] Created monthly log: {monthFilePath}");
            }

            UpdateIndex(monthDateUtc, monthFileName);
            AssetDatabase.Refresh();

            if (!triggeredByAuto)
                EditorUtility.DisplayDialog("DevOps Monthly Log", $"Monthly log ready: {monthFileName}", "OK");
        }

        private static void UpdateIndex(DateTime monthDateUtc, string monthFileName)
        {
            EnsureIndexExists();

            string year = monthDateUtc.Year.ToString(CultureInfo.InvariantCulture);
            string monthName = monthDateUtc.ToString("MMMM", CultureInfo.InvariantCulture);
            string monthLine = $"- {monthName}: [{monthFileName}]({monthFileName})";
            string pendingLine = $"- {monthName}: pending";

            List<string> lines = new List<string>(File.ReadAllLines(IndexPath));

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().Equals(monthLine, StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllLines(IndexPath, lines);
                    return;
                }
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().Equals(pendingLine, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = monthLine;
                    File.WriteAllLines(IndexPath, lines);
                    return;
                }
            }

            int yearHeaderIndex = FindLineIndex(lines, $"### {year}");
            if (yearHeaderIndex >= 0)
            {
                int insertIndex = yearHeaderIndex + 1;
                while (insertIndex < lines.Count)
                {
                    string l = lines[insertIndex];
                    if (l.StartsWith("### ", StringComparison.Ordinal) || l.StartsWith("## ", StringComparison.Ordinal))
                        break;
                    insertIndex++;
                }

                lines.Insert(insertIndex, monthLine);
                File.WriteAllLines(IndexPath, lines);
                return;
            }

            int monthlyHeaderIndex = FindLineIndex(lines, "## Monthly Run Logs");
            if (monthlyHeaderIndex >= 0)
            {
                int insertIndex = monthlyHeaderIndex + 1;
                while (insertIndex < lines.Count && string.IsNullOrWhiteSpace(lines[insertIndex]))
                    insertIndex++;

                lines.Insert(insertIndex, $"### {year}");
                lines.Insert(insertIndex + 1, string.Empty);
                lines.Insert(insertIndex + 2, monthLine);
                File.WriteAllLines(IndexPath, lines);
                return;
            }

            lines.Add(string.Empty);
            lines.Add("## Monthly Run Logs");
            lines.Add(string.Empty);
            lines.Add($"### {year}");
            lines.Add(string.Empty);
            lines.Add(monthLine);
            File.WriteAllLines(IndexPath, lines);
        }

        private static int FindLineIndex(List<string> lines, string expected)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().Equals(expected, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static bool IsAutoEnabled()
        {
            return EditorPrefs.GetBool(AutoEnabledKey, true);
        }

        private static string ToMonthKey(DateTime dateUtc)
        {
            return dateUtc.ToString("yyyy_MM", CultureInfo.InvariantCulture);
        }

        private static void EnsureTemplateExists()
        {
            if (File.Exists(TemplatePath))
                return;

            const string fallback = "# CI Run Log - YYYY_MM\n\n## Day: YYYY-MM-DD\n\n### Runs\n\n| Run # | Time UTC | Stage | Job | Result | Root Cause | Action | Owner |\n|---|---|---|---|---|---|---|---|\n|  |  |  |  |  |  |  |  |\n";
            File.WriteAllText(TemplatePath, fallback);
        }

        private static void EnsureIndexExists()
        {
            if (File.Exists(IndexPath))
                return;

            const string fallback = "# DevOps Master Index\n\n## Monthly Run Logs\n";
            File.WriteAllText(IndexPath, fallback);
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
                return;

            string[] parts = assetFolderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
