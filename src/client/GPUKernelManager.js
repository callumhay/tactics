import {GPU} from 'gpu.js';

class GPUKernelManager {
  constructor(renderer) {
    this.gpu = new GPU({mode: 'webgl2'});
    this._initBasicFunctions();
  }

  _initBasicFunctions() {
    // Helper functions for all kernels
    this.gpu.addFunction(function clampValue(value, min, max) {
      return Math.min(max, Math.max(min, value));
    });
    this.gpu.addFunction(function xyzLookup() {
      return [this.thread.z, this.thread.y, this.thread.x];
    });
    this.gpu.addFunction(function clampm1(c) {
      return Math.max(c-1, 0);
    });
    this.gpu.addFunction(function clampp1(c,extent) {
      return Math.min(c+1, extent);
    });

    this.gpu.addFunction(function length3(vec) {
      return Math.sqrt(vec[0]*vec[0] + vec[1]*vec[1] + vec[2]*vec[2]);
    });

    // Liquid Kernel helpers
    this.gpu.addFunction(function cellLiquidVol(cell) {
      return cell[this.constants.NODE_VOL_IDX];
    });
    this.gpu.addFunction(function cellType(cell) {
      return cell[this.constants.NODE_TYPE_IDX];
    });
    this.gpu.addFunction(function cellSettled(cell) {
      return cell[this.constants.NODE_SETTLED_IDX];
    });
    this.gpu.addFunction(function absCSFlow(ui) {
      return Math.abs(ui * this.constants.unitArea);
    });
    this.gpu.addFunction(function liquidFlowHelperLRDU(dt, vel, cell, cL, cR, cB, cD, cU) {
      const [x,y,z] = xyzLookup();
      const liquidVol = cellLiquidVol(cell);
      const u = vel[x][y][z];
      const liquidVolL = cellLiquidVol(cL), liquidVolR = cellLiquidVol(cR);
      const liquidVolB = cellLiquidVol(cB);
      const liquidVolD = cellLiquidVol(cD), liquidVolU = cellLiquidVol(cU);
      const typeL = cellType(cL), typeR = cellType(cR);
      const typeB = cellType(cB);
      const typeD = cellType(cD), typeU = cellType(cU);

      const absCSFlowL = absCSFlow(Math.min(0, u[0]));
      const absCSFlowR = absCSFlow(Math.max(0, u[0]));
      const absCSFlowD = absCSFlow(Math.min(0, u[2]));
      const absCSFlowU = absCSFlow(Math.max(0, u[2]));

      let flowToL = clampValue(this.constants.unitVolume * absCSFlowL * dt, 0, liquidVol);
      let flowToR = clampValue(this.constants.unitVolume * absCSFlowR * dt, 0, liquidVol);
      let flowToD = clampValue(this.constants.unitVolume * absCSFlowD * dt, 0, liquidVol);
      let flowToU = clampValue(this.constants.unitVolume * absCSFlowU * dt, 0, liquidVol);

      let maskL = 1, maskR = 1, maskD = 1, maskU = 1;

      // Can we flow left?
      if (liquidVolL >= this.constants.unitVolume || liquidVolL >= liquidVol || 
          typeL === this.constants.SOLID_NODE_TYPE || 
          (typeB === this.constants.EMPTY_NODE_TYPE && liquidVolB < liquidVol)) {
        // No flow leftward
        const flowToLDiv = flowToL/3; 
        flowToR += flowToLDiv;
        flowToU += flowToLDiv;
        flowToD += flowToLDiv;
        maskL = 0;
      }
      // Can we flow right?
      if (liquidVolR >= this.constants.unitVolume || liquidVolR >= liquidVol || 
          typeR === this.constants.SOLID_NODE_TYPE || 
          (typeB === this.constants.EMPTY_NODE_TYPE && liquidVolB < liquidVol)) {
        // No flow rightward
        const flowToRDiv = flowToR/3; 
        flowToL += flowToRDiv;
        flowToU += flowToRDiv;
        flowToD += flowToRDiv;
        maskR = 0;
      }
      // Can we flow back?
      if (liquidVolD >= this.constants.unitVolume || liquidVolD >= liquidVol || 
          typeD === this.constants.SOLID_NODE_TYPE || 
          (typeB === this.constants.EMPTY_NODE_TYPE && liquidVolB < liquidVol)) {
        // No flow backward
        const flowToDDiv = flowToD/3; 
        flowToL += flowToDDiv;
        flowToR += flowToDDiv;
        flowToU += flowToDDiv;
        maskD = 0;
      }
      // Can we flow forward?
      if (liquidVolU >= this.constants.unitVolume || liquidVolU >= liquidVol || 
          typeU === this.constants.SOLID_NODE_TYPE || 
          (typeB === this.constants.EMPTY_NODE_TYPE && liquidVolB < liquidVol)) {
        // No flow forward
        const flowToUDiv = flowToU/3; 
        flowToL += flowToUDiv;
        flowToR += flowToUDiv;
        flowToD += flowToUDiv;
        maskU = 0;
      }

      flowToL *= maskL; flowToR *= maskR;
      flowToD *= maskD; flowToU *= maskU;
      return [flowToL, flowToR, flowToD, flowToU];
    }, {returnType:'Array(4)',  argumentTypes:{dt:'Float', cellData:'Array3D(3)'}});

    this.gpu.addFunction(function liquidFlowHelperBT(dt, vel, cell, cB, cT) {
      const [x,y,z] = xyzLookup();
      const liquidVol = cellLiquidVol(cell);
      const u = vel[x][y][z];
      const liquidVolB = cellLiquidVol(cB), liquidVolT = cellLiquidVol(cT);
      const typeB = cellType(cB), typeT = cellType(cT);
      const absCSFlowB = absCSFlow(Math.min(0, u[1]));
      const absCSFlowT = absCSFlow(Math.max(0, u[1]));
      let flowToB = clampValue(this.constants.unitVolume * absCSFlowB * dt, 0, liquidVol);
      let flowToT = clampValue((liquidVol-liquidVolT) * absCSFlowT * dt, 0, liquidVol); // Yup, this is a hack, it just works.
      let maskT = 1, maskB = 1;

      // Can we flow down?
      if (liquidVolB >= this.constants.unitVolume || typeB === this.constants.SOLID_NODE_TYPE) {
        // No flow downward
        maskB = 0;
      }
      // Can we flow up?
      if (liquidVolT >= liquidVol || typeT === this.constants.SOLID_NODE_TYPE) {
        // No flow upward
        maskT = 0;
      }
      flowToB *= maskB; flowToT *= maskT;
      return [flowToB, flowToT];

    }, {returnType:'Array(2)'});
  }

  clearLiquidKernels() {
    if (this._liquidKernelsAreInit) {
      this.buildLiquidBufferScalar.destroy();
      this.buildLiquidBufferVec3.destroy();
      this.liquidAdvectVel.destroy();
      this.liquidApplyExtForces.destroy();
      this.liquidCurl.destroy();
      this.liquidCurlLen.destroy();
      this.liquidApplyVC.destroy();
      this.liquidDiv.destroy();
      this.liquidComputePressure.destroy();
      this.liquidProjVel.destroy();
      this.liquidCalcFlowsLRB.destroy();
      this.liquidCalcFlowsDUT.destroy();
      this.liquidSumFlows.destroy();
      this.liquidAdjustFlows.destroy();
      this._liquidKernelsAreInit = false;
    }
  }
  reinitLiquidKernels(sizeXYZ, unitSize, constants) {
    console.log("Reinitializing liquid kernels " + (new Date()).toISOString());
    this.clearLiquidKernels();

    const VEL_TYPE  = 'Array3D(3)';
    const CELL_TYPE = 'Array3D(3)';

    const allConstants = {...constants,
      NX: sizeXYZ[0], NY: sizeXYZ[1], NZ: sizeXYZ[2],
      NX_PLUSAHALF: sizeXYZ[0]+0.5, NY_PLUSAHALF: sizeXYZ[1]+0.5, NZ_PLUSAHALF: sizeXYZ[2]+0.5,
      NX_PLUS1: sizeXYZ[0]+1, NY_PLUS1: sizeXYZ[1]+1, NZ_PLUS1: sizeXYZ[2]+1,
      NXYZ_SUM: sizeXYZ[0]+sizeXYZ[1]+sizeXYZ[2],
      FRICTION_AMT: 15,
      unitSize: unitSize, unitArea: unitSize*unitSize, unitVolume: unitSize*unitSize*unitSize,
    };
    const settings = {
      output: [sizeXYZ[0]+2, sizeXYZ[1]+2, sizeXYZ[2]+2],
      pipeline: true,
      immutable: true,
      constants: allConstants
    };

    this.buildLiquidBufferScalar = this.gpu.createKernel(function() {
      return 0;
    }, {...settings, returnType:'Float'});
    this.buildLiquidBufferVec3 = this.gpu.createKernel(function() {
      return [0,0,0];
    }, {...settings, returnType:'Array(3)'});

    this.liquidAdvectVel = this.gpu.createKernel(function(dt, vel, cellData) {
      const [x,y,z] = xyzLookup();
      const cell = cellData[x][y][z];
      if (cellType(cell) === this.constants.SOLID_NODE_TYPE || x < 1 || y < 1 || z < 1 || 
          x > this.constants.NX || y > this.constants.NY || z > this.constants.NZ) { 
        return [0,0,0]; 
      }

      const u = vel[x][y][z];
      const xx = clampValue(x-dt*u[0], 0.5, this.constants.NX_PLUSAHALF);
      const yy = clampValue(y-dt*u[1], 0.5, this.constants.NY_PLUSAHALF);
      const zz = clampValue(z-dt*u[2], 0.5, this.constants.NZ_PLUSAHALF);
      const i0 = Math.floor(xx), i1 = i0 + 1;
      const j0 = Math.floor(yy), j1 = j0 + 1;
      const k0 = Math.floor(zz), k1 = k0 + 1;
      const sx1 = xx-i0, sx0 = 1-sx1;
      const sy1 = yy-j0, sy0 = 1-sy1;
      const sz1 = zz-k0, sz0 = 1-sz1;
      const vel000 = vel[i0][j0][k0], vel010 = vel[i0][j1][k0];
      const vel100 = vel[i1][j0][k0], vel110 = vel[i1][j1][k0];
      const vel001 = vel[i0][j0][k1], vel011 = vel[i0][j1][k1];
      const vel101 = vel[i1][j0][k1], vel111 = vel[i1][j1][k1];
      const result = [0,0,0];
      for (let i = 0; i < 3; i++) {
        const v0 = sx0*(sy0*vel000[i] + sy1*vel010[i]) + sx1*(sy0*vel100[i] + sy1*vel110[i]);
        const v1 = sx0*(sy0*vel001[i] + sy1*vel011[i]) + sx1*(sy0*vel101[i] + sy1*vel111[i]);
        result[i] = sz0*v0 + sz1*v1;
      }
      return result;
    }, {...settings, returnType:'Array(3)', argumentTypes:{dt:'Float', vel:VEL_TYPE, cellData:CELL_TYPE}});

    this.liquidApplyExtForces = this.gpu.createKernel(function(dt, gravity, vel, cellData) {
      const [x,y,z] = xyzLookup();

      // Boundary condition - no forces are applied outside of the liquid
      const cell = cellData[x][y][z];
      const cellLiquidVol = cellLiquidVol(cell);
      const u = vel[x][y][z];
      if (cellType(cell) === this.constants.SOLID_NODE_TYPE || x < 1 || y < 1 || z < 1 || 
          x > this.constants.NX || y > this.constants.NY || z > this.constants.NZ || 
          Math.abs(cellLiquidVol) < this.constants.LIQUID_EPSILON) { 
        return u; 
      }
      
      const result = [u[0], u[1], u[2]];
      const ym1 = clampm1(y);

      // Apply Gravity
      const bCell = cellData[x][ym1][z];
      const bCellType = cellType(bCell);
      result[1] = clampValue(result[1] - gravity*dt, -this.constants.MAX_GRAVITY_VEL, this.constants.MAX_GRAVITY_VEL);

      // Determine the hydrostatic pressure = density*gravity*(height of the fluid above 
      // How much pressure is pressing down on this cell?
      let liquidVolAboveCell = 0;
      const pressureHeightIdx = Math.min(this.constants.NY, y+1+this.constants.PRESSURE_MAX_HEIGHT);
      for (let i = y+1; i < pressureHeightIdx; i++) {
        const aboveCell = cellData[x][i][z];
        const aboveCellType = cellType(aboveCell);
        const aboveCellVol = cellLiquidVol(aboveCell);
        if (aboveCellType === this.constants.SOLID_NODE_TYPE || 
            aboveCellVol < this.constants.LIQUID_EPSILON) { break; }
        liquidVolAboveCell += aboveCellVol;
      }
      const liquidMassAboveCell = this.constants.LIQUID_DENSITY*liquidVolAboveCell;
      const hsForce = this.constants.ATMO_PRESSURE*this.constants.unitArea + liquidMassAboveCell*gravity;
      const dHSVel  = hsForce*dt;

      const xm1 = clampm1(x), xp1 = clampp1(x,this.constants.NX_PLUS1);
      const zm1 = clampm1(z), zp1 = clampp1(z,this.constants.NZ_PLUS1);
      const cellL = cellData[xm1][y][z];
      const cellR = cellData[xp1][y][z];
      const cellD = cellData[x][y][zm1];
      const cellU = cellData[x][y][zp1];
      const bCellLiquidVol = cellLiquidVol(bCell);

      let totalVelX = 0, totalVelZ = 0;
      if (bCellType === this.constants.SOLID_NODE_TYPE || bCellLiquidVol >= cellLiquidVol) {
        totalVelX -= (cellType(cellL) === this.constants.EMPTY_NODE_TYPE && 
                      cellLiquidVol(cellL) < cellLiquidVol) ? dHSVel : 0;
        totalVelX += (cellType(cellR) === this.constants.EMPTY_NODE_TYPE && 
                      cellLiquidVol(cellR) < cellLiquidVol) ? dHSVel : 0;
        totalVelZ -= (cellType(cellD) === this.constants.EMPTY_NODE_TYPE &&
                      cellLiquidVol(cellD) < cellLiquidVol) ? dHSVel : 0;
        totalVelZ += (cellType(cellU) === this.constants.EMPTY_NODE_TYPE &&
                      cellLiquidVol(cellU) < cellLiquidVol) ? dHSVel : 0;
      }
      result[0] = clampValue(result[0] + totalVelX, -this.constants.MAX_PRESSURE_VEL, this.constants.MAX_PRESSURE_VEL);
      result[2] = clampValue(result[2] + totalVelZ, -this.constants.MAX_PRESSURE_VEL, this.constants.MAX_PRESSURE_VEL);
      
      // Friction hack
      const frictionVelX = dt*this.constants.FRICTION_AMT;
      const frictionVelZ = dt*this.constants.FRICTION_AMT;
      result[0] = result[0] < 0 ? Math.min(0, result[0] + frictionVelX) : Math.max(0, result[0] - frictionVelX); 
      result[2] = result[2] < 0 ? Math.min(0, result[2] + frictionVelZ) : Math.max(0, result[2] - frictionVelZ);

      return result;

    }, {...settings, returnType:'Array(3)', argumentTypes:{dt:'Float', gravity:'Float', vel:VEL_TYPE, cellData:CELL_TYPE}});

    this.liquidCurl = this.gpu.createKernel(function(vel) {
      const [x,y,z] = xyzLookup();

      const xm1 = clampm1(x), xp1 = clampp1(x,this.constants.NX_PLUS1);
      const ym1 = clampm1(y), yp1 = clampp1(y,this.constants.NY_PLUS1);
      const zm1 = clampm1(z), zp1 = clampp1(z,this.constants.NZ_PLUS1);

      const L = vel[xm1][y][z], R = vel[xp1][y][z];
      const B = vel[x][ym1][z], T = vel[x][yp1][z];
      const D = vel[x][y][zm1], U = vel[x][y][zp1];

      return [
        ((T[2] - B[2]) - (U[1] - D[1])) / (2*this.constants.unitSize),
        ((U[0] - D[0]) - (R[2] - L[2])) / (2*this.constants.unitSize),
        ((R[1] - L[1]) - (T[0] - B[0])) / (2*this.constants.unitSize)
      ];
    }, {...settings, returnType:'Array(3)', argumentTypes:{vel:VEL_TYPE}});

    this.liquidCurlLen = this.gpu.createKernel(function(curl) {
      const [x,y,z] = xyzLookup();
      return length3(curl[x][y][z]);
    }, {...settings, returnType:'Float', argumentTypes:{curl:'Array3D(3)'}});

    this.liquidApplyVC = this.gpu.createKernel(function(dtVC, vel, curl, curlLen) {
      const [x,y,z] = xyzLookup();

      const xm1 = clampm1(x), xp1 = clampp1(x,this.constants.NX_PLUS1);
      const ym1 = clampm1(y), yp1 = clampp1(y,this.constants.NY_PLUS1);
      const zm1 = clampm1(z), zp1 = clampp1(z,this.constants.NZ_PLUS1);
      
      const omega  = curl[x][y][z];
      const omegaL = curlLen[xm1][y][z], omegaR = curlLen[xp1][y][z];
      const omegaB = curlLen[x][ym1][z], omegaT = curlLen[x][yp1][z];
      const omegaD = curlLen[x][y][zm1], omegaU = curlLen[x][y][zp1];

      const eta = [
        (omegaR - omegaL) / (2*this.constants.unitSize),
        (omegaT - omegaB) / (2*this.constants.unitSize), 
        (omegaU - omegaD) / (2*this.constants.unitSize)
      ];
      const etaLen = length3(eta) + 1e-6;
      eta[0] /= etaLen; eta[1] /= etaLen; eta[2] /= etaLen;
      const u = vel[x][y][z];
      return [
        u[0] + dtVC * (eta[0]*omega[2] - eta[2]*omega[1]),
        u[1] + dtVC * (eta[2]*omega[0] - eta[0]*omega[2]),
        u[2] + dtVC * (eta[0]*omega[1] - eta[1]*omega[0])
      ];
    }, {...settings, returnType:'Array(3)', argumentTypes:{
      dtVC:'Float', vel:VEL_TYPE, curl:'Array3D(3)', curlLen:'Array'
    }});

    this.liquidDiv = this.gpu.createKernel(function(vel, cellData) {
      const [x,y,z] = xyzLookup();

      const xm1 = clampm1(x), xp1 = clampp1(x,this.constants.NX_PLUS1);
      const ym1 = clampm1(y), yp1 = clampp1(y,this.constants.NY_PLUS1);
      const zm1 = clampm1(z), zp1 = clampp1(z,this.constants.NZ_PLUS1);

      const cL = cellData[xm1][y][z], cR = cellData[xp1][y][z];
      const cB = cellData[x][ym1][z], cT = cellData[x][yp1][z];
      const cD = cellData[x][y][zm1], cU = cellData[x][y][zp1];

      // NOTE: If the boundary has a velocity then change noVel to that velocity!
      const noVel = [0,0,0];
      const fieldL = (cellType(cL) === this.constants.SOLID_NODE_TYPE) ? noVel : vel[xm1][y][z];
      const fieldR = (cellType(cR) === this.constants.SOLID_NODE_TYPE) ? noVel : vel[xp1][y][z];
      const fieldB = (cellType(cB) === this.constants.SOLID_NODE_TYPE) ? noVel : vel[x][ym1][z];
      const fieldT = (cellType(cT) === this.constants.SOLID_NODE_TYPE) ? noVel : vel[x][yp1][z];
      const fieldD = (cellType(cD) === this.constants.SOLID_NODE_TYPE) ? noVel : vel[x][y][zm1];
      const fieldU = (cellType(cU) === this.constants.SOLID_NODE_TYPE) ? noVel : vel[x][y][zp1];
      return ((fieldR[0]-fieldL[0]) + (fieldT[1]-fieldB[1]) + (fieldU[2]-fieldD[2])) / this.constants.NXYZ_SUM;

    }, {...settings, returnType:'Float', argumentTypes:{vel:VEL_TYPE, cellData:CELL_TYPE}});

    this.liquidComputePressure = this.gpu.createKernel(function(pressure, cellData, div) {
      // NOTE: The pressure buffer MUST be cleared before calling this!!
      const [x,y,z] = xyzLookup();
      const pC = pressure[x][y][z];
      const cell = cellData[x][y][z];
      if (cellType(cell) === this.constants.SOLID_NODE_TYPE || x < 1 || y < 1 || z < 1 || 
          x > this.constants.NX || y > this.constants.NY || z > this.constants.NZ) { return pC; }
      if (Math.abs(cellLiquidVol(cell)) < this.constants.LIQUID_EPSILON) { return 0; } 

      const xm1 = clampm1(x), xp1 = clampp1(x,this.constants.NX_PLUS1);
      const ym1 = clampm1(y), yp1 = clampp1(y,this.constants.NY_PLUS1);
      const zm1 = clampm1(z), zp1 = clampp1(z,this.constants.NZ_PLUS1);

      const cL = cellData[xm1][y][z], cR = cellData[xp1][y][z];
      const cB = cellData[x][ym1][z], cT = cellData[x][yp1][z];
      const cD = cellData[x][y][zm1], cU = cellData[x][y][zp1];

      const bC = div[x][y][z]; // Contains the 'divergence' calculated previously
      const pL = (cellType(cL) === this.constants.SOLID_NODE_TYPE) ? pC : pressure[xm1][y][z];
      const pR = (cellType(cR) === this.constants.SOLID_NODE_TYPE) ? pC : pressure[xp1][y][z];
      const pB = (cellType(cB) === this.constants.SOLID_NODE_TYPE) ? pC : pressure[x][ym1][z];
      const pT = (cellType(cT) === this.constants.SOLID_NODE_TYPE) ? pC : pressure[x][yp1][z];
      const pD = (cellType(cD) === this.constants.SOLID_NODE_TYPE) ? pC : pressure[x][y][zm1];
      const pU = (cellType(cU) === this.constants.SOLID_NODE_TYPE) ? pC : pressure[x][y][zp1];

      return (pL + pR + pB + pT + pU + pD - bC) / 6.0;

    }, {...settings, returnType:'Float', argumentTypes:{pressure:'Array', cellData:CELL_TYPE, div:'Array'}});

    this.liquidProjVel = this.gpu.createKernel(function(pressure, vel, cellData) {
      const [x,y,z] = xyzLookup();
      const cell = cellData[x][y][z];
      if (cellType(cell) === this.constants.SOLID_NODE_TYPE || x < 1 || y < 1 || z < 1 || 
          x > this.constants.NX || y > this.constants.NY || z > this.constants.NZ) { return [0,0,0]; }

      const xm1 = clampm1(x), xp1 = clampp1(x,this.constants.NX_PLUS1);
      const ym1 = clampm1(y), yp1 = clampp1(y,this.constants.NY_PLUS1);
      const zm1 = clampm1(z), zp1 = clampp1(z,this.constants.NZ_PLUS1);

      const cL = cellData[xm1][y][z], cR = cellData[xp1][y][z];
      const cB = cellData[x][ym1][z], cT = cellData[x][yp1][z];
      const cD = cellData[x][y][zm1], cU = cellData[x][y][zp1];

      const u = vel[x][y][z];
      const pC = pressure[x][y][z];  
      let pL = pressure[xm1][y][z], pR = pressure[xp1][y][z];
      let pB = pressure[x][ym1][z], pT = pressure[x][yp1][z];
      let pD = pressure[x][y][zm1], pU = pressure[x][y][zp1];

      // NOTE: This requires augmentation if the boundaries have velocity!
      const vMaskPos = [1,1,1];
      const vMaskNeg = [1,1,1];
      if (cellType(cL) === this.constants.SOLID_NODE_TYPE || cellSettled(cL) === this.constants.SETTLED_NODE) { pL = pC; vMaskNeg[0] = 0; }
      if (cellType(cR) === this.constants.SOLID_NODE_TYPE || cellSettled(cR) === this.constants.SETTLED_NODE) { pR = pC; vMaskPos[0] = 0; }
      if (cellType(cB) === this.constants.SOLID_NODE_TYPE || cellSettled(cB) === this.constants.SETTLED_NODE) { pB = pC; vMaskNeg[1] = 0; }
      if (cellType(cT) === this.constants.SOLID_NODE_TYPE || cellSettled(cT) === this.constants.SETTLED_NODE) { pT = pC; vMaskPos[1] = 0; }
      if (cellType(cD) === this.constants.SOLID_NODE_TYPE || cellSettled(cD) === this.constants.SETTLED_NODE) { pD = pC; vMaskNeg[2] = 0; }
      if (cellType(cU) === this.constants.SOLID_NODE_TYPE || cellSettled(cU) === this.constants.SETTLED_NODE) { pU = pC; vMaskPos[2] = 0; }

      const result = [
        u[0] - (pR-pL) / this.constants.NXYZ_SUM,
        u[1] - (pT-pB) / this.constants.NXYZ_SUM,
        u[2] - (pU-pD) / this.constants.NXYZ_SUM
      ];
      result[0] = Math.min(result[0]*vMaskPos[0], Math.max(result[0]*vMaskNeg[0], result[0]));
      result[1] = Math.min(result[1]*vMaskPos[1], Math.max(result[1]*vMaskNeg[1], result[1]));
      result[2] = Math.min(result[2]*vMaskPos[2], Math.max(result[2]*vMaskNeg[2], result[2]));
      return result;

    }, {...settings, returnType:'Array(3)', argumentTypes:{pressure:'Array', vel:VEL_TYPE, cellData:CELL_TYPE}});

    this.liquidCalcFlowsLRB = this.gpu.createKernel(function(dt, vel, cellData) {
      const [x,y,z] = xyzLookup();
      const cell = cellData[x][y][z];
      const liquidVol = cellLiquidVol(cell);
      if (cellType(cell) === this.constants.SOLID_NODE_TYPE || liquidVol === 0 || x < 1 || y < 1 || z < 1 || 
          x > this.constants.NX || y > this.constants.NY || z > this.constants.NZ ||
          cellSettled(cell) === this.constants.SETTLED_NODE) { return [0,0,0]; }

      const xm1 = clampm1(x), xp1 = clampp1(x,this.constants.NX_PLUS1);
      const ym1 = clampm1(y), yp1 = clampp1(y,this.constants.NY_PLUS1);
      const zm1 = clampm1(z), zp1 = clampp1(z,this.constants.NZ_PLUS1);
      const cL = cellData[xm1][y][z], cR = cellData[xp1][y][z];
      const cD = cellData[x][y][zm1], cU = cellData[x][y][zp1];
      const cB = cellData[x][ym1][z], cT = cellData[x][yp1][z];
      const liquidVolB = cellLiquidVol(cB);
      const liquidVolL = cellLiquidVol(cL), liquidVolR = cellLiquidVol(cR);

      const [flowToL, flowToR, flowToD, flowToU] = liquidFlowHelperLRDU(dt, vel, cell, cL, cR, cB, cD, cU);
      const [flowToB, flowToT] = liquidFlowHelperBT(dt, vel, cell, cB, cT);
      const totalFlow = (flowToL+flowToR+flowToB+flowToT+flowToD+flowToU)+1e-6;

      return [
        Math.min(Math.max(0.5*(liquidVol-liquidVolL),0), Math.min(liquidVol*(flowToL/totalFlow), flowToL)),
        Math.min(Math.max(0.5*(liquidVol-liquidVolR),0), Math.min(liquidVol*(flowToR/totalFlow), flowToR)),
        Math.min(0.5*(liquidVol+liquidVolB), Math.min(liquidVol*(flowToB/totalFlow), flowToB))
      ];
    }, {...settings, returnType:'Array(3)', argumentTypes:{dt:'Float', vel:VEL_TYPE, cellData:CELL_TYPE }});

    this.liquidCalcFlowsDUT = this.gpu.createKernel(function(dt, vel, cellData) {
      const [x,y,z] = xyzLookup();
      const cell = cellData[x][y][z];
      const liquidVol = cellLiquidVol(cell);
      if (cellType(cell) === this.constants.SOLID_NODE_TYPE || liquidVol === 0 || x < 1 || y < 1 || z < 1 || 
          x > this.constants.NX || y > this.constants.NY || z > this.constants.NZ ||
          cellSettled(cell) === this.constants.SETTLED_NODE) { return [0,0,0]; }

      const xm1 = clampm1(x), xp1 = clampp1(x,this.constants.NX_PLUS1);
      const ym1 = clampm1(y), yp1 = clampp1(y,this.constants.NY_PLUS1);
      const zm1 = clampm1(z), zp1 = clampp1(z,this.constants.NZ_PLUS1);
      const cL = cellData[xm1][y][z], cR = cellData[xp1][y][z];
      const cB = cellData[x][ym1][z], cT = cellData[x][yp1][z];
      const cD = cellData[x][y][zm1], cU = cellData[x][y][zp1];
      const liquidVolT = cellLiquidVol(cT);
      const liquidVolD = cellLiquidVol(cD), liquidVolU = cellLiquidVol(cU);

      const [flowToL, flowToR, flowToD, flowToU] = liquidFlowHelperLRDU(dt, vel, cell, cL, cR, cB, cD, cU);
      const [flowToB, flowToT] = liquidFlowHelperBT(dt, vel, cell, cB, cT);
      const totalFlow = (flowToL+flowToR+flowToB+flowToT+flowToD+flowToU)+1e-6;

      return [
        Math.min(Math.max(0.5*(liquidVol-liquidVolD),0), Math.min(liquidVol*(flowToD/totalFlow), flowToD)),
        Math.min(Math.max(0.5*(liquidVol-liquidVolU),0), Math.min(liquidVol*(flowToU/totalFlow), flowToU)),
        Math.min(Math.max(0.5*(liquidVol-liquidVolT),0), Math.min(liquidVol*(flowToT/totalFlow), flowToT))
      ];
    }, {...settings, returnType:'Array(3)', argumentTypes:{dt:'Float', vel:VEL_TYPE, cellData:CELL_TYPE }});

    this.liquidSumFlows = this.gpu.createKernel(function(cellFlowsLRB, cellFlowsDUT, cellData) {
      const [x,y,z] = xyzLookup();
      const cell = cellData[x][y][z];
      if (cellType(cell) === this.constants.SOLID_NODE_TYPE || x < 1 || y < 1 || z < 1 || 
          x > this.constants.NX || y > this.constants.NY || z > this.constants.NZ) { return 0; }

      const xm1 = clampm1(x), xp1 = clampp1(x,this.constants.NX_PLUS1);
      const ym1 = clampm1(y), yp1 = clampp1(y,this.constants.NY_PLUS1);
      const zm1 = clampm1(z), zp1 = clampp1(z,this.constants.NZ_PLUS1);

      const fC_LRB = cellFlowsLRB[x][y][z],   fC_DUT = cellFlowsDUT[x][y][z];
      const fL_LRB = cellFlowsLRB[xm1][y][z], fR_LRB = cellFlowsLRB[xp1][y][z];
      const fB_DUT = cellFlowsDUT[x][ym1][z], fT_LRB = cellFlowsLRB[x][yp1][z];
      const fD_DUT = cellFlowsDUT[x][y][zm1], fU_DUT = cellFlowsDUT[x][y][zp1];

      // The total volume change in this cell is equal to all the incoming flows
      // from neighbour cells minus the total outward flow from the current cell
      return (
        (fL_LRB[1] + fR_LRB[0] + fB_DUT[2] + fD_DUT[1] + fU_DUT[0] + fT_LRB[2]) - 
        (fC_LRB[0] + fC_LRB[1] + fC_LRB[2] + fC_DUT[0] + fC_DUT[1] + fC_DUT[2])
      );
      
    }, {...settings, returnType:'Float', argumentTypes:{
      cellFlowsLRB: 'Array3D(3)', cellFlowsDUT: 'Array3D(3)', cellData:CELL_TYPE
    }});

    this.liquidAdjustFlows = this.gpu.createKernel(function(cellFlowSums, cellData) {
      const [x,y,z] = xyzLookup();
      const cell = cellData[x][y][z];
      const result = [cell[0], cell[1], cell[2]];
      if (cellType(cell) === this.constants.SOLID_NODE_TYPE || x < 1 || y < 1 || z < 1 || 
          x > this.constants.NX || y > this.constants.NY || z > this.constants.NZ) { 
        return result; 
      }

      const xm1 = clampm1(x), xp1 = clampp1(x,this.constants.NX_PLUS1);
      const ym1 = clampm1(y), yp1 = clampp1(y,this.constants.NY_PLUS1);
      const zm1 = clampm1(z), zp1 = clampp1(z,this.constants.NZ_PLUS1);
      let sC = cellFlowSums[x][y][z];
      const sL = cellFlowSums[xm1][y][z], sR = cellFlowSums[xp1][y][z];
      const sB = cellFlowSums[x][ym1][z], sT = cellFlowSums[x][yp1][z];
      const sD = cellFlowSums[x][y][zm1], sU = cellFlowSums[x][y][zp1];
      
      const liquidVol = cell[this.constants.NODE_VOL_IDX];
      const finalVol = (liquidVol + sC);
      result[this.constants.NODE_VOL_IDX] = (Math.abs(finalVol) < this.constants.LIQUID_EPSILON) ? 0 : finalVol;

      // Unsettle the cell if there are any changes in the neighbour cell flows
      if (Math.abs(sL) >= this.constants.LIQUID_EPSILON || Math.abs(sR) >= this.constants.LIQUID_EPSILON ||
          Math.abs(sB) >= this.constants.LIQUID_EPSILON || Math.abs(sT) >= this.constants.LIQUID_EPSILON ||
          Math.abs(sD) >= this.constants.LIQUID_EPSILON || Math.abs(sU) >= this.constants.LIQUID_EPSILON) {
            result[this.constants.NODE_SETTLED_IDX] = this.constants.UNSETTLED_NODE;
      }
      else {
        // If there's no change in flow then the cell becomes settled
        result[this.constants.NODE_SETTLED_IDX] = (Math.abs(sC) < this.constants.LIQUID_EPSILON && 
          Math.abs(liquidVol) < this.constants.LIQUID_EPSILON || 
          Math.abs(liquidVol-this.constants.unitVolume) < this.constants.LIQUID_EPSILON) ? 
          this.constants.SETTLED_NODE : this.constants.UNSETTLED_NODE;
      }
      return result;

    }, {...settings, pipeline: false, returnType:'Array(3)', argumentTypes:{
      cellFlowSums: 'Array', cellData:CELL_TYPE
    }});
    
    this._liquidKernelsAreInit = true;
    console.log("Liquid kernels reinitialized " + (new Date()).toISOString());
  }

}

export default GPUKernelManager;