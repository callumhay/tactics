import * as CANNON from 'cannon-es';
import { threeToCannon } from './threetocannon';

import GameMaterials from './GameMaterials';
import GameTypes from './GameTypes';

const MIN_SLOW_FRAME_TIME = 1/30;

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
    this.toAdd = [];

    this.onBodyCollision = this.onBodyCollision.bind(this);
    this.onBodySleep = this.onBodySleep.bind(this);
  }

  update(dt) {
    if (dt > MIN_SLOW_FRAME_TIME) { return; } // If we don't do this then the physics goes crazy on long frames/hangs
    if (this.toAdd.length > 0) {
      this.toAdd.forEach(gameObj => this.world.addBody(gameObj.body));
      this.toAdd = [];
    }

    this.world.step(dt);

    // Copy transforms from cannon to three
    const toRemove = [];
    for (const gameObject of Object.values(this.gameObjects)) {
      const {mesh, body, remove} = gameObject;
      if (remove) {
        toRemove.push(gameObject);
        console.log("Removing physics object: " + gameObject.id);
        continue;
      }
      mesh.position.copy(body.position);
      mesh.quaternion.copy(body.quaternion);
    }
    toRemove.forEach(gameObject => {
      const {body, id} = gameObject;
      this.world.removeBody(body);
      delete this.gameObjects[id];
    });
  }

  addObject(shapeType, physType, gameType, config) {
    const { gameObject, mesh, density, material } = config;
    const shape = threeToCannon(mesh, { type: shapeType });
    if (shape === null) { return null; }
    const mass = (density || material.density) * shape.volume();
    const body = new CANNON.Body({
      type: physType,
      shape: shape,
      position: mesh.position,
      quaternion: mesh.quaternion,
      mass: mass,
      velocity: new CANNON.Vec3(0, 0, 0),
      angularVelocity: new CANNON.Vec3(0, 0, 0),
      material: material,
      allowSleep: true,
    });
    body.addEventListener('collide', this.onBodyCollision);
    body.addEventListener('sleep', this.onBodySleep);
    body.sleepSpeedLimit = 0.2;
    
    const id = body.id
    const gameObj = { id, mesh, body, gameType, gameObject };
    this.gameObjects[id] = gameObj;
    this.toAdd.push(gameObj);
    return gameObj;
  }

  addDebris(config) { return this.addObject(threeToCannon.Type.HULL, CANNON.Body.DYNAMIC, GameTypes.DEBRIS, config); }
  addTerrain(config) { return this.addObject(threeToCannon.Type.HULL, CANNON.Body.STATIC, GameTypes.TERRAIN, config); }

  addBedrock(config) {
    const gameObj = this.addObject(threeToCannon.Type.PLANE, CANNON.Body.STATIC, GameTypes.BEDROCK, config);
    const {body} = gameObj;
    body.quaternion.setFromAxisAngle(new CANNON.Vec3(1, 0, 0), -Math.PI / 2);
    return gameObj;
  }

  removeObject(gameObj) {
    gameObj.remove = true;
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
      
      case GameTypes.DEBRIS:
        // This wakes the object up: gameObj.wakeUp();

        // When a dynamic chunk of terrain falls asleep we need to merge it back into the environment
        this.gameModel.reattachTerrain(gameObj);
        this.removeObject(gameObj);
        break;

      default:
        break;
    }  
  }
}

export default GamePhysics;