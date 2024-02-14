#if UNITY_WEBGL && !UNITY_EDITOR
using Extreal.Integration.Web.Common;

namespace Extreal.Integration.SFU.OME.MVS.ClientControl
{
    public class WebGLAudioStreamClient : AudioStreamClient
    {
        public WebGLAudioStreamClient() => WebGLHelper.CallAction("start");
    }
}
#endif
