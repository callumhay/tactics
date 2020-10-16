import GamePhysics from './GamePhysics';
import Battlefield from './Battlefield';

class GameModel {
  constructor(scene, gpuManager) {
    this.gpuManager = gpuManager;
    this.physics = new GamePhysics(scene, this);
    this.battlefield = new Battlefield(scene, this);
  }

  update(dt) {
    this.physics.update(dt);
    this.battlefield.update(dt);
  }

  reattachTerrain(physicsObj) {
    this.battlefield.convertDebrisToTerrain(physicsObj);
  }
}

export default GameModel;