using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using Extreal.Core.StageNavigation;
using Extreal.Integration.SFU.OME.MVS.App;
using UniRx;
using VContainer.Unity;

namespace Extreal.Integration.SFU.OME.MVS.OmeControl
{
    public class OmeControlPresenter : DisposableBase, IInitializable
    {
        private readonly StageNavigator<StageName, SceneName> stageNavigator;
        private readonly AppState appState;
        private readonly OmeClient omeClient;

        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();


        public OmeControlPresenter(
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            OmeClient omeClient)
        {
            this.stageNavigator = stageNavigator;
            this.appState = appState;
            this.omeClient = omeClient;
        }

        public void Initialize()
        {
            stageNavigator.OnStageTransitioning
                .Subscribe(_ => StopOmeClientAsync().Forget())
                .AddTo(disposables);

            stageNavigator.OnStageTransitioned
                .Subscribe(_ => StartOmeClientAsync(appState).Forget())
                .AddTo(disposables);

            omeClient.OnJoined
                .Subscribe(appState.SetClientId)
                .AddTo(disposables);

            omeClient.OnLeft
                .Subscribe(_ => appState.SetClientId(string.Empty))
                .AddTo(disposables);

            omeClient.OnUnexpectedLeft
                .Subscribe(_ => appState.SetClientId(string.Empty))
                .AddTo(disposables);
        }

        private async UniTask StartOmeClientAsync(AppState appState)
            => await omeClient.JoinAsync(appState.GroupName);

        private async UniTask StopOmeClientAsync()
            => await omeClient.LeaveAsync();

        protected override void ReleaseManagedResources() => disposables.Dispose();
    }
}
