import { isAsync } from "@extreal-dev/extreal.integration.web.common";
import { GroupListResponse, OmeMessage } from "./OmeMessage";
import { OmeRTCPeerConnection } from "./OmeRTCPeerConnection";

type OmeWebSocketCallbacks = {
    onJoined: (clientId: string) => void;
    onLeft: () => void;
    onUnexpectedLeft: (reason: string) => void;
    onUserJoined: (clientId: string) => void;
    onUserLeft: (clientId: string) => void;
    handleGroupList: (groupList: GroupListResponse) => void;
};

type PcCreateHook = (clientId: string, pc: RTCPeerConnection) => void | Promise<void>;
type PcCloseHook = (clientId: string) => void | Promise<void>;

class OmeWebSocket extends WebSocket {
    private defaultIceServers;
    private isDebug;
    private callbacks;

    private isConnected = false;
    private groupName = "";
    private localClientId = "";

    private publishPcCreateHooks: PcCreateHook[] = [];
    private subscribePcCreateHooks: PcCreateHook[] = [];
    private publishPcCloseHooks: PcCloseHook[] = [];
    private subscribePcCloseHooks: PcCloseHook[] = [];

    private publishConnection: OmeRTCPeerConnection | null = null;
    private subscribeConnections = new Map<string, OmeRTCPeerConnection>();

    private subscribeRetryCounts = new Map<string, number>();
    private readonly MaxSubscribeRetries = 20;
    private readonly SubscribeRetryInterval = 0.5;

    constructor(url: string, defaultIceServers: RTCIceServer[], isDebug: boolean, callbacks: OmeWebSocketCallbacks) {
        super(url);
        this.defaultIceServers = defaultIceServers;
        this.isDebug = isDebug;
        this.callbacks = callbacks;

        this.onopen = this.onOpenEvent;
        this.onclose = this.onCloseEvent;
        this.onerror = this.onErrorEvent;
        this.onmessage = this.onMessageEvent;
    }

    public releaseManagedResources = () => {
        this.onopen = null;
        this.onclose = null;
        this.onerror = null;
        this.onmessage = null;

        this.closeAllRTCConnections();
        this.close();
    };

    private onOpenEvent = () => {
        if (this.isDebug) {
            console.log("OnOpen");
        }
        this.isConnected = true;
    };

    private publish = (groupName: string) => {
        if (!this.isConnected) {
            if (this.isDebug) {
                console.log("WebSocket is not connected");
            }
            return;
        }
        this.send(OmeMessage.createPublishOffer(groupName));
    };

    private onCloseEvent = (ev: CloseEvent) => {
        if (this.isDebug) {
            console.log(`OnClose: reason=${ev.reason}`);
        }
        this.isConnected = false;
        this.closeAllRTCConnections();
        if (ev.code === 1000) {
            this.callbacks.onLeft();
        } else {
            this.callbacks.onUnexpectedLeft(ev.reason);
        }
    };

    private onErrorEvent = (ev: Event) => {
        if (this.isDebug) {
            console.log(`OnError at ${ev.type}`);
        }
        this.closeAllRTCConnections();
    };

    private closeAllRTCConnections = () => {
        if (this.isDebug) {
            console.log(`Left room: groupName=${this.groupName}`);
        }

        if (this.publishConnection) {
            for (const hook of this.publishPcCloseHooks) {
                this.handleHook(hook, this.localClientId, this.publishConnection as OmeRTCPeerConnection);
            }
            this.publishConnection.close();
            this.publishConnection = null;
            this.localClientId = "";
        }

        for (const clientId of this.subscribeConnections.keys()) {
            this.closeConnection(clientId);
        }
        this.subscribeConnections.clear();
    };

    private closeConnection = (clientId: string) => {
        const connection = this.subscribeConnections.get(clientId);
        if (connection) {
            for (const hook of this.subscribePcCloseHooks) {
                this.handleHook(hook, clientId, connection);
            }
            connection.close();
            this.subscribeConnections.delete(clientId);
        }
    };

    private onMessageEvent = (ev: MessageEvent<string>) => {
        const command: OmeMessage = Object.assign(new OmeMessage(), JSON.parse(ev.data));
        if (this.isDebug) {
            console.log(`OnMessage: ${command.toString()}`);
        }

        if (command.command === "list groups") {
            this.callbacks.handleGroupList(command.groupListResponse as GroupListResponse);
        } else if (command.command === "publish offer") {
            this.publishOfferEvent(command);
        } else if (command.command === "subscribe offer") {
            this.subscribeOfferEvent(command);
        } else if (command.command === "join") {
            this.joinMemberEvent(command);
        } else if (command.command === "leave") {
            this.leaveMemberEvent(command);
        }
    };

    private publishOfferEvent = async (command: OmeMessage) => {
        let isSetLocalCandidate = false;

        const configuration = this.createRTCConfiguration(command.getIceServers());
        const pc = new OmeRTCPeerConnection(configuration, this.isDebug);
        pc.setCreateAnswerCompletion((answer) => {
            const message = OmeMessage.createAnswerMessage(command.id, answer);
            this.send(message);
        });
        pc.setIceCandidateCallback((candidate) => {
            if (!isSetLocalCandidate) {
                // const sdpMid = candidate.sdpMid;
                if (command.candidates) {
                    for (const candidateInit of command.candidates) {
                        // candidateInit.sdpMid = sdpMid;
                        pc.addIceCandidate(candidateInit);
                    }
                }
                isSetLocalCandidate = true;
            }
            const message = OmeMessage.createIceCandidate(command.id, candidate);
            this.send(message);
        });
        pc.setConnectedCallback(() => {
            if (this.isDebug) {
                console.log(`Joined room: groupName=${this.groupName}`);
            }

            const message = OmeMessage.createJoinMessage(command.id);
            this.send(message);
            this.callbacks.onJoined(command.getClientId());
        });

        for (const hook of this.publishPcCreateHooks) {
            await this.handleHook(hook, command.getClientId(), pc);
        }

        this.localClientId = command.getClientId();
        this.publishConnection = pc;
        pc.createAnswerSdpAsync(command.getSdp());
    };

    private subscribeOfferEvent = (command: OmeMessage) => {
        const currentRetryCount = this.subscribeRetryCounts.get(command.getClientId()) ?? 0;

        if (command.error) {
            if (command.error === "Cannot create offer") {
                if (currentRetryCount < this.MaxSubscribeRetries) {
                    setTimeout(() => {
                        this.subscribe(command.getClientId());
                        this.subscribeRetryCounts.set(command.getClientId(), currentRetryCount + 1);
                    }, this.SubscribeRetryInterval * 1000);
                } else {
                    if (this.isDebug) {
                        console.error(`Maximum retryCount reached: ${command.getClientId()}`);
                    }
                    this.subscribeRetryCounts.delete(command.getClientId());
                }
            } else {
                if (this.isDebug) {
                    console.error(`Subscribe error: ${command.error}`);
                }
                this.subscribeRetryCounts.delete(command.getClientId());
            }
            return;
        } else {
            if (this.isDebug) {
                console.log(`SubscribeOfferEvent: id=${command.id}, sdp=${command.sdp}`);
            }

            if (!command.id) {
                return;
            }
        }
        this.subscribeRetryCounts.delete(command.getClientId());

        let isSetLocalCandidate = false;

        const configuration = this.createRTCConfiguration(command.getIceServers());
        const pc = new OmeRTCPeerConnection(configuration, this.isDebug);
        pc.setCreateAnswerCompletion((answer) => {
            const message = OmeMessage.createAnswerMessage(command.id, answer);
            this.send(message);
        });
        pc.setIceCandidateCallback((candidate) => {
            if (!isSetLocalCandidate) {
                const sdpMid = candidate.sdpMid;
                if (command.candidates) {
                    for (const candidateInit of command.candidates) {
                        candidateInit.sdpMid = sdpMid;
                        const rtcIceCandidate = new RTCIceCandidate(candidateInit);
                        pc.addIceCandidate(rtcIceCandidate);
                    }
                }
                isSetLocalCandidate = true;
            }
            const message = OmeMessage.createIceCandidate(command.id, candidate);
            this.send(message);
        });

        for (const hook of this.subscribePcCreateHooks) {
            this.handleHook(hook, command.getClientId(), pc);
        }

        this.subscribeConnections.set(command.getClientId(), pc);
        pc.createAnswerSdpAsync(command.getSdp());
    };

    private createRTCConfiguration = (optionalServers: RTCIceServer[]) => {
        const iceServers = [...this.defaultIceServers];
        iceServers.push(...optionalServers);

        const configuration: RTCConfiguration = {
            iceServers: iceServers,
        };

        return configuration;
    };

    private handleHook = async (hook: PcCreateHook | PcCloseHook, clientId: string, pc: OmeRTCPeerConnection) => {
        try {
            if (isAsync(hook)) {
                await hook(clientId, pc);
            } else {
                hook(clientId, pc);
            }
        } catch (e) {
            console.error(e);
        }
    };

    private subscribe = (clientId: string) => {
        if (!this.isConnected) {
            if (this.isDebug) {
                console.log("WebSocket is not connected");
            }
            return;
        }
        this.send(OmeMessage.createSubscribeOffer(clientId));
    };

    private joinMemberEvent = (command: OmeMessage) => {
        if (this.isDebug) {
            console.log(`User joined: clientId=${command.getClientId()}`);
        }

        this.subscribe(command.getClientId());
        this.callbacks.onUserJoined(command.getClientId());
    };

    private leaveMemberEvent = (command: OmeMessage) => {
        if (this.isDebug) {
            console.log(`User left: clientId=${command.getClientId()}`);
        }

        this.closeConnection(command.getClientId());
        this.callbacks.onUserLeft(command.getClientId());
    };

    public addPublishPcCreateHooks = (hooks: PcCreateHook[]) => {
        this.publishPcCreateHooks.push(...hooks);
    };

    public addSubscribePcCreateHooks = (hooks: PcCreateHook[]) => {
        this.subscribePcCreateHooks.push(...hooks);
    };

    public addPublishPcCloseHooks = (hooks: PcCloseHook[]) => {
        this.publishPcCloseHooks.push(...hooks);
    };

    public addSubscribePcCloseHooks = (hooks: PcCloseHook[]) => {
        this.subscribePcCloseHooks.push(...hooks);
    };

    public listGroups = () => {
        this.send(OmeMessage.createListGroupsRequest());
    };

    public connect = (groupName: string) => {
        this.groupName = groupName;
        this.publish(this.groupName);
    };
}

export { OmeWebSocket, PcCreateHook, PcCloseHook };
