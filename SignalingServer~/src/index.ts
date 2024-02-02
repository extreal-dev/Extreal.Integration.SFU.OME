import express from "express";
import cors from "cors";
import http from "http";
import https from "https";
import fs from "fs";
import bodyParser from "body-parser";
import WebSocket, { WebSocketServer } from "ws";
import { OmeWebSocket, OmeMessage } from "./OMEWebSocket";
import { v4 as uuidv4 } from "uuid";

const isLogging = process.env.LOGGING === "on";
const log = (message: string) => {
    if (isLogging) {
        console.log(message);
    }
};

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
    const omeWebSockets = new Map<string, OmeWebSocket>();

    log("connected to client");

    clientWebSocket.on("message", (message) => {
        const omeMessageFromClient: OmeMessage = JSON.parse(message.toString());
        log(`received message from client: ${JSON.stringify(omeMessageFromClient)}`);
        switch (omeMessageFromClient.command) {
            case "list groups": {
                log(`groupMembers: ${JSON.stringify(Object.fromEntries(groupMembers))}`);
                omeMessageFromClient.groupListResponse = {
                    groups: [...groupMembers].map((entry) => ({ name: entry[0], id: [...entry[1]][0] })),
                };
                log(`list groups: ${JSON.stringify(omeMessageFromClient.groupListResponse)}`);
                clientWebSocket.send(JSON.stringify(omeMessageFromClient));
                break;
            }
            case "publish": {
                clientId = uuidv4();
                groupName = omeMessageFromClient.groupName as string;
                log(`publish: groupName=${groupName}, clientId: ${clientId}`);
                const publishWebSocket = new OmeWebSocket(`${omeServerUrl}/app/${clientId}?direction=send`, isLogging);
                publishWebSocket.onMessageCallback = (omeMessageFromOme: OmeMessage) => {
                    omeMessageFromOme.command = "publish offer";
                    omeMessageFromOme.clientId = clientId;
                    clientWebSocket.send(JSON.stringify(omeMessageFromOme));

                    clientWebSockets.set(clientId, clientWebSocket);
                    omeWebSockets.set(omeMessageFromOme.id as string, publishWebSocket);
                };
                publishWebSocket.onopen = () => {
                    publishWebSocket.send(JSON.stringify({ command: "request_offer" }));
                };
                break;
            }
            case "subscribe": {
                log(`subscribe: clientId=${omeMessageFromClient.clientId}`);
                const subscribeWebSocket = new OmeWebSocket(
                    `${omeServerUrl}/app/${omeMessageFromClient.clientId}`,
                    isLogging,
                );
                subscribeWebSocket.onMessageCallback = (omeMessageFromOme: OmeMessage) => {
                    log(
                        `received message from OME server: id=${omeMessageFromOme.id}, clientId=${omeMessageFromOme.command}`,
                    );
                    omeMessageFromOme.command = "subscribe offer";
                    omeMessageFromOme.clientId = omeMessageFromClient.clientId;

                    if (
                        omeMessageFromOme.code &&
                        omeMessageFromOme.code === 404 &&
                        omeMessageFromOme.error === "Cannot create offer"
                    ) {
                        subscribeWebSocket.close();
                        clientWebSocket.send(JSON.stringify(omeMessageFromOme));
                        return;
                    }
                    clientWebSocket.send(JSON.stringify(omeMessageFromOme));
                    omeWebSockets.set(omeMessageFromOme.id as string, subscribeWebSocket);
                };
                subscribeWebSocket.onopen = () => {
                    subscribeWebSocket.send(JSON.stringify({ command: "request_offer" }));
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
                const omeWebSocket = omeWebSockets.get(omeMessageFromClient.id as string);
                log(`send message to OME server: ${JSON.stringify(omeMessageFromClient)}`);
                omeWebSocket?.send(message);
                break;
            }
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

        log("closed connection to client");
    });
});

const port = process.env.PORT || 3000;
server.listen(port, () => {
    log(`Server is running on port ${port}`);
});
