using System;

namespace Extreal.Integration.SFU.OME
{
    /// <summary>
    /// Class that provides OmeClient.
    /// </summary>
    public class OmeClientProvider
    {
        /// <summary>
        /// Provides the OmeClient.
        /// </summary>
        /// <remarks>
        /// Creates and returns a OmeClient for Native (C#) or WebGL (JavaScript) depending on the platform.
        /// </remarks>
        /// <param name="omeConfig">OME configuration</param>
        /// <returns>OmeClient</returns>
        public static OmeClient Provide(OmeConfig omeConfig)
        {
            if (omeConfig == null)
            {
                // Not covered by testing due to defensive implementation
                throw new ArgumentNullException(nameof(omeConfig));
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            return new NativeOmeClient(omeConfig);
#else
            return new WebGLOmeClient(new WebGLOmeConfig(omeConfig));
#endif
        }
    }
}
