import { OmeClientProvider } from "@extreal-dev/Extreal.Integration.SFU.OME";

class DummyClient {
    private static createPcError = (clientId: string, pc: RTCPeerConnection) => {
        throw new Error("Create OmeClient Error Test");
    };

    private static createPcErrorAsync = async (clientId: string, pc: RTCPeerConnection) => {
        throw new Error("Create OmeClient Async Error Test");
    };

    private static createPcAsync = async (clientId: string, pc: RTCPeerConnection) => {
        console.log("Create OmeClient Async Test Start");
        await new Promise<void>((resolve) =>
            setTimeout(() => {
                console.log("Create OmeClient Async Test Finish");
                resolve();
            }, 500),
        );
    };

    private static closePcError = (clientId: string) => {
        throw new Error("CloseOmeClient Error Test");
    };

    private static closePcErrorAsync = async (clientId: string) => {
        throw new Error("Close OmeClient Async Error Test");
    };

    private static closePcAsync = async (clientId: string) => {
        console.log("Close OmeClient Async Test Start");
        await new Promise<void>((resolve) =>
            setTimeout(() => {
                console.log("Close OmeClient Async Test Finish");
                resolve();
            }, 500),
        );
    };

    static dummyHook(getOmeClient: OmeClientProvider) {
        getOmeClient().addPublishPcCreateHook(DummyClient.createPcError);
        getOmeClient().addPublishPcCreateHook(DummyClient.createPcAsync);
        getOmeClient().addPublishPcCreateHook(DummyClient.createPcErrorAsync);

        getOmeClient().addSubscribePcCreateHook(DummyClient.createPcError);
        getOmeClient().addSubscribePcCreateHook(DummyClient.createPcAsync);
        getOmeClient().addSubscribePcCreateHook(DummyClient.createPcErrorAsync);

        getOmeClient().addPublishPcCloseHook(DummyClient.closePcError);
        getOmeClient().addPublishPcCloseHook(DummyClient.closePcAsync);
        getOmeClient().addPublishPcCloseHook(DummyClient.closePcErrorAsync);

        getOmeClient().addSubscribePcCloseHook(DummyClient.closePcError);
        getOmeClient().addSubscribePcCloseHook(DummyClient.closePcAsync);
        getOmeClient().addSubscribePcCloseHook(DummyClient.closePcErrorAsync);
    }
}

export { DummyClient };
