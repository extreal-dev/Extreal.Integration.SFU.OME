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

namespace Extreal.Integration.SFU.OME
{
    public class OmeWebSocket : WebSocket, IDisposable
    {
        public IObservable<string> OnJoined => onJoined;
        private readonly Subject<string> onJoined;

        public IObservable<string> OnLeft => onLeft;
        private readonly Subject<string> onLeft;

        public IObservable<string> OnUserJoined => onUserJoined;
        private readonly Subject<string> onUserJoined;

        public IObservable<string> OnUserLeft => onUserLeft;
        private readonly Subject<string> onUserLeft;

        private bool isConnected;
        private string roomName;
        private readonly List<RTCIceServer> defaultIceServers;
        private readonly string userName;
        private string localClientId;

        private readonly List<Action<string, OmeRTCPeerConnection>> publishPcCreateHooks = new List<Action<string, OmeRTCPeerConnection>>();
        private readonly List<Action<string, OmeRTCPeerConnection>> subscribePcCreateHooks = new List<Action<string, OmeRTCPeerConnection>>();
        private readonly List<Action<string>> publishPcCloseHooks = new List<Action<string>>();
        private readonly List<Action<string>> subscribePcCloseHooks = new List<Action<string>>();

        private OmeRTCPeerConnection publishConnection;
        private readonly Dictionary<string, OmeRTCPeerConnection> subscribeConnections = new Dictionary<string, OmeRTCPeerConnection>();

        private readonly Dictionary<string, int> subscribeRetryCounts = new Dictionary<string, int>();
        private const int MaxSubscribeRetries = 20;
        private const float SubscribeRetryInterval = 0.5f;

        private readonly SafeDisposer safeDisposer;
        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(OmeWebSocket));

        [SuppressMessage("Usage", "CC0022")]
        public OmeWebSocket(string url, List<RTCIceServer> defaultIceServers, string userName) : base(url)
        {
            safeDisposer = new SafeDisposer(this, ReleaseManagedResources);

            onJoined = new Subject<string>().AddTo(disposables);
            onLeft = new Subject<string>().AddTo(disposables);
            onUserJoined = new Subject<string>().AddTo(disposables);
            onUserLeft = new Subject<string>().AddTo(disposables);

            OnOpen += OnOpenEvent;
            OnClose += OnCloseEvent;
            OnError += OnErrorEvent;
            OnMessage += OnMessageEvent;

            this.userName = userName;
            this.defaultIceServers = defaultIceServers;

#if !UNITY_WEBGL || UNITY_EDITOR
            Observable.EveryUpdate()
                .Subscribe(_ => DispatchMessageQueue())
                .AddTo(disposables);
#endif
        }

        ~OmeWebSocket()
            => safeDisposer.DisposeByFinalizer();

        public void Dispose()
            => safeDisposer.Dispose();

        private void ReleaseManagedResources()
        {
            OnOpen -= OnOpenEvent;
            OnClose -= OnCloseEvent;
            OnError -= OnErrorEvent;
            OnMessage -= OnMessageEvent;

            CloseAllRTCConnections(WebSocketCloseCode.Normal.ToString());
            UniTask.Void(async () => await Close());
            disposables.Dispose();
        }

        private void OnOpenEvent()
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug("OnOpen");
            }

            isConnected = true;
            SendPublishRequest(roomName);
        }

        private void SendPublishRequest(string roomName)
        {
            if (!isConnected)
            {
                if (Logger.IsDebug())
                {
                    Logger.LogDebug("WebSocket is not connected.");
                }
                return;
            }
            Send(OmeCommand.CreatePublishRequest(roomName));
        }

        private void OnCloseEvent(WebSocketCloseCode closeCode)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"OnClose: CloseCode=${closeCode}");
            }

            isConnected = false;
            CloseAllRTCConnections(closeCode.ToString());
        }

        private void OnErrorEvent(string e) => UniTask.Void(async () =>
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"OnError: {e}");
            }

            CloseAllRTCConnections(e);
            await Close();
        });

        private void CloseAllRTCConnections(string reason)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Left Room: roomName={roomName}");
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

            onLeft.OnNext(reason);
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
            var command = OmeCommand.FromJsonBytes(bytes);
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"OnMessage: {command.ToJson()}");
            }

            if (command.Command == "publishOffer")
            {
                ReceivePublishOffer(command);
            }
            else if (command.Command == "subscribeOffer")
            {
                ReceiveSubscribeOffer(command);
            }
            else if (command.Command == "join")
            {
                ReceiveJoinMember(command);
            }
            else if (command.Command == "leave")
            {
                ReceiveLeaveMember(command);
            }
        }

        private void ReceivePublishOffer(OmeCommand command)
        {
            var isSetLocalCandidate = false;

            var configuration = CreateRTCConfiguration(command.GetIceServers());
            var pc = new OmeRTCPeerConnection(ref configuration);
            pc.SetCreateAnswerCompletion(answer => UniTask.Void(async () =>
            {
                var messageBytes = OmeCommand.CreateAnswerMessage(command.Id, answer);
                await Send(messageBytes);
            }));
            pc.SetIceCandidateCallback(candidate => UniTask.Void(async () =>
            {
                if (!isSetLocalCandidate)
                {
                    var sdpMid = candidate.SdpMid;
                    foreach (var c in command.Candidates)
                    {
                        var candidateInit = c.RtcIceCandidateInit;
                        candidateInit.sdpMid = sdpMid;
                        var rtcIceCandidate = new RTCIceCandidate(candidateInit);
                        pc.AddIceCandidate(rtcIceCandidate);
                    }
                    isSetLocalCandidate = true;
                }
                var messageBytes = OmeCommand.CreateIceCandidate(command.Id, candidate);
                await Send(messageBytes);
            }));
            pc.SetConnectedCallback(() => UniTask.Void(async () =>
            {
                if (Logger.IsDebug())
                {
                    Logger.LogDebug($"Joined Room: roomName={roomName}");
                }

                await Send(OmeCommand.CreateJoinMessage(command.Id));
                onJoined.OnNext(command.ClientId);
            }));

            publishPcCreateHooks.ForEach(hook => HandleHook(nameof(ReceivePublishOffer), () => hook.Invoke(command.ClientId, pc)));

            localClientId = command.ClientId;
            publishConnection = pc;
            pc.CreateAnswerSdpAsync(command.GetSdp()).Forget();
        }

        private void ReceiveSubscribeOffer(OmeCommand command)
        {
            var currentRetryCount = subscribeRetryCounts.ContainsKey(command.ClientId) ? subscribeRetryCounts[command.ClientId] : 0;

            if (!string.IsNullOrEmpty(command.Error))
            {
                if (command.Error == "Cannot create offer")
                {
                    if (currentRetryCount < MaxSubscribeRetries)
                    {
                        UniTask.Void(async () =>
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(SubscribeRetryInterval));
                            SendSubscribeRequest(command.ClientId);
                            subscribeRetryCounts[command.ClientId] = currentRetryCount + 1;
                        });
                    }
                    else
                    {
                        if (Logger.IsError())
                        {
                            Logger.LogError($"Maximum retryCount reached: {command.ClientId}");
                        }
                        subscribeRetryCounts.Remove(command.ClientId);
                    }
                }
                else
                {
                    if (Logger.IsError())
                    {
                        Logger.LogError($"Subscribe error: {command.Error}");
                    }
                    subscribeRetryCounts.Remove(command.ClientId);
                }
                return;
            }
            else
            {
                // エラーではないが，SDPがない場合は何もしない
                if (Logger.IsDebug())
                {
                    Logger.LogDebug($"SubscribeOfferEvent: Id={command.Id}, Sdp={command.Sdp.RtcSessionDescription.sdp}");
                }

                if (command.Id == 0)
                {
                    return;
                }
            }
            subscribeRetryCounts.Remove(command.ClientId);

            var isSetLocalCandidate = false;

            var configuration = CreateRTCConfiguration(command.GetIceServers());
            var pc = new OmeRTCPeerConnection(ref configuration);
            pc.SetCreateAnswerCompletion(answer => UniTask.Void(async () =>
            {
                var messageBytes = OmeCommand.CreateAnswerMessage(command.Id, answer);
                await Send(messageBytes);
            }));
            pc.SetIceCandidateCallback(candidate => UniTask.Void(async () =>
            {
                if (!isSetLocalCandidate)
                {
                    var sdpMid = candidate.SdpMid;
                    foreach (var c in command.Candidates)
                    {
                        var candidateInit = c.RtcIceCandidateInit;
                        candidateInit.sdpMid = sdpMid;
                        var rtcIceCandidate = new RTCIceCandidate(candidateInit);
                        pc.AddIceCandidate(rtcIceCandidate);
                    }
                    isSetLocalCandidate = true;
                }
                var msgBytes = OmeCommand.CreateIceCandidate(command.Id, candidate);
                await Send(msgBytes);
            }));

            subscribePcCreateHooks.ForEach(hook => HandleHook(nameof(ReceiveSubscribeOffer), () => hook.Invoke(command.ClientId, pc)));

            subscribeConnections[command.ClientId] = pc;
            pc.CreateAnswerSdpAsync(command.GetSdp()).Forget();
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

        private void SendSubscribeRequest(string clientId)
        {
            if (!isConnected)
            {
                if (Logger.IsDebug())
                {
                    Logger.LogDebug("WebSocket is not connected.");
                }
                return;
            }
            Send(OmeCommand.CreateSubscribeRequest(clientId));
        }

        private void ReceiveJoinMember(OmeCommand command)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Join: ClientId={command.ClientId}");
            }

            SendSubscribeRequest(command.ClientId);
            onUserJoined.OnNext(command.ClientId);
        }

        private void ReceiveLeaveMember(OmeCommand command)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Leave: ClientId={command.ClientId}");
            }

            CloseConnection(command.ClientId);
            onUserLeft.OnNext(command.ClientId);
        }

        public void AddPublishPcCreateHook(List<Action<string, OmeRTCPeerConnection>> hooks)
            => publishPcCreateHooks.AddRange(hooks);

        public void AddSubscribePcCreateHook(List<Action<string, OmeRTCPeerConnection>> hooks)
            => subscribePcCreateHooks.AddRange(hooks);

        public void AddPublishPcCloseHook(List<Action<string>> hooks)
            => publishPcCloseHooks.AddRange(hooks);

        public void AddSubscribePcCloseHook(List<Action<string>> hooks)
            => subscribePcCloseHooks.AddRange(hooks);

        public async UniTask ConnectAsync(string roomName)
        {
            this.roomName = roomName;
            await Connect();
        }
    }
}
#endif
