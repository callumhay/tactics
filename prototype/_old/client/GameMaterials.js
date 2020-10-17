import * as THREE from 'three';
import * as CANNON from 'cannon-es';

const MATERIAL_TYPE_BEDROCK = "bedrock";
const MATERIAL_TYPE_ROCK    = "rock";
const MATERIAL_TYPE_DIRT    = "dirt";

// NOTE: Material density is measured in kg/m^3
const materials = {
  [MATERIAL_TYPE_BEDROCK]: {
    density: 2000,
    threeMaterial: new THREE.MeshLambertMaterial({color: 0x666666}),
    cannonMaterial: new CANNON.Material(MATERIAL_TYPE_BEDROCK)
  },
  [MATERIAL_TYPE_ROCK]: { 
    density: 1600, 
    threeMaterial: new THREE.MeshLambertMaterial({ color: 0xcccccc}),
    cannonMaterial: new CANNON.Material(MATERIAL_TYPE_ROCK)
  },
  [MATERIAL_TYPE_DIRT]: {
    density: 1225,
    threeMaterial: new THREE.MeshLambertMaterial({ color: 0xa0522d }),
    cannonMaterial: new CANNON.Material(MATERIAL_TYPE_DIRT)
  }
};

const BEDROCK_FRICTION = 0.5;
const BEDROCK_RESTITUTION = 0.3;
const ROCK_FRICTION = 0.4;
const ROCK_RESTITUTION = 0.315;
const DIRT_FRICTION = 0.562;
const DIRT_RESTITUTION = 0.3;


const contactMaterials = [
  new CANNON.ContactMaterial(materials[MATERIAL_TYPE_ROCK], materials[MATERIAL_TYPE_ROCK], { friction: ROCK_FRICTION, restitution: ROCK_RESTITUTION}),
  new CANNON.ContactMaterial(materials[MATERIAL_TYPE_DIRT], materials[MATERIAL_TYPE_DIRT], { friction: DIRT_FRICTION, restitution: DIRT_RESTITUTION}),
  new CANNON.ContactMaterial(materials[MATERIAL_TYPE_ROCK], materials[MATERIAL_TYPE_DIRT], { friction: (ROCK_FRICTION+DIRT_FRICTION)/2, restitution: (ROCK_RESTITUTION+DIRT_RESTITUTION)/2}),
  new CANNON.ContactMaterial(materials[MATERIAL_TYPE_BEDROCK], materials[MATERIAL_TYPE_DIRT], { friction: (BEDROCK_FRICTION + DIRT_FRICTION) / 2, restitution: (BEDROCK_RESTITUTION + DIRT_RESTITUTION) / 2 }),
  new CANNON.ContactMaterial(materials[MATERIAL_TYPE_BEDROCK], materials[MATERIAL_TYPE_ROCK], { friction: (ROCK_FRICTION + BEDROCK_FRICTION) / 2, restitution: (ROCK_RESTITUTION + BEDROCK_RESTITUTION) / 2 })
];

class GameMaterials {
  
  static get MATERIAL_TYPE_ROCK() { return MATERIAL_TYPE_ROCK; }
  static get MATERIAL_TYPE_BEDROCK() { return MATERIAL_TYPE_BEDROCK; }

  static get materials() { return materials; }
  static get contactMaterials() { return contactMaterials; }
}

export default GameMaterials;