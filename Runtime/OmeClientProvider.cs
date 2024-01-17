using System;

namespace Extreal.Integration.SFU.OME
{
    public class OmeClientProvider
    {
        public static OmeClient Provide(OmeConfig omeConfig)
        {
            if (omeConfig == null)
            {
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
