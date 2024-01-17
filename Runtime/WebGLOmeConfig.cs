using Extreal.Core.Logging;

namespace Extreal.Integration.SFU.OME
{
    public class WebGLOmeConfig : OmeConfig
    {
        public bool IsDebug => Logger.IsDebug();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(WebGLOmeConfig));

        public WebGLOmeConfig(OmeConfig omeConfig) : base(omeConfig.ServerUrl, omeConfig.IceServerConfigs)
        {
        }
    }
}
