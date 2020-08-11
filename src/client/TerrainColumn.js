import * as THREE from 'three';

class TerrainColumn {
  static get SIZE() { return 1; }
  static get HALF_SIZE() { return TerrainColumn.SIZE/2; }

  constructor(terrainGrp, u, v, landingRanges) {
    this.uIndex = u;
    this.vIndex = v;
    this.landingRanges = landingRanges;

    this.geometries = [];
    this.meshes = [];
    this.material = new THREE.MeshLambertMaterial({color: 0xffffff});

    this.landingRanges.forEach(range => {
      const [start, end] = range;
      const height = end-start;

      const geometry = new THREE.BoxBufferGeometry(TerrainColumn.SIZE, height*TerrainColumn.SIZE, TerrainColumn.SIZE);
      const mesh = new THREE.Mesh(geometry, this.material);
      mesh.translateX(this.uIndex*TerrainColumn.SIZE + TerrainColumn.HALF_SIZE);
      mesh.translateZ(this.vIndex*TerrainColumn.SIZE + TerrainColumn.HALF_SIZE);
      mesh.translateY(start + height/2);
      mesh.terrainColumn = this;

      this.geometries.push(geometry);
      this.meshes.push(mesh);

      terrainGrp.add(mesh);
    });
  }
}

export default TerrainColumn;