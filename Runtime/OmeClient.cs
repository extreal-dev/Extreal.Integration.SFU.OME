using System;
using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using Extreal.Core.Logging;
using UniRx;

namespace Extreal.Integration.SFU.OME
{
    public abstract class OmeClient : DisposableBase
    {
        public IObservable<string> OnJoined => onJoined;
        private readonly Subject<string> onJoined;
        protected void FireOnJoined(string streamName) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnJoined)}: streamName={streamName}");
            }
            onJoined.OnNext(streamName);
        });

        public IObservable<string> OnLeft => onLeft;
        private readonly Subject<string> onLeft;
        protected void FireOnLeft(string reason) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnLeft)}: reason={reason}");
            }
            onLeft.OnNext(reason);
        });

        public IObservable<string> OnUserJoined => onUserJoined;
        private readonly Subject<string> onUserJoined;
        protected void FireOnUserJoined(string streamName) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUserJoined)}: streamName={streamName}");
            }
            onUserJoined.OnNext(streamName);
        });

        public IObservable<string> OnUserLeft => onUserLeft;
        private readonly Subject<string> onUserLeft;
        protected void FireOnUserLeft(string streamName) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUserLeft)}: streamName={streamName}");
            }
            onUserLeft.OnNext(streamName);
        });

        private readonly string serverUrl;

        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(OmeClient));

        [SuppressMessage("Usage", "CC0022")]
        protected OmeClient(OmeConfig omeConfig)
        {
            serverUrl = omeConfig.ServerUrl;

            onJoined = new Subject<string>().AddTo(disposables);
            onLeft = new Subject<string>().AddTo(disposables);
            onUserJoined = new Subject<string>().AddTo(disposables);
            onUserLeft = new Subject<string>().AddTo(disposables);
        }

        protected sealed override void ReleaseManagedResources()
        {
            DoReleaseManagedResources();
            disposables.Dispose();
        }

        protected abstract void DoReleaseManagedResources();

        public UniTask ConnectAsync(string roomName)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Connect: RoomName={roomName}, ServerUrl={serverUrl}");
            }

            return DoConnectAsync(roomName);
        }

        protected abstract UniTask DoConnectAsync(string roomName);

        public abstract UniTask DisconnectAsync();
    }
}
