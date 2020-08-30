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
    three: new THREE.MeshPhongMaterial({color: 0xffffff, shininess:10}),
    cannon: new CANNON.Material(MATERIAL_TYPE_BEDROCK)
  },
  [MATERIAL_TYPE_ROCK]: { 
    density: 1600,
    dynamic: true,
    three: new THREE.MeshPhongMaterial({ color: 0xffffff, shininess:10}),
    cannon: new CANNON.Material(MATERIAL_TYPE_ROCK)
  },
  [MATERIAL_TYPE_DIRT]: {
    density: 1225,
    dynamic: true,
    three: new THREE.MeshPhongMaterial({ color: 0xffffff, shininess:0 }),
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
        //materialObj.debrisThree = three.clone();
        three.onBeforeCompile = function(shader) {
          //console.log(shader);
          let newVertShader = wsPositionPreVertCode + shader.vertexShader.replace("#include <worldpos_vertex>\n", "#include <worldpos_vertex>\n" + wsPositionVertCode);
          shader.vertexShader = newVertShader;

          let newFragShader = dynamicTexturePreFragCode + shader.fragmentShader.replace("#include <map_fragment>\n", "");
          newFragShader = newFragShader.replace("#include <emissivemap_fragment>\n", "#include <emissivemap_fragment>\n" + dynamicTextureLookupFragCode);
          shader.fragmentShader = newFragShader;

          shader.uniforms.mapXZ = {value: materialObj.xzTexture};
          shader.uniforms.mapXY = {value: materialObj.xyTexture};
          shader.uniforms.mapZY = {value: materialObj.zyTexture};
        };

        if (!materialObj.xzTexture) {
          const texture = materialObj.xzTexture = new THREE.TextureLoader().load(`assets/textures/${materialName}_xz.jpg`);
          setTexProperties(texture);
          three.map = texture;
        }
        if (!materialObj.xyTexture) {
          const texture = materialObj.xyTexture = new THREE.TextureLoader().load(`assets/textures/${materialName}_xy.jpg`);
          setTexProperties(texture);
          //materialObj.debrisThree.map = texture;
        }
        if (!materialObj.zyTexture) {
          const texture = materialObj.zyTexture = new THREE.TextureLoader().load(`assets/textures/${materialName}_zy.jpg`);
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

// The following is injected shader code for texture coordinate mapping for
// geometry that's been generated by the Marching Cubes algorithm.
const wsPositionPreVertCode = 
`
varying vec3 wsPosition;
varying vec3 wsNormal;
`;
const wsPositionVertCode = `
wsPosition = vec3(position); //worldPosition.xyz/worldPosition.w;
wsNormal = objectNormal.xyz;
`;

const dynamicTexturePreFragCode = `
uniform sampler2D mapXZ;
uniform sampler2D mapZY;
uniform sampler2D mapXY;
varying vec3 wsPosition;
varying vec3 wsNormal;
`;

const dynamicTextureLookupFragCode = `
  {
    vec3 blendWeights = abs(normalize(wsNormal)) - 0.2;
    blendWeights *= 7.0;
    blendWeights = pow(blendWeights, vec3(3.0));
    blendWeights = max(vec3(0.0), blendWeights);
    blendWeights /= dot(blendWeights, vec3(1.0));

    vec2 zyCoord = wsPosition.zy;
    vec2 xzCoord = wsPosition.xz;
    vec2 xyCoord = wsPosition.xy;

    vec4 zyColour = texture2D(mapZY, zyCoord);
    vec4 xzColour = texture2D(mapXZ, xzCoord);
    vec4 xyColour = texture2D(mapXY, xyCoord);

    diffuseColor *= (zyColour * blendWeights.xxxx) + (xzColour * blendWeights.yyyy) + (xyColour * blendWeights.zzzz);
  }
`;