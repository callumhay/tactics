import * as CANNON from 'cannon-es';

import { threeToCannon } from './threetocannon';

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
    toRemove.forEach(gameObject => this.removeObject(gameObject));
  }

  addObject(type, gameType, config) {
    const { gameObject, mesh, density, material } = config;
    const shape = threeToCannon(mesh);
    const mass = density * shape.volume();
    const body = new CANNON.Body({
      type: type,
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
    this.world.addBody(body);

    const id = body.id
    const gameObj = { id, mesh, body, gameType, gameObject };
    this.gameObjects[id] = gameObj;
    return gameObj;
  }

  addDebris(config) { return this.addObject(CANNON.Body.DYNAMIC, GameTypes.DEBRIS, config); }
  addTerrain(config) { return this.addObject(CANNON.Body.KINEMATIC, GameTypes.TERRAIN, config); }

  addBedrock(config) {
    const { gameType, gameObject, mesh, material } = config;
    const shape = new CANNON.Plane();
    const body = new CANNON.Body({
      type: CANNON.Body.STATIC,
      mass: 0,
      shape: shape,
      material: material,
    });
    body.quaternion.setFromAxisAngle(new CANNON.Vec3(1, 0, 0), -Math.PI / 2);
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
      
      case GameTypes.DEBRIS:
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