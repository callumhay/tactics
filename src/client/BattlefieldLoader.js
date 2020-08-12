import TerrainColumn from './TerrainColumn';

class BattlefieldLoader {
  constructor() {}

  static loadFromString(bfFileStr, battlefield) {
    battlefield.clear();
    try {
      const terrain = [];

      // Each line defines the x-axis (width) of terrain columns in the map
      const lines = bfFileStr.split(/\r\n|\r|\n/g);
      lines.forEach(line => {
        const terrainColumns = [];

        // Seperate out all of the terrain columns on the current row
        const terrainColEntries = line.match(/\{(\w+\(\d+\,\d+\))(,\w+\(\d+\,\d+\))*\}/g);
        terrainColEntries.forEach(terrainColEntry => {
          // Match the type of terrain and the landing sites for the current terrain col
          const entryComponents = terrainColEntry.match(/\{(\w+)\((\d+)\,(\d+)\)\}/);
          if (entryComponents.length < 3 || (entryComponents.length-2) % 2 !== 0) {
            throw `Invalid terrain column entry found: ${terrainColEntry} could not parse.`;
          }
          
          const terrainType = entryComponents[1];
          const landingRanges = [];
          for (let i = 2; i < entryComponents.length; i += 2) {
            landingRanges.push([parseFloat(entryComponents[i]), parseFloat(entryComponents[i+1])]);
          }

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