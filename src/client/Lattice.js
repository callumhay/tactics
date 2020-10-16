import * as THREE from 'three';
import { assert } from 'chai';
import TerrainColumn from './TerrainColumn';

const NUM_NODES_PER_UNIT = 5;

class Lattice {
  constructor(terrainGroup, nodesPerUnit=NUM_NODES_PER_UNIT) {
    this._nodesPerUnit = nodesPerUnit;
    this.terrainGroup  = terrainGroup;
  }

  get numNodesPerUnit() {
    return this._nodesPerUnit;
  }
  get unitsBetweenNodes() {
    return TerrainColumn.SIZE / this.numNodesPerUnit;
  }
  get unitsPerNode() {
    return this.unitsBetweenNodes;
  }
  get nodeVolume() {
    return Math.pow(this.unitsPerNode,3);
  }
  get halfUnitsBetweenNodes() {
    return this.unitsBetweenNodes / 2;
  }
  get diagonalUnitsBetweenNodes() {
    return Math.SQRT2*this.unitsBetweenNodes;
  }

  initNodeSpace(xUnitSize, zUnitSize, yUnitSize) {
    assert("Abstract method 'initNodeSpace' called in Lattice.");
  }
  addTerrainColumnBox(terrainColumn, config) { 
    assert("Abstract method 'addTerrainColumnBox' called in Lattice.");
  }

  _posToNodeIndex(unitPos, target) {
    target.set(
      this._unitsToNodeIndex(unitPos.x), 
      this._unitsToNodeIndex(unitPos.y), 
      this._unitsToNodeIndex(unitPos.z)
    );
    return target;
  } 
  _unitsToNodeIndex(unitVal) {
    return Math.floor(unitVal * this.numNodesPerUnit);
  }
  _nodeIndexToUnits(idx) {
    return idx*this.unitsBetweenNodes + this.halfUnitsBetweenNodes;
  }
  _nodeIndexToPosition(xIdx, yIdx, zIdx, target) {
    return target.set(this._nodeIndexToUnits(xIdx), this._nodeIndexToUnits(yIdx), this._nodeIndexToUnits(zIdx));
  }

  _getIndexRangeForBoundingBox(boundingBox) {
    const {min, max} = boundingBox;
    return this._getIndexRangeForMinMax(min, max);
  }
  _getIndexRangeForMinMax(min, max) {
    const {clamp} = THREE.MathUtils;
    return {
      nodeXIdxStart: clamp(this._unitsToNodeIndex(min.x), 0, this.nodes.length-1),
      nodeXIdxEnd: clamp(this._unitsToNodeIndex(max.x), 0, this.nodes.length-1),
      nodeYIdxStart: Math.max(0, this._unitsToNodeIndex(min.y)),
      nodeYIdxEnd: Math.max(0, this._unitsToNodeIndex(max.y)),
      nodeZIdxStart: Math.max(0, this._unitsToNodeIndex(min.z)),
      nodeZIdxEnd: Math.max(0, this._unitsToNodeIndex(max.z))
    };
  }
  _getXZIndexRangeForTerrainColumn(terrainColumn) {
    const {xIndex, zIndex} = terrainColumn;
    const numNodesPerUnitMinusOne = (this.numNodesPerUnit - 1);
    const nodeXIdxStart = this._unitsToNodeIndex(xIndex * TerrainColumn.SIZE);
    const nodeZIdxStart = this._unitsToNodeIndex(zIndex * TerrainColumn.SIZE);
    return {
      nodeXIdxStart,
      nodeXIdxEnd: nodeXIdxStart + numNodesPerUnitMinusOne,
      nodeZIdxStart,
      nodeZIdxEnd: nodeZIdxStart + numNodesPerUnitMinusOne,
    };
  }

}

export default Lattice;