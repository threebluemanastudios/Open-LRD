using System;
using OPEN_LRD.Foundations;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class MathFunctionDispatcherTests
    {
        public static void RunAll()
        {
            Run("Math function dispatcher keyed call", TestKeyedCall);
            Run("Math function dispatcher FunctionBox routing", TestFunctionBoxRouting);
            Run("Math function dispatcher unique inference", TestUniqueInference);
            Run("Math function dispatcher validation and ambiguity", TestValidationAndAmbiguity);
            Run("Math function dispatcher unit requirements", TestUnitRequirements);
            Run("Math function dispatcher clears prior result", TestResultClearing);
        }

        private static void TestKeyedCall()
        {
            FunctionResult result = MathFunctionDispatcher.Dispatch("Math_1.AddInt", typeof(int), 2, 3);
            Assert(result.Value is int value && value == 5, "keyed call should return the integer result");
            Assert(result.DataType == typeof(int), "keyed call should report the declared return type");
            AssertEqual(MathFunctionDispatcher.Dispatch<double>("Math_1.AddDouble", 1.25, 2.75), 4.0, "generic keyed call");
        }

        private static void TestFunctionBoxRouting()
        {
            FunctionBox box = new FunctionBox(typeof(double), "Math_2.CalculateHypotenuse");
            box.AddVariable(new FunctionVariable("legA", 3.0, typeof(double), "m"));
            box.AddVariable(new FunctionVariable("legB", 4.0, typeof(double), "m"));

            FunctionResult result = MathFunctionDispatcher.Dispatch(box);

            AssertEqual((double)result.Value!, 5.0, "FunctionBox result value");
            Assert(result.DataType == typeof(double), "FunctionBox should report the result type");
            Assert(result.UnitTitle == "m", "FunctionBox result should carry its output unit");
            Assert(box.Result == result, "dispatcher should store the one invocation result on the box");
        }

        private static void TestUniqueInference()
        {
            FunctionBox box = new FunctionBox(typeof(long));
            box.AddVariable(new FunctionVariable("number", 5, typeof(int)));

            FunctionResult result = MathFunctionDispatcher.Dispatch(box);

            Assert(result.Value is long value && value == 120L, "keyless box should route a unique signature");
            Assert(box.FunctionKey == null, "inference should not silently assign a function key");

            FunctionBox unitQualifiedBox = new FunctionBox(typeof(double), expectedResultUnitTitle: "m²");
            unitQualifiedBox.AddVariable(new FunctionVariable("length", 3.0, typeof(double), "m"));
            unitQualifiedBox.AddVariable(new FunctionVariable("width", 4.0, typeof(double), "m"));
            FunctionResult area = MathFunctionDispatcher.Dispatch(unitQualifiedBox);
            AssertEqual((double)area.Value!, 12.0, "unit metadata should disambiguate a keyless request");
            Assert(area.UnitTitle == "m²", "inferred result should preserve its unit title");
        }

        private static void TestValidationAndAmbiguity()
        {
            AssertThrows<ArgumentException>(
                () => MathFunctionDispatcher.Dispatch("Math_1.AddDouble", typeof(double), 2, 3),
                "dispatcher should reject mismatched input types");
            AssertThrows<ArgumentException>(
                () => MathFunctionDispatcher.Dispatch("Math_1.AddInt", typeof(double), 2, 3),
                "dispatcher should reject an unexpected output type");
            FunctionBox ambiguousBox = new FunctionBox(typeof(double));
            ambiguousBox.AddVariable(new FunctionVariable("first", 2.0, typeof(double)));
            ambiguousBox.AddVariable(new FunctionVariable("second", 3.0, typeof(double)));
            AssertThrows<InvalidOperationException>(
                () => MathFunctionDispatcher.Dispatch(ambiguousBox),
                "dispatcher should report an ambiguous keyless signature");
        }

        private static void TestUnitRequirements()
        {
            FunctionBox conversionBox = new FunctionBox(typeof(double), "Math_2.CalculateHypotenuse");
            conversionBox.AddVariable(new FunctionVariable("legA", 300.0, typeof(double), "cm"));
            conversionBox.AddVariable(new FunctionVariable("legB", 4.0, typeof(double), "m"));
            AssertThrows<UnitConversionRequiredException>(
                () => MathFunctionDispatcher.Dispatch(conversionBox),
                "dispatcher should require explicit conversion between compatible units");

            FunctionBox incompatibleBox = new FunctionBox(typeof(double), "Math_2.CalculateHypotenuse");
            incompatibleBox.AddVariable(new FunctionVariable("legA", 3.0, typeof(double), "m"));
            incompatibleBox.AddVariable(new FunctionVariable("legB", 4.0, typeof(double), "s"));
            AssertThrows<ArgumentException>(
                () => MathFunctionDispatcher.Dispatch(incompatibleBox),
                "dispatcher should reject incompatible units");

            FunctionBox outputConversionBox = new FunctionBox(typeof(double), "Math_2.CalculateCircleArea", "ft²");
            outputConversionBox.AddVariable(new FunctionVariable("radius", 2.0, typeof(double), "m"));
            AssertThrows<UnitConversionRequiredException>(
                () => MathFunctionDispatcher.Dispatch(outputConversionBox),
                "dispatcher should reject an output unit requiring conversion");
        }

        private static void TestResultClearing()
        {
            FunctionBox box = new FunctionBox(typeof(double), "Math_2.CalculateHypotenuse");
            box.AddVariable(new FunctionVariable("legA", 3.0, typeof(double), "m"));
            box.AddVariable(new FunctionVariable("legB", 4.0, typeof(double), "m"));
            MathFunctionDispatcher.Dispatch(box);
            Assert(box.Result != null, "successful dispatch should populate the result slot");

            box.FunctionKey = "Missing.Function";
            AssertThrows<System.Collections.Generic.KeyNotFoundException>(
                () => MathFunctionDispatcher.Dispatch(box),
                "unknown function should fail");
            Assert(box.Result == null, "failed invocation should not leave a stale result on the box");
        }
    }
}
