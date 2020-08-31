import TerrainColumn from './TerrainColumn';
import GameMaterials from './GameMaterials';
import { assert } from 'chai';

const isEmpty = (obj) => { return Object.keys(obj).length === 0; }

class BattlefieldLoader {
  static loadFromJsonObj(battlefieldJsonObj, battlefield) {
    battlefield.clear();
    try {
      const terrain = [];
      const {terrain:terrainJsonObj} = battlefieldJsonObj;
      if (!terrain) { throw `Invalid terrain entry found could not find 'terrain'.`; }
      for (const terrainLineJsonObj of terrainJsonObj) {
        const terrainCols = [];
        for (const terrainColJsonObj of terrainLineJsonObj) {
          // The terrain column may have multiple material groupings
          const {matgroups} = terrainColJsonObj;
          let terrainColumn = null;
          if (matgroups) {
            for (const matgroup of matgroups) {
              BattlefieldLoader.verifyMaterialGroup(matgroup);
            }
            terrainColumn = new TerrainColumn(
              battlefield, terrain.length, terrainCols.length, (matgroups ? matgroups.filter(m => !isEmpty(m)) : [])
            );
          }
          else {
            BattlefieldLoader.verifyMaterialGroup(terrainColJsonObj);
            terrainColumn = new TerrainColumn(battlefield, terrain.length, terrainCols.length, 
              isEmpty(terrainColJsonObj) ? [] : [terrainColJsonObj]);
          }


          terrainCols.push(terrainColumn);
        }
        terrain.push(terrainCols);
      }
      battlefield.setTerrain(terrain);
    }
    catch (err) {
      console.error(err);
      battlefield.clear();
    }
  }

  static verifyMaterialGroup(materialGroup) {
    assert(materialGroup, "There must be an object (even if its an empty one) to represent a given terrain column.");
    if (isEmpty(materialGroup)) {
      // Empty object just means the terrain column has nothing in it
      return null;
    }

    const { material, geometry } = materialGroup;
    if (!material) { throw `Invalid terrain column entry found: ${materialGroup} could not find 'material'.`; }
    if (!GameMaterials.materials[material]) { throw `Invalid material type found: ${material} could not find a matching lookup.`; }
    if (!geometry) { throw `Invalid terrain column entry found: ${materialGroup} could not find 'geometry'.`; }

    BattlefieldLoader.verifyGeometry(geometry);
    return materialGroup;
  }

  static verifyGeometry(geometry) {
    assert(geometry !== null, "Geometry should have already been verified to exist.");

    for (const geomPiece of geometry) {
      const {type} = geomPiece;
      if (!type) { throw `Invalid geometry entry found: ${geometry} could not find 'type'.`; }

      switch (type) {
        case "box":
          const {range} = geomPiece;
          if (!range) { throw `Invalid box geometry entry found: ${geomPiece} could not find 'range'.`; }
          const [startY, endY] = range;
          if (startY < 0 || endY < 0) { throw `Invalid range in box geometry found: ${geomPiece} ranges must be >= 0.`; }
          if (endY <= startY) { throw `Invalid range in box geometry found: ${geomPiece} The start of the range must be strictly less than the end.`; }
          break;
        default:
          throw `Invalid geometry type ${type} found, no loader is available for this type.`;
      }
    }
  }

}

export default BattlefieldLoader;