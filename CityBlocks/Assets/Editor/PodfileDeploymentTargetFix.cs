#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

public static class PodfileDeploymentTargetFix
{
    // EDM4U writes the Podfile at order 40 and runs `pod install` at 50,
    // so 45 slips in between.
    [PostProcessBuild(45)]
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        var podfile = Path.Combine(path, "Podfile");
        if (!File.Exists(podfile)) return;

        var text = File.ReadAllText(podfile);
        if (text.Contains("post_install")) return; // avoid a duplicate block

        File.AppendAllText(podfile, @"

post_install do |installer|
  installer.pods_project.targets.each do |t|
    t.build_configurations.each do |config|
      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '15.0'
    end
  end
end
");
    }
}
#endif