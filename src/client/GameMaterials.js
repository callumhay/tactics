import * as THREE from 'three';
import * as CANNON from 'cannon-es';

import GameTypes from './GameTypes';

const MATERIAL_TYPE_BEDROCK = "bedrock";
const MATERIAL_TYPE_ROCK    = "rock";
const MATERIAL_TYPE_DIRT    = "dirt";
const MATERIAL_TYPE_MOSS    = "moss";

const BEDROCK_FRICTION = 0.9; const BEDROCK_RESTITUTION = 0.2;
const ROCK_FRICTION = 0.8; const ROCK_RESTITUTION = 0.2;
const DIRT_FRICTION = 0.9; const DIRT_RESTITUTION = 0.1;
const MOSS_FRICTION = 0.8; const MOSS_RESTITUTION = 0.15;

const MODERATE_STIFFNESS = 1e8;
const STRONG_STIFFNESS = 1e10;

// NOTE: Material density is measured in kg/m^3
const materials = {
  [MATERIAL_TYPE_BEDROCK]: {
    type: MATERIAL_TYPE_BEDROCK,
    gameType: GameTypes.BEDROCK,
    density: 2000,
    stiffness:STRONG_STIFFNESS,
    friction: BEDROCK_FRICTION, 
    restitution: BEDROCK_RESTITUTION,
    dynamic: false,
    three: new THREE.MeshPhongMaterial({color: 0xffffff, shininess:10}),
    cannon: new CANNON.Material(MATERIAL_TYPE_BEDROCK),
  },
  [MATERIAL_TYPE_ROCK]: {
    type: MATERIAL_TYPE_ROCK,
    gameType: GameTypes.TERRAIN,
    density: 1600,
    stiffness: STRONG_STIFFNESS,
    friction: ROCK_FRICTION, 
    restitution: ROCK_RESTITUTION,
    dynamic: true,
    three: new THREE.MeshPhongMaterial({ color: 0xffffff, shininess:10}),
    cannon: new CANNON.Material(MATERIAL_TYPE_ROCK),
  },
  [MATERIAL_TYPE_DIRT]: {
    type: MATERIAL_TYPE_DIRT,
    gameType: GameTypes.TERRAIN,
    density: 1225,
    stiffness: MODERATE_STIFFNESS,
    friction: DIRT_FRICTION, 
    restitution: DIRT_RESTITUTION,
    dynamic: true,
    three: new THREE.MeshPhongMaterial({ color: 0xffffff, shininess:0 }),
    cannon: new CANNON.Material(MATERIAL_TYPE_DIRT),
  },
  [MATERIAL_TYPE_MOSS]: {
    type: MATERIAL_TYPE_MOSS,
    gameType: GameTypes.TERRAIN,
    density: 1000,
    friction: MOSS_FRICTION, 
    restitution: MOSS_RESTITUTION,
    stiffness: MODERATE_STIFFNESS,
    dynamic: true,
    three: new THREE.MeshPhongMaterial({ color: 0xffffff, shininess:20 }),
    cannon: new CANNON.Material(MATERIAL_TYPE_MOSS),
  }
};

const contactMaterials = [];
const initContactMaterials = (materials) => {
  const materialValues = Object.values(materials);
  for (let i = 0; i < materialValues.length; i++) {
    const {cannon:cMat1, friction:mat1Friction, restitution:mat1Restitution,
       stiffness:mat1Stiffness} = materialValues[i];
    
    for (let j = i; j < materialValues.length; j++) {
      const { cannon: cMat2, friction: mat2Friction, restitution: mat2Restitution,
        stiffness:mat2Stiffness} = materialValues[j];
      contactMaterials.push(
        new CANNON.ContactMaterial(cMat1, cMat2, {
          friction: Math.max(mat1Friction, mat2Friction),
          restitution: Math.min(mat1Restitution, mat2Restitution),
          contactEquationStiffness: (mat1Stiffness + mat2Stiffness)/2,
          frictionEquationStiffness: Math.max(mat1Stiffness, mat2Stiffness)
        })
      );
    }
  }
}

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
    initContactMaterials(GameMaterials.materials);
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