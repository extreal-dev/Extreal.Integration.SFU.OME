import { addAction } from "@extreal-dev/extreal.integration.web.common";
import { OmeAdapter } from "@extreal-dev/Extreal.Integration.SFU.OME";
import { AudioStreamClient } from "./AudioStreamClient";
import { DummyClient } from "./DummyClient";


const omeAdapter = new OmeAdapter();
omeAdapter.adapt();

let audioStreamClient: AudioStreamClient;
addAction("start", () => audioStreamClient = new AudioStreamClient(omeAdapter.getOmeClient));
addAction("dummyhook", () => DummyClient.dummyHook(omeAdapter.getOmeClient));
