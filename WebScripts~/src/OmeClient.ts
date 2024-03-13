import { waitUntil } from "@extreal-dev/extreal.integration.web.common";
import { GroupListResponse } from "./OmeMessage";
import { OmeWebSocket, PcCreateHook, PcCloseHook } from "./OmeWebSocket";

type OmeConfig = {
    serverUrl: string;
    iceServers: RTCIceServer[];
    isDebug: boolean;
};

type OmeClientCallbacks = {
    onJoined: (clientId: string) => void;
    onLeft: () => void;
    onUnexpectedLeft: (reason: string) => void;
    onUserJoined: (clientId: string) => void;
    onUserLeft: (clientId: string) => void;
    onJoinRetrying: (count: number) => void;
    onJoinRetried: (result: boolean) => void;
    handleGroupList: (groupList: GroupListResponse) => void;
};

class OmeClient {
    private readonly isDebug;

    private readonly omeConfig;
    private readonly callbacks;

    private socket: OmeWebSocket | null = null;
    public localClientId = "";

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

    private getSocket = async () => {
        if (this.socket !== null) {
            if (this.socket.readyState === WebSocket.OPEN) {
                return this.socket;
            }
            this.stopSocket();
        }

        this.socket = new OmeWebSocket(this.omeConfig.serverUrl, this.omeConfig.iceServers, this.isDebug, {
            onJoined: (clientId) => {
                this.callbacks.onJoined(clientId);
                this.localClientId = clientId;
            },
            onLeft: () => {
                this.callbacks.onLeft();
                this.localClientId = "";
            },
            onUnexpectedLeft: (reason) => {
                this.callbacks.onUnexpectedLeft(reason);
                this.localClientId = "";
            },
            onUserJoined: this.callbacks.onUserJoined,
            onUserLeft: this.callbacks.onUserLeft,
            onJoinRetrying: this.callbacks.onJoinRetrying,
            onJoinRetried: this.callbacks.onJoinRetried,
            handleGroupList: this.callbacks.handleGroupList,
        });

        this.socket.addPublishPcCreateHooks(this.publishPcCreateHooks);
        this.socket.addSubscribePcCreateHooks(this.subscribePcCreateHooks);
        this.socket.addPublishPcCloseHooks(this.publishPcCloseHooks);
        this.socket.addSubscribePcCloseHooks(this.subscribePcCloseHooks);

        await waitUntil(
            () => this.socket?.readyState === WebSocket.OPEN || this.socket?.readyState === WebSocket.CLOSED,
            () => false,
        );

        if (this.socket.readyState === WebSocket.CLOSED) {
            throw "Connection failed";
        }

        return this.socket;
    };

    private stopSocket = () => {
        if (this.socket === null) {
            return;
        }
        this.socket.close(1000);
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

    public listGroups = async () => {
        (await this.getSocket()).listGroups();
    };

    public join = async (groupName: string) => {
        (await this.getSocket()).connect(groupName);
    };

    public leave = () => {
        this.stopSocket();
    };
}

export { OmeClient };
