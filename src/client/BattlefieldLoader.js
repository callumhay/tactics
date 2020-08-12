
import fs from 'fs';

import Battlefield from './Battlefield';

class BattlefieldLoader {
  constructor() {}

  static load(bfFileStr, battlefield) {



    const reader = new FileReader();
    reader.addEventListener('load', event => {
      const data = event.target.result;
      console.log(data);
    });
    reader.addEventListener('progress', event => {
      if (event.loaded && event.total) {
        const percent = Math.floor((event.loaded / event.total) * 100);
        console.log(`Loading '${bfFilepath}... ${percent}`);
      }
    });
    reader.addEventListener('error', event => {
      console.error(`Failed to load file ${bfFilepath}: ${reader.error}`);
    });

    reader.readAsText(bfFilepath);
  }
}

export default BattlefieldLoader;