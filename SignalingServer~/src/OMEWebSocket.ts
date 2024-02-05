import WebSocket from "ws";

type OmeMessage = {
    id?: string;
    command?: string;
    groupListResponse?: groupListResponse;

    groupName?: string;
    clientId?: string;

    code?: number;
    error?: string;
};

type groupListResponse = {
    groups: GroupResponse[];
};

type GroupResponse = {
    name: string;
};

class OmeWebSocket extends WebSocket {
    public onMessageCallback: ((command: OmeMessage) => void) | null = null;

    constructor(url: string, isLogging: boolean, protocols?: string | string[]) {
        super(url, protocols);
        this.onclose = () => {
            if (isLogging) {
                console.log("OMEWebSocket closed");
            }
        };
        this.onerror = (error) => {
            if (isLogging) {
                console.log("OMEWebSocket error: %o", error);
            }
        };
        this.onmessage = (event) => {
            const message = JSON.parse(event.data.toString());
            if (isLogging) {
                console.log("OMEWebSocket onMessage: %o", message);
            }

            if (this.onMessageCallback) {
                this.onMessageCallback(message);
            }
        };
    }
}

export { OmeWebSocket, OmeMessage };
