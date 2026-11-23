#if UNITY_IOS
using System.IO;
using AppleAuth.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace CityBlocks.EditorTools
{
    /// <summary>
    /// Adds the Game Center, Sign in with Apple and In-App Purchase capabilities (and their entitlements)
    /// to the generated Xcode project on every iOS build, so they never have to be ticked by hand in Xcode.
    /// Runs after the Unity IAP and Mobile Notifications post-processors and reuses whatever entitlements
    /// file they configured, so all entitlements end up in one file.
    /// </summary>
    internal sealed class iOSCapabilitiesPostProcess : IPostprocessBuildWithReport
    {
        private const string DefaultEntitlementsPath = "Unity-iPhone/CityBlocks.entitlements";

        // Unity IAP uses 0 and Mobile Notifications uses the default [PostProcessBuild] order, so run after both.
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;

            string buildPath = report.summary.outputPath;
            string projectPath = PBXProject.GetPBXProjectPath(buildPath);
            PBXProject project = new PBXProject();
            project.ReadFromFile(projectPath);
            string mainTarget = project.GetUnityMainTargetGuid();

            // Reuse an entitlements file another post-processor already set; otherwise create ours.
            string entitlementsPath = project.GetBuildPropertyForAnyConfig(mainTarget, "CODE_SIGN_ENTITLEMENTS");
            if (string.IsNullOrEmpty(entitlementsPath)) entitlementsPath = DefaultEntitlementsPath;

            ProjectCapabilityManager manager = new ProjectCapabilityManager(projectPath, entitlementsPath, null, mainTarget);
            manager.AddGameCenter();
            manager.AddSignInWithAppleWithCompatibility();
            manager.AddInAppPurchase();
            manager.WriteToFile();

            Debug.Log("[iOSCapabilitiesPostProcess] Added Game Center, Sign in with Apple and In-App Purchase (entitlements: " + entitlementsPath + ")");
        }
    }
}
#endif
