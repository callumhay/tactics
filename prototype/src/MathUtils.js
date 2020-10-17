

class MathUtils {
  static roundToDecimal(value, decimals) {
    const multiple = Math.pow(10, decimals);
    return Math.round(value * multiple) / multiple;
  }
  static approxEquals(value1, value2, epsilon=1e-6) {
    return Math.abs(value1-value2) <= epsilon;
  }

}
export default MathUtils;