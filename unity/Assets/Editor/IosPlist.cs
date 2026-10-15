#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// Declares no custom encryption, so TestFlight does not stop each build for export compliance.
// Godot's export preset adds the same key.
public static class IosPlist
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        var plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        plist.WriteToFile(plistPath);
    }
}
#endif
