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
    /// <summary>
    /// class that handles OME client for native application.
    /// </summary>
    public class NativeOmeClient : OmeClient
    {
        private OmeWebSocket websocket;
        private readonly OmeConfig omeConfig;
        private readonly List<RTCIceServer> defaultIceServers;

        private readonly List<Action<string, RTCPeerConnection>> publishPcCreateHooks = new List<Action<string, RTCPeerConnection>>();
        private readonly List<Action<string, RTCPeerConnection>> subscribePcCreateHooks = new List<Action<string, RTCPeerConnection>>();
        private readonly List<Action<string>> publishPcCloseHooks = new List<Action<string>>();
        private readonly List<Action<string>> subscribePcCloseHooks = new List<Action<string>>();

        private CompositeDisposable websocketDisposables = new CompositeDisposable();

        /// <summary>
        /// Creates NativeOmeClient with OmeConfig.
        /// </summary>
        /// <param name="omeConfig">OME config.</param>
        /// <returns></returns>
        public NativeOmeClient(OmeConfig omeConfig) : base(omeConfig)
        {
            this.omeConfig = omeConfig;
            defaultIceServers = omeConfig.IceServerConfigs.Select(iceServerConfig => new RTCIceServer
            {
                urls = iceServerConfig.Urls.ToArray(),
                username = iceServerConfig.UserName,
                credential = iceServerConfig.Credential,
            }).ToList();
        }

        /// <inheritdoc/>
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

            websocket = new OmeWebSocket(omeConfig.ServerUrl, defaultIceServers, omeConfig.MaxJoinRetryCount, omeConfig.JoinRetryInterval).AddTo(websocketDisposables);

            websocket.OnJoined.Subscribe(FireOnJoined).AddTo(websocketDisposables);
            websocket.OnLeft.Subscribe(_ => FireOnLeft()).AddTo(websocketDisposables);
            websocket.OnUnexpectedLeft.Subscribe(FireOnUnexpectedLeft).AddTo(websocketDisposables);
            websocket.OnUserJoined.Subscribe(FireOnUserJoined).AddTo(websocketDisposables);
            websocket.OnUserLeft.Subscribe(FireOnUserLeft).AddTo(websocketDisposables);
            websocket.OnJoinRetrying.Subscribe(FireOnJoinRetrying).AddTo(websocketDisposables);
            websocket.OnJoinRetried.Subscribe(FireOnJoinRetried).AddTo(websocketDisposables);

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
            await websocket.Close().AsUniTask();
            websocketDisposables.Dispose();
            websocketDisposables = new CompositeDisposable();
            websocket = null;
        }

        /// <summary>
        /// Add a process to be called when creating a publish peer connection.
        /// </summary>
        /// <param name="hook"></param>
        public void AddPublishPcCreateHook(Action<string, RTCPeerConnection> hook)
            => publishPcCreateHooks.Add(hook);

        /// <summary>
        /// Add a process to be called when creating a subscribe peer connection.
        /// </summary>
        /// <param name="hook"></param>
        public void AddSubscribePcCreateHook(Action<string, RTCPeerConnection> hook)
            => subscribePcCreateHooks.Add(hook);

        /// <summary>
        /// Add a process to be called when a publish peer connection is terminated.
        /// </summary>
        /// <param name="hook"></param>
        public void AddPublishPcCloseHook(Action<string> hook)
            => publishPcCloseHooks.Add(hook);

        /// <summary>
        /// Add a process to be called when a subscribe peer connection is terminated.
        /// </summary>
        /// <param name="hook"></param>
        public void AddSubscribePcCloseHook(Action<string> hook)
            => subscribePcCloseHooks.Add(hook);

        /// <inheritdoc/>
        protected override async UniTask<GroupListResponse> DoListGroupsAsync()
            => await (await GetSocketAsync()).ListGroupsAsync();

        /// <inheritdoc/>
        protected override async UniTask DoJoinAsync(string groupName)
            => (await GetSocketAsync()).Connect(groupName);

        /// <inheritdoc/>
        public override UniTask LeaveAsync()
            => StopSocketAsync();
    }
}
#endif
