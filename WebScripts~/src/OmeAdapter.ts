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
                onLeft: () => callback(this.withPrefix("HandleOnLeft")),
                onUnexpectedLeft: (reason) => callback(this.withPrefix("HandleOnUnexpectedLeft"), reason),
                onUserJoined: (streamName) => callback(this.withPrefix("HandleOnUserJoined"), streamName),
                onUserLeft: (streamName) => callback(this.withPrefix("HandleOnUserLeft"), streamName),
                handleGroupList: (groupListResponse) =>
                    callback(this.withPrefix("ReceiveListHostsResponse"), JSON.stringify(groupListResponse)),
            });
        });

        addAction(this.withPrefix("DoReleaseManagedResources"), () => this.getOmeClient().releaseManagedResources());
        addAction(this.withPrefix("DoListGroupsAsync"), () => this.getOmeClient().listGroups());
        addAction(this.withPrefix("DoConnectAsync"), (groupName) => this.getOmeClient().connect(groupName));
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
