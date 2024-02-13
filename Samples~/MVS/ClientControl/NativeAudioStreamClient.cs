#if !UNITY_WEBGL || UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Extreal.Core.Logging;
using Unity.WebRTC;
using UnityEngine;

namespace Extreal.Integration.SFU.OME.MVS.ClientControl
{
    public class NativeAudioStreamClient : AudioStreamClient
    {
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(NativeAudioStreamClient));

        private (AudioSource inAudio, AudioStreamTrack inTrack, MediaStream inStream) inResource;
        private readonly Dictionary<string, (AudioSource outAudio, MediaStream outStream)> outResources = new Dictionary<string, (AudioSource, MediaStream)>();

        private readonly Transform audioSourceContainer;

        public NativeAudioStreamClient(NativeOmeClient omeClient)
        {
            audioSourceContainer = new GameObject(nameof(audioSourceContainer)).transform;
            Object.DontDestroyOnLoad(audioSourceContainer);

            omeClient.AddPublishPcCreateHook(CreatePublishPc);
            omeClient.AddSubscribePcCreateHook(CreateSubscribePc);
            omeClient.AddPublishPcCloseHook(ClosePublishPc);
            omeClient.AddSubscribePcCloseHook(CloseSubscribePc);
        }

        private void CreatePublishPc(string clientId, OmeRTCPeerConnection pc)
        {
            inResource.inAudio = new GameObject("InAudio").AddComponent<AudioSource>();
            inResource.inAudio.transform.SetParent(audioSourceContainer);

            inResource.inTrack = new AudioStreamTrack(inResource.inAudio)
            {
                Loopback = false
            };
            inResource.inStream = new MediaStream();
            pc.AddTrack(inResource.inTrack, inResource.inStream);
        }

        private void CreateSubscribePc(string clientId, OmeRTCPeerConnection pc) =>
            pc.OnTrack = (RTCTrackEvent e) =>
                {
                    if (Logger.IsDebug())
                    {
                        Logger.LogDebug($"OnTrack: Kind={e.Track.Kind}");
                    }
                };

        private void ClosePublishPc(string clientId)
        {
            if (inResource.inAudio != null)
            {
                inResource.inAudio.Stop();
                Object.Destroy(inResource.inAudio.gameObject);
            }
            if (inResource.inTrack != null)
            {
                inResource.inTrack.Dispose();
            }
            if (inResource.inStream != null)
            {
                inResource.inStream.GetTracks().ToList().ForEach(track => track.Stop());
                inResource.inStream.Dispose();
            }
            inResource = (default, default, default);
        }

        private void CloseSubscribePc(string clientId)
        {
        }

        protected override void DoReleaseManagedResources()
        {
            if (audioSourceContainer != null && audioSourceContainer.gameObject != null)
            {
                Object.Destroy(audioSourceContainer.gameObject);
            }
        }
    }
}
#endif
