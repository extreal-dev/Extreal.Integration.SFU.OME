import express from "express";
import cors from "cors";
import http from "http";
import https from "https";
import fs from "fs";
import bodyParser from "body-parser";
import WebSocket, { WebSocketServer } from "ws";
import OMEWebSocket, { OMECommand } from "./OMEWebSocket";
import { v4 as uuidv4 } from "uuid";

const app = express();
app.use(cors());
app.use(bodyParser.urlencoded({ extended: true }));
app.use(bodyParser.json());

let server;
if (process.env.USE_HTTPS === "true") {
    const privateKey = fs.readFileSync("./keys/privkey.pem", "utf8");
    const certificate = fs.readFileSync("./keys/fullchain.pem", "utf8");

    const credentials = { key: privateKey, cert: certificate };
    server = https.createServer(credentials, app);
} else {
    server = http.createServer(app);
}
const wss = new WebSocketServer({ server });

const omeServerUrl = process.env.OME_SERVER_URL || "ws://localhost:3333";

const groupMembers = new Map<string, Set<string>>();
const clientWebSockets = new Map<string, WebSocket>();

wss.on("connection", (clientWebSocket: WebSocket) => {
    let groupName = "";
    let clientId = "";

    const omeWebSockets = new Map<string, OMEWebSocket>();
    console.log("connected");

    clientWebSocket.on("message", (message) => {
        const omeCommand: OMECommand = JSON.parse(message.toString());
        console.log("clientWebSocket onMessage: %o", omeCommand);
        switch (omeCommand.command) {
            case "publish": {
                clientId = uuidv4();
                groupName = omeCommand.groupName as string;
                console.log("publish: Group:%o, stream: %o", groupName, clientId);
                const publishWebSocket = new OMEWebSocket(`${omeServerUrl}/app/${clientId}?direction=send`);
                publishWebSocket.onMessageCallback = (command: OMECommand) => {
                    command.command = "publishOffer";
                    command.clientId = clientId;
                    clientWebSocket.send(JSON.stringify(command));

                    clientWebSockets.set(clientId, clientWebSocket);
                    omeWebSockets.set(command.id as string, publishWebSocket);
                };
                publishWebSocket.onopen = () => {
                    publishWebSocket.send(JSON.stringify({ command: "request_offer" }));
                };
                break;
            }
            case "subscribe": {
                console.log("subscribe:%s", omeCommand.clientId);
                const subscribeWebSocket = new OMEWebSocket(`${omeServerUrl}/app/${omeCommand.clientId}`);
                subscribeWebSocket.onMessageCallback = (command: OMECommand) => {
                    console.log("subscribeWebSocket onMessage[%s]: %s", command.id, command.command);
                    command.command = "subscribeOffer";
                    command.clientId = omeCommand.clientId;

                    if (command.code && command.code === 404 && command.error === "Cannot create offer") {
                        subscribeWebSocket.close();
                        clientWebSocket.send(JSON.stringify(command));
                        return;
                    }
                    clientWebSocket.send(JSON.stringify(command));
                    omeWebSockets.set(command.id as string, subscribeWebSocket);
                };
                subscribeWebSocket.onopen = () => {
                    subscribeWebSocket.send(JSON.stringify({ command: "request_offer" }));
                };
                subscribeWebSocket.onclose = () => {
                    console.log("subscribeWebSocket[%s] closed", omeCommand.clientId);
                };
                break;
            }
            case "join": {
                const members = groupMembers.get(groupName);
                if (members) {
                    members.forEach((member) => {
                        const memberClientWebSocket = clientWebSockets.get(member);
                        if (memberClientWebSocket) {
                            memberClientWebSocket.send(JSON.stringify({ command: "join", clientId: clientId }));
                        }
                        clientWebSocket.send(JSON.stringify({ command: "join", clientId: member }));
                    });
                }
                const roomSet = groupMembers.get(groupName);
                if (roomSet) {
                    roomSet.add(clientId);
                } else {
                    groupMembers.set(groupName, new Set<string>([clientId]));
                }
                break;
            }
            case "answer":
            case "candidate": {
                if (!omeCommand.id) {
                    console.error("id is not found:%o", omeCommand);
                    return;
                }
                const omeWebSocket = omeWebSockets.get(omeCommand.id);
                if (!omeWebSocket) {
                    console.error("websocket is not found:%o", omeCommand);
                    return;
                }
                console.log("send message to clientWebSockets[%s]:%o", omeCommand.id, omeCommand);
                omeWebSocket.send(message);
                break;
            }
            default:
                console.error("unknown command:%o", omeCommand.command);
                break;
        }
    });

    clientWebSocket.on("close", () => {
        if (!groupName) {
            return;
        }

        const members = groupMembers.get(groupName);
        if (members) {
            members.delete(clientId);
            members.forEach((member) => {
                const memberClientWebSocket = clientWebSockets.get(member);
                if (memberClientWebSocket) {
                    memberClientWebSocket.send(
                        JSON.stringify({
                            command: "leave",
                            clientId: clientId,
                        }),
                    );
                }
            });
            if (members.size === 0) {
                groupMembers.delete(groupName);
            }
        }
        clientWebSockets.delete(clientId);
        omeWebSockets.forEach((omeWebSocket) => {
            omeWebSocket.close();
        });
        console.log("closed");
    });
});

const port = process.env.PORT || 3000;
server.listen(port, () => {
    console.log(`Server is running on port ${port}`);
});
