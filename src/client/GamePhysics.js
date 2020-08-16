import * as CANNON from 'cannon';

import GameMaterials from './GameMaterials';

class GamePhysics {
  constructor(scene) {
    this.scene = scene; // THREE.js scene

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
    Object.values(this.gameObjects).forEach(gameObject => {
      const {mesh, body} = gameObject;
      mesh.position.copy(body.position);
      mesh.quaternion.copy(body.quaternion);
    });
  }

  addObject(mesh, config) {
    const { physicsBodyType, mass, shape, size, material} = config;
    let bodyShape = null;
    switch (shape) {
      case 'box':
      default:
        bodyShape = new CANNON.Box(new CANNON.Vec3(size[0]/2, size[1]/2, size[2]/2));
        break;
    }
    let bodyType = null;
    switch (physicsBodyType) {
      case 'dynamic':
        bodyType = CANNON.Body.DYNAMIC; break;
      case 'kinematic':
        bodyType = CANNON.Body.KINEMATIC; break;
      case 'static':
      default:
        bodyType = CANNON.Body.STATIC; break;
    }

    const body = new CANNON.Body({
      mass: mass || 0,
      type: bodyType,
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
    const gameObj = { id, mesh, body };
    this.gameObjects[id] = gameObj;
    return gameObj;
  }

  removeObject(gameObj) {
    const {body, id} = gameObj;
    this.world.remove(body);
    delete this.gameObjects[id];
  }

  onBodyCollision(e) {
    //const {body, target, contact} = e;
    //console.log("Collision: " + target.id);
  }
  onBodySleep(e) {
    const {target} = e;
    if (target.mass <= 0) { return; }

    // When a dynamic chunk of terrain of sufficient size falls asleep we need to merge
    // it back into the environment
    // TODO
    
  }
}

export default GamePhysics;