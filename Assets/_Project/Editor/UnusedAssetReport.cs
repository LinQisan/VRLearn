using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Which files under Assets/ThirdParty and Assets/_Project/Hikone/Textures nothing uses: everything
/// reachable from the build scenes, every Resources folder and the assets the project settings
/// point at counts as used. Writes Logs/unused-assets.txt (per package: used / unused files and
/// sizes). Scripts are reported but never counted as removable on their own: code can depend on
/// code without a serialized reference.
/// </summary>
public static class UnusedAssetReport
{
    const string Report = "Logs/unused-assets.txt";
    static readonly string[] Candidates = { "Assets/ThirdParty", "Assets/_Project/Hikone/Textures" };

    [MenuItem("Tools/VRLearn/Maintenance/Report Unused Assets")]
    public static void Run()
    {
        var roots = new List<string>();
        roots.AddRange(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
        roots.AddRange(AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && p.Contains("/Resources/") && !AssetDatabase.IsValidFolder(p)));
        foreach (var settings in Directory.GetFiles("ProjectSettings", "*.asset"))
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(settings))
                if (o != null)
                    foreach (var d in EditorUtility.CollectDependencies(new[] { o }))
                    {
                        var p = AssetDatabase.GetAssetPath(d);
                        if (p.StartsWith("Assets/")) roots.Add(p);
                    }
        var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.Distinct().ToArray(), true));

        var sb = new StringBuilder($"Unused assets ({used.Count} files reachable from {roots.Count} roots)\n");
        foreach (var top in Candidates)
        {
            var packages = AssetDatabase.IsValidFolder(top) && top.EndsWith("ThirdParty")
                ? AssetDatabase.GetSubFolders(top) : new[] { top };
            foreach (var pkg in packages)
            {
                var files = AssetDatabase.FindAssets("", new[] { pkg }).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => !AssetDatabase.IsValidFolder(p)).Distinct().ToList();
                long Size(IEnumerable<string> ps) => ps.Sum(p => File.Exists(p) ? new FileInfo(p).Length : 0);
                var usedFiles = files.Where(used.Contains).ToList();
                var scripts = files.Where(p => p.EndsWith(".cs") || p.EndsWith(".dll") || p.EndsWith(".asmdef")).ToList();
                var unused = files.Except(usedFiles).ToList();
                sb.Append($"\n== {pkg}: {files.Count} files ({Size(files) / 1048576f:0.0} MB), used {usedFiles.Count}, scripts {scripts.Count}, unused {unused.Count} ({Size(unused) / 1048576f:0.0} MB)\n");
                foreach (var p in usedFiles) sb.Append("  USED   ").Append(p).Append('\n');
                foreach (var p in unused) sb.Append("  unused ").Append(p).Append('\n');
            }
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, sb.ToString());
        Debug.Log("[UnusedAssets] report written to " + Report);
    }
}
