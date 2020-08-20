

class MathUtils {
  static roundToDecimal(value, decimals) {
    return Number(Math.round(value + 'e' + decimals) + 'e-' + decimals);
  }   

}
export default MathUtils;