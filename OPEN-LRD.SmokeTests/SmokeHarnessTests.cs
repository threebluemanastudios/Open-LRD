using System;

namespace OPEN_LRD.SmokeTests
{
    internal static class SmokeHarnessTests
    {
        internal static void RunAll()
        {
            Program.Run("Smoke assertion helpers handle failures and extreme magnitudes", AssertionHelpersHandleEdgeCases);
        }

        private static void AssertionHelpersHandleEdgeCases()
        {
            Program.AssertThrows<InvalidOperationException>(
                () => Program.AssertThrows<ArgumentException>(() => throw new InvalidOperationException("wrong"), "wrong type"),
                "AssertThrows should fail when the action throws the wrong exception type.");
            Program.AssertThrows<InvalidOperationException>(
                () => Program.AssertThrows<ArgumentException>(() => { }, "missing exception"),
                "AssertThrows should fail when the action does not throw.");

            Program.AssertEqual(1e300, 1e300 + 1e285, "relative tolerance at extreme magnitude", 1e-14);
            Program.AssertEqual(double.PositiveInfinity, double.PositiveInfinity, "matching positive infinities");
            Program.AssertEqual(double.NegativeInfinity, double.NegativeInfinity, "matching negative infinities");
            Program.AssertThrows<InvalidOperationException>(
                () => Program.AssertEqual(1e300, 1.01e300, "large relative mismatch", 1e-8),
                "AssertEqual should reject a relative mismatch at extreme magnitude.");
            Program.AssertThrows<InvalidOperationException>(
                () => Program.AssertEqual(double.NaN, double.NaN, "NaN comparison"),
                "AssertEqual should reject NaN values rather than treating them as equal.");
        }
    }
}
