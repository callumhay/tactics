import TerrainColumn from './TerrainColumn';

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
          const materialGroups = [];

 
          if (matgroups) {
            for (const matgroup of matgroups) {
              materialGroups.push(BattlefieldLoader.verifyMaterialGroup(matgroup));
            }
          }
          else {
            materialGroups.push(BattlefieldLoader.verifyMaterialGroup(terrainColJsonObj));
          }

          const terrainColumn = new TerrainColumn(
            battlefield, terrain.length, terrainCols.length, materialGroups.filter(m => m !== null)
          );
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
    if (Object.keys(materialGroup).length === 0) {
      // Empty object just means no landing ranges are present
      return null;
    }
    const { type, landingRanges } = materialGroup;
    if (!type) { throw `Invalid terrain column entry found: ${materialGroup} could not find 'type'.`; }
    if (!landingRanges) { throw `Invalid terrain column entry found: ${materialGroup} could not find 'landingRanges'.`; }
    return {type: type, landingRanges: landingRanges};
  }

}

export default BattlefieldLoader;