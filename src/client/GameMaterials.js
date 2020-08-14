import * as THREE from 'three';
import * as CANNON from 'cannon';

const MATERIAL_TYPE_ROCK = "rock";
const MATERIAL_TYPE_DIRT = "dirt";

// NOTE: Material density is measured in kg/m^3
const materials = {
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

class GameMaterials {
  static get MATERIAL_TYPE_ROCK() { return MATERIAL_TYPE_ROCK; }
  static get materials() { return materials; }
}

export default GameMaterials;