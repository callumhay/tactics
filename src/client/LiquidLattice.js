import * as THREE from 'three';
import { assert } from 'chai';
import GamePhysics from './GamePhysics';
import Lattice from './Lattice';
import Debug from '../debug';

const LIQUID_DENSITY      = 1000;   // kg/m^3
const ATMO_PRESSURE       = 101325; // N/m^2 (or Pascals)
const MAX_GRAVITY_VEL     = 11;     // m/s
const MAX_PRESSURE_VEL    = 8;     // m/s
const PRESSURE_MAX_HEIGHT = 5;     // m

const PRESSURE_ITERS = 10;
//const DIFFUSE_ITERS  = 10;

const SOLID_NODE_TYPE = 1;
const EMPTY_NODE_TYPE = 0;

const UNSETTLED_NODE = 0;
const SETTLED_NODE   = 1;

const LIQUID_EPSILON = 1e-6;

const NODE_VOL_IDX     = 0;
const NODE_TYPE_IDX    = 1;
const NODE_SETTLED_IDX = 2;

const OFFSET = 1;

class LiquidLattice extends Lattice {
  constructor(terrainGroup, gpuManager) {
    super(terrainGroup);
    this.gpuManager = gpuManager;

    this.gravity = GamePhysics.GRAVITY;
    this.vorticityConfinement = 0.012;
    this.viscosity = 0.00015;
    
    this._buffersInit = false;
    this.clear();
  }

  _clearBuffers() {
    if (!this._buffersInit) {return;}
    this.tempBuffScalar.delete();
    this.pressureField.delete();
    this.tempPressure.delete();
    this.tempBuffVec3.delete();
    this.velField.delete();
    this.flowFieldLRB.delete();
    this.flowFieldDUT.delete();
    this.flowSumField.delete();
    this._buffersInit = false;
  }
  _initBuffers() {
    this.tempBuffScalar = this.gpuManager.buildLiquidBufferScalar();
    this.pressureField  = this.gpuManager.buildLiquidBufferScalar();
    this.tempPressure   = this.gpuManager.buildLiquidBufferScalar();
    this.tempBuffVec3   = this.gpuManager.buildLiquidBufferVec3();
    this.velField       = this.gpuManager.buildLiquidBufferVec3();
    this.flowFieldLRB   = this.gpuManager.buildLiquidBufferVec3();
    this.flowFieldDUT   = this.gpuManager.buildLiquidBufferVec3();
    this.flowSumField   = this.gpuManager.buildLiquidBufferScalar();
    this._buffersInit = true;
  }

  clear() {
    this.dtAccumulate = 0;
    this.nodes = [];
    this._clearBuffers();
    this.gpuManager.clearLiquidKernels();
    this._clearDebugDraw();
  }

  initNodeSpace(xUnitSize, zUnitSize, yUnitSize) {
    const sizeXYZ = [
      xUnitSize * this.numNodesPerUnit,
      yUnitSize * this.numNodesPerUnit,
      zUnitSize * this.numNodesPerUnit,
    ];
    const constants = {
      SOLID_NODE_TYPE, EMPTY_NODE_TYPE, 
      SETTLED_NODE, UNSETTLED_NODE,
      MAX_GRAVITY_VEL, MAX_PRESSURE_VEL, PRESSURE_MAX_HEIGHT,
      LIQUID_DENSITY, LIQUID_EPSILON, ATMO_PRESSURE,
      NODE_VOL_IDX, NODE_TYPE_IDX, NODE_SETTLED_IDX,
    };
    
    this.gpuManager.reinitLiquidKernels(sizeXYZ, this.unitsPerNode, constants);
    this._initBuffers();

    // Make sure the nodes array is completely filled
    const numNodesXYZ = [sizeXYZ[0] + 2*OFFSET, sizeXYZ[1] + 2*OFFSET, sizeXYZ[2] + 2*OFFSET];
    while (this.nodes.length < numNodesXYZ[0]) { this.nodes.push([]); }
    assert(this.nodes.length === numNodesXYZ[0]);
    for (let x = 0; x < numNodesXYZ[0]; x++) {
      const nodesX = this.nodes[x];
      while (nodesX.length < numNodesXYZ[1]) { nodesX.push([]); }
      assert(nodesX.length === numNodesXYZ[1]);
      for (let y = 0; y < numNodesXYZ[1]; y++) {
        const nodesY = nodesX[y];
        while (nodesY.length < numNodesXYZ[2]) { 
          nodesY.push(new Float32Array([0,EMPTY_NODE_TYPE,SETTLED_NODE]));
        }
        assert(nodesY.length === numNodesXYZ[2]);
      }
    }

    // Add boundaries (6 walls for +/- x,y,z) around the outside
    for (let x = 0; x < numNodesXYZ[0]; x++) {
      // xy plane bounds (x2)
      for (let y = 0; y < numNodesXYZ[1]; y++) {
        this.nodes[x][y][0][NODE_TYPE_IDX] = SOLID_NODE_TYPE;
        this.nodes[x][y][numNodesXYZ[2]-1][NODE_TYPE_IDX] = SOLID_NODE_TYPE;
      }
      // xz plane bounds (x2)
      for (let z = 0; z < numNodesXYZ[2]; z++) {
        this.nodes[x][0][z][NODE_TYPE_IDX] = SOLID_NODE_TYPE;
        this.nodes[x][numNodesXYZ[1]-1][z][NODE_TYPE_IDX] = SOLID_NODE_TYPE;
      }
    }
    for (let y = 0; y < numNodesXYZ[1]; y++) {
      // yz plane bounds (x2)
      for (let z = 0; z < numNodesXYZ[2]; z++) {
        this.nodes[0][y][z][NODE_TYPE_IDX] = SOLID_NODE_TYPE;
        this.nodes[numNodesXYZ[0]-1][y][z][NODE_TYPE_IDX] = SOLID_NODE_TYPE;
      }
    }
 
    this.debugDrawNodes(true);
  }
  
  // Adds water to the lattice
  addTerrainColumnBox(terrainColumn, config) {
    const {startY, endY} = config;
    let {nodeXIdxStart, nodeXIdxEnd, nodeZIdxStart, nodeZIdxEnd} = 
      this._getXZIndexRangeForTerrainColumn(terrainColumn);

    let nodeYIdxStart = this._unitsToNodeIndex(startY) + OFFSET;
    let nodeYIdxEnd   = this._unitsToNodeIndex(endY) + OFFSET;
    nodeXIdxStart += OFFSET, nodeXIdxEnd += OFFSET;
    nodeZIdxStart += OFFSET, nodeZIdxEnd += OFFSET;

    const nodeVolume = this.nodeVolume;
    while (this.nodes.length <= nodeXIdxEnd) { this.nodes.push([]); }
    for (let x = nodeXIdxStart; x <= nodeXIdxEnd; x++) {
      const nodesX = this.nodes[x];
      while (nodesX.length <= nodeYIdxEnd) { nodesX.push([]); }
      for (let y = nodeYIdxStart; y <= nodeYIdxEnd; y++) {
        const nodesY = nodesX[y];
        while (nodesY.length <= nodeZIdxEnd) {
          nodesY.push(new Float32Array([0, EMPTY_NODE_TYPE, UNSETTLED_NODE]));
        }
        for (let z = nodeZIdxStart; z <= nodeZIdxEnd; z++) {
          const currNode = nodesY[z];
          currNode[NODE_VOL_IDX] = nodeVolume;
          currNode[NODE_TYPE_IDX] = EMPTY_NODE_TYPE;
          currNode[NODE_SETTLED_IDX] = UNSETTLED_NODE;
        }
      }
    }
    this.debugDrawNodes(true);
  }

  setTerrainNodes(terrainLattice) {
    const {numNodesPerUnit:tlNodesPerUnit, nodes:tlNodes} = terrainLattice;
    const {numNodesPerUnit:wlNodesPerUnit, nodes:wlNodes} = this;
    assert(tlNodesPerUnit === wlNodesPerUnit, 
      "Different nodes per unit in water vs. terrain lattice - requires implementation!");

    for (let x = OFFSET; x < wlNodes.length-OFFSET; x++) {
      const tlMappedX = x-OFFSET;
      for (let y = OFFSET; y < wlNodes[x].length-OFFSET; y++) {
        const tlMappedY = y-OFFSET;
        for (let z = OFFSET; z < wlNodes[x][y].length-OFFSET; z++) {
          const tlMappedZ = z-OFFSET;
          const wlNode = wlNodes[x][y][z];
          if (tlNodes[tlMappedX] && tlNodes[tlMappedX][tlMappedZ] && tlNodes[tlMappedX][tlMappedZ][tlMappedY]) {
            // TODO: If there's liquid in this node then we need to displace it somewhere
            wlNode[NODE_VOL_IDX] = 0;
            wlNode[NODE_TYPE_IDX] = SOLID_NODE_TYPE;
            wlNode[NODE_SETTLED_IDX] = SETTLED_NODE;
          }
          else {
            wlNode[NODE_TYPE_IDX] = EMPTY_NODE_TYPE;
            wlNode[NODE_SETTLED_IDX] = UNSETTLED_NODE;
          }
        }
      }
    }
    this.debugDrawNodes(true);
  }

  update(dt) {
    const MAX_REFRESH_S = 1/15;
    this.dtAccumulate += dt;
    if (!this._buffersInit || this.dtAccumulate < MAX_REFRESH_S) { return; }
    this.dtAccumulate -= MAX_REFRESH_S;
    // Enforce CFL condition on dt
    //dt = this._applyCFL(dt);

    this._advectVelocity(dt);
    this._applyExternalForces(dt);
    this._applyVorticityConfinement(dt);
    this._computeDivergence();
    this._computePressure();
    this._projectVelocityFromPressure();

    let temp = this.flowFieldLRB;
    this.flowFieldLRB = this.gpuManager.liquidCalcFlowsLRB(dt, this.velField, this.nodes);
    temp.delete();
    temp = this.flowFieldDUT;
    this.flowFieldDUT = this.gpuManager.liquidCalcFlowsDUT(dt, this.velField, this.nodes);
    temp.delete();

    temp = this.flowSumField;
    this.flowSumField = this.gpuManager.liquidSumFlows(this.flowFieldLRB, this.flowFieldDUT, this.nodes);
    temp.delete();

    // The final method overwrites the node array with a new CPU array 
    // (no delete here, this method is not pipelined).
    this.nodes = this.gpuManager.liquidAdjustFlows(this.flowSumField, this.nodes);
    this.debugDrawNodes(true);
    
  }

  _applyCFL(dt) {
    return Math.min(dt, 0.3*this.unitsPerNode/(Math.max(MAX_GRAVITY_VEL, MAX_PRESSURE_VEL)));
  }

  _advectVelocity(dt) {
    let temp = this.velField;
    this.velField = this.gpuManager.liquidAdvectVel(dt, this.velField, this.nodes);
    temp.delete();
  }

  _applyExternalForces(dt) {
    let temp = this.velField;
    this.velField = this.gpuManager.liquidApplyExtForces(dt, this.gravity, this.velField, this.nodes);
    temp.delete();
  }

  _applyVorticityConfinement(dt) {
    let temp = this.tempBuffVec3;
    this.tempBuffVec3 = this.gpuManager.liquidCurl(this.velField);
    temp.delete();

    temp = this.tempBuffScalar;
    this.tempBuffScalar = this.gpuManager.liquidCurlLen(this.tempBuffVec3);
    temp.delete();

    const dtVC = this._applyCFL(dt*this.vorticityConfinement);
    temp = this.velField;
    this.velField = this.gpuManager.liquidApplyVC(dtVC, this.velField, this.tempBuffVec3, this.tempBuffScalar);
    temp.delete();
  }

  _computeDivergence() {
    let temp = this.tempBuffScalar;
    this.tempBuffScalar = this.gpuManager.liquidDiv(this.velField, this.nodes);
    temp.delete();
  }

  _computePressure(numIter=PRESSURE_ITERS) {
    this.pressureField.clear();
    let temp = null;
    for (let i = 0; i < numIter; i++) {
      temp = this.pressureField;
      this.pressureField = this.gpuManager.liquidComputePressure(
        this.pressureField, this.nodes, this.tempBuffScalar
      );
      temp.delete();
    }
  }
  _projectVelocityFromPressure() {
    let temp = this.velField;
    this.velField = this.gpuManager.liquidProjVel(this.pressureField, this.velField, this.nodes);
    temp.delete();
  }

  _clearDebugDraw() {
    if (this.debugNodePoints) {
      this.terrainGroup.remove(this.debugNodePoints);
      this.debugNodePoints.geometry.dispose();
      this.debugNodePoints = null;
    }
  }
  debugDrawNodes(show=true) {
    const {clamp} = THREE.MathUtils;
    this._clearDebugDraw();
    if (show) {
      const nodeVolume = this.nodeVolume;
      const nodeGeometry = new THREE.BufferGeometry();
      const vertices = [];
      const colours  = [];
      const pos = new THREE.Vector3();
      for (let x = 0; x < this.nodes.length; x++) {
        for (let y = 0; y < this.nodes[x].length; y++) {
          for (let z = 0; z < this.nodes[x][y].length; z++) {
            const currNode = this.nodes[x][y][z];
            this._nodeIndexToPosition(x-OFFSET,y-OFFSET,z-OFFSET,pos);
            switch (currNode[NODE_TYPE_IDX]) {
              case SOLID_NODE_TYPE:
                //vertices.push(pos.x); vertices.push(pos.y); vertices.push(pos.z);
                //colours.push(0); colours.push(0); colours.push(0);
                break;
              case EMPTY_NODE_TYPE:
                const volumePct = clamp(5*currNode[NODE_VOL_IDX]/nodeVolume,0,1);
                if (volumePct > 0) {
                  vertices.push(pos.x); vertices.push(pos.y); vertices.push(pos.z);
                  colours.push(0); colours.push(volumePct/2); colours.push(volumePct);
                }
                break;
              default:
                assert(false);
                break;
            }
          }
        }
      }

      nodeGeometry.setAttribute('position', new THREE.BufferAttribute(new Float32Array(vertices), 3));
      nodeGeometry.setAttribute('color', new THREE.BufferAttribute(new Float32Array(colours), 3));
      this.debugNodePoints = new THREE.Points(nodeGeometry, new THREE.PointsMaterial({color:0xFFFFFF, vertexColors:true, size:this.unitsBetweenNodes/5, depthFunc:THREE.AlwaysDepth}));
      this.debugNodePoints.renderOrder = Debug.NODE_DEBUG_RENDER_ORDER;
      this.terrainGroup.add(this.debugNodePoints);
    }
  }

}

export default LiquidLattice;