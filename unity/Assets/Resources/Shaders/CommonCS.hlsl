// Lookup a flattened index from a 3D index given the constant size/resolution for each axis
uint index(uint x, uint y, uint z, uint size) {
	return x + y * size + z * size * size;
}

uint clampm1(uint c) {
  return max(c-1, 0);
}
uint clampp1(uint c, uint extent) {
  return min(c+1, extent);
}