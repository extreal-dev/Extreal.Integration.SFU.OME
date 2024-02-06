using Extreal.Core.Logging;

namespace Extreal.Integration.SFU.OME
{
    /// <summary>
    /// Class that holds OME configuration for WebGL.
    /// </summary>
    public class WebGLOmeConfig : OmeConfig
    {
        public bool IsDebug => Logger.IsDebug();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(WebGLOmeConfig));

        public WebGLOmeConfig(OmeConfig omeConfig) : base(omeConfig.ServerUrl, omeConfig.IceServerConfigs)
        {
        }
    }
}
