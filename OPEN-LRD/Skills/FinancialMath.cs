using System;
using System.Collections.Generic;

namespace OPEN_LRD.Skills
{
    internal enum CompoundingFrequency
    {
        Annual,
        Semiannual,
        Quarterly,
        Monthly,
        Daily,
        Continuous
    }

    internal enum DepreciationMethod
    {
        StraightLine,
        DecliningBalance,
        SumOfYearsDigits
    }

    internal sealed record AmortizationEntry(int Period, double Payment, double Interest, double Principal, double Balance);

    internal sealed record CashFlow(double Time, double Amount);

    internal sealed record InvestmentResult(double NetPresentValue, double InternalRateOfReturn, bool Converged);

    internal sealed record BondResult(double Price, double Yield, double MacaulayDuration, double ModifiedDuration, double Convexity);

    internal static class FinancialMath
    {
        /// <summary>
        /// Computes compound interest or compounded value for principal at annualRate over years with frequency.
        /// </summary>
        public static double CompoundInterest(double principal, double annualRate, double years, CompoundingFrequency frequency)
        {
            ValidateNonNegativeFinite(principal, nameof(principal));
            EnsureFinite(annualRate, nameof(annualRate));
            ValidateNonNegativeFinite(years, nameof(years));
            if (frequency == CompoundingFrequency.Continuous)
            {
                return ContinuousCompoundInterest(principal, annualRate, years);
            }
            int periodsPerYear = PeriodsPerYear(frequency);
            if (annualRate / periodsPerYear <= -1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(annualRate), "Periodic rate must exceed -100%.");
            }
            return principal * Math.Pow(1.0 + annualRate / periodsPerYear, periodsPerYear * years);
        }

        /// <summary>
        /// Computes continuously compounded value or interest for principal at annualRate over years.
        /// </summary>
        public static double ContinuousCompoundInterest(double principal, double annualRate, double years)
        {
            ValidateNonNegativeFinite(principal, nameof(principal));
            EnsureFinite(annualRate, nameof(annualRate));
            ValidateNonNegativeFinite(years, nameof(years));
            return principal * Math.Exp(annualRate * years);
        }

        /// <summary>
        /// Computes the effective annual interest rate from nominalRate and compounding frequency.
        /// </summary>
        public static double EffectiveAnnualRate(double nominalRate, CompoundingFrequency frequency)
        {
            EnsureFinite(nominalRate, nameof(nominalRate));
            if (frequency == CompoundingFrequency.Continuous)
            {
                return Math.Exp(nominalRate) - 1.0;
            }
            int periodsPerYear = PeriodsPerYear(frequency);
            if (nominalRate / periodsPerYear <= -1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(nominalRate), "Periodic rate must exceed -100%.");
            }
            return Math.Pow(1.0 + nominalRate / periodsPerYear, periodsPerYear) - 1.0;
        }

        /// <summary>
        /// Computes the present value of futureValue discounted at rate over periods.
        /// </summary>
        public static double PresentValue(double futureValue, double rate, double periods)
        {
            ValidateNonNegativeFinite(futureValue, nameof(futureValue));
            ValidateRate(rate, nameof(rate));
            ValidateNonNegativeFinite(periods, nameof(periods));
            return futureValue / Math.Pow(1.0 + rate, periods);
        }

        /// <summary>
        /// Computes the future value of a single present amount compounded at rate over periods.
        /// </summary>
        public static double FutureValue(double presentValue, double rate, double periods)
        {
            ValidateNonNegativeFinite(presentValue, nameof(presentValue));
            ValidateRate(rate, nameof(rate));
            ValidateNonNegativeFinite(periods, nameof(periods));
            return presentValue * Math.Pow(1.0 + rate, periods);
        }
        /// <summary>
        /// Computes the present value of an annuity paying payment per period at rate over periods.
        /// </summary>

        public static double AnnuityPresentValue(double payment, double rate, int periods)
        {
            ValidateNonNegativeFinite(payment, nameof(payment));
            ValidateRate(rate, nameof(rate));
            ValidatePeriods(periods);
            return rate == 0.0
                ? payment * periods
                : payment * (1.0 - Math.Pow(1.0 + rate, -periods)) / rate;
        }

        /// <summary>
        /// Computes the future value of an annuity paying payment per period at rate over periods.
        /// </summary>
        public static double AnnuityFutureValue(double payment, double rate, int periods)
        {
            ValidateNonNegativeFinite(payment, nameof(payment));
            ValidateRate(rate, nameof(rate));
            ValidatePeriods(periods);
            return rate == 0.0
                ? payment * periods
                : payment * (Math.Pow(1.0 + rate, periods) - 1.0) / rate;
        }

        /// <summary>
        /// Computes the periodic payment required to amortize principal at rate over periods.
        /// </summary>
        public static double LoanPayment(double principal, double rate, int periods)
        {
            ValidateNonNegativeFinite(principal, nameof(principal));
            if (!double.IsFinite(rate) || rate < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(rate), "Loan rate must be finite and non-negative.");
            }
            ValidatePeriods(periods);
            if (rate == 0.0)
            {
                return principal / periods;
            }
            double growth = Math.Pow(1.0 + rate, periods);
            return principal * rate * growth / (growth - 1.0);
        }

        /// <summary>
        /// Builds the period-by-period amortization schedule for a loan of principal at rate over periods.
        /// </summary>
        public static IReadOnlyList<AmortizationEntry> AmortizationSchedule(double principal, double rate, int periods)
        {
            ValidateNonNegativeFinite(principal, nameof(principal));
            if (!double.IsFinite(rate) || rate < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(rate), "Loan rate must be finite and non-negative.");
            }
            ValidatePeriods(periods);
            double payment = LoanPayment(principal, rate, periods);
            double balance = principal;
            List<AmortizationEntry> schedule = new List<AmortizationEntry>(periods);
            for (int period = 1; period <= periods; period++)
            {
                double interest = balance * rate;
                double principalPayment = period == periods ? balance : Math.Min(balance, payment - interest);
                double actualPayment = interest + principalPayment;
                balance = Math.Max(0.0, balance - principalPayment);
                schedule.Add(new AmortizationEntry(period, actualPayment, interest, principalPayment, balance));
            }
            return schedule.AsReadOnly();
        }

        /// <summary>
        /// Computes the net present value of cashFlows discounted at discountRate.
        /// </summary>
        public static double NetPresentValue(IReadOnlyList<CashFlow> cashFlows, double discountRate)
        {
            ValidateCashFlows(cashFlows);
            ValidateRate(discountRate, nameof(discountRate));
            double result = 0.0;
            foreach (CashFlow cashFlow in cashFlows)
            {
                result += cashFlow.Amount / Math.Pow(1.0 + discountRate, cashFlow.Time);
            }
            return result;
        }

        /// <summary>
        /// Computes the internal rate of return of cashFlows by bracketed bisection and reports whether it converged.
        /// </summary>
        public static InvestmentResult InternalRateOfReturn(IReadOnlyList<CashFlow> cashFlows, double tolerance = 1e-9, int maxIterations = 200)
        {
            ValidateCashFlows(cashFlows);
            ValidatePositiveFinite(tolerance, nameof(tolerance));
            ValidateIterations(maxIterations);
            bool hasPositive = false;
            bool hasNegative = false;
            foreach (CashFlow cashFlow in cashFlows)
            {
                hasPositive |= cashFlow.Amount > 0.0;
                hasNegative |= cashFlow.Amount < 0.0;
            }
            double zeroRateNpv = NetPresentValue(cashFlows, 0.0);
            if (!hasPositive || !hasNegative)
            {
                return new InvestmentResult(zeroRateNpv, 0.0, false);
            }

            double lower = -0.999999999;
            double lowerValue = NetPresentValue(cashFlows, lower);
            double upper = 1.0;
            double upperValue = NetPresentValue(cashFlows, upper);
            for (int expansion = 0; expansion < 60 && Math.Sign(lowerValue) == Math.Sign(upperValue); expansion++)
            {
                upper = upper * 2.0 + 1.0;
                if (!double.IsFinite(upper))
                {
                    break;
                }
                upperValue = NetPresentValue(cashFlows, upper);
            }
            if (!double.IsFinite(upper) || Math.Sign(lowerValue) == Math.Sign(upperValue))
            {
                return new InvestmentResult(zeroRateNpv, 0.0, false);
            }
            double midpoint = 0.0;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                midpoint = lower + (upper - lower) * 0.5;
                double value = NetPresentValue(cashFlows, midpoint);
                if (Math.Abs(value) <= tolerance || upper - lower <= tolerance * (1.0 + Math.Abs(midpoint)))
                {
                    return new InvestmentResult(value, midpoint, true);
                }
                if (Math.Sign(value) == Math.Sign(lowerValue))
                {
                    lower = midpoint;
                    lowerValue = value;
                }
                else
                {
                    upper = midpoint;
                    upperValue = value;
                }
            }
            double finalNpv = NetPresentValue(cashFlows, midpoint);
            return new InvestmentResult(finalNpv, midpoint, Math.Abs(finalNpv) <= tolerance * (1.0 + Math.Abs(zeroRateNpv)));
        }

        /// <summary>
        /// Computes the payback period required to recover initial outlay from cashFlows.
        /// </summary>
        public static double PaybackPeriod(IReadOnlyList<CashFlow> cashFlows)
        {
            ValidateCashFlows(cashFlows);
            double cumulative = cashFlows[0].Amount;
            if (cumulative >= 0.0)
            {
                return cashFlows[0].Time;
            }
            for (int index = 1; index < cashFlows.Count; index++)
            {
                double previous = cumulative;
                cumulative += cashFlows[index].Amount;
                if (previous < 0.0 && cumulative >= 0.0)
                {
                    double fraction = -previous / cashFlows[index].Amount;
                    double previousTime = cashFlows[index - 1].Time;
                    return previousTime + fraction * (cashFlows[index].Time - previousTime);
                }
            }
            return double.PositiveInfinity;
        }

        /// <summary>
        /// Computes the ratio of present inflows to present outflows of cashFlows at discountRate.
        /// </summary>
        public static double ProfitabilityIndex(IReadOnlyList<CashFlow> cashFlows, double discountRate)
        {
            ValidateCashFlows(cashFlows);
            ValidateRate(discountRate, nameof(discountRate));
            double presentInflows = 0.0;
            double presentOutflows = 0.0;
            foreach (CashFlow cashFlow in cashFlows)
            {
                double presentValue = cashFlow.Amount / Math.Pow(1.0 + discountRate, cashFlow.Time);
                if (presentValue > 0.0)
                {
                    presentInflows += presentValue;
                }
                else
                {
                    presentOutflows -= presentValue;
                }
            }
            if (presentOutflows == 0.0)
            {
                throw new InvalidOperationException("Profitability index requires at least one cash outflow.");
            }
            return presentInflows / presentOutflows;
        }

        /// <summary>
        /// Computes a depreciation schedule for cost, salvageValue, and life using the selected method.
        /// </summary>
        public static double[] DepreciationSchedule(double cost, double salvageValue, int life, DepreciationMethod method)
        {
            ValidateDepreciation(cost, salvageValue, life);
            if (!Enum.IsDefined(method))
            {
                throw new ArgumentOutOfRangeException(nameof(method));
            }
            return method switch
            {
                DepreciationMethod.StraightLine => StraightLineDepreciation(cost, salvageValue, life),
                DepreciationMethod.DecliningBalance => DecliningBalanceDepreciation(cost, salvageValue, life),
                DepreciationMethod.SumOfYearsDigits => SumOfYearsDigitsDepreciation(cost, salvageValue, life),
                _ => throw new ArgumentOutOfRangeException(nameof(method))
            };
        }

        /// <summary>
        /// Computes an annual straight-line depreciation schedule for cost, salvageValue, and life.
        /// </summary>
        public static double[] StraightLineDepreciation(double cost, double salvageValue, int life)
        {
            ValidateDepreciation(cost, salvageValue, life);
            double annual = (cost - salvageValue) / life;
            double[] schedule = new double[life];
            Array.Fill(schedule, annual);
            schedule[^1] = cost - salvageValue - annual * (life - 1);
            return schedule;
        }

        /// <summary>
        /// Computes an annual declining-balance depreciation schedule for cost, salvageValue, and life, absorbing any remainder in the final year.
        /// </summary>
        public static double[] DecliningBalanceDepreciation(double cost, double salvageValue, int life)
        {
            ValidateDepreciation(cost, salvageValue, life);
            double[] schedule = new double[life];
            double balance = cost;
            double rate = Math.Min(1.0, 2.0 / life);
            for (int year = 0; year < life; year++)
            {
                int remainingYears = life - year;
                double declining = balance * rate;
                double straightLine = Math.Max(0.0, balance - salvageValue) / remainingYears;
                double depreciation = Math.Min(Math.Max(declining, straightLine), balance - salvageValue);
                schedule[year] = Math.Max(0.0, depreciation);
                balance -= schedule[year];
            }
            schedule[^1] += Math.Max(0.0, balance - salvageValue);
            return schedule;
        }

        /// <summary>
        /// Computes an annual sum-of-years-digits depreciation schedule for cost, salvageValue, and life.
        /// </summary>
        public static double[] SumOfYearsDigitsDepreciation(double cost, double salvageValue, int life)
        {
            ValidateDepreciation(cost, salvageValue, life);
            double[] schedule = new double[life];
            double baseValue = cost - salvageValue;
            double digitSum = life * (life + 1.0) / 2.0;
            for (int year = 0; year < life; year++)
            {
                schedule[year] = baseValue * (life - year) / digitSum;
            }
            schedule[^1] = baseValue - Sum(schedule, life - 1);
            return schedule;
        }

        /// <summary>
        /// Computes the price of a bond from its face value, coupon rate, and yield.
        /// </summary>
        public static double BondPrice(double faceValue, double couponRate, double yield, int years, int paymentsPerYear)
        {
            ValidateBond(faceValue, couponRate, yield, years, paymentsPerYear);
            int periods = checked(years * paymentsPerYear);
            double periodicYield = yield / paymentsPerYear;
            double coupon = faceValue * couponRate / paymentsPerYear;
            double price = 0.0;
            for (int period = 1; period <= periods; period++)
            {
                double cashFlow = coupon + (period == periods ? faceValue : 0.0);
                price += cashFlow / Math.Pow(1.0 + periodicYield, period);
            }
            return price;
        }

        /// <summary>
        /// Computes the yield of a bond priced at price by bracketed root search.
        /// </summary>
        public static double BondYield(double faceValue, double couponRate, double price, int years, int paymentsPerYear)
        {
            ValidateNonNegativeFinite(faceValue, nameof(faceValue));
            ValidateNonNegativeFinite(couponRate, nameof(couponRate));
            ValidatePositiveFinite(price, nameof(price));
            ValidateYearsAndFrequency(years, paymentsPerYear);
            if (faceValue <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(faceValue));
            }
            double lower = -paymentsPerYear + 1e-10;
            double upper = Math.Max(1.0, couponRate + 1.0);
            double lowerValue = BondPrice(faceValue, couponRate, lower, years, paymentsPerYear) - price;
            double upperValue = BondPrice(faceValue, couponRate, upper, years, paymentsPerYear) - price;
            for (int expansion = 0; expansion < 60 && Math.Sign(lowerValue) == Math.Sign(upperValue); expansion++)
            {
                upper = upper * 2.0 + 1.0;
                upperValue = BondPrice(faceValue, couponRate, upper, years, paymentsPerYear) - price;
            }
            if (Math.Sign(lowerValue) == Math.Sign(upperValue))
            {
                throw new InvalidOperationException("Bond yield could not be bracketed.");
            }
            for (int iteration = 0; iteration < 250; iteration++)
            {
                double middle = lower + (upper - lower) * 0.5;
                double value = BondPrice(faceValue, couponRate, middle, years, paymentsPerYear) - price;
                if (Math.Abs(value) <= 1e-10 * (1.0 + price))
                {
                    return middle;
                }
                if (Math.Sign(value) == Math.Sign(lowerValue))
                {
                    lower = middle;
                    lowerValue = value;
                }
                else
                {
                    upper = middle;
                    upperValue = value;
                }
            }
            throw new InvalidOperationException("Bond yield did not converge.");
        }

        /// <summary>
        /// Computes the Macaulay duration of a bond.
        /// </summary>
        public static double MacaulayDuration(double faceValue, double couponRate, double yield, int years, int paymentsPerYear)
        {
            return CalculateBondMetrics(faceValue, couponRate, yield, years, paymentsPerYear).MacaulayDuration;
        }

        /// <summary>
        /// Computes the modified duration of a bond.
        /// </summary>
        public static double ModifiedDuration(double faceValue, double couponRate, double yield, int years, int paymentsPerYear)
        {
            return CalculateBondMetrics(faceValue, couponRate, yield, years, paymentsPerYear).ModifiedDuration;
        }

        /// <summary>
        /// Computes the convexity of a bond.
        /// </summary>
        public static double Convexity(double faceValue, double couponRate, double yield, int years, int paymentsPerYear)
        {
            return CalculateBondMetrics(faceValue, couponRate, yield, years, paymentsPerYear).Convexity;
        }

        /// <summary>
        /// Computes bond price, Macaulay duration, modified duration, and convexity in a single pass.
        /// </summary>
        public static BondResult CalculateBondMetrics(double faceValue, double couponRate, double yield, int years, int paymentsPerYear)
        {
            ValidateBond(faceValue, couponRate, yield, years, paymentsPerYear);
            int periods = checked(years * paymentsPerYear);
            double periodicYield = yield / paymentsPerYear;
            double coupon = faceValue * couponRate / paymentsPerYear;
            double price = 0.0;
            double weightedTime = 0.0;
            double convexityNumerator = 0.0;
            for (int period = 1; period <= periods; period++)
            {
                double cashFlow = coupon + (period == periods ? faceValue : 0.0);
                double discountFactor = Math.Pow(1.0 + periodicYield, period);
                double presentValue = cashFlow / discountFactor;
                price += presentValue;
                weightedTime += (period / (double)paymentsPerYear) * presentValue;
                convexityNumerator += presentValue * period * (period + 1.0)
                    / (paymentsPerYear * (double)paymentsPerYear * Math.Pow(1.0 + periodicYield, 2.0));
            }
            double macaulay = weightedTime / price;
            double modified = macaulay / (1.0 + periodicYield);
            double convexity = convexityNumerator / price;
            return new BondResult(price, yield, macaulay, modified, convexity);
        }

        /// <summary>
        /// Validates that cash flows are non-null, non-empty, finite, and time-ordered.
        /// </summary>
        private static void ValidateCashFlows(IReadOnlyList<CashFlow> cashFlows)
        {
            if (cashFlows == null)
            {
                throw new ArgumentNullException(nameof(cashFlows));
            }
            if (cashFlows.Count == 0)
            {
                throw new ArgumentException("Cash flows must not be empty.", nameof(cashFlows));
            }
            double previousTime = -1.0;
            foreach (CashFlow cashFlow in cashFlows)
            {
                ValidateNonNegativeFinite(cashFlow.Time, nameof(cashFlows));
                EnsureFinite(cashFlow.Amount, nameof(cashFlows));
                if (cashFlow.Time < previousTime)
                {
                    throw new ArgumentException("Cash flows must be time-ordered.", nameof(cashFlows));
                }
                previousTime = cashFlow.Time;
            }
        }

        /// <summary>
        /// Validates bond face value, coupon rate, yield, and term arguments.
        /// </summary>
        private static void ValidateBond(double faceValue, double couponRate, double yield, int years, int paymentsPerYear)
        {
            ValidateNonNegativeFinite(faceValue, nameof(faceValue));
            ValidateNonNegativeFinite(couponRate, nameof(couponRate));
            EnsureFinite(yield, nameof(yield));
            ValidateYearsAndFrequency(years, paymentsPerYear);
            if (faceValue <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(faceValue), "Face value must be positive.");
            }
            if (yield / paymentsPerYear <= -1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(yield), "Periodic yield must exceed -100%.");
            }
        }

        /// <summary>
        /// Validates years and paymentsPerYear, and that their product does not overflow.
        /// </summary>
        private static void ValidateYearsAndFrequency(int years, int paymentsPerYear)
        {
            ValidatePeriods(years);
            if (paymentsPerYear <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(paymentsPerYear));
            }
            _ = checked(years * paymentsPerYear);
        }

        /// <summary>
        /// Returns the number of compounding periods per year for a discrete frequency.
        /// </summary>
        private static int PeriodsPerYear(CompoundingFrequency frequency)
        {
            return frequency switch
            {
                CompoundingFrequency.Annual => 1,
                CompoundingFrequency.Semiannual => 2,
                CompoundingFrequency.Quarterly => 4,
                CompoundingFrequency.Monthly => 12,
                CompoundingFrequency.Daily => 365,
                CompoundingFrequency.Continuous => throw new ArgumentException("Continuous compounding has no discrete period count.", nameof(frequency)),
                _ => throw new ArgumentOutOfRangeException(nameof(frequency))
            };
        }

        /// <summary>
        /// Validates cost, salvageValue, and life arguments for depreciation calculations.
        /// </summary>
        private static void ValidateDepreciation(double cost, double salvageValue, int life)
        {
            ValidateNonNegativeFinite(cost, nameof(cost));
            ValidateNonNegativeFinite(salvageValue, nameof(salvageValue));
            ValidatePeriods(life);
            if (salvageValue > cost)
            {
                throw new ArgumentOutOfRangeException(nameof(salvageValue), "Salvage value must not exceed cost.");
            }
        }

        /// <summary>
        /// Sums the first count elements of values.
        /// </summary>
        private static double CalculateDepreciationSum(double[] values, int count)
        {
            double sum = 0.0;
            for (int index = 0; index < count; index++)
            {
                sum += values[index];
            }
            return sum;
        }

        /// <summary>
        /// Sums the first count elements of values.
        /// </summary>
        private static double Sum(double[] values, int count)
        {
            return CalculateDepreciationSum(values, count);
        }

        /// <summary>
        /// Validates that rate is finite and greater than -100%.
        /// </summary>
        private static void ValidateRate(double rate, string parameterName)
        {
            if (!double.IsFinite(rate) || rate <= -1.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Rate must be finite and greater than -100%.");
            }
        }

        /// <summary>
        /// Validates that the period count is at least one.
        /// </summary>
        private static void ValidatePeriods(int periods)
        {
            if (periods < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(periods));
            }
        }

        /// <summary>
        /// Validates that value is finite and non-negative.
        /// </summary>
        private static void ValidateNonNegativeFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and non-negative.");
            }
        }

        /// <summary>
        /// Validates that value is finite and positive.
        /// </summary>
        private static void ValidatePositiveFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and positive.");
            }
        }

        /// <summary>
        /// Validates that value is finite.
        /// </summary>
        private static void EnsureFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
            }
        }

        /// <summary>
        /// Validates that maxIterations is at least one.
        /// </summary>
        private static void ValidateIterations(int maxIterations)
        {
            if (maxIterations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIterations));
            }
        }
    }
}
