using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AOT;
using Cysharp.Threading.Tasks;
using Extreal.Integration.Web.Common;
using UnityEngine;

namespace Extreal.Integration.SFU.OME
{
    public class WebGLOmeClient : OmeClient
    {
        private static WebGLOmeClient instance;

        public WebGLOmeClient(WebGLOmeConfig omeConfig) : base(omeConfig)
        {
            instance = this;
            WebGLHelper.CallAction(WithPrefix(nameof(WebGLOmeClient)), JsonOmeConfig.ToJson(omeConfig));
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnJoined)), HandleOnJoined);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnLeft)), HandleOnLeft);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUserJoined)), HandleOnUserJoined);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUserLeft)), HandleOnUserLeft);
        }

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnJoined(string streamName, string unused) => instance.FireOnJoined(streamName);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnLeft(string reason, string unused) => instance.FireOnLeft(reason);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUserJoined(string streamName, string unused) => instance.FireOnUserJoined(streamName);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUserLeft(string streamName, string unused) => instance.FireOnUserLeft(streamName);

        protected override void DoReleaseManagedResources()
            => WebGLHelper.CallAction(WithPrefix(nameof(DoReleaseManagedResources)));

#pragma warning disable CS1998
        protected override async UniTask DoConnectAsync(string roomName)
#pragma warning restore CS1998
            => WebGLHelper.CallAction(WithPrefix(nameof(DoConnectAsync)), roomName);

#pragma warning disable CS1998
        public override async UniTask DisconnectAsync()
#pragma warning restore CS1998
            => WebGLHelper.CallAction(WithPrefix(nameof(DisconnectAsync)));

        private static string WithPrefix(string name) => $"{nameof(WebGLOmeClient)}#{name}";
    }

    [Serializable]
    public class JsonOmeConfig
    {
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private string serverUrl;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private JsonRtcIceServer[] iceServers;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private bool isDebug;

        public static string ToJson(WebGLOmeConfig omeConfig)
        {
            var jsonOmeConfig = new JsonOmeConfig
            {
                serverUrl = omeConfig.ServerUrl,
                iceServers = omeConfig.IceServerConfigs != null
                    ? omeConfig.IceServerConfigs.Select(iceServerConfig => new JsonRtcIceServer(iceServerConfig)).ToArray()
                    : Array.Empty<JsonRtcIceServer>(),
                isDebug = omeConfig.IsDebug,
            };
            return JsonUtility.ToJson(jsonOmeConfig);
        }
    }

    [Serializable]
    public class JsonRtcIceServer
    {
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private string[] urls;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private string userName;
        [SerializeField, SuppressMessage("Usage", "IDE0052"), SuppressMessage("Usage", "CC0052")] private string credential;

        public JsonRtcIceServer(IceServerConfig iceServerConfig)
        {
            urls = iceServerConfig.Urls.ToArray();
            userName = iceServerConfig.UserName;
            credential = iceServerConfig.Credential;
        }
    }
}
