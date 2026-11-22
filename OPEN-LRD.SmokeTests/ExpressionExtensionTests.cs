using System;
using System.Collections.Generic;
using System.Linq;
using OPEN_LRD.Foundations;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class ExpressionExtensionTests
    {
        public static void RunAll()
        {
            Run("Expression extensions parse mathematical and physical constants", TestConstants);
            Run("Expression extensions parse postfix, prefix, modulo, and literal forms", TestOperatorsAndLiterals);
            Run("Expression extensions provide contextual diagnostics", TestDiagnostics);
            Run("Expression extensions evaluate conditional and boolean expressions", TestConditionals);
            Run("Expression extensions evaluate variadic aggregations", TestAggregations);
            Run("Expression extensions expand indexed sums and products", TestIndexedAggregations);
            Run("Expression extensions integrate additional functions", TestIntegration);
            Run("Expression extensions simplify algebraic identities", TestSimplificationIdentities);
            Run("Expression extensions desugar let bindings", TestLetBindings);
            Run("Expression extensions validate error cases", TestErrorCases);
        }

        private static void TestConstants()
        {
            AssertEqual(Math_3.Evaluate("tau", "x", 0.0), 2.0 * Math.PI, "tau constant");
            AssertEqual(Math_3.Evaluate("phi", "x", 0.0), (1.0 + Math.Sqrt(5.0)) / 2.0, "phi constant");
            AssertEqual(Math_3.Evaluate("const.c", "x", 0.0), PhysicalConstants.SpeedOfLight.ValueSI, "qualified speed of light");
            AssertEqual(Math_3.Evaluate("PhysicalConstants.PlanckConstant", "x", 0.0), PhysicalConstants.PlanckConstant.ValueSI, "qualified Planck constant");
            AssertEqual(Math_3.Evaluate("const.kB", "x", 0.0), PhysicalConstants.BoltzmannConstant.ValueSI, "qualified Boltzmann constant");
            AssertEqual(Math_3.Evaluate("2 * const.hbar + 1", "x", 0.0), (2.0 * PhysicalConstants.ReducedPlanckConstant.ValueSI) + 1.0, "qualified constants combine with arithmetic");
            AssertEqual(Math_3.Evaluate("c", "c", 3.0), 3.0, "single-letter c remains a variable");
            AssertEqual(Math_3.Evaluate("h", "h", 7.0), 7.0, "single-letter h remains a variable");
        }

        private static void TestOperatorsAndLiterals()
        {
            AssertEqual(Math_3.Evaluate("4!", "x", 0.0), 24.0, "postfix factorial");
            AssertEqual(Math_3.Evaluate("3!^2", "x", 0.0), 36.0, "factorial binds tighter than power");
            AssertEqual(Math_3.Evaluate("-3!", "x", 0.0), -6.0, "unary minus applies after factorial");
            AssertEqual(Math_3.Evaluate("|x - 3|", "x", 1.0), 2.0, "absolute-value bars");
            AssertEqual(Math_3.Evaluate("||x| - 3|", "x", 5.0), 2.0, "nested absolute-value bars");
            AssertEqual(Math_3.Evaluate("|x| + |x - 4|", "x", 1.0), 4.0, "adjacent absolute-value bars");
            AssertEqual(Math_3.Evaluate("2|x|", "x", -3.0), 6.0, "implicit multiplication with absolute-value bars");
            AssertEqual(Math_3.Evaluate("7 % 4", "x", 0.0), 3.0, "infix modulo");
            AssertEqual(Math_3.Evaluate("!0", "x", 0.0), 1.0, "prefix logical not");
            AssertEqual(Math_3.Evaluate("1_000 + 24", "x", 0.0), 1024.0, "digit separators");
            AssertEqual(Math_3.Evaluate("0xFF + 0b10", "x", 0.0), 257.0, "hexadecimal and binary literals");
            AssertEqual(Math_3.Evaluate("0xFFFFFFFF", "x", 0.0), 4294967295.0, "unsigned 32-bit hexadecimal literal");
            AssertEqual(Math_3.Evaluate("1.25e+1_0", "x", 0.0), 1.25e10, "digit separators in exponents");
            AssertEqual(Math_3.Evaluate("lerp(2, 10, 0.25)", "x", 0.0), 4.0, "lerp non-zero start evaluation");
        }

        private static void TestDiagnostics()
        {
            try
            {
                ExpressionParser.Parse("1 + @");
                throw new InvalidOperationException("Expected malformed expression to fail.");
            }
            catch (FormatException exception)
            {
                Assert(exception.Message.Contains("line 1", StringComparison.Ordinal), "diagnostics include line number");
                Assert(exception.Message.Contains("column 5", StringComparison.Ordinal), "diagnostics include column number");
                Assert(exception.Message.Contains("^", StringComparison.Ordinal), "diagnostics include caret indicator");
            }
        }

        private static void TestConditionals()
        {
            AssertEqual(Math_3.Evaluate("if(x > 0, x^2, -x)", "x", 3.0), 9.0, "function-style conditional true branch");
            AssertEqual(Math_3.Evaluate("if(x > 0, x^2, -x)", "x", -3.0), 3.0, "function-style conditional false branch");
            AssertEqual(Math_3.Evaluate("x > 0 ? x^2 : -x", "x", 3.0), 9.0, "ternary conditional true branch");
            AssertEqual(Math_3.Evaluate("x > 0 ? x^2 : -x", "x", -3.0), 3.0, "ternary conditional false branch");
            AssertEqual(Math_3.Evaluate("0 ? 2 : 1 ? 3 : 4", "x", 0.0), 3.0, "right-associative ternary");
            AssertEqual(Math_3.Evaluate("if(1, 2, 1 / 0)", "x", 0.0), 2.0, "conditional evaluation short-circuits unselected branch");
            AssertEqual(Math_3.Evaluate("1 < 2 && 3 >= 3", "x", 0.0), 1.0, "logical and");
            AssertEqual(Math_3.Evaluate("1 == 2 || 2 != 3", "x", 0.0), 1.0, "logical or with comparisons");
            Assert(Math_3.Simplify("1 > 2 ? 3 : 4") == "4", "constant conditional folds");

            Expression derivative = Math_3.Differentiate("x > 0 ? x^2 : -x", "x");
            AssertEqual(Math_3.Evaluate(derivative, "x", 2.0), 4.0, "conditional derivative true branch");
            AssertEqual(Math_3.Evaluate(derivative, "x", -2.0), -1.0, "conditional derivative false branch");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("y > 0", "x"), "x", 0.0), 0.0, "inactive comparison derivative is zero");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("!y", "x"), "x", 0.0), 0.0, "inactive logical-not derivative is zero");
        }

        private static void TestAggregations()
        {
            AssertEqual(Math_3.Evaluate("min(5, 2, 8)", "x", 0.0), 2.0, "variadic min");
            AssertEqual(Math_3.Evaluate("max(5, 2, 8)", "x", 0.0), 8.0, "variadic max");
            AssertEqual(Math_3.Evaluate("sum(1, 2, 3, 4)", "x", 0.0), 10.0, "variadic sum");
            AssertEqual(Math_3.Evaluate("avg(2, 4, 6)", "x", 0.0), 4.0, "variadic average");
            AssertEqual(Math_3.Evaluate("mean(2, 4, 6)", "x", 0.0), 4.0, "mean alias");
            AssertEqual(Math_3.Evaluate("hypot(3, 4, 12)", "x", 0.0), 13.0, "variadic hypot");
            AssertEqual(Math_3.Evaluate("prod(2, 3, 4)", "x", 0.0), 24.0, "variadic product");

            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("sum(x, 2 * x)", "x"), "x", 5.0), 3.0, "sum derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("avg(x, 2 * x)", "x"), "x", 5.0), 1.5, "average derivative");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("prod(x, x + 1)", "x"), "x", 2.0), 5.0, "product derivative");
            AssertThrows<NotSupportedException>(() => Math_3.Differentiate("min(x, 2)", "x"), "variadic min remains non-differentiable");
        }

        private static void TestIndexedAggregations()
        {
            AssertEqual(Math_3.Evaluate("sum(k, 1, 4, k^2)", "x", 0.0), 30.0, "indexed sum expansion");
            AssertEqual(Math_3.Evaluate("prod(k, 1, 4, k)", "x", 0.0), 24.0, "indexed product expansion");
            AssertEqual(Math_3.Evaluate("sum(k, -2147483648, -2147483648, k)", "x", 0.0),
                int.MinValue, "indexed sum accepts int.MinValue endpoint");
            AssertEqual(Math_3.Evaluate("sum(k, 2147483646, 2147483647, k)", "x", 0.0),
                4294967293.0, "short indexed range ending at int.MaxValue");
            AssertEqual(Math_3.Evaluate("sum(k, 3, 1, k)", "x", 0.0), 0.0, "empty indexed sum");
            AssertEqual(Math_3.Evaluate("prod(k, 3, 1, k)", "x", 0.0), 1.0, "empty indexed product");
            AssertEqual(Math_3.Evaluate("sum(k, 1, 4, x)", "x", 2.0), 8.0, "indexed sum keeps free variables");
        }

        private static void TestIntegration()
        {
            AssertDerivativeMatchesIntegral("sech(x)", 0.5, "sech antiderivative");
            AssertDerivativeMatchesIntegral("log2(x)", 8.0, "log2 antiderivative");
            AssertDerivativeMatchesIntegral("log(x, 2)", 8.0, "arbitrary-base log antiderivative");
            AssertDerivativeMatchesIntegral("sigmoid(x)", 0.25, "sigmoid antiderivative");
            AssertDerivativeMatchesIntegral("normcdf(x)", 0.5, "normal CDF antiderivative");
            AssertDerivativeMatchesIntegral("deg2rad(x)", 2.0, "degrees-to-radians antiderivative");

            Dictionary<string, double> logVars = new Dictionary<string, double> { ["x"] = 8.0, ["b"] = 2.0 };
            Expression symbolicBaseLogIntegral = Math_3.Integrate("log(x, b)", "x");
            AssertEqual(
                Math_3.Evaluate(Math_3.Differentiate(symbolicBaseLogIntegral, "x"), logVars),
                Math_3.Evaluate(ExpressionParser.Parse("log(x, b)"), logVars),
                "symbolic-base log antiderivative",
                1e-6);

            Dictionary<string, double> rootVars = new Dictionary<string, double> { ["x"] = 8.0, ["n"] = 3.0 };
            Expression symbolicDegreeRootIntegral = Math_3.Integrate("nthroot(x, n)", "x");
            AssertEqual(
                Math_3.Evaluate(Math_3.Differentiate(symbolicDegreeRootIntegral, "x"), rootVars),
                Math_3.Evaluate(ExpressionParser.Parse("nthroot(x, n)"), rootVars),
                "symbolic-degree nthroot antiderivative",
                1e-6);
        }

        private static void TestSimplificationIdentities()
        {
            Assert(Math_3.Simplify("ln(exp(x))") == "x", "ln(exp(x)) identity");
            Assert(Math_3.Simplify("log10(10 ^ x)") == "x", "log10 identity");
            Assert(Math_3.Simplify("log2(2 ^ x)") == "x", "log2 identity");
            Assert(Math_3.Simplify("rad2deg(deg2rad(x))") == "x", "angle conversion identity");
            Assert(Math_3.Simplify("deg2rad(rad2deg(x))") == "x", "reverse angle conversion identity");
            Assert(Math_3.Simplify("abs(abs(x))") == "abs(x)", "absolute-value idempotence");
            Assert(Math_3.Simplify("abs(-x)") == "abs(x)", "absolute-value negation identity");
            Assert(Math_3.Simplify("floor(floor(x))") == "floor(x)", "floor idempotence");
            Assert(Math_3.Simplify("ceil(ceil(x))") == "ceil(x)", "ceiling idempotence");
            Assert(Math_3.Simplify("trunc(trunc(x))") == "trunc(x)", "truncate idempotence");
            Assert(Math_3.Simplify("min(x, x)") == "x", "min idempotence");
            Assert(Math_3.Simplify("max(x, x)") == "x", "max idempotence");
            Assert(Math_3.Simplify("log(x, x)") == "1", "log(x, x) identity");
            Assert(Math_3.Simplify("log(1, 1)") == "log(1, 1)", "log(1, 1) does not fold through invalid base");
            Assert(Math_3.Simplify("log(0, 0)") == "log(0, 0)", "log(0, 0) does not fold through invalid domain");
            Assert(Math_3.Simplify("lerp(x, y, 0)") == "x", "lerp zero identity");
            Assert(Math_3.Simplify("lerp(x, y, 1)") == "y", "lerp one identity");
            Assert(Math_3.Simplify("lerp(x, x, t)") == "x", "lerp equal endpoints identity");
        }

        private static void TestLetBindings()
        {
            AssertEqual(Math_3.Evaluate("let(x = 2, x * x)", "x", 0.0), 4.0, "function-style let");
            AssertEqual(Math_3.Evaluate("let x = 2, y = x + 3 in y * 2", "x", 0.0), 10.0, "keyword-style let");
            AssertEqual(Math_3.Evaluate("let(a = 3, b = a^2, b + a)", "x", 0.0), 12.0, "sequential let bindings");
            AssertEqual(Math_3.Evaluate("let(x = x + 1, x * 2)", "x", 3.0), 8.0, "let binding shadows outer variable");
            AssertEqual(Math_3.Evaluate(Math_3.Differentiate("let(y = x^2, y + x)", "x"), "x", 3.0), 7.0, "let desugars for calculus");
            AssertEqual(Math_3.Evaluate("let(y = 1 / 0, 5)", "x", 0.0), 5.0, "unused let bindings are removed before evaluation");
        }

        private static void TestErrorCases()
        {
            AssertThrows<FormatException>(() => ExpressionParser.Parse("const.nope"), "unknown qualified constants fail");
            AssertThrows<FormatException>(() => ExpressionParser.Parse("sum(k, 1, 2_000, k)"), "excessive indexed bounds fail");
            AssertThrows<FormatException>(() => ExpressionParser.Parse("sum(k, x, 4, k)"), "non-constant indexed bounds fail");
            AssertThrows<FormatException>(() => ExpressionParser.Parse("1e9999"), "overflowing decimal literals fail deterministically");
            AssertThrows<FormatException>(() => ExpressionParser.Parse(new string('9', 400)),
                "overflowing decimal integer literals fail deterministically");
            AssertThrows<FormatException>(() => ExpressionParser.Parse("x\uD800"), "unpaired surrogate input is rejected as malformed syntax");
            AssertThrows<FormatException>(() => ExpressionParser.Parse("f(" + string.Join(",", Enumerable.Repeat("1", 33)) + ")"),
                "function argument count is bounded");
            AssertThrows<FormatException>(() => ExpressionParser.Parse("let(x = 1, x = 2, x)"), "duplicate let bindings fail");
            AssertThrows<NotSupportedException>(() => ExpressionParser.Parse("let(x = y, y = x, x)"), "forward let references fail");
            AssertThrows<NotSupportedException>(() => Math_3.Differentiate("x > 0", "x"), "standalone comparison derivatives fail");
            AssertThrows<NotSupportedException>(() => Math_3.Differentiate("!x", "x"), "standalone logical-not derivatives fail");
        }

        private static void AssertDerivativeMatchesIntegral(string expression, double value, string message)
        {
            Expression integral = Math_3.Integrate(expression, "x");
            Expression derivative = Math_3.Differentiate(integral, "x");
            double actual = Math_3.Evaluate(derivative, "x", value);
            double expected = Math_3.Evaluate(expression, "x", value);
            AssertEqual(actual, expected, message, 1e-6);
        }
    }
}
