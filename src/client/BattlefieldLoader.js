import TerrainColumn from './TerrainColumn';

class BattlefieldLoader {
  static loadFromJsonObj(battlefieldJsonObj, battlefield) {
    battlefield.clear();
    try {
      const terrain = [];
      const {terrain:terrainJsonObj} = battlefieldJsonObj;
      terrainJsonObj.forEach(terrainLineJsonObj => {
        const terrainCols = [];
        terrainLineJsonObj.forEach(terrainColJsonObj => {
          const {type, landingRanges} = terrainColJsonObj;

          if (!type) { throw `Invalid terrain column entry found: ${terrainColJsonObj} could not parse type.`; }
          if (!landingRanges) { throw `Invalid terrain column entry found: ${terrainColJsonObj} could not parse landing ranges.`; }
        
          const terrainColumn = new TerrainColumn(
            battlefield.terrainGroup, terrain.length, terrainCols.length, landingRanges
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
}

export default BattlefieldLoader;