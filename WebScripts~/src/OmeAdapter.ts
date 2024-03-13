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
                onJoined: (clientId) => callback(this.withPrefix("HandleOnJoined"), clientId),
                onLeft: () => callback(this.withPrefix("HandleOnLeft")),
                onUnexpectedLeft: (reason) => callback(this.withPrefix("HandleOnUnexpectedLeft"), reason),
                onUserJoined: (clientId) => callback(this.withPrefix("HandleOnUserJoined"), clientId),
                onUserLeft: (clientId) => callback(this.withPrefix("HandleOnUserLeft"), clientId),
                onJoinRetrying: (count) => callback(this.withPrefix("HandleOnJoinRetrying"), count.toString()),
                onJoinRetried: (result) => callback(this.withPrefix("HandleOnJoinRetried"), result.toString()),
                handleGroupList: (groupListResponse) =>
                    callback(this.withPrefix("ReceiveListHostsResponse"), JSON.stringify(groupListResponse)),
            });
        });

        addAction(this.withPrefix("DoReleaseManagedResources"), () => this.getOmeClient().releaseManagedResources());
        addAction(this.withPrefix("DoListGroupsAsync"), () => this.getOmeClient().listGroups());
        addAction(this.withPrefix("DoJoinAsync"), (groupName) => this.getOmeClient().join(groupName));
        addAction(this.withPrefix("LeaveAsync"), () => this.getOmeClient().leave());
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
