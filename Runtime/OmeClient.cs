using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using Extreal.Core.Logging;
using UniRx;

namespace Extreal.Integration.SFU.OME
{
    /// <summary>
    /// Client class for OME connections.
    /// </summary>
    public abstract class OmeClient : DisposableBase
    {
        /// <summary>
        /// <para>Invokes immediately after this client joined a group.</para>
        /// Arg: Client ID of this client.
        /// </summary>
        public IObservable<string> OnJoined => onJoined;
        private readonly Subject<string> onJoined;
        protected void FireOnJoined(string clientId) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnJoined)}: clientId={clientId}");
            }
            onJoined.OnNext(clientId);
        });

        /// <summary>
        /// <para>Invokes immediately after this client leaves a group.</para>
        /// Arg: reason why this client leaves.
        /// </summary>
        public IObservable<Unit> OnLeft => onLeft;
        private readonly Subject<Unit> onLeft;
        protected void FireOnLeft() => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnLeft)}");
            }
            onLeft.OnNext(Unit.Default);
        });

        /// <summary>
        /// <para>Invokes immediately after this client unexpectedly leaves a group.</para>
        /// Arg: reason why this client leaves.
        /// </summary>
        public IObservable<string> OnUnexpectedLeft => onUnexpectedLeft;
        private readonly Subject<string> onUnexpectedLeft;
        protected void FireOnUnexpectedLeft(string reason) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUnexpectedLeft)}: reason={reason}");
            }
            onUnexpectedLeft.OnNext(reason);
        });

        /// <summary>
        /// <para>Invokes immediately after a client joined the same group this client joined.</para>
        /// Arg: ID of the joined client.
        /// </summary>
        public IObservable<string> OnUserJoined => onUserJoined;
        private readonly Subject<string> onUserJoined;
        protected void FireOnUserJoined(string clientId) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUserJoined)}: clientId={clientId}");
            }
            onUserJoined.OnNext(clientId);
        });

        /// <summary>
        /// <para>Invokes immediately after a client left the group this client joined.</para>
        /// Arg: ID of the left client.
        /// </summary>
        public IObservable<string> OnUserLeft => onUserLeft;
        private readonly Subject<string> onUserLeft;
        protected void FireOnUserLeft(string clientId) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUserLeft)}: clientId={clientId}");
            }
            onUserLeft.OnNext(clientId);
        });

        private readonly string serverUrl;

        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(OmeClient));

        /// <summary>
        /// Creates a new OmeClient.
        /// </summary>
        /// <param name="omeConfig">OME config.</param>
        [SuppressMessage("Usage", "CC0022")]
        protected OmeClient(OmeConfig omeConfig)
        {
            serverUrl = omeConfig.ServerUrl;

            onJoined = new Subject<string>().AddTo(disposables);
            onLeft = new Subject<Unit>().AddTo(disposables);
            onUnexpectedLeft = new Subject<string>().AddTo(disposables);
            onUserJoined = new Subject<string>().AddTo(disposables);
            onUserLeft = new Subject<string>().AddTo(disposables);
        }

        /// <inheritdoc/>
        protected sealed override void ReleaseManagedResources()
        {
            DoReleaseManagedResources();
            disposables.Dispose();
        }

        /// <summary>
        /// Releases managed resources in sub class.
        /// </summary>
        protected abstract void DoReleaseManagedResources();

        /// <summary>
        /// Lists groups that currently exist.
        /// </summary>
        /// <returns>List of the groups that currently exist.</returns>
        public async UniTask<List<Group>> ListGroupsAsync()
        {
            var groupList = await DoListGroupsAsync();
            return groupList.Groups.Select(groupResponse => new Group(groupResponse.Name)).ToList();
        }

        /// <summary>
        /// Lists groups that currently exist in sub class.
        /// </summary>
        /// <returns>List of the groups that currently exist.</returns>
        protected abstract UniTask<GroupListResponse> DoListGroupsAsync();

        /// <summary>
        /// Joins to a room.
        /// </summary>
        /// <param name="roomName">Room name to join to.</param>
        public UniTask JoinAsync(string roomName)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Join: RoomName={roomName}, ServerUrl={serverUrl}");
            }

            return DoJoinAsync(roomName);
        }

        /// <summary>
        /// Joins to a room in sub class.
        /// </summary>
        /// <param name="roomName">Room name to join to.</param>
        protected abstract UniTask DoJoinAsync(string roomName);

        /// <summary>
        /// Leaves from joined room.
        /// </summary>
        public abstract UniTask LeaveAsync();
    }
}
