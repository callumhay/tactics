import * as THREE from 'three';
import * as CANNON from 'cannon-es';

const MATERIAL_TYPE_BEDROCK = "bedrock";
const MATERIAL_TYPE_ROCK    = "rock";
const MATERIAL_TYPE_DIRT    = "dirt";

// NOTE: Material density is measured in kg/m^3
const materials = {
  [MATERIAL_TYPE_BEDROCK]: {
    density: 2000,
    dynamic: false,
    three: new THREE.MeshLambertMaterial({color: 0xffffff}),
    cannon: new CANNON.Material(MATERIAL_TYPE_BEDROCK)
  },
  [MATERIAL_TYPE_ROCK]: { 
    density: 1600,
    dynamic: true,
    three: new THREE.MeshLambertMaterial({ color: 0xcccccc}),
    cannon: new CANNON.Material(MATERIAL_TYPE_ROCK)
  },
  [MATERIAL_TYPE_DIRT]: {
    density: 1225,
    dynamic: true,
    three: new THREE.MeshLambertMaterial({ color: 0xa0522d }),
    cannon: new CANNON.Material(MATERIAL_TYPE_DIRT)
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

const setTexProperties = (materialObj, texture) => {
  texture.wrapS = THREE.RepeatWrapping;
  texture.wrapT = THREE.RepeatWrapping;
  materialObj.three.map = texture;
};

class GameMaterials {
  
  static get MATERIAL_TYPE_ROCK() { return MATERIAL_TYPE_ROCK; }
  static get MATERIAL_TYPE_BEDROCK() { return MATERIAL_TYPE_BEDROCK; }

  static loadMaterials() {
    for (const materialEntry of Object.entries(materials)) {
      const [materialName, materialObj] = materialEntry;
      if (materialObj.dynamic) {
        if (!materialObj.xzTexture) {
          const texture = materialObj.xzTexture = new THREE.TextureLoader().load(`assets/textures/${materialName}_xz.jpg`);
          setTexProperties(materialObj, texture);
        }
        if (!materialObj.xyTexture) {
          const texture = materialObj.xzTexture = new THREE.TextureLoader().load(`assets/textures/${materialName}_xy.jpg`);
          setTexProperties(materialObj, texture);
        }
        if (!materialObj.zyTexture) {
          const texture = materialObj.xzTexture = new THREE.TextureLoader().load(`assets/textures/${materialName}_zy.jpg`);
          setTexProperties(materialObj, texture);
        }
      }
      else {
        const texture = materialObj.texture = new THREE.TextureLoader().load(`assets/textures/${materialName}.jpg`);
        setTexProperties(materialObj, texture);
      }
    }
  }

  static get materials() { return materials; }
  static get contactMaterials() { return contactMaterials; }
}

export default GameMaterials;