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

class OmeWebSocket {
    public onMessageCallback: ((command: OmeMessage) => void) | null = null;
    public ws: WebSocket;
    constructor(url: string, isLogging: boolean, protocols?: string | string[]) {
        this.ws = new WebSocket(url, protocols);
        this.ws.onclose = () => {
            if (isLogging) {
                console.log("OMEWebSocket closed");
            }
        };
        this.ws.onerror = (error) => {
            if (isLogging) {
                console.log("OMEWebSocket error: %o", error);
            }
        };
        this.ws.onmessage = (event) => {
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
