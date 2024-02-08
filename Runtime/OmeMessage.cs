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
            sdp = rtcSessionDescription.sdp;
        }

        public void OnAfterDeserialize()
        {
            rtcSessionDescription.type = RTCSdpType.Offer;
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
    public class OmeMessage
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

        public GroupListResponse GroupListResponse => groupListResponse;
        [SerializeField] private GroupListResponse groupListResponse;

        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(OmeMessage));

        public RTCSessionDescription GetSdp()
            => Sdp.RtcSessionDescription;

        public List<RTCIceServer> GetIceServers()
            => IceServers.Select(iceServer => iceServer.RtcIceServer).ToList();

        public string ToJson()
            => JsonUtility.ToJson(this);

        public byte[] ToJsonBytes()
            => System.Text.Encoding.UTF8.GetBytes(ToJson());

        public static OmeMessage FromJsonBytes(byte[] jsonBytes)
            => JsonUtility.FromJson<OmeMessage>(System.Text.Encoding.UTF8.GetString(jsonBytes));

        public static byte[] CreateAnswerMessage(int id, RTCSessionDescription answerSdp)
        {
            const string commandName = "answer";
            var message = new OmeMessage
            {
                command = commandName,
                id = id,
                sdp = new OmeRTCSessionDescription(answerSdp),
            };
            message.Log();

            return message.ToJsonBytes();
        }

        public static byte[] CreateJoinMessage(int id)
        {
            const string commandName = "join";
            var message = new OmeMessage
            {
                command = commandName,
                id = id,
            };
            message.Log();

            return message.ToJsonBytes();
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
            var message = new OmeMessage
            {
                command = commandName,
                id = id,
                candidates = new OmeIceCandidate[] {
                    new OmeIceCandidate(rtcIceCandidateInit),
                },
            };
            message.Log();

            return message.ToJsonBytes();
        }

        public static byte[] CreateListGroupsRequest()
        {
            const string commandName = "list groups";
            var message = new OmeMessage
            {
                command = commandName,
            };
            message.Log();

            return message.ToJsonBytes();
        }

        public static byte[] CreatePublishRequest(string groupName)
        {
            const string commandName = "publish";
            var message = new OmeMessage
            {
                command = commandName,
                groupName = groupName,
            };
            message.Log();

            return message.ToJsonBytes();
        }

        public static byte[] CreateSubscribeRequest(string clientId)
        {
            const string commandName = "subscribe";
            var message = new OmeMessage
            {
                command = commandName,
                clientId = clientId,
            };
            message.Log();

            return message.ToJsonBytes();
        }

        private void Log()
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Create {command} message: {ToJson()}");
            }
        }
    }
}
#endif
