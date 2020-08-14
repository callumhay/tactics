import * as CANNON from 'cannon';

const MATERIAL_TYPE_ROCK = "rock";
// NOTE: Material density is measured in kg/m^3
const materials = {
  MATERIAL_TYPE_ROCK: {density: 1600, cannonMaterial: new CANNON.Material(MATERIAL_TYPE_ROCK)},
};


class GamePhysics {
  constructor(scene) {
    this.scene = scene; // THREE.js scene

    // Setup the cannon physics world
    this.world = new CANNON.World();
    this.world.gravity.set(0, -9.8, 0);
    this.world.broadphase = new CANNON.NaiveBroadphase();
    this.world.solver.iterations = 10;

    this.gameObjects = {};
    this.nextId = 0;

    // Materials
    this.terrainMaterial = new CANNON.Material("terrain");
    this.world.addContactMaterial(new CANNON.ContactMaterial(
      this.terrainMaterial, 
      this.terrainMaterial, 
      {
        friction:0.4,
        restitution:0.01,
      }
    ));

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
    const {type, mass, shape, size} = config;
    let bodyShape = null;
    switch (shape) {
      case 'box':
      default:
        bodyShape = new CANNON.Box(new CANNON.Vec3(size[0]/2, size[1]/2, size[2]/2));
        break;
    }
    let bodyType = null;
    switch (type) {
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
      material: this.terrainMaterial, // TODO: Add dynamic materials
    });
    this.world.addBody(body);

    const id = this._generateId();
    const gameObj = { id: id, mesh: mesh, body: body };
    this.gameObjects[id] = gameObj;
    return gameObj;
  }

  removeObject(gameObj) {
    const {body, id} = gameObj;
    this.world.remove(body);
    delete this.gameObjects[id];
  }

  _generateId() {
    return this.nextId++;
  }
}

export default GamePhysics;