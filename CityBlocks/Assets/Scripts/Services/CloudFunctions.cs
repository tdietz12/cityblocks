using System;
using System.Globalization;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace CloudServices
{
    /// <summary>
    /// Client calls to the City Blocks Cloud Code module (CityBlocksCCM, deployed from Assets/CloudCode/CityBlocks.ccmr).
    /// Each call makes sure the player is signed in first and returns null instead of throwing when offline / failing.
    /// </summary>
    public static class CloudFunctions
    {
        /// <summary>Module name = file name of the .ccmr reference.</summary>
        public const string ModuleName = "CityBlocks";

        /// <summary>Server time in UTC, or null if offline / the call failed.</summary>
        public static async Task<DateTime?> GetServerTimeAsync()
        {
            if (!await CloudBootstrap.EnsureSignedInAsync()) return null;
            try
            {
                string value = await CloudCodeService.Instance.CallModuleEndpointAsync<string>(ModuleName, "GetServerTime");
                return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
            }
            catch (CloudCodeException exception)
            {
                Debug.LogWarning("[CloudFunctions] GetServerTime failed (" + exception.Reason + "): " + exception.Message);
                return null;
            }
            catch (FormatException exception)
            {
                Debug.LogWarning("[CloudFunctions] GetServerTime returned an unexpected value: " + exception.Message);
                return null;
            }
        }
    }
}
