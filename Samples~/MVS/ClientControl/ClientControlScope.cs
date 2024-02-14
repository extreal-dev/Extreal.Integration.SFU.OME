using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

namespace Extreal.Integration.SFU.OME.MVS.ClientControl
{
    public class ClientControlScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var omeConfig = new OmeConfig(
                "ws://localhost:3000",
                new List<IceServerConfig>
                {
                    new IceServerConfig(new List<string>
                    {
                        "stun:stun.l.google.com:19302",
                        "stun:stun1.l.google.com:19302",
                        "stun:stun2.l.google.com:19302",
                        "stun:stun3.l.google.com:19302",
                        "stun:stun4.l.google.com:19302"
                    }, "test-name", "test-credential")
                });

            var omeClient = OmeClientProvider.Provide(omeConfig);
            builder.RegisterComponent(omeClient);

#if !UNITY_WEBGL || UNITY_EDITOR
            NativeFailureClient.NativeFailureHook(omeClient as NativeOmeClient);
            builder.RegisterComponent<AudioStreamClient>(new NativeAudioStreamClient(omeClient as NativeOmeClient));
            NativeFailureClient.NativeFailureHook(omeClient as NativeOmeClient);
#else
            WebGLDummyClient.DummyHookSet();
            builder.RegisterComponent<AudioStreamClient>(new WebGLAudioStreamClient());
            WebGLDummyClient.DummyHookSet();
#endif

            builder.RegisterEntryPoint<ClientControlPresenter>();
        }
    }
}
