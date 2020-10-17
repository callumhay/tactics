import GamePhysics from './GamePhysics';
import Battlefield from './Battlefield';

class GameModel {
  constructor(scene) {
    this.physics = new GamePhysics(scene, this);
    this.battlefield = new Battlefield(scene, this.physics);
  }

  update(dt) {
    this.physics.update(dt);
  }

  reattachTerrain(physicsObj) {
    this.battlefield.convertDebrisToTerrain(physicsObj);
  }
}

export default GameModel;