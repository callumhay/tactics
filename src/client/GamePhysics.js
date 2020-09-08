import * as CANNON from 'cannon-es';
import { threeToCannon } from './threetocannon';

import GameMaterials from './GameMaterials';
import GameTypes from './GameTypes';

const MIN_SLOW_FRAME_TIME = 1/30;

class GamePhysics {
  static get GRAVITY() { return 9.81; }
  constructor(scene, gameModel) {
    this.scene = scene; // THREE.js scene
    this.gameModel = gameModel;

    // Setup the cannon physics world
    this.world = new CANNON.World();
    this.world.gravity.set(0, -GamePhysics.GRAVITY, 0);
    this.world.broadphase = new CANNON.NaiveBroadphase();
    this.world.solver.iterations = 10;
    this.world.allowSleep = true;

    // Setup all the contact materials...
    GameMaterials.contactMaterials.forEach(contactMaterial => this.world.addContactMaterial(contactMaterial));

    this.physicsObjs = {};
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

    // Copy transforms from cannon to three
    const toRemove = [];
    for (const gameObject of Object.values(this.physicsObjs)) {
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
      delete this.physicsObjs[id];
    });

    this.world.step(dt);
  }

  _createPhysicsObj(mesh,body,gameType,gameObject) {
    const id = body.id
    const physObj = { id, mesh, body, gameType, gameObject };
    this.physicsObjs[id] = physObj;
    this.toAdd.push(physObj);
    return physObj;
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

    return this._createPhysicsObj(mesh,body,gameType,gameObject);
  }

  addDebris(config) { return this.addObject(threeToCannon.Type.HULL, CANNON.Body.DYNAMIC, GameTypes.DEBRIS, config); }
  addTerrain(config) { return this.addObject(threeToCannon.Type.HULL, CANNON.Body.STATIC, GameTypes.TERRAIN, config); }
  addBedrock(config) {
    const gameObj = this.addObject(threeToCannon.Type.PLANE, CANNON.Body.STATIC, GameTypes.BEDROCK, config);
    const {body} = gameObj;
    body.quaternion.setFromAxisAngle(new CANNON.Vec3(1, 0, 0), -Math.PI / 2);
    return gameObj;
  }
  /*
  addWater(config) {
    // Water is made up of tons of individual SPH particles, defined by the nodes inside the water body
    const {nodes, translation, quaternion, distanceBetweenNodes, density, material} = config;
    
    // TODO: Should sph be a member of this object?
    const sph = new CANNON.SPHSystem();
    sph.density = 1;
    sph.viscosity = 0.03;
    sph.smoothingRadius = Math.sqrt(2*distanceBetweenNodes*distanceBetweenNodes); // This should be enough distance to allow for 15-20 neighbour particles
    this.world.subsystems.push(sph);

    const mass = density * (4/3)*Math.PI*Math.pow(sph.smoothingRadius,3)/27;
    console.log("Mass of water particle: " + mass);
    const particles = [];
    for (const node of nodes) {
      const {pos} = node;
      const particle = new CANNON.Body({mass, material});
      particle.addShape(new CANNON.Particle());
      particle.position.set(pos.x+translation.x, pos.y+translation.y, pos.z+translation.z);
      particle.quaternion.set(quaternion.x, quaternion.y, quaternion.z, quaternion.w);

      // TODO: add the particles properly
      sph.add(particle);
      this.world.addBody(particle);
      particles.push(particle);
    }

  }
  */

  removeObject(gameObj) {
    gameObj.remove = true;
  }

  clear(force) {
    if (force) {
      Object.values(this.physicsObjs).forEach(obj => this.removeObject(obj));
      this.physicsObjs = {};
    }
    else {
      Object.values(this.physicsObjs).forEach(obj => obj.remove = true);
    }
  }

  onBodyCollision(e) {
    //const {body, target, contact} = e;
    //console.log("Collision: " + target.id);
  }
  onBodySleep(e) {
    const {target:body} = e;
    if (body.mass <= 0) { return; }

    const physObj = this.physicsObjs[body.id];
    switch (physObj.gameType) {
      
      case GameTypes.DEBRIS:
        // This wakes the object up: physObj.wakeUp();

        // When a dynamic chunk of terrain falls asleep we need to merge it back into the environment
        this.gameModel.reattachTerrain(physObj);
        this.removeObject(physObj);
        break;

      default:
        break;
    }  
  }
}

export default GamePhysics;