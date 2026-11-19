using System;
using System.Collections.Generic;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class FinancialMathTests
    {
        public static void RunAll()
        {
            Run("Interest and time value", TestInterestAndTimeValue);
            Run("Annuities and amortization", TestAnnuitiesAndAmortization);
            Run("Investment analysis", TestInvestmentAnalysis);
            Run("Depreciation schedules", TestDepreciation);
            Run("Bond pricing and duration", TestBonds);
            Run("Financial math validation", TestValidation);
        }

        private static void TestInterestAndTimeValue()
        {
            AssertEqual(FinancialMath.CompoundInterest(1000.0, 0.05, 2.0, CompoundingFrequency.Annual), 1102.5, "annual compound interest");
            AssertEqual(
                FinancialMath.CompoundInterest(1000.0, 0.12, 1.0, CompoundingFrequency.Monthly),
                1000.0 * Math.Pow(1.01, 12),
                "monthly compound interest");
            AssertEqual(FinancialMath.ContinuousCompoundInterest(1000.0, 0.1, 1.0), 1000.0 * Math.Exp(0.1), "continuous compounding");
            AssertEqual(FinancialMath.EffectiveAnnualRate(0.12, CompoundingFrequency.Monthly), Math.Pow(1.01, 12) - 1.0, "effective annual rate");
            AssertEqual(FinancialMath.EffectiveAnnualRate(0.05, CompoundingFrequency.Continuous), Math.Exp(0.05) - 1.0, "continuous effective rate");
            AssertEqual(FinancialMath.PresentValue(121.0, 0.1, 2.0), 100.0, "present value");
            AssertEqual(FinancialMath.FutureValue(100.0, 0.1, 2.0), 121.0, "future value");
        }

        private static void TestAnnuitiesAndAmortization()
        {
            AssertEqual(FinancialMath.AnnuityPresentValue(100.0, 0.0, 5), 500.0, "zero-rate annuity present value");
            AssertEqual(FinancialMath.AnnuityFutureValue(100.0, 0.0, 5), 500.0, "zero-rate annuity future value");
            AssertEqual(FinancialMath.LoanPayment(1000.0, 0.01, 12), 88.8487886783417, "loan payment", 1e-8);
            AssertEqual(FinancialMath.LoanPayment(1200.0, 0.0, 12), 100.0, "zero-rate loan payment");

            IReadOnlyList<AmortizationEntry> schedule = FinancialMath.AmortizationSchedule(1000.0, 0.01, 12);
            Assert(schedule.Count == 12, "amortization row count");
            double principalTotal = 0.0;
            foreach (AmortizationEntry entry in schedule)
            {
                principalTotal += entry.Principal;
            }
            AssertEqual(principalTotal, 1000.0, "amortization principal total", 1e-8);
            AssertEqual(schedule[^1].Balance, 0.0, "amortization final balance");
            Assert(schedule[0].Interest > 0.0 && schedule[0].Principal > 0.0, "payment splits into interest and principal");
        }

        private static void TestInvestmentAnalysis()
        {
            CashFlow[] cashFlows =
            {
                new CashFlow(0.0, -100.0),
                new CashFlow(1.0, 60.0),
                new CashFlow(2.0, 60.0)
            };
            AssertEqual(FinancialMath.NetPresentValue(cashFlows, 0.1), 4.1322314049587, "net present value", 1e-9);
            InvestmentResult irr = FinancialMath.InternalRateOfReturn(cashFlows);
            Assert(irr.Converged, "IRR should converge");
            AssertEqual(irr.InternalRateOfReturn, 0.1306623862918, "internal rate of return", 1e-8);
            AssertEqual(FinancialMath.ProfitabilityIndex(cashFlows, 0.1), 1.041322314049587, "profitability index", 1e-9);

            double payback = FinancialMath.PaybackPeriod(new[]
            {
                new CashFlow(0.0, -100.0),
                new CashFlow(1.0, 50.0),
                new CashFlow(2.0, 75.0)
            });
            AssertEqual(payback, 1.0 + 50.0 / 75.0, "interpolated payback period");
            Assert(double.IsPositiveInfinity(FinancialMath.PaybackPeriod(new[] { new CashFlow(0.0, -5.0), new CashFlow(1.0, 1.0) })), "unrecovered investment has infinite payback");
            Assert(!FinancialMath.InternalRateOfReturn(new[] { new CashFlow(0.0, -1.0), new CashFlow(1.0, -1.0) }).Converged, "IRR reports failure without sign change");
        }

        private static void TestDepreciation()
        {
            double[] straight = FinancialMath.DepreciationSchedule(100.0, 10.0, 3, DepreciationMethod.StraightLine);
            AssertEqual(straight[0], 30.0, "straight-line annual depreciation");
            AssertEqual(Sum(straight), 90.0, "straight-line depreciable base");

            foreach (DepreciationMethod method in new[] { DepreciationMethod.DecliningBalance, DepreciationMethod.SumOfYearsDigits })
            {
                double[] schedule = FinancialMath.DepreciationSchedule(100.0, 10.0, 3, method);
                AssertEqual(Sum(schedule), 90.0, $"{method} depreciable base", 1e-9);
                Assert(schedule[^1] >= 0.0, $"{method} final depreciation");
            }
        }

        private static void TestBonds()
        {
            double price = FinancialMath.BondPrice(1000.0, 0.05, 0.04, 5, 2);
            double yield = FinancialMath.BondYield(1000.0, 0.05, price, 5, 2);
            AssertEqual(yield, 0.04, "bond yield round trip", 1e-8);
            Assert(FinancialMath.MacaulayDuration(1000.0, 0.05, 0.04, 5, 2) > 0.0, "positive Macaulay duration");
            Assert(FinancialMath.ModifiedDuration(1000.0, 0.05, 0.04, 5, 2) > 0.0, "positive modified duration");
            Assert(FinancialMath.Convexity(1000.0, 0.05, 0.04, 5, 2) > 0.0, "positive convexity");
            AssertEqual(FinancialMath.BondPrice(1000.0, 0.0, 0.05, 5, 1), 1000.0 / Math.Pow(1.05, 5), "zero-coupon bond price");
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentOutOfRangeException>(
                () => FinancialMath.CompoundInterest(100.0, -1.0, 1.0, CompoundingFrequency.Annual),
                "periodic rate must exceed -100 percent");
            AssertThrows<ArgumentOutOfRangeException>(() => FinancialMath.LoanPayment(100.0, -0.01, 12), "negative loan rate");
            AssertThrows<ArgumentOutOfRangeException>(() => FinancialMath.DepreciationSchedule(100.0, 101.0, 5, DepreciationMethod.StraightLine), "salvage exceeds cost");
            AssertThrows<ArgumentException>(
                () => FinancialMath.NetPresentValue(new[] { new CashFlow(1.0, 1.0), new CashFlow(0.0, -1.0) }, 0.05),
                "cash flows must be time-ordered");
            AssertThrows<ArgumentOutOfRangeException>(
                () => FinancialMath.BondPrice(100.0, 0.05, -2.0, 3, 1),
                "bond periodic yield must exceed -100 percent");
        }

        private static double Sum(double[] values)
        {
            double sum = 0.0;
            foreach (double value in values)
            {
                sum += value;
            }
            return sum;
        }
    }
}
