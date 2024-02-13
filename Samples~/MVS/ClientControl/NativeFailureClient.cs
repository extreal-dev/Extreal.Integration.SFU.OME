#if !UNITY_WEBGL || UNITY_EDITOR
namespace Extreal.Integration.SFU.OME.MVS.ClientControl
{
    public class NativeFailureClient
    {
        public static void NativeFailureHook(NativeOmeClient omeClient)
        {
            omeClient.AddPublishPcCreateHook((clientId, pc) => throw new System.Exception("OmeClient Publish Create Error Test"));
            omeClient.AddSubscribePcCreateHook((clientId, pc) => throw new System.Exception("OmeClient Subscribe Create Error Test"));
            omeClient.AddPublishPcCloseHook(clientId => throw new System.Exception("OmeClient Publish Close Error Test"));
            omeClient.AddSubscribePcCloseHook(clientId => throw new System.Exception("OmeClient Subscribe Close Error Test"));
        }
    }
}
#endif
