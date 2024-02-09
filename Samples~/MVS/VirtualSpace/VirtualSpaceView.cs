using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Extreal.Integration.SFU.OME.MVS.VirtualSpace
{
    public class VirtualSpaceView : MonoBehaviour
    {
        [SerializeField] private TMP_Text clientIdText;
        [SerializeField] private Button backButton;

        public IObservable<Unit> OnBackButtonClicked => backButton.OnClickAsObservable().TakeUntilDestroy(this);

        public void SetClientId(string clientId) => clientIdText.text = clientId;
    }
}
