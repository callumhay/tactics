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
          const {type, landingRanges} = terrainColJsonObj;

          if (!type) { throw `Invalid terrain column entry found: ${terrainColJsonObj} could not find 'type'.`; }
          if (!landingRanges) { throw `Invalid terrain column entry found: ${terrainColJsonObj} could not find 'landingRanges'.`; }
        
          const terrainColumn = new TerrainColumn(
            battlefield, terrain.length, terrainCols.length, landingRanges
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