import * as THREE from 'three';
import * as CANNON from 'cannon-es';

import GameTypes from './GameTypes';

const MATERIAL_TYPE_BEDROCK = "bedrock";
const MATERIAL_TYPE_ROCK    = "rock";
const MATERIAL_TYPE_DIRT    = "dirt";
const MATERIAL_TYPE_MOSS    = "moss";

// NOTE: Material density is measured in kg/m^3
const materials = {
  [MATERIAL_TYPE_BEDROCK]: {
    type: MATERIAL_TYPE_BEDROCK,
    gameType: GameTypes.BEDROCK,
    density: 2000,
    dynamic: false,
    three: new THREE.MeshPhongMaterial({color: 0xffffff, shininess:10}),
    cannon: new CANNON.Material(MATERIAL_TYPE_BEDROCK),
  },
  [MATERIAL_TYPE_ROCK]: {
    type: MATERIAL_TYPE_ROCK,
    gameType: GameTypes.TERRAIN,
    density: 1600,
    dynamic: true,
    three: new THREE.MeshPhongMaterial({ color: 0xffffff, shininess:10}),
    cannon: new CANNON.Material(MATERIAL_TYPE_ROCK),
  },
  [MATERIAL_TYPE_DIRT]: {
    type: MATERIAL_TYPE_DIRT,
    gameType: GameTypes.TERRAIN,
    density: 1225,
    dynamic: true,
    three: new THREE.MeshPhongMaterial({ color: 0xffffff, shininess:0 }),
    cannon: new CANNON.Material(MATERIAL_TYPE_DIRT),
  },
  [MATERIAL_TYPE_MOSS]: {
    type: MATERIAL_TYPE_MOSS,
    gameType: GameTypes.TERRAIN,
    density: 1000,
    dynamic: true,
    three: new THREE.MeshPhongMaterial({ color: 0xffffff, shininess:20 }),
    cannon: new CANNON.Material(MATERIAL_TYPE_MOSS),
  }
};

const BEDROCK_FRICTION = 0.9;
const BEDROCK_RESTITUTION = 0.2;
const ROCK_FRICTION = 0.8;
const ROCK_RESTITUTION = 0.2;
const DIRT_FRICTION = 0.9;
const DIRT_RESTITUTION = 0.1;

const MODERATE_STIFFNESS = 1e8;
const STRONG_STIFFNESS = 1e10;

const contactMaterials = [
  new CANNON.ContactMaterial(materials[MATERIAL_TYPE_ROCK], materials[MATERIAL_TYPE_ROCK], { 
    friction: ROCK_FRICTION, restitution: ROCK_RESTITUTION,
    contactEquationStiffness: MODERATE_STIFFNESS, contactEquationRelaxation: 3,
    frictionEquationStiffness: MODERATE_STIFFNESS, frictionEquationRegularizationTime: 3,
  }),
  new CANNON.ContactMaterial(materials[MATERIAL_TYPE_DIRT], materials[MATERIAL_TYPE_DIRT], { 
    friction: DIRT_FRICTION, restitution: DIRT_RESTITUTION,
    contactEquationStiffness: STRONG_STIFFNESS, contactEquationRelaxation: 3,
    frictionEquationStiffness: STRONG_STIFFNESS, frictionEquationRegularizationTime: 3,
  }),
  new CANNON.ContactMaterial(materials[MATERIAL_TYPE_ROCK], materials[MATERIAL_TYPE_DIRT], { 
    friction: DIRT_FRICTION, restitution: DIRT_RESTITUTION,
    contactEquationStiffness: STRONG_STIFFNESS, contactEquationRelaxation: 3,
    frictionEquationStiffness: STRONG_STIFFNESS, frictionEquationRegularizationTime: 3,
  }),
  new CANNON.ContactMaterial(materials[MATERIAL_TYPE_BEDROCK], materials[MATERIAL_TYPE_DIRT], {
    friction: BEDROCK_FRICTION, restitution: (BEDROCK_RESTITUTION + DIRT_RESTITUTION) / 2,
    contactEquationStiffness: STRONG_STIFFNESS, contactEquationRelaxation: 3,
    frictionEquationStiffness: STRONG_STIFFNESS, frictionEquationRegularizationTime: 3,
  }),
  new CANNON.ContactMaterial(materials[MATERIAL_TYPE_BEDROCK], materials[MATERIAL_TYPE_ROCK], {
    friction: BEDROCK_FRICTION, restitution: BEDROCK_RESTITUTION,
    contactEquationStiffness: STRONG_STIFFNESS, contactEquationRelaxation: 3,
    frictionEquationStiffness: STRONG_STIFFNESS, frictionEquationRegularizationTime: 3, 
  })
];

const setTexProperties = (texture) => {
  texture.wrapS = THREE.RepeatWrapping;
  texture.wrapT = THREE.RepeatWrapping;
};

class GameMaterials {
  
  static get MATERIAL_TYPE_ROCK() { return MATERIAL_TYPE_ROCK; }
  static get MATERIAL_TYPE_DIRT() { return MATERIAL_TYPE_DIRT; }
  static get MATERIAL_TYPE_BEDROCK() { return MATERIAL_TYPE_BEDROCK; }

  static loadMaterials() {
    for (const materialEntry of Object.entries(materials)) {
      const [materialName, materialObj] = materialEntry;
      const {three, dynamic} = materialObj;

      if (dynamic) {
        materialObj.debrisThree = three.clone();
        const {debrisThree} = materialObj;
        debrisThree.customProgramCacheKey = function() {
          return 'localSpace';
        };
        debrisThree.onBeforeCompile = three.onBeforeCompile = function(shader) {
          const posVertCode = shader.customProgramCacheKey === 'localSpace' ? lsPositionVertCode : wsPositionVertCode;
          //console.log(shader);
          let newVertShader = preVertCode + shader.vertexShader.replace("#include <worldpos_vertex>\n", "#include <worldpos_vertex>\n" + wsNormalVertCode + posVertCode);
          shader.vertexShader = newVertShader;

          let newFragShader = dynamicTexturePreFragCode + shader.fragmentShader.replace("#include <map_fragment>\n", "");
          newFragShader = newFragShader.replace("#include <emissivemap_fragment>\n", "#include <emissivemap_fragment>\n" + dynamicTextureLookupFragCode);
          shader.fragmentShader = newFragShader;

          shader.uniforms.mapXZ = {value: materialObj.xzTexture};
          shader.uniforms.mapXY = {value: materialObj.xyTexture};
          shader.uniforms.mapZY = {value: materialObj.zyTexture};
        };

        if (!materialObj.xzTexture) {
          const texture = materialObj.xzTexture = 
            new THREE.TextureLoader().load(`assets/textures/${materialName}_xz.jpg`);
          setTexProperties(texture);
        }
        if (!materialObj.xyTexture) {
          const texture = materialObj.xyTexture =
            new THREE.TextureLoader().load(`assets/textures/${materialName}_xy.jpg`);
          setTexProperties(texture);
        }
        if (!materialObj.zyTexture) {
          const texture = materialObj.zyTexture = 
            new THREE.TextureLoader().load(`assets/textures/${materialName}_zy.jpg`);
          setTexProperties(texture);
        }
      }
      else {
        const texture = materialObj.texture = new THREE.TextureLoader().load(`assets/textures/${materialName}.jpg`);
        setTexProperties(texture);
        three.map = texture;
      }
    }
  }

  static get materials() { return materials; }
  static get contactMaterials() { return contactMaterials; }
}

export default GameMaterials;

// TODO: YOU NEED TO USE LOCAL POSITIONS FOR DEBRIS AND WORLD POSITIONS FOR TERRAIN!!!

// The following is injected shader code for texture coordinate mapping for
// geometry that's been generated by the Marching Cubes algorithm.
const preVertCode = `
varying vec3 uvPosition;
varying vec3 wsNormal;
`;
const wsPositionVertCode = `
uvPosition = worldPosition.xyz/worldPosition.w;
`;
const lsPositionVertCode = `
uvPosition = vec3(position);
`;
const wsNormalVertCode = `
wsNormal = objectNormal.xyz;
`;
const dynamicTexturePreFragCode = `
uniform sampler2D mapXZ;
uniform sampler2D mapZY;
uniform sampler2D mapXY;
varying vec3 uvPosition;
varying vec3 wsNormal;
`;

const dynamicTextureLookupFragCode = `
  {
    vec3 blendWeights = abs(normalize(wsNormal)) - 0.2;
    blendWeights *= 7.0;
    blendWeights = pow(blendWeights, vec3(3.0));
    blendWeights = max(vec3(0.0), blendWeights);
    blendWeights /= dot(blendWeights, vec3(1.0));

    vec2 zyCoord = uvPosition.zy;
    vec2 xzCoord = uvPosition.xz;
    vec2 xyCoord = uvPosition.xy;

    vec4 zyColour = texture2D(mapZY, zyCoord);
    vec4 xzColour = texture2D(mapXZ, xzCoord);
    vec4 xyColour = texture2D(mapXY, xyCoord);

    diffuseColor *= (zyColour * blendWeights.xxxx) + (xzColour * blendWeights.yyyy) + (xyColour * blendWeights.zzzz);
  }
`;