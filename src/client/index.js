import * as THREE from 'three';
import CSG from 'three-csg';
import {OrbitControls} from 'three/examples/jsm/controls/OrbitControls';
import {BufferGeometryUtils} from 'three/examples/jsm/utils/BufferGeometryUtils';
import * as CANNON from 'cannon';
import Battlefield from './Battlefield';


// Setup Cannon library
const physicsWorld = new CANNON.World();
physicsWorld.gravity.set(0,0,0);
physicsWorld.broadphase = new CANNON.NaiveBroadphase();
physicsWorld.solver.iterations = 10;


// Setup THREE library boilerplate for getting a scene + camera + basic controls up and running
const renderer = new THREE.WebGLRenderer();
renderer.autoClear = false;
renderer.setSize(window.innerWidth, window.innerHeight);
document.body.appendChild(renderer.domElement);

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(75, window.innerWidth/window.innerHeight, 0.1, 1000);
const controls = new OrbitControls(camera, renderer.domElement);

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

//let intersectedObj = null;
const raycaster = new THREE.Raycaster();
window.addEventListener('click', onMouseClick, false);
function onMouseClick(event) {
  event.preventDefault();

  const mousePos = new THREE.Vector2((event.clientX/window.innerWidth)*2-1, -(event.clientY/window.innerHeight)*2+1);
  camera.updateMatrixWorld();
  raycaster.setFromCamera(mousePos, camera);

  const intersects = raycaster.intersectObjects(battlefield.terrainGroup.children, true);
  //if (intersectedObj) {
  //  intersectedObj.material.emissive.setHex(intersectedObj.currentHex);
  //}
  if (intersects.length > 0) {
    let intersectingTerrain = null;
    for (let i = 0; i < intersects.length; i++) {
      if (intersects[i].object && intersects[i].object.terrainColumn) { intersectingTerrain = intersects[i]; break; }
    }
    if (!intersectingTerrain) {
      return;
    }

    const {object, point} = intersectingTerrain;

    const BLAST_RADIUS = 1;
    // Based on where the point is intersecting, it's easy to find all the terrain squares that might be caught in the sphere (blast radius)



    const sphere = new THREE.SphereBufferGeometry(BLAST_RADIUS, 10, 10);
    sphere.translate(point.x, point.y, point.z);
    //scene.add(new THREE.Mesh(sphere));

    const group = object.parent;
    const groupTransform = group.matrixWorld;

    object.geometry.applyMatrix4(object.matrixWorld);
    let csgGeometry = CSG.subtract([object.geometry, sphere]);
    
    group.remove(object);

    let newGeometry = CSG.BufferGeometry(csgGeometry);
    newGeometry.applyMatrix4(groupTransform.getInverse(groupTransform));
 
    const newMesh = new THREE.Mesh(newGeometry, new THREE.MeshPhongMaterial({color: 0xffffff}));
    newMesh.terrainColumn = object.terrainColumn;
    group.add(newMesh);

    object.geometry.dispose();

    //intersectedObj = newMesh;
    //intersectedObj.currentHex = intersectedObj.material.emissive.getHex();
    //intersectedObj.material.emissive.setHex( 0xff0000 );


  }
  else {
    //intersectedObj = null; 
  }
}


/*
// Temporary stuff just to get this working / test
const shape = new CANNON.Box(new CANNON.Vec3(1,1,1));
const body = new CANNON.Body({mass: 1});
body.addShape(shape);
body.angularVelocity.set(0,10,0);
body.angularDamping = 0.1;
physicsWorld.addBody(body);

const geometry = new THREE.BoxGeometry(2, 2, 2);
const material = new THREE.MeshBasicMaterial({ color: 0xff0000, wireframe: true });
const mesh = new THREE.Mesh(geometry, material);
scene.add(mesh);
*/
const battlefield = new Battlefield(scene);

const updatePhysics = (dt) => {
  // Step the physics world
  physicsWorld.step(dt);

  // Copy coordinates from Cannon.js to Three.js
  // TODO
  //mesh.position.copy(body.position);
  //mesh.quaternion.copy(body.quaternion);
}

let lastFrameTime = Date.now();
const render = function () {
  let currFrameTime = Date.now();
  let dt = (currFrameTime - lastFrameTime) / 1000;

  requestAnimationFrame(render);

  // Updates for physics/controls/sound/etc.
  updatePhysics(dt);
  controls.update();

  // Render the scene
  renderer.clear();
  renderer.render(scene, camera);
};
render();