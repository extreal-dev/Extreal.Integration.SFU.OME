import {OmeWebSocket, OmeMessage} from "./OMEWebSocket.ts";

const isLogging = Deno.env.get("LOGGING") === "on";
const log = (logMessage: () => string | object) => isLogging && console.log(logMessage());

const omeServerUrl = Deno.env.get("OME_SERVER_URL") || "ws://localhost:3041";
const groupMembers = new Map<string, Set<string>>();
const clientWebSockets = new Map<string, WebSocket>();
const port = Number(Deno.env.get("PORT")) || 3040;

const handleWebSocket = (ws: WebSocket) => {
  let groupName = "";
  const clientId = crypto.randomUUID();
  const clientWebSocket = ws;
  const omeWebSockets = new Map<string, OmeWebSocket>();

  ws.onopen = () => {
    clientWebSockets.set(clientId, clientWebSocket);
  }

  ws.onmessage = (event) => {
    const dataStr = new TextDecoder().decode(event.data);
    const omeMessageFromClient = JSON.parse(dataStr);
    log(() => `received message from client: ${JSON.stringify(omeMessageFromClient)}`);

    switch (omeMessageFromClient.command) {
      case "list groups": {
        log(() => `groupMembers: ${JSON.stringify(Object.fromEntries(groupMembers))}`);
        omeMessageFromClient.groupListResponse = {
          groups: [...groupMembers].map((entry) => ({name: entry[0]})),
        };
        log(() => `list groups: ${JSON.stringify(omeMessageFromClient.groupListResponse)}`);
        clientWebSocket.send(JSON.stringify(omeMessageFromClient));
        break;
      }
      case "publish": {
        groupName = omeMessageFromClient.groupName as string;
        log(() => `publish: groupName=${groupName}, clientId: ${clientId}`);
        const publishWebSocket = new OmeWebSocket(`${omeServerUrl}/app/${clientId}?direction=send`, isLogging);
        publishWebSocket.onMessageCallback = (omeMessageFromOme: OmeMessage) => {
          omeMessageFromOme.command = "publish offer";
          omeMessageFromOme.clientId = clientId;
          clientWebSocket.send(JSON.stringify(omeMessageFromOme));

          omeWebSockets.set(omeMessageFromOme.id as string, publishWebSocket);
        };
        publishWebSocket.ws.onopen = () => {
          publishWebSocket.ws.send(JSON.stringify({command: "request_offer"}));
        };
        break;
      }
      case "subscribe": {
        log(() => `subscribe: clientId=${omeMessageFromClient.clientId}`);
        const subscribeWebSocket = new OmeWebSocket(
          `${omeServerUrl}/app/${omeMessageFromClient.clientId}`,
          isLogging,
        );
        subscribeWebSocket.onMessageCallback = (omeMessageFromOme: OmeMessage) => {
          log(
            () => `received message from OME server: id=${omeMessageFromOme.id}, clientId=${omeMessageFromOme.command}`,
          );
          omeMessageFromOme.command = "subscribe offer";
          omeMessageFromOme.clientId = omeMessageFromClient.clientId;

          if (
            omeMessageFromOme.code &&
            omeMessageFromOme.code === 404 &&
            omeMessageFromOme.error === "Cannot create offer"
          ) {
            subscribeWebSocket.ws.close();
            clientWebSocket.send(JSON.stringify(omeMessageFromOme));
            return;
          }
          clientWebSocket.send(JSON.stringify(omeMessageFromOme));
          omeWebSockets.set(omeMessageFromOme.id as string, subscribeWebSocket);
        };
        subscribeWebSocket.ws.onopen = () => {
          subscribeWebSocket.ws.send(JSON.stringify({command: "request_offer"}));
        };
        break;
      }
      case "join": {
        const members = groupMembers.get(groupName);
        if (members) {
          members.forEach((member) => {
            const memberClientWebSocket = clientWebSockets.get(member);
            if (memberClientWebSocket) {
              memberClientWebSocket.send(JSON.stringify({command: "join", clientId: clientId}));
            }
            clientWebSocket.send(JSON.stringify({command: "join", clientId: member}));
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
        log(() => `send message to OME server: ${JSON.stringify(omeMessageFromClient)}`);
        omeWebSocket?.ws.send(JSON.stringify(omeMessageFromClient));
        break;
      }
    }
  }

  ws.onclose = () => {
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
        omeWebSocket.ws.close();
    });

    log(() => "closed connection to client");

    clientWebSockets.delete(clientId);
    log(() => "closed connection to client");
  };
};


const useHttps = Deno.env.get("USE_HTTPS") === "true";

const options = useHttps
  ? {
      port: port,
      cert: Deno.readTextFileSync('/work/keys/fullchain.pem'),
      key: Deno.readTextFileSync('/work/keys/privkey.pem'),
    }
  : { port };

const requestHandler = (req) => {
  if (req.headers.get("upgrade") !== "websocket") {
    return new Response("not found", { status: 404 });
  }
  const { socket, response } = Deno.upgradeWebSocket(req);
  handleWebSocket(socket);
  return response;
};

Deno.serve(options, requestHandler);

console.log(`Server is running on ${useHttps ? 'wss' : 'ws'}://localhost:${port}`);
