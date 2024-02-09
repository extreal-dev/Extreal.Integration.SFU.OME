using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Extreal.Core.Common.System;
using Extreal.Integration.SFU.OME.MVS.App;
using UniRx;
using VContainer.Unity;

namespace Extreal.Integration.SFU.OME.MVS.NotificationControl
{
    public class NotificationControlPresenter : DisposableBase, IInitializable
    {
        private readonly AppState appState;
        private readonly NotificationControlView notificationControlView;

        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private readonly Queue<string> notificationQueue = new Queue<string>();
        private bool isShown;

        public NotificationControlPresenter(
            AppState appState,
            NotificationControlView notificationControlView)
        {
            this.appState = appState;
            this.notificationControlView = notificationControlView;
        }

        public void Initialize()
        {
            appState.OnNotificationReceived
                .Subscribe(OnNotificationReceivedHandler)
                .AddTo(disposables);

            notificationControlView.OnBackButtonClicked
                .Subscribe(_ => OnBackButtonClickedHandler())
                .AddTo(disposables);
        }

        public void OnNotificationReceivedHandler(string notification)
        {
            if (isShown)
            {
                notificationQueue.Enqueue(notification);
            }
            else
            {
                notificationControlView.Show(notification);
            }
            isShown = true;
        }

        public void OnBackButtonClickedHandler()
        {
            if (notificationQueue.Count == 0)
            {
                notificationControlView.Hide();
                isShown = false;
            }
            else
            {
                var notification = notificationQueue.Dequeue();
                notificationControlView.Show(notification);
            }
        }

        protected override void ReleaseManagedResources() => disposables.Dispose();
    }
}
