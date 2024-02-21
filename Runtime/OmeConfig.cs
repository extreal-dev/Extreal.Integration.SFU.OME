using System.Collections.Generic;

namespace Extreal.Integration.SFU.OME
{
    /// <summary>
    /// Class that holds OME configuration.
    /// </summary>
    public class OmeConfig
    {
        /// <summary>
        /// URL of the signaling server.
        /// </summary>
        public string ServerUrl { get; }

        /// <summary>
        /// Ice server configurations.
        /// </summary>
        public List<IceServerConfig> IceServerConfigs { get; }

        /// <summary>
        /// Creates a new OME configuration.
        /// </summary>
        /// <param name="serverUrl">URL of the signaling server</param>
        /// <param name="iceServerConfigs">Ice server configurations</param>
        public OmeConfig(string serverUrl, List<IceServerConfig> iceServerConfigs = default)
        {
            ServerUrl = serverUrl;
            IceServerConfigs = iceServerConfigs ?? new List<IceServerConfig>();
        }
    }

    /// <summary>
    /// Class that holds ICE server configuration (such as a STUN or TURN server).
    /// </summary>
    public class IceServerConfig
    {
        /// <summary>
        /// ICE server URLs.
        /// </summary>
        public List<string> Urls { get; }

        /// <summary>
        /// Username for TURN server.
        /// </summary>
        public string UserName { get; }

        /// <summary>
        /// Credential for TURN server.
        /// </summary>
        public string Credential { get; }

        /// <summary>
        /// Creates a new Ice server configuration.
        /// </summary>
        /// <param name="urls">ICE server URLs</param>
        /// <param name="username">Username for TURN server</param>
        /// <param name="credential">Credential for TURN server</param>
        public IceServerConfig(List<string> urls, string username = "", string credential = "")
        {
            Urls = urls;
            UserName = username;
            Credential = credential;
        }
    }
}
