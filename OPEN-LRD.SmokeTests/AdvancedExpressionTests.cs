using System;
using System.Collections.Generic;
using OPEN_LRD.Foundations;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class AdvancedExpressionTests
    {
        public static void RunAll()
        {
            Run("Advanced expressions parse and evaluate unary functions", TestUnaryFunctions);
            Run("Advanced expressions evaluate multi-argument functions", TestMultiArgumentFunctions);
            Run("Advanced expressions invoke scalar skill functions", TestScalarSkillFunctions);
            Run("Advanced expressions validate function calls", TestCallValidation);
            Run("Advanced expressions differentiate supported functions", TestDifferentiation);
            Run("Advanced expressions reject unsupported derivatives", TestUnsupportedDerivatives);
        }

        private static void TestUnaryFunctions()
        {
            AssertEqual(Math_3.Evaluate("log(100)", "x", 0.0), 2.0, "one-argument log stays base-10");
            AssertEqual(Math_3.Evaluate("log2(8)", "x", 0.0), 3.0, "log2 evaluation");
            AssertEqual(Math_3.Evaluate("gamma(5)", "x", 0.0), 24.0, "gamma evaluation");
            AssertEqual(Math_3.Evaluate("lgamma(5)", "x", 0.0), Math.Log(24.0), "log-gamma evaluation");
            AssertEqual(Math_3.Evaluate("erf(0)", "x", 0.0), 0.0, "erf evaluation");
            AssertEqual(Math_3.Evaluate("erfc(0)", "x", 0.0), 1.0, "erfc evaluation");
            AssertEqual(Math_3.Evaluate("normpdf(0)", "x", 0.0), 1.0 / Math.Sqrt(2.0 * Math.PI), "normal pdf evaluation");
            AssertEqual(Math_3.Evaluate("sigmoid(0)", "x", 0.0), 0.5, "sigmoid evaluation");
            AssertEqual(Math_3.Evaluate("softplus(0)", "x", 0.0), Math.Log(2.0), "softplus evaluation");
            AssertEqual(Math_3.Evaluate("relu(-2)", "x", 0.0), 0.0, "relu evaluation");
            Assert(Math_3.Evaluate("gelu(1)", "x", 0.0) > 0.0, "gelu evaluation");
            AssertEqual(Math_3.Evaluate("sech(0)", "x", 0.0), 1.0, "sech evaluation");
            AssertEqual(Math_3.Evaluate("csch(1)", "x", 0.0), 1.0 / Math.Sinh(1.0), "csch evaluation");
            AssertEqual(Math_3.Evaluate("coth(1)", "x", 0.0), Math.Cosh(1.0) / Math.Sinh(1.0), "coth evaluation");
            AssertEqual(Math_3.Evaluate("acsch(1)", "x", 0.0), Math.Asinh(1.0), "acsch evaluation");
            AssertEqual(Math_3.Evaluate("acoth(2)", "x", 0.0), 0.5 * Math.Log(3.0), "acoth evaluation");
            AssertEqual(Math_3.Evaluate("floor(-1.2)", "x", 0.0), -2.0, "floor evaluation");
            AssertEqual(Math_3.Evaluate("ceil(1.2)", "x", 0.0), 2.0, "ceiling evaluation");
            AssertEqual(Math_3.Evaluate("round(2.5)", "x", 0.0), 2.0, "round evaluation");
            AssertEqual(Math_3.Evaluate("trunc(-1.8)", "x", 0.0), -1.0, "truncation evaluation");
            AssertEqual(Math_3.Evaluate("frac(1.25)", "x", 0.0), 0.25, "fractional-part evaluation");
            AssertEqual(Math_3.Evaluate("deg2rad(180)", "x", 0.0), Math.PI, "degrees-to-radians evaluation");
            AssertEqual(Math_3.Evaluate("rad2deg(pi)", "x", 0.0), 180.0, "radians-to-degrees evaluation");
            AssertEqual(Math_3.Evaluate("factorial(5)", "x", 0.0), 120.0, "factorial evaluation");
        }

        private static void TestMultiArgumentFunctions()
        {
            AssertEqual(Math_3.Evaluate("log(8, 2)", "x", 0.0), 3.0, "two-argument log uses the requested base");
            AssertEqual(Math_3.Evaluate("atan2(0, -1)", "x", 0.0), Math.PI, "atan2 evaluation");
            AssertEqual(Math_3.Evaluate("hypot(3, 4)", "x", 0.0), 5.0, "hypot evaluation");
            AssertEqual(Math_3.Evaluate("nthroot(27, 3)", "x", 0.0), 3.0, "nthroot evaluation");
            AssertEqual(Math_3.Evaluate("pow(2, 10)", "x", 0.0), 1024.0, "pow desugaring");
            AssertEqual(Math_3.Evaluate("min(3, 7)", "x", 0.0), 3.0, "min evaluation");
            AssertEqual(Math_3.Evaluate("max(3, 7)", "x", 0.0), 7.0, "max evaluation");
            AssertEqual(Math_3.Evaluate("clamp(12, 0, 10)", "x", 0.0), 10.0, "clamp evaluation");
            AssertEqual(Math_3.Evaluate("lerp(0, 10, 0.25)", "x", 0.0), 2.5, "lerp evaluation");
            AssertEqual(Math_3.Evaluate("gcd(12, 18)", "x", 0.0), 6.0, "gcd evaluation");
            AssertEqual(Math_3.Evaluate("lcm(4, 6)", "x", 0.0), 12.0, "lcm evaluation");
            AssertEqual(Math_3.Evaluate("mod(-7, 3)", "x", 0.0), 2.0, "mod evaluation");
            AssertEqual(Math_3.Evaluate("modpow(2, 10, 1000)", "x", 0.0), 24.0, "modpow evaluation");
            AssertEqual(Math_3.Evaluate("ncr(5, 2)", "x", 0.0), 10.0, "ncr evaluation");
            AssertEqual(Math_3.Evaluate("npr(5, 2)", "x", 0.0), 20.0, "npr evaluation");
            AssertEqual(Math_3.Evaluate("beta(2, 3)", "x", 0.0), 1.0 / 12.0, "beta evaluation");
            AssertEqual(Math_3.Evaluate("gammap(2, 1)", "x", 0.0), ProbabilityAndStatistics.RegularizedGammaP(2.0, 1.0), "gammap evaluation");
            AssertEqual(Math_3.Evaluate("gammaq(2, 1)", "x", 0.0), ProbabilityAndStatistics.RegularizedGammaQ(2.0, 1.0), "gammaq evaluation");
            AssertEqual(Math_3.Evaluate("betainc(2, 3, 0.5)", "x", 0.0), ProbabilityAndStatistics.IncompleteBeta(2.0, 3.0, 0.5), "betainc evaluation");
        }

        private static void TestScalarSkillFunctions()
        {
            AssertEqual(Math_3.Evaluate("Math_1.AddDouble(1.25, 2.75)", "x", 0.0), 4.0, "qualified scalar skill call");
            AssertEqual(Math_3.Evaluate("Math_2.CalculateHypotenuse(3, 4)", "x", 0.0), 5.0, "qualified scalar skill call with integer literals");
            AssertEqual(Math_3.Evaluate("Math_1.CalculateGCD(12, 18)", "x", 0.0), 6.0, "qualified integer-returning skill call");
            AssertEqual(Math_3.Evaluate("InformationTheoryAndMLMath.Sigmoid(0)", "x", 0.0), 0.5, "qualified statistics skill call");
            AssertEqual(Math_3.Evaluate("Math_1.AddDouble(1, 2) + log(100)", "x", 0.0), 5.0, "skill calls combine with built-in expressions");

            Expression parsed = ExpressionParser.Parse("Math_1.AddDouble(1, 2)");
            Assert(parsed is CallExpression, "qualified skill calls should produce CallExpression nodes");
            Assert(parsed.ToString() == "Math_1.AddDouble(1, 2)", "qualified skill calls should round-trip through ToString");
        }

        private static void TestCallValidation()
        {
            Assert(ExpressionParser.Parse("log(8, 2)") is CallExpression, "two-argument log should produce a call node");
            Assert(Math_3.Simplify("log(8, 2)") == "3", "constant two-argument log should fold");
            Assert(Math_3.Simplify("log(100)") == "2", "constant one-argument log should fold to base-10");
            AssertThrows<FormatException>(() => ExpressionParser.Parse("log()"), "empty argument lists should be rejected");
            AssertThrows<FormatException>(() => ExpressionParser.Parse("log(8,)"), "trailing commas should be rejected");
            AssertThrows<FormatException>(() => ExpressionParser.Parse("log(8, 2, 3)"), "incorrect built-in arity should be rejected");
            AssertThrows<FormatException>(() => ExpressionParser.Parse("no_such_function(1)"), "unknown functions should be rejected");
            AssertThrows<FormatException>(() => ExpressionParser.Parse("Math_1.AddDouble(1, 2, 3)"), "unknown qualified overloads should be rejected");
        }

        private static void TestDifferentiation()
        {
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("erf(x)", "x"), "x", 0.0), 2.0 / Math.Sqrt(Math.PI), "erf derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("erfc(x)", "x"), "x", 0.0), -2.0 / Math.Sqrt(Math.PI), "erfc derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("sigmoid(x)", "x"), "x", 0.0), 0.25, "sigmoid derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("softplus(x)", "x"), "x", 0.0), 0.5, "softplus derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("log2(x)", "x"), "x", 8.0), 1.0 / (8.0 * Math.Log(2.0)), "log2 derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("log(x, 2)", "x"), "x", 8.0), 1.0 / (8.0 * Math.Log(2.0)), "two-argument log derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("nthroot(x, 3)", "x"), "x", 8.0), 1.0 / 12.0, "nthroot derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("atan2(x, 1)", "x"), "x", 1.0), 0.5, "atan2 derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("hypot(x, 4)", "x"), "x", 3.0), 0.6, "hypot derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("lerp(0, 1, x)", "x"), "x", 0.37), 1.0, "lerp derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("deg2rad(x)", "x"), "x", 10.0), Math.PI / 180.0, "degrees-to-radians derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("floor(2)", "x"), "x", 0.0), 0.0, "constant non-differentiable function derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("gcd(2, 6)", "x"), "x", 0.0), 0.0, "constant discrete function derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("Math_1.SquareDouble(2)", "x"), "x", 0.0), 0.0, "constant skill-call derivative");
        }

        private static void TestUnsupportedDerivatives()
        {
            AssertThrows<NotSupportedException>(() => Math_3.Differentiate("floor(x)", "x"), "floor derivative should be unsupported for an active variable");
            AssertThrows<NotSupportedException>(() => Math_3.Differentiate("gamma(x)", "x"), "gamma derivative should be unsupported for an active variable");
            AssertThrows<NotSupportedException>(() => Math_3.Differentiate("min(x, 2)", "x"), "min derivative should be unsupported for an active variable");
            AssertThrows<NotSupportedException>(() => Math_3.Differentiate("gcd(x, 6)", "x"), "gcd derivative should be unsupported for an active variable");
            AssertThrows<NotSupportedException>(() => Math_3.Differentiate("Math_1.SquareDouble(x)", "x"), "dispatcher-call derivative should be unsupported for an active variable");
        }
    }
}
