import { OmeClient } from "./OmeClient";
import { addAction, callback } from "@extreal-dev/extreal.integration.web.common";

type OmeClientProvider = () => OmeClient;

class OmeAdapter {
    private omeClient: OmeClient | null = null;

    public adapt = () => {
        addAction(this.withPrefix("WebGLOmeClient"), (jsonOmeConfig) => {
            const voiceChatConfig = JSON.parse(jsonOmeConfig);
            if (voiceChatConfig.isDebug) {
                console.log(voiceChatConfig);
            }
            this.omeClient = new OmeClient(voiceChatConfig, {
                onJoined: (streamName) => callback(this.withPrefix("HandleOnJoined"), streamName),
                onLeft: (reason) => callback(this.withPrefix("HandleOnLeft"), reason),
                onUserJoined: (streamName) => callback(this.withPrefix("HandleOnUserJoined"), streamName),
                onUserLeft: (streamName) => callback(this.withPrefix("HandleOnUserLeft"), streamName),
            });
        });

        addAction(this.withPrefix("DoReleaseManagedResources"), () => this.getOmeClient().releaseManagedResources());
        addAction(this.withPrefix("DoConnectAsync"), (roomName) => this.getOmeClient().connect(roomName));
        addAction(this.withPrefix("DisconnectAsync"), () => this.getOmeClient().disconnect());
    };

    private withPrefix = (name: string) => `WebGLOmeClient#${name}`;

    public getOmeClient: OmeClientProvider = () => {
        if (!this.omeClient) {
            throw new Error("Call the WebGLOmeClient constructor first in Unity.");
        }
        return this.omeClient;
    };
}

export { OmeAdapter, OmeClientProvider };
