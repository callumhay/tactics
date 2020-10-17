import * as THREE from 'three';
import {OrbitControls} from 'three/examples/jsm/controls/OrbitControls';

import GameModel from './GameModel';
import GameClient from './GameClient';

// Setup THREE library boilerplate for getting a scene + camera + basic controls up and running
const renderer = new THREE.WebGLRenderer();
renderer.autoClear = false;
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;

renderer.setClearColor(new THREE.Color(0xB1E1FF));
document.body.appendChild(renderer.domElement);

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(75, window.innerWidth/window.innerHeight, 0.1, 1000);
const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = false;

// Make sure the camera is positioned somewhere where we can see everything we need to at initial render
scene.position.set(0,0,0);
camera.position.x = 10;
camera.position.y = 10;
camera.position.z = 10;
controls.update();

// Constants used to make window resizing a bit more user-friendly
const originalTanFOV = Math.tan(((Math.PI / 180) * camera.fov / 2));
const originalWindowHeight = window.innerHeight;

// Event Listeners
// -----------------------------------------------------------------------------
window.addEventListener('resize', onWindowResize, false);
function onWindowResize(event) {

  camera.aspect = window.innerWidth / window.innerHeight;
  camera.fov = (360 / Math.PI) * Math.atan(originalTanFOV * (window.innerHeight / originalWindowHeight));
  camera.updateProjectionMatrix();
  camera.lookAt(scene.position);

  controls.update();

  renderer.setSize(window.innerWidth, window.innerHeight);
  renderer.render(scene, camera);
}

// Setup game objects
const gameModel = new GameModel(scene);
// Setup the client, connect to the game server
const client = new GameClient();
client.start(gameModel);

const raycaster = new THREE.Raycaster();
window.addEventListener('click', onMouseClick, false);
function onMouseClick(event) {
  event.preventDefault();

  const mousePos = new THREE.Vector2((event.clientX/window.innerWidth)*2-1, -(event.clientY/window.innerHeight)*2+1);
  camera.updateMatrixWorld();
  raycaster.setFromCamera(mousePos, camera);

  const intersects = raycaster.intersectObjects(gameModel.battlefield.terrainGroup.children, true);
  if (intersects.length > 0) {
    let intersectingTerrain = null;
    for (let i = 0; i < intersects.length; i++) {
      if (intersects[i].object && intersects[i].object.terrainLandingRange) { intersectingTerrain = intersects[i]; break; }
    }
    if (!intersectingTerrain) {
      return;
    }

    const {object, point} = intersectingTerrain;

    const group = object.parent;
    const groupInvTransform = new THREE.Matrix4();
    groupInvTransform.getInverse(group.matrixWorld);

    const BLAST_RADIUS = 1;
    const blastGeometry = new THREE.DodecahedronBufferGeometry(BLAST_RADIUS, 0);
    blastGeometry.translate(point.x, point.y, point.z);
    blastGeometry.applyMatrix4(groupInvTransform);
    //scene.add(new THREE.Mesh(blastGeometry));
    gameModel.battlefield.blowupTerrain(blastGeometry);
    blastGeometry.dispose();
  }
}

// Setup and execute the game loop
const clock = new THREE.Clock(true);
const render = function () {
  let dt = clock.getDelta();

  requestAnimationFrame(render);

  // Updates for physics/controls/sound/etc.
  gameModel.update(dt);
  controls.update();

  // Render the scene
  renderer.clear();
  renderer.render(scene, camera);
};
render();