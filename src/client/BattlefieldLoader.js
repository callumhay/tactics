import TerrainColumn from './TerrainColumn';

class BattlefieldLoader {
  static loadFromJsonObj(battlefieldJsonObj, battlefield) {
    battlefield.clear();
    try {
      const terrain = [];
      const {terrain:terrainJsonObj} = battlefieldJsonObj;
      if (!terrain) { throw `Invalid terrain entry found could not find 'terrain'.`; }
      terrainJsonObj.forEach(terrainLineJsonObj => {
        const terrainCols = [];
        terrainLineJsonObj.forEach(terrainColJsonObj => {

          // The terrain column may have multiple material groupings
          const {matGroups} = terrainColJsonObj;
          const materialGroups = [];
          if (matGroups) {
            matGroups.forEach(matGroup => {
              materialGroups.push(BattlefieldLoader.verifyMaterialGroup(matGroup));
            });
          }
          else {
            materialGroups.push(BattlefieldLoader.verifyMaterialGroup(terrainColJsonObj));
          }

          const terrainColumn = new TerrainColumn(
            battlefield, terrain.length, terrainCols.length, materialGroups
          );
          terrainCols.push(terrainColumn);
        });
        terrain.push(terrainCols);
      });
      battlefield.setTerrain(terrain);
    }
    catch (err) {
      console.error(err);
      battlefield.clear();
    }
  }

  static verifyMaterialGroup(materialGroup) {
    const { type, landingRanges } = materialGroup;
    if (!type) { throw `Invalid terrain column entry found: ${materialGroup} could not find 'type'.`; }
    if (!landingRanges) { throw `Invalid terrain column entry found: ${materialGroup} could not find 'landingRanges'.`; }
    return {type: type, landingRanges: landingRanges};
  }

}

export default BattlefieldLoader;