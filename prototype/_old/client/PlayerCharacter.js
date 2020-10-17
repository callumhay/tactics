

export default class PlayerCharacter {
  constructor() {
    this.name = 'Beowulf';
    this.level = 1;
    this.primaryClass = 'Mage';
    this.secondaryClass = 'Fighter';

    // Primary stats
    this.strength = 10;
    this.speed = 10;
    this.constitution = 10;
    this.intelligence = 10;

    // Derived stats
    this.hpMax = this._calcTotalHP();
    this.mpMax = this._calcTotalMP();

    // Current status
    this.hp = this.hpMax;
    this.mp = this.mpMax;
  }

  _calcTotalHP() { return 10; }
  _calcTotalMP() { return 5;  }



}