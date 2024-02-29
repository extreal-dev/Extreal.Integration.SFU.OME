using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using Extreal.Core.StageNavigation;
using Extreal.Integration.SFU.OME.MVS.App;
using UniRx;
using VContainer.Unity;

namespace Extreal.Integration.SFU.OME.MVS.VirtualSpace
{
    public class VirtualSpacePresenter : DisposableBase, IInitializable
    {
        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private readonly StageNavigator<StageName, SceneName> stageNavigator;
        private readonly AppState appState;
        private readonly VirtualSpaceView virtualSpaceView;

        public VirtualSpacePresenter(
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            VirtualSpaceView virtualSpaceView)
        {
            this.stageNavigator = stageNavigator;
            this.appState = appState;
            this.virtualSpaceView = virtualSpaceView;
        }

        public void Initialize()
        {
            virtualSpaceView.OnBackButtonClicked
                .Subscribe(_ => stageNavigator.ReplaceAsync(StageName.GroupSelectionStage).Forget())
                .AddTo(disposables);

            stageNavigator.OnStageTransitioned
                .Subscribe(_ => virtualSpaceView.SetClientId($"My ID: "))
                .AddTo(disposables);

            appState.OnClientIdSet
                .Subscribe(id => virtualSpaceView.SetClientId($"My ID: {id}"))
                .AddTo(disposables);
        }

        protected override void ReleaseManagedResources() => disposables.Dispose();
    }
}
