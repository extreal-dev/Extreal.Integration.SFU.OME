type GroupListResponse = {
    groups: GroupResponse[];
};

type GroupResponse = {
    id: string;
    name: string;
};

class OmeMessage {
    public id: number | undefined;
    public command: string | undefined;
    private groupName: string | undefined;
    public clientId: string | undefined;
    public error: string | undefined;
    public sdp: RTCSessionDescriptionInit | undefined;
    public candidates: RTCIceCandidateInit[] | undefined;
    public iceServers: RTCIceServer[] | undefined;
    public groupListResponse: GroupListResponse | undefined;

    public getClientId = () => {
        return this.clientId as string;
    };

    public getSdp = () => {
        return this.sdp as RTCSessionDescription;
    };

    public getIceServers = () => {
        return this.iceServers as RTCIceServer[];
    };

    public toString = () => {
        return JSON.stringify(this);
    };

    public static createAnswerMessage = (id: number | undefined, rtcSessionDescription: RTCSessionDescriptionInit) => {
        const commandName = "answer";
        const sendMessage = new OmeMessage();
        sendMessage.command = commandName;
        sendMessage.id = id;
        sendMessage.sdp = rtcSessionDescription;
        sendMessage.candidates = [];
        return OmeMessage.convertToBuffer(sendMessage);
    };

    public static createJoinMessage = (id: number | undefined) => {
        const commandName = "join";
        const sendMessage = new OmeMessage();
        sendMessage.command = commandName;
        sendMessage.id = id;
        return OmeMessage.convertToBuffer(sendMessage);
    };

    public static createIceCandidate = (id: number | undefined, rtcIceCandidate: RTCIceCandidateInit) => {
        const commandName = "candidate";
        const sendMessage = new OmeMessage();
        sendMessage.command = commandName;
        sendMessage.id = id;
        sendMessage.candidates = [rtcIceCandidate];
        return OmeMessage.convertToBuffer(sendMessage);
    };

    public static createListGroupsRequest = () => {
        const message = new OmeMessage();
        message.command = "list groups";
        return OmeMessage.convertToBuffer(message);
    };

    public static createPublishOffer = (groupName: string) => {
        const message = new OmeMessage();
        message.command = "publish";
        message.groupName = groupName;
        return OmeMessage.convertToBuffer(message);
    };

    public static createSubscribeOffer = (clientId: string) => {
        const message = new OmeMessage();
        message.command = "subscribe";
        message.clientId = clientId;
        return OmeMessage.convertToBuffer(message);
    };

    private static convertToBuffer = (message: OmeMessage)  => {
      const encoder = new TextEncoder();
      return encoder.encode(JSON.stringify(message));
    };
}

export { OmeMessage, GroupListResponse };
