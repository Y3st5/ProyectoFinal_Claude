using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;

namespace DuelModule
{
    /// <summary>
    /// Dependency injection entry point (one per module). <c>IGameApiClient</c> needs registering;
    /// <c>IPushClient</c> does not: since Com.Unity.Services.CloudCode.Core 0.0.4 Cloud Code injects it
    /// into any [CloudCodeFunction] parameter list by itself.
    /// </summary>
    public class ModuleConfig : ICloudCodeSetup
    {
        public void Setup(ICloudCodeConfig config)
        {
            config.AddGameApiClient();
        }
    }
}
