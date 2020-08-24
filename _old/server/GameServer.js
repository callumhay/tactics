import fs from 'fs';
import ws from 'ws';

import GameProtocol from '../shared/GameProtocol';

class GameServer {
  constructor() {
    const self = this;
    this.webClientSockets = [];
    this.webSocketServer = new ws.Server({
      port: GameProtocol.WEBSOCKET_PORT,
      //perMessageDeflate: true, // Enable compression... lots of repetitive data.
    });

    this.webSocketServer.on('open', function() {
      console.log("Websocket server is now running.");
    });
    this.webSocketServer.on('connection', function(socket, request, client) {
      console.log("Websocket opened.");

      self.webClientSockets.push(socket);

      socket.on('message', function(data) {
      });
      socket.on('close', function() {
        console.log("Websocket closed.");
        self.webClientSockets.splice(self.webClientSockets.indexOf(socket), 1);
      });

      const bfFilepath = 'test/maps/gravity/complex_multi_fall_terrain.json';
      fs.readFile(bfFilepath, (err, data) => {
        if (err) {
          console.error(`Failed to read file '${bfFilepath}': ${err}`);
          return;
        }

        const battlefieldData = {
          type: GameProtocol.BATTLEFIELD_PACKET_TYPE,
          data: JSON.parse(data.toString('utf8')),
        };
        socket.send(JSON.stringify(battlefieldData));
      });
      

    });
    this.webSocketServer.on('close', function() {
      console.log("Websocket server closed.");
    });
  }



}

export default GameServer;