using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace CityBlocks.CloudCode
{
    /// <summary>Registers the UGS API client so module classes can receive it by constructor injection.</summary>
    public class ModuleSetup : ICloudCodeSetup
    {
        public void Setup(ICloudCodeConfig config)
        {
            config.Dependencies.AddSingleton(GameApiClient.Create());
        }
    }
}
