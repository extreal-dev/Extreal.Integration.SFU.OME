import { OmeWebSocket, PcCreateHook, PcCloseHook } from "./OmeWebSocket";
import { v4 as uuidv4 } from "uuid";

type OmeConfig = {
    serverUrl: string;
    iceServers: RTCIceServer[];
    isDebug: boolean;
};

type OmeClientCallbacks = {
    onJoined: (streamName: string) => void;
    onLeft: (reason: string) => void;
    onUserJoined: (streamName: string) => void;
    onUserLeft: (streamName: string) => void;
};

class OmeClient {
    private readonly isDebug;

    private readonly omeConfig;
    private readonly callbacks;

    private socket: OmeWebSocket | null = null;
    private userName = uuidv4();
    public localStreamName = "";

    private publishPcCreateHooks: PcCreateHook[] = [];
    private subscribePcCreateHooks: PcCreateHook[] = [];
    private publishPcCloseHooks: PcCloseHook[] = [];
    private subscribePcCloseHooks: PcCloseHook[] = [];

    constructor(omeConfig: OmeConfig, callbacks: OmeClientCallbacks) {
        this.omeConfig = omeConfig;
        this.isDebug = omeConfig.isDebug;
        this.callbacks = callbacks;
    }

    public releaseManagedResources = () => {
        if (this.socket) {
            this.socket.releaseManagedResources();
        }
    };

    private getSocket = (roomName: string) => {
        if (this.socket !== null) {
            if (this.socket.readyState === WebSocket.OPEN) {
                return this.socket;
            }
            this.stopSocket();
        }

        this.socket = new OmeWebSocket(
            this.omeConfig.serverUrl,
            this.omeConfig.iceServers,
            roomName,
            this.userName,
            this.isDebug,
            {
                onJoined: (streamName) => {
                    this.callbacks.onJoined(streamName);
                    this.localStreamName = streamName;
                },
                onLeft: (reason) => {
                    this.callbacks.onLeft(reason);
                    this.localStreamName = "";
                },
                onUserJoined: this.callbacks.onUserJoined,
                onUserLeft: this.callbacks.onUserLeft,
            },
        );

        this.socket.addPublishPcCreateHooks(this.publishPcCreateHooks);
        this.socket.addSubscribePcCreateHooks(this.subscribePcCreateHooks);
        this.socket.addPublishPcCloseHooks(this.publishPcCloseHooks);
        this.socket.addSubscribePcCloseHooks(this.subscribePcCloseHooks);
    };

    private stopSocket = () => {
        if (this.socket === null) {
            return;
        }
        this.socket.close();
        this.socket = null;
    };

    public addPublishPcCreateHook = (hook: PcCreateHook) => {
        this.publishPcCreateHooks.push(hook);
    };

    public addSubscribePcCreateHook = (hook: PcCreateHook) => {
        this.subscribePcCreateHooks.push(hook);
    };

    public addPublishPcCloseHook = (hook: PcCloseHook) => {
        this.publishPcCloseHooks.push(hook);
    };

    public addSubscribePcCloseHook = (hook: PcCloseHook) => {
        this.subscribePcCloseHooks.push(hook);
    };

    public connect = (roomName: string) => {
        this.getSocket(roomName);
    };

    public disconnect = () => {
        this.stopSocket();
    };
}

export { OmeClient };
