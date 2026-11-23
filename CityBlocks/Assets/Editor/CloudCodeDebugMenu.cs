using System;
using CloudServices;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTools
{
    /// <summary>Play Mode helpers for checking the deployed Cloud Code module.</summary>
    internal static class CloudCodeDebugMenu
    {
        [MenuItem("Tools/City Blocks/Cloud/Log Server Time")]
        private static async void LogServerTime()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[CloudCode] Enter Play Mode first (the call needs the game's signed-in player).");
                return;
            }
            DateTime? serverTime = await CloudFunctions.GetServerTimeAsync();
            if (serverTime == null)
            {
                Debug.LogWarning("[CloudCode] GetServerTime failed — is the module deployed (Services ▸ Deployment)?");
                SessionState.SetString("CloudCode.LastServerTime", "failed");
                return;
            }
            double drift = (DateTime.UtcNow - serverTime.Value).TotalSeconds;
            Debug.Log("[CloudCode] Server time (UTC): " + serverTime.Value.ToString("u") + " — device clock differs by " + drift.ToString("F1") + " s");
            SessionState.SetString("CloudCode.LastServerTime", serverTime.Value.ToString("o"));
        }
    }
}
