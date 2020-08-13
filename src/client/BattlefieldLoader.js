import TerrainColumn from './TerrainColumn';

class BattlefieldLoader {
  constructor() {}

  static loadFromString(bfFileStr, battlefield) {
    battlefield.clear();
    try {
      const terrain = [];

      // Each line defines the x-axis (width) of terrain columns in the map
      const lines = bfFileStr.split(/\r\n|\r|\n/g);
      lines.forEach((line, idx) => {
        const terrainColumns = [];

        // Seperate out all of the terrain columns on the current row
        const terrainColEntries = line.match(/\{(\w+(\(\d+\,\d+\))+)*\}/g);
        if (!terrainColEntries) {
          throw `Invalid terrain column formats on line ${idx+1}.`;
        }
        terrainColEntries.forEach(terrainColEntry => {
          // Match the type of terrain and the landing sites for the current terrain col
          const typeComponent = terrainColEntry.match(/([a-zA-Z]+)/g);
          if (!typeComponent || !typeComponent[0]) {
            throw `Invalid terrain column entry found: ${terrainColEntry} could not parse type name.`;
          }
          const terrainType = typeComponent[0];

          const rangeComponents = terrainColEntry.match(/(\(\d+\,\d+\))/g);
          if (!rangeComponents) {
            throw `Invalid terrain column entry found: ${terrainColEntry} could not parse range components.`;
          }
          
          const landingRanges = [];
          rangeComponents.forEach((rangeStr) => {
            const rangeNumbers = rangeStr.match(/(\d+),(\d+)/);
            landingRanges.push([parseFloat(rangeNumbers[1]), parseFloat(rangeNumbers[2])]);
          });

          const terrainColumn = new TerrainColumn(
            battlefield.terrainGroup, terrain.length, terrainColumns.length, landingRanges
          );
          terrainColumns.push(terrainColumn);
        });

        terrain.push(terrainColumns);
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