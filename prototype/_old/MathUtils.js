

class MathUtils {
  static roundToDecimal(value, decimals) {
    const multiple = Math.pow(10, decimals);
    return Math.round(value * multiple) / multiple;
  }   

}
export default MathUtils;