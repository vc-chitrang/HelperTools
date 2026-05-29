namespace ViitorCloud.Soreal {
    public class MathUtility {
        private const float FootAndMeterConverterUnit = 3.28084f;

        public static void ConvertToMeters(ref float foot) {
            foot /= FootAndMeterConverterUnit;
        }

        public static void ConvertToFoot(ref float meter) {
            meter *= FootAndMeterConverterUnit;
        }

        public static float GetMeterValue(float foot) {
            return foot /= FootAndMeterConverterUnit;
        }

        public static float GetFootValue(float meter) {
            return meter *= FootAndMeterConverterUnit;
        }

        public static float MapValue(float input,float inputStart,float inputEnd,float outputStart,float outputEnd) {
            if (input < inputStart)
                input = inputStart;
            if (input > inputEnd)
                input = inputEnd;
            return outputStart + ((outputEnd - outputStart) / (inputEnd - inputStart)) * (input - inputStart);
        }
    }
}