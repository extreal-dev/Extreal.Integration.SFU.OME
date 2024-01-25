#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using UnityEngine;
using System.Collections.Generic;
using Unity.WebRTC;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Extreal.Core.Logging;

namespace Extreal.Integration.SFU.OME
{
    [Serializable]
    public class OmeRTCSessionDescription : ISerializationCallbackReceiver
    {
        [SerializeField] private string type;
        [SerializeField] private string sdp;
        public RTCSessionDescription RtcSessionDescription => rtcSessionDescription;
        private RTCSessionDescription rtcSessionDescription;

        public OmeRTCSessionDescription(RTCSessionDescription rtcSessionDescription)
            => this.rtcSessionDescription = rtcSessionDescription;

        public void OnBeforeSerialize()
        {
            if (rtcSessionDescription.type is RTCSdpType.Offer)
            {
                type = "offer";
            }
            else if (rtcSessionDescription.type is RTCSdpType.Answer)
            {
                type = "answer";
            }
            else if (rtcSessionDescription.type is RTCSdpType.Pranswer)
            {
                type = "pranswer";
            }
            else if (rtcSessionDescription.type is RTCSdpType.Rollback)
            {
                type = "rollback";
            }
            sdp = rtcSessionDescription.sdp;
        }

        public void OnAfterDeserialize()
        {
            if (type == "offer")
            {
                rtcSessionDescription.type = RTCSdpType.Offer;
            }
            else if (type == "answer")
            {
                rtcSessionDescription.type = RTCSdpType.Answer;
            }
            else if (type == "rollback")
            {
                rtcSessionDescription.type = RTCSdpType.Rollback;
            }
            rtcSessionDescription.sdp = sdp;
        }
    }

    [Serializable]
    public class OmeIceCandidate : ISerializationCallbackReceiver
    {
        [SerializeField] private string candidate;
        [SerializeField] private string sdpMid;
        [SerializeField] private int sdpMLineIndex;
        public RTCIceCandidateInit RtcIceCandidateInit => rtcIceCandidateInit;
        private RTCIceCandidateInit rtcIceCandidateInit;

        public OmeIceCandidate(RTCIceCandidateInit rtcIceCandidateInit)
            => this.rtcIceCandidateInit = rtcIceCandidateInit;

        public void OnBeforeSerialize()
        {
            candidate = RtcIceCandidateInit.candidate;
            sdpMid = RtcIceCandidateInit.sdpMid;
            if (RtcIceCandidateInit.sdpMLineIndex != null)
            {
                sdpMLineIndex = (int)RtcIceCandidateInit.sdpMLineIndex;
            }
        }

        public void OnAfterDeserialize()
            => rtcIceCandidateInit = new RTCIceCandidateInit
            {
                candidate = candidate,
                sdpMid = sdpMid,
                sdpMLineIndex = sdpMLineIndex
            };
    }

    [Serializable]
    public class OmeIceServer : ISerializationCallbackReceiver
    {
        [SerializeField] private string username;
        [SerializeField] private string credential;
        [SerializeField] private string[] urls;
        public RTCIceServer RtcIceServer => rtcIceServer;
        private RTCIceServer rtcIceServer;

        public OmeIceServer(RTCIceServer rtcIceServer)
            => this.rtcIceServer = rtcIceServer;

        public void OnBeforeSerialize()
        {
            username = RtcIceServer.username;
            credential = RtcIceServer.credential;
            urls = RtcIceServer.urls;
        }

        public void OnAfterDeserialize()
            => rtcIceServer = new RTCIceServer
            {
                username = username,
                credential = credential,
                urls = urls
            };
    }

    [Serializable]
    public class OmeCommand
    {
        public int Id => id;
        [SerializeField] private int id;

        public string Command => command;
        [SerializeField] private string command;

        [SerializeField, SuppressMessage("Usage", "IDE0052")] private string groupName;

        public string ClientId => clientId;
        [SerializeField] private string clientId;

        public string Error => error;
        [SerializeField] private string error;

        public OmeRTCSessionDescription Sdp => sdp;
        [SerializeField] private OmeRTCSessionDescription sdp;

        public OmeIceCandidate[] Candidates => candidates;
        [SerializeField] private OmeIceCandidate[] candidates;

        public OmeIceServer[] IceServers => iceServers;
        [SerializeField] private OmeIceServer[] iceServers;

        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(OmeCommand));

        public RTCSessionDescription GetSdp()
            => Sdp.RtcSessionDescription;

        public List<RTCIceServer> GetIceServers()
            => IceServers.Select(iceServer => iceServer.RtcIceServer).ToList();

        public string ToJson()
            => JsonUtility.ToJson(this);

        public byte[] ToJsonBytes()
            => System.Text.Encoding.UTF8.GetBytes(ToJson());

        public static OmeCommand FromJsonBytes(byte[] jsonBytes)
            => JsonUtility.FromJson<OmeCommand>(System.Text.Encoding.UTF8.GetString(jsonBytes));

        public static byte[] CreateAnswerMessage(int id, RTCSessionDescription answerSdp)
        {
            const string commandName = "answer";
            var command = new OmeCommand
            {
                command = commandName,
                id = id,
                sdp = new OmeRTCSessionDescription(answerSdp),
            };
            command.Log(commandName);

            return command.ToJsonBytes();
        }

        public static byte[] CreateJoinMessage(int id)
        {
            const string commandName = "join";
            var command = new OmeCommand
            {
                command = commandName,
                id = id,
            };
            command.Log(commandName);

            return command.ToJsonBytes();
        }

        public static byte[] CreateIceCandidate(int id, RTCIceCandidate rtcIceCandidate)
        {
            const string commandName = "candidate";
            var rtcIceCandidateInit = new RTCIceCandidateInit
            {
                candidate = rtcIceCandidate.Candidate,
                sdpMid = rtcIceCandidate.SdpMid,
                sdpMLineIndex = rtcIceCandidate.SdpMLineIndex,
            };
            var command = new OmeCommand
            {
                command = commandName,
                id = id,
                candidates = new OmeIceCandidate[] {
                    new OmeIceCandidate(rtcIceCandidateInit),
                },
            };
            command.Log(commandName);

            return command.ToJsonBytes();
        }

        public static byte[] CreatePublishRequest(string groupName)
        {
            const string commandName = "publish";
            var command = new OmeCommand
            {
                command = commandName,
                groupName = groupName,
            };
            command.Log(commandName);

            return command.ToJsonBytes();
        }

        public static byte[] CreateSubscribeRequest(string clientId)
        {
            const string commandName = "subscribe";
            var command = new OmeCommand
            {
                command = commandName,
                clientId = clientId,
            };
            command.Log(commandName);

            return command.ToJsonBytes();
        }

        private void Log(string commandName)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Create {commandName} message: {ToJson()}");
            }
        }
    }
}
#endif
