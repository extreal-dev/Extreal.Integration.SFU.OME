using System;
using System.Diagnostics.CodeAnalysis;
using Extreal.Core.Common.System;
using Extreal.Integration.SFU.OME.MVS.App;
using UniRx;
using VContainer.Unity;

namespace Extreal.Integration.SFU.OME.MVS.ClientControl
{
    public class ClientControlPresenter : DisposableBase, IInitializable
    {
        private readonly AppState appState;
        private readonly OmeClient omeClient;

        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        [SuppressMessage("CodeCracker", "CC0057")]
        public ClientControlPresenter(AppState appState, OmeClient omeClient)
        {
            this.appState = appState;
            this.omeClient = omeClient;
        }

        public void Initialize()
        {
            omeClient.OnJoined
                .Subscribe(id =>
                {
                    appState.Notify($"Received: {nameof(OmeClient.OnJoined)}{Environment.NewLine}My ID: {id}");
                    appState.SetClientId(id);
                })
                .AddTo(disposables);

            omeClient.OnUnexpectedLeft
                .Subscribe(_ => appState.Notify($"Received: {nameof(OmeClient.OnUnexpectedLeft)}"))
                .AddTo(disposables);

            omeClient.OnLeft
                .Subscribe(_ => appState.Notify($"Received: {nameof(OmeClient.OnLeft)}"))
                .AddTo(disposables);

            omeClient.OnUserJoined
                .Subscribe(id => appState.Notify($"Received: {nameof(OmeClient.OnUserJoined)}{Environment.NewLine}Joining user ID: {id}"))
                .AddTo(disposables);

            omeClient.OnUserLeft
                .Subscribe(id => appState.Notify($"Received: {nameof(OmeClient.OnUserLeft)}{Environment.NewLine}Leaving user ID: {id}"))
                .AddTo(disposables);
        }

        protected override void ReleaseManagedResources() => disposables.Dispose();
    }
}
