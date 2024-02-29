import { OmeClientProvider } from "@extreal-dev/Extreal.Integration.SFU.OME";

class InResource {
    public inStream: MediaStream | undefined;
    public inTrack: MediaStreamTrack | undefined;
}

class AudioStreamClient {
    private readonly label: string = "sample";
    private readonly isDebug: boolean;
    private readonly getOmeClient: OmeClientProvider;

    private inResource: InResource | undefined;

    constructor(getOmeClient: OmeClientProvider) {
        this.isDebug = true;
        this.getOmeClient = getOmeClient;
        this.getOmeClient().addPublishPcCreateHook(this.createPublishPc);
        this.getOmeClient().addSubscribePcCreateHook(this.createSubscribePc);
        this.getOmeClient().addPublishPcCloseHook(this.closePublishPc);
        this.getOmeClient().addSubscribePcCloseHook(this.closeSubscribePc);
    }

    private createPublishPc = (clientId: string, pc: RTCPeerConnection) => {
        this.inResource = new InResource();

        const audioContext = new AudioContext();
        const gainNode = audioContext.createGain();
        gainNode.gain.setValueAtTime(0, audioContext.currentTime);

        const destination = audioContext.createMediaStreamDestination();
        gainNode.connect(destination);

        const inStream = destination.stream;
        const inTrack = inStream.getAudioTracks()[0];
        this.inResource.inStream = inStream;
        this.inResource.inTrack = inTrack;

        pc.addTrack(this.inResource.inTrack, this.inResource.inStream);
    };

    private createSubscribePc = (clientId: string, pc: RTCPeerConnection) => {
        pc.addEventListener("track", (event) => {
            if (this.isDebug) {
                console.log(`OnTrack: Kind=${event.track.kind}`);
            }
        });
    };

    private closePublishPc = (clientId: string) => (this.inResource = undefined);

    private closeSubscribePc = (clientId: string) => {};
}

export { AudioStreamClient };
