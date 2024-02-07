//import WebSocket from "ws";
//import { WebSocket } from "https://deno.land/std/ws/mod.ts";
//import { WebSocketClient, WebSocketServer } from "https://deno.land/x/websocket@v0.1.4/mod.ts";
import WebSocket, { WebSocketServer }  from "npm:ws@^8.5.9";

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

//class OmeWebSocket extends WebSocket {
class OmeWebSocket extends WebSocketServer {
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
