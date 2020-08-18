import * as CANNON from 'cannon';

import GameMaterials from './GameMaterials';
import GameTypes from './GameTypes';

class GamePhysics {
  constructor(scene, gameModel) {
    this.scene = scene; // THREE.js scene
    this.gameModel = gameModel;

    // Setup the cannon physics world
    this.world = new CANNON.World();
    this.world.gravity.set(0, -9.8, 0);
    this.world.broadphase = new CANNON.NaiveBroadphase();
    this.world.solver.iterations = 10;
    this.world.allowSleep = true;

    // Setup all the contact materials...
    GameMaterials.contactMaterials.forEach(contactMaterial => this.world.addContactMaterial(contactMaterial));

    this.gameObjects = {};

    this.onBodyCollision = this.onBodyCollision.bind(this);
    this.onBodySleep = this.onBodySleep.bind(this);
  }

  update(dt) {
    const cleandt = Math.min(0.1, dt); // If the dt is too large it can do some fucked up things to our physics
    this.world.step(cleandt);

    // Copy transforms from cannon to three
    const toRemove = [];
    Object.values(this.gameObjects).forEach(gameObject => {
      const {mesh, body, remove} = gameObject;
      if (remove) { toRemove.push(gameObject); console.log("Removing physics object: " + gameObject.id); }
      mesh.position.copy(body.position);
      mesh.quaternion.copy(body.quaternion);
    });
    toRemove.forEach(gameObject => this.removeObject(gameObject));
  }

  addObject(mesh, config) {
    const {gameType, gameObject, physicsBodyType, mass, shape, size, material} = config;
    let bodyShape = null;
    switch (shape) {
      case 'box':
      default:
        bodyShape = new CANNON.Box(new CANNON.Vec3(size[0]/2, size[1]/2, size[2]/2));
        break;
    }

    const body = new CANNON.Body({
      mass: mass || 0,
      type: physicsBodyType,
      shape: bodyShape,
      position: mesh.position,
      quaternion: mesh.quaternion,
      velocity: new CANNON.Vec3(0,0,0),
      angularVelocity: new CANNON.Vec3(0,0,0),
      material: material,
      allowSleep: true,
    });

    body.addEventListener('collide', this.onBodyCollision);
    body.addEventListener('sleep', this.onBodySleep);
    this.world.addBody(body);

    const id = body.id
    const gameObj = { id, mesh, body, gameType, gameObject };
    this.gameObjects[id] = gameObj;
    return gameObj;
  }

  removeObject(gameObj) {
    const {body, id} = gameObj;
    this.world.removeBody(body);
    delete this.gameObjects[id];
  }

  clear(force) {
    if (force) {
      Object.values(this.gameObjects).forEach(obj => this.removeObject(obj));
      this.gameObjects = {};
    }
    else {
      Object.values(this.gameObjects).forEach(obj => obj.remove = true);
    }
  }

  onBodyCollision(e) {
    //const {body, target, contact} = e;
    //console.log("Collision: " + target.id);
  }
  onBodySleep(e) {
    const {target:body} = e;
    if (body.mass <= 0) { return; }

    const gameObj = this.gameObjects[body.id];
    switch (gameObj.gameType) {
      case GameTypes.DETACHED_TERRAIN:
        // When a dynamic chunk of terrain falls asleep we need to merge it back into the environment
        this.gameModel.reattachTerrain(gameObj);
        gameObj.remove = true;
        break;

      default:
        break;
    }  
  }
}

export default GamePhysics;