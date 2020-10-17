import { Plane, Box, ConvexPolyhedron, Cylinder, Shape, Sphere, Quaternion as CQuaternion, Trimesh, Vec3 } from 'cannon-es';
import { ConvexHull } from 'three/examples/jsm/math/ConvexHull.js';
import { Box3, BufferGeometry, Geometry, Math as _Math, Matrix4, Mesh, Quaternion, Vector3 } from 'three';

var PI_2 = Math.PI / 2;

var Type = {
  BOX: 'Box',
  CYLINDER: 'Cylinder',
  SPHERE: 'Sphere',
  HULL: 'ConvexPolyhedron',
  MESH: 'Trimesh',
  PLANE: 'Plane',
};

/**
 * Given a THREE.Object3D instance, creates a corresponding CANNON shape.
 * @param  {THREE.Object3D} object
 * @return {CANNON.Shape}
 */
export const threeToCannon = function (object, options) {
  options = options || {};

  var geometry;

  switch (options.type) {
    case Type.PLANE:
      return new Plane();
    case Type.BOX:
      return createBoundingBoxShape(object);
    case Type.CYLINDER:
      return createBoundingCylinderShape(object, options);
    case Type.SPHERE:
      return createBoundingSphereShape(object, options);
    case Type.HULL:
      return createConvexPolyhedron(object);
    case Type.MESH:
      geometry = getGeometry(object);
      return geometry ? createTrimeshShape(geometry) : null;
    default:
      if (options.type) { throw new Error('[CANNON.threeToCannon] Invalid type "%s".', options.type); }
      break;
  }

  geometry = getGeometry(object);
  if (!geometry) return null;

  var type = geometry.metadata
    ? geometry.metadata.type
    : geometry.type;

  switch (type) {
    case 'BoxGeometry':
    case 'BoxBufferGeometry':
      return createBoxShape(geometry);
    case 'CylinderGeometry':
    case 'CylinderBufferGeometry':
      return createCylinderShape(geometry);
    case 'PlaneGeometry':
    case 'PlaneBufferGeometry':
      return createPlaneShape(geometry);
    case 'SphereGeometry':
    case 'SphereBufferGeometry':
      return createSphereShape(geometry);
    case 'TubeGeometry':
    case 'Geometry':
    case 'BufferGeometry':
      return createBoundingBoxShape(object);
    default:
      console.warn('Unrecognized geometry: "%s". Using bounding box as shape.', geometry.type);
      return createBoxShape(geometry);
  }
};

threeToCannon.Type = Type;

/******************************************************************************
 * Shape construction
 */

 /**
  * @param  {THREE.Geometry} geometry
  * @return {CANNON.Shape}
  */
 function createBoxShape (geometry) {
   geometry.computeBoundingBox();
   var box = geometry.boundingBox;
   return new Box(new Vec3(
     (box.max.x - box.min.x) / 2,
     (box.max.y - box.min.y) / 2,
     (box.max.z - box.min.z) / 2
   ));
 }

/**
 * Bounding box needs to be computed with the entire mesh, not just geometry.
 * @param  {THREE.Object3D} mesh
 * @return {CANNON.Shape}
 */
function createBoundingBoxShape (object) {
  return createBoxShape(object.geometry);
}

/**
 * Computes 3D convex hull as a CANNON.ConvexPolyhedron.
 * @param  {THREE.Object3D} mesh
 * @return {CANNON.Shape}
 */
function createConvexPolyhedron (object) {
  // Compute the 3D convex hull.
  let hull = null;
  try {
    hull = new ConvexHull().setFromObject(new Mesh(object.geometry));
  }
  catch (err) {
    console.log("Invalid convex hull geometry, discarding.");
  }
  if (hull === null || hull.vertices.length === 0 || hull.faces.length === 0) {
    return null;
  }

  const vertices = [];
  const normals = [];
  const faces = [];

  const decimalShift = Math.log10(1 / 1e-4);
  const shiftMultiplier = Math.pow(10, decimalShift);
  const makeValueHash = (value) => {
    return `${~ ~(value * shiftMultiplier)}`; // ~ ~ truncates the value
  }
  const makeVertexHash = (vertex) => {
    const { x, y, z } = vertex;
    return `${makeValueHash(x)},${makeValueHash(y)},${makeValueHash(z)}`;
  }

  const vertexMap = {};

  for (const face of hull.faces) {
    let edge = face.edge;
    normals.push(new Vec3(face.normal.x, face.normal.y, face.normal.z));
    const faceIndices = [];
    do {
      const point = edge.head().point;
      const hash = makeVertexHash(point);
      let vertexIdx = -1;
      if (hash in vertexMap) {
        vertexIdx = vertexMap[hash];
      }
      else {
        vertexMap[hash] = vertexIdx = vertices.length;
        vertices.push(new Vec3(point.x, point.y, point.z));
      }
      faceIndices.push(vertexIdx);
      
      edge = edge.next;
    } while ( edge !== face.edge );
    faces.push(faceIndices);
  }

  return new ConvexPolyhedron({vertices, faces, normals});
}

/**
 * @param  {THREE.Geometry} geometry
 * @return {CANNON.Shape}
 */
function createCylinderShape (geometry) {
  var params = geometry.metadata
    ? geometry.metadata.parameters
    : geometry.parameters;

  var shape = new Cylinder(
    params.radiusTop,
    params.radiusBottom,
    params.height,
    params.radialSegments
  );

  // Include metadata for serialization.
  shape._type = Shape.types.CYLINDER; // Patch schteppe/cannon.js#329.
  shape.radiusTop = params.radiusTop;
  shape.radiusBottom = params.radiusBottom;
  shape.height = params.height;
  shape.numSegments = params.radialSegments;

  shape.orientation = new CQuaternion();
  shape.orientation.setFromEuler(_Math.degToRad(90), 0, 0, 'XYZ').normalize();
  return shape;
}

/**
 * @param  {THREE.Object3D} object
 * @return {CANNON.Shape}
 */
function createBoundingCylinderShape (object, options) {
  var axes = ['x', 'y', 'z'];
  var majorAxis = options.cylinderAxis || 'y';
  var minorAxes = axes.splice(axes.indexOf(majorAxis), 1) && axes;
  var box = new Box3().setFromObject(object);

  if (!isFinite(box.min.lengthSq())) return null;

  // Compute cylinder dimensions.
  var height = box.max[majorAxis] - box.min[majorAxis];
  var radius = 0.5 * Math.max(
    box.max[minorAxes[0]] - box.min[minorAxes[0]],
    box.max[minorAxes[1]] - box.min[minorAxes[1]]
  );

  // Create shape.
  var shape = new Cylinder(radius, radius, height, 12);

  // Include metadata for serialization.
  shape._type = Shape.types.CYLINDER; // Patch schteppe/cannon.js#329.
  shape.radiusTop = radius;
  shape.radiusBottom = radius;
  shape.height = height;
  shape.numSegments = 12;

  shape.orientation = new CQuaternion();
  shape.orientation.setFromEuler(
    majorAxis === 'y' ? PI_2 : 0,
    majorAxis === 'z' ? PI_2 : 0,
    0,
    'XYZ'
  ).normalize();
  return shape;
}

/**
 * @param  {THREE.Geometry} geometry
 * @return {CANNON.Shape}
 */
function createPlaneShape (geometry) {
  geometry.computeBoundingBox();
  var box = geometry.boundingBox;
  return new Box(new Vec3(
    (box.max.x - box.min.x) / 2 || 0.1,
    (box.max.y - box.min.y) / 2 || 0.1,
    (box.max.z - box.min.z) / 2 || 0.1
  ));
}

/**
 * @param  {THREE.Geometry} geometry
 * @return {CANNON.Shape}
 */
function createSphereShape (geometry) {
  var params = geometry.metadata
    ? geometry.metadata.parameters
    : geometry.parameters;
  return new Sphere(params.radius);
}

/**
 * @param  {THREE.Object3D} object
 * @return {CANNON.Shape}
 */
function createBoundingSphereShape (object, options) {
  if (options.sphereRadius) {
    return new Sphere(options.sphereRadius);
  }
  var geometry = getGeometry(object);
  if (!geometry) return null;
  geometry.computeBoundingSphere();
  return new Sphere(geometry.boundingSphere.radius);
}

/**
 * @param  {THREE.Geometry} geometry
 * @return {CANNON.Shape}
 */
function createTrimeshShape (geometry) {
  var vertices = getVertices(geometry);
  if (!vertices.length) { return null; }
  return new Trimesh(vertices, geometry.index.array);
}

/******************************************************************************
 * Utils
 */

/**
 * Returns a single geometry for the given object. If the object is compound,
 * its geometries are automatically merged.
 * @param {THREE.Object3D} object
 * @return {THREE.Geometry}
 */
function getGeometry (object) {
  return object.geometry;
}

/**
 * @param  {THREE.Geometry} geometry
 * @return {Array<number>}
 */
function getVertices (geometry) {
  return geometry.getAttribute('position').array;
}