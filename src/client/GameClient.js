
import GameProtocol from '../shared/GameProtocol';

class GameClient {
  constructor() {
    this.socket = new WebSocket('ws://' + GameProtocol.WEBSOCKET_HOST + ':' + GameProtocol.WEBSOCKET_PORT);
  }

  start() {
    const self = this;

    this.socket.addEventListener('open', (event) => {
      console.log("Websocket open on " + event.currentTarget.url);
    });
    this.socket.addEventListener('error', (error) => {
      console.log("Websocket error: " + error);
    });
    this.socket.addEventListener('close', (event) => {
      console.log("Websocket closed.");
    });
    this.socket.addEventListener('message', (event) => {
      console.log(event.data);
      const dataObj = JSON.parse(event.data);
      self._readPacket(dataObj);
    });
  }

  _readPacket(dataObj) {

  }


}

export default GameClient;