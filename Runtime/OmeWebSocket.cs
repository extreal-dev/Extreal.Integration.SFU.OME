#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NativeWebSocket;
using Unity.WebRTC;
using Cysharp.Threading.Tasks;
using Extreal.Core.Logging;
using Extreal.Core.Common.System;
using UniRx;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Extreal.Integration.SFU.OME
{
    public class OmeWebSocket : WebSocket, IDisposable
    {
        public IObservable<string> OnJoined => onJoined;
        private readonly Subject<string> onJoined;

        public IObservable<Unit> OnLeft => onLeft;
        private readonly Subject<Unit> onLeft;

        public IObservable<string> OnUnexpectedLeft => onUnexpectedLeft;
        private readonly Subject<string> onUnexpectedLeft;

        public IObservable<string> OnUserJoined => onUserJoined;
        private readonly Subject<string> onUserJoined;

        public IObservable<string> OnUserLeft => onUserLeft;
        private readonly Subject<string> onUserLeft;

        private string roomName;
        private readonly List<RTCIceServer> defaultIceServers;
        private string localClientId;

        private readonly List<Action<string, RTCPeerConnection>> publishPcCreateHooks = new List<Action<string, RTCPeerConnection>>();
        private readonly List<Action<string, RTCPeerConnection>> subscribePcCreateHooks = new List<Action<string, RTCPeerConnection>>();
        private readonly List<Action<string>> publishPcCloseHooks = new List<Action<string>>();
        private readonly List<Action<string>> subscribePcCloseHooks = new List<Action<string>>();

        private OmeRTCPeerConnection publishConnection;
        private readonly Dictionary<string, OmeRTCPeerConnection> subscribeConnections = new Dictionary<string, OmeRTCPeerConnection>();

        private int publishRetryCount;
        private const int MaxPublishRetries = 3;
        private const float PublishRetryInterval = 5f;

        private readonly Dictionary<string, int> subscribeRetryCounts = new Dictionary<string, int>();
        private const int MaxSubscribeRetries = 10;
        private const float SubscribeRetryInterval = 2f;

        private GroupListResponse groupList;

        private readonly SafeDisposer safeDisposer;
        private bool isDisposed;
        [SuppressMessage("Usage", "CC0033")]
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(OmeWebSocket));

        [SuppressMessage("Usage", "CC0022")]
        public OmeWebSocket(string url, List<RTCIceServer> defaultIceServers) : base(url)
        {
            safeDisposer = new SafeDisposer(this, ReleaseManagedResources);

            onJoined = new Subject<string>().AddTo(disposables);
            onLeft = new Subject<Unit>().AddTo(disposables);
            onUnexpectedLeft = new Subject<string>().AddTo(disposables);
            onUserJoined = new Subject<string>().AddTo(disposables);
            onUserLeft = new Subject<string>().AddTo(disposables);

            OnOpen += OnOpenEvent;
            OnClose += OnCloseEvent;
            OnError += OnErrorEvent;
            OnMessage += OnMessageEvent;

            this.defaultIceServers = defaultIceServers;

#if !UNITY_WEBGL || UNITY_EDITOR
            UniTask.Void(async () =>
            {
                while (!isDisposed)
                {
                    DispatchMessageQueue();
                    await UniTask.Yield();
                }
            });
#endif
        }

        ~OmeWebSocket()
            // Not covered by testing due to defensive implementation
            => safeDisposer.DisposeByFinalizer();

        public void Dispose()
            => safeDisposer.Dispose();

        private void ReleaseManagedResources()
        {
            OnOpen -= OnOpenEvent;
            OnClose -= OnCloseEvent;
            OnError -= OnErrorEvent;
            OnMessage -= OnMessageEvent;

            CloseAllRTCConnections();
            UniTask.Void(async () => await Close());

            cancellation.Cancel();
            cancellation.Dispose();

            disposables.Dispose();
            isDisposed = true;
        }

        private static void OnOpenEvent()
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug("OnOpen");
            }
        }

        private void SendPublishRequest(string roomName) => UniTask.Void(async () =>
        {
            if (State != WebSocketState.Open)
            {
                // Not covered by testing due to defensive implementation
                if (Logger.IsDebug())
                {
                    Logger.LogDebug("WebSocket is not connected.");
                }
                return;
            }
            await Send(OmeMessage.CreatePublishRequest(roomName));
        });

        private void OnCloseEvent(WebSocketCloseCode closeCode)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"OnClose: CloseCode=${closeCode}");
            }

            CloseAllRTCConnections();
            if (closeCode is WebSocketCloseCode.Normal)
            {
                onLeft.OnNext(Unit.Default);
            }
            else
            {
                onUnexpectedLeft.OnNext(closeCode.ToString());
            }
        }

        private void OnErrorEvent(string e) => UniTask.Void(async () =>
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"OnError: {e}");
            }

            CloseAllRTCConnections();
            await Close();
        });

        private void CloseAllRTCConnections()
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug("Close all RTCConnections");
            }

            if (publishConnection != null)
            {
                publishPcCloseHooks.ForEach(hook => HandleHook(nameof(CloseAllRTCConnections), () => hook.Invoke(localClientId)));
                publishConnection.Close();
                publishConnection.Dispose();
                publishConnection = null;
                localClientId = null;
            }

            subscribeConnections.Keys.ToList().ForEach(CloseConnection);
            subscribeConnections.Clear();
        }

        private void CloseConnection(string clientId)
        {
            if (subscribeConnections.TryGetValue(clientId, out var connection))
            {
                subscribePcCloseHooks.ForEach(hook => HandleHook(nameof(CloseConnection), () => hook.Invoke(clientId)));
                connection.Close();
                connection.Dispose();
                subscribeConnections.Remove(clientId);
            }
        }

        private void OnMessageEvent(byte[] bytes)
        {
            var message = OmeMessage.FromJsonBytes(bytes);
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"OnMessage: {message.ToJson()}");
            }

            if (message.Command == "list groups")
            {
                groupList = message.GroupListResponse;
            }
            else if (message.Command == "publish offer")
            {
                ReceivePublishOffer(message);
            }
            else if (message.Command == "subscribe offer")
            {
                ReceiveSubscribeOffer(message);
            }
            else if (message.Command == "join")
            {
                ReceiveJoinMember(message);
            }
            else if (message.Command == "leave")
            {
                ReceiveLeaveMember(message);
            }
        }

        private void ReceivePublishOffer(OmeMessage message)
        {
            var isSetLocalCandidate = false;

            var configuration = CreateRTCConfiguration(message.GetIceServers());
            var pc = new OmeRTCPeerConnection(ref configuration);
            pc.SetFailedCallback(() =>
            {
                CloseAllRTCConnections();

                if (publishRetryCount < MaxPublishRetries)
                {
                    UniTask.Void(async () =>
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(PublishRetryInterval));
                        SendPublishRequest(roomName);
                        publishRetryCount++;
                    });
                }
                else
                {
                    if (Logger.IsError())
                    {
                        Logger.LogError("Maximum publish retryCount reached");
                    }
                    publishRetryCount = 0;
                }
            });
            pc.SetCreateAnswerCompletion(answer => UniTask.Void(async () =>
            {
                var messageBytes = OmeMessage.CreateAnswerMessage(message.Id, answer);
                await Send(messageBytes);
            }));
            pc.SetIceCandidateCallback(candidate => UniTask.Void(async () =>
            {
                if (!isSetLocalCandidate)
                {
                    var sdpMid = candidate.SdpMid;
                    foreach (var c in message.Candidates)
                    {
                        var candidateInit = c.RtcIceCandidateInit;
                        candidateInit.sdpMid = sdpMid;
                        var rtcIceCandidate = new RTCIceCandidate(candidateInit);
                        pc.AddIceCandidate(rtcIceCandidate);
                    }
                    isSetLocalCandidate = true;
                }
                var messageBytes = OmeMessage.CreateIceCandidate(message.Id, candidate);
                await Send(messageBytes);
            }));
            pc.SetConnectedCallback(() => UniTask.Void(async () =>
            {
                if (Logger.IsDebug())
                {
                    Logger.LogDebug($"Joined Room: roomName={roomName}");
                }

                publishRetryCount = 0;
                await Send(OmeMessage.CreateJoinMessage(message.Id));
                onJoined.OnNext(message.ClientId);
            }));

            publishPcCreateHooks.ForEach(hook => HandleHook(nameof(ReceivePublishOffer), () => hook.Invoke(message.ClientId, pc)));

            localClientId = message.ClientId;
            publishConnection = pc;
            pc.CreateAnswerSdpAsync(message.GetSdp()).Forget();
        }

        private void ReceiveSubscribeOffer(OmeMessage message)
        {
            var currentRetryCount = subscribeRetryCounts.TryGetValue(message.ClientId, out var count) ? count : 0;

            if (!string.IsNullOrEmpty(message.Error))
            {
                if (message.Error == "Cannot create offer")
                {
                    if (currentRetryCount < MaxSubscribeRetries)
                    {
                        UniTask.Void(async () =>
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(SubscribeRetryInterval));
                            SendSubscribeRequest(message.ClientId);
                            subscribeRetryCounts[message.ClientId] = currentRetryCount + 1;
                        });
                    }
                    else
                    {
                        if (Logger.IsError())
                        {
                            Logger.LogError($"Maximum subscribe retryCount reached: {message.ClientId}");
                        }
                        subscribeRetryCounts.Remove(message.ClientId);
                    }
                }
                else
                {
                    // Not covered by testing due to defensive implementation
                    if (Logger.IsError())
                    {
                        Logger.LogError($"Subscribe error: {message.Error}");
                    }
                    subscribeRetryCounts.Remove(message.ClientId);
                }
                return;
            }
            else
            {
                if (Logger.IsDebug())
                {
                    Logger.LogDebug($"SubscribeOfferEvent: Id={message.Id}, Sdp={message.Sdp.RtcSessionDescription.sdp}");
                }

                if (message.Id == 0)
                {
                    return;
                }
            }
            subscribeRetryCounts.Remove(message.ClientId);

            var isSetLocalCandidate = false;

            var configuration = CreateRTCConfiguration(message.GetIceServers());
            var pc = new OmeRTCPeerConnection(ref configuration);
            pc.SetCreateAnswerCompletion(answer => UniTask.Void(async () =>
            {
                var messageBytes = OmeMessage.CreateAnswerMessage(message.Id, answer);
                await Send(messageBytes);
            }));
            pc.SetIceCandidateCallback(candidate => UniTask.Void(async () =>
            {
                if (!isSetLocalCandidate)
                {
                    var sdpMid = candidate.SdpMid;
                    foreach (var c in message.Candidates)
                    {
                        var candidateInit = c.RtcIceCandidateInit;
                        candidateInit.sdpMid = sdpMid;
                        var rtcIceCandidate = new RTCIceCandidate(candidateInit);
                        pc.AddIceCandidate(rtcIceCandidate);
                    }
                    isSetLocalCandidate = true;
                }
                var msgBytes = OmeMessage.CreateIceCandidate(message.Id, candidate);
                await Send(msgBytes);
            }));

            subscribePcCreateHooks.ForEach(hook => HandleHook(nameof(ReceiveSubscribeOffer), () => hook.Invoke(message.ClientId, pc)));

            subscribeConnections[message.ClientId] = pc;
            pc.CreateAnswerSdpAsync(message.GetSdp()).Forget();
        }

        private RTCConfiguration CreateRTCConfiguration(List<RTCIceServer> optionalIceServers)
        {
            var iceServers = optionalIceServers;
            iceServers.InsertRange(0, defaultIceServers);

            var configuration = new RTCConfiguration
            {
                iceServers = iceServers.ToArray()
            };

            return configuration;
        }

        private static void HandleHook(string name, Action hook)
        {
            try
            {
                hook.Invoke();
            }
            catch (Exception e)
            {
                Logger.LogError($"Error has occurred at {name}", e);
            }
        }

        private void SendSubscribeRequest(string clientId) => UniTask.Void(async () =>
        {
            if (State != WebSocketState.Open)
            {
                // Not covered by testing due to defensive implementation
                if (Logger.IsDebug())
                {
                    Logger.LogDebug("WebSocket is not connected.");
                }
                return;
            }
            await Send(OmeMessage.CreateSubscribeRequest(clientId));
        });

        private void ReceiveJoinMember(OmeMessage message)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Join: ClientId={message.ClientId}");
            }

            SendSubscribeRequest(message.ClientId);
            onUserJoined.OnNext(message.ClientId);
        }

        private void ReceiveLeaveMember(OmeMessage message)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Leave: ClientId={message.ClientId}");
            }

            CloseConnection(message.ClientId);
            onUserLeft.OnNext(message.ClientId);
        }

        public void AddPublishPcCreateHook(List<Action<string, RTCPeerConnection>> hooks)
            => publishPcCreateHooks.AddRange(hooks);

        public void AddSubscribePcCreateHook(List<Action<string, RTCPeerConnection>> hooks)
            => subscribePcCreateHooks.AddRange(hooks);

        public void AddPublishPcCloseHook(List<Action<string>> hooks)
            => publishPcCloseHooks.AddRange(hooks);

        public void AddSubscribePcCloseHook(List<Action<string>> hooks)
            => subscribePcCloseHooks.AddRange(hooks);

        public async UniTask<GroupListResponse> ListGroupsAsync()
        {
            await Send(OmeMessage.CreateListGroupsRequest());
            await UniTask.WaitUntil(() => groupList != null, cancellationToken: cancellation.Token);
            var result = groupList;
            groupList = null;
            return result;
        }

        public void Connect(string roomName)
        {
            this.roomName = roomName;
            SendPublishRequest(this.roomName);
        }
    }
}
#endif
