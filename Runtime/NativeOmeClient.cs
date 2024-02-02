#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Cysharp.Threading.Tasks;
using NativeWebSocket;
using UniRx;
using Unity.WebRTC;

namespace Extreal.Integration.SFU.OME
{
    public class NativeOmeClient : OmeClient
    {
        private OmeWebSocket websocket;
        private readonly string serverUrl;
        private readonly List<RTCIceServer> defaultIceServers;

        private readonly List<Action<string, OmeRTCPeerConnection>> publishPcCreateHooks = new List<Action<string, OmeRTCPeerConnection>>();
        private readonly List<Action<string, OmeRTCPeerConnection>> subscribePcCreateHooks = new List<Action<string, OmeRTCPeerConnection>>();
        private readonly List<Action<string>> publishPcCloseHooks = new List<Action<string>>();
        private readonly List<Action<string>> subscribePcCloseHooks = new List<Action<string>>();

        private CompositeDisposable websocketDisposables = new CompositeDisposable();

        public NativeOmeClient(OmeConfig omeConfig) : base(omeConfig)
        {
            serverUrl = omeConfig.ServerUrl;
            defaultIceServers = omeConfig.IceServerConfigs.Select(iceServerConfig => new RTCIceServer
            {
                urls = iceServerConfig.Urls.ToArray(),
                username = iceServerConfig.UserName,
                credential = iceServerConfig.Credential,
            }).ToList();
        }

        protected override void DoReleaseManagedResources()
            => websocketDisposables.Dispose();

        [SuppressMessage("Usage", "CC0022")]
        private async UniTask<OmeWebSocket> GetSocketAsync()
        {
            if (websocket is not null)
            {
                if (websocket.State == WebSocketState.Open)
                {
                    return websocket;
                }
                // Not covered by testing due to defensive implementation
                await StopSocketAsync();
            }

            websocket = new OmeWebSocket(serverUrl, defaultIceServers).AddTo(websocketDisposables);

            websocket.OnJoined.Subscribe(FireOnJoined).AddTo(websocketDisposables);
            websocket.OnLeft.Subscribe(_ => FireOnLeft()).AddTo(websocketDisposables);
            websocket.OnUnexpectedLeft.Subscribe(FireOnUnexpectedLeft).AddTo(websocketDisposables);
            websocket.OnUserJoined.Subscribe(FireOnUserJoined).AddTo(websocketDisposables);
            websocket.OnUserLeft.Subscribe(FireOnUserLeft).AddTo(websocketDisposables);

            websocket.AddPublishPcCreateHook(publishPcCreateHooks);
            websocket.AddSubscribePcCreateHook(subscribePcCreateHooks);
            websocket.AddPublishPcCloseHook(publishPcCloseHooks);
            websocket.AddSubscribePcCloseHook(subscribePcCloseHooks);

            UniTask.Void(async () => await websocket.Connect());

            await UniTask.WaitUntil(() => websocket.State is WebSocketState.Open or WebSocketState.Closed);

            if (websocket.State == WebSocketState.Closed)
            {
                throw new WebSocketException("Connection failed");
            }

            return websocket;
        }

        private async UniTask StopSocketAsync()
        {
            if (websocket is null)
            {
                // Not covered by testing due to defensive implementation
                return;
            }
            await websocket.Close();
            websocketDisposables.Dispose();
            websocketDisposables = new CompositeDisposable();
            websocket = null;
        }

        public void AddPublishPcCreateHook(Action<string, OmeRTCPeerConnection> hook)
            => publishPcCreateHooks.Add(hook);

        public void AddSubscribePcCreateHook(Action<string, OmeRTCPeerConnection> hook)
            => subscribePcCreateHooks.Add(hook);

        public void AddPublishPcCloseHook(Action<string> hook)
            => publishPcCloseHooks.Add(hook);

        public void AddSubscribePcCloseHook(Action<string> hook)
            => subscribePcCloseHooks.Add(hook);

        protected override async UniTask<GroupListResponse> DoListGroupsAsync()
            => await (await GetSocketAsync()).ListGroupsAsync();

        protected override async UniTask DoConnectAsync(string roomName)
            => (await GetSocketAsync()).Connect(roomName);

        public override UniTask DisconnectAsync()
            => StopSocketAsync();
    }
}
#endif
