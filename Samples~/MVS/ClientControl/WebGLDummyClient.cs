#if UNITY_WEBGL && !UNITY_EDITOR
using Extreal.Integration.Web.Common;

namespace Extreal.Integration.SFU.OME.MVS.ClientControl
{
    public class WebGLDummyClient
    {
        public static void DummyHookSet() => WebGLHelper.CallAction("dummyhook");
    }
}
#endif
