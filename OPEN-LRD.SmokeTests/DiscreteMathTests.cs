using System;
using System.Linq;
using System.Numerics;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class DiscreteMathTests
    {
        public static void RunAll()
        {
            Run("Modular arithmetic", TestModularArithmetic);
            Run("Prime sieve and primality", TestPrimes);
            Run("Factorization and arithmetic functions", TestFactorization);
            Run("Chinese remainder theorem", TestChineseRemainder);
            Run("Combinatorics", TestCombinatorics);
            Run("Base conversion and digit sums", TestBaseConversion);
            Run("Discrete math validation", TestValidation);
        }

        private static void TestModularArithmetic()
        {
            Assert(DiscreteMath.Mod(-3, 5) == 2, "negative modular reduction");
            Assert(DiscreteMath.ModPow(2, 10, 1000) == 24, "modular exponentiation");
            Assert(DiscreteMath.ModPow(new BigInteger(2), new BigInteger(100), new BigInteger(1009)) == BigInteger.ModPow(2, 100, 1009), "BigInteger modular exponentiation");
            Assert(DiscreteMath.ModInverse(3, 11) == 4, "modular inverse");
            Assert(!DiscreteMath.TryModInverse(6, 9, out _), "non-coprime modular inverse should fail");

            long gcd = DiscreteMath.ExtendedGcd(240, 46, out long x, out long y);
            Assert(gcd == 2, "extended GCD value");
            Assert(240 * x + 46 * y == gcd, "Bezout identity");
            Assert(DiscreteMath.IsCoprime(35, 64), "coprime values");

            CongruenceSolution solution = DiscreteMath.SolveLinearCongruence(14, 30, 100);
            Assert(solution.Exists && solution.Solution == 45 && solution.Modulus == 50, "linear congruence solution");
            Assert(!DiscreteMath.SolveLinearCongruence(6, 5, 9).Exists, "inconsistent linear congruence");
        }

        private static void TestPrimes()
        {
            Assert(DiscreteMath.Sieve(30).SequenceEqual(new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29 }), "small sieve");
            Assert(DiscreteMath.SegmentedSieve(100, 120).SequenceEqual(new long[] { 101, 103, 107, 109, 113 }), "segmented sieve");
            Assert(DiscreteMath.IsPrime(2), "two is prime");
            Assert(DiscreteMath.IsPrime(2305843009213693951), "large 64-bit prime");
            Assert(!DiscreteMath.IsPrime(561), "Carmichael number is composite");
            Assert(!DiscreteMath.IsPrime(9223372036854775807), "maximum Int64 is composite");
            Assert(DiscreteMath.IsProbablePrime(new BigInteger(104729), 12, new Random(7)), "probable-prime test");
            Assert(!DiscreteMath.IsProbablePrime(new BigInteger(3215031751), 12, new Random(7)), "probable-prime test rejects pseudoprime");
        }

        private static void TestFactorization()
        {
            PrimeFactorization factorization = DiscreteMath.Factorize(600851475143);
            Assert(
                factorization.Factors.SequenceEqual(new[] { (71L, 1), (839L, 1), (1471L, 1), (6857L, 1) }),
                "known prime factorization");
            long reconstructed = 1;
            foreach ((long prime, int exponent) in factorization.Factors)
            {
                for (int index = 0; index < exponent; index++)
                {
                    reconstructed = checked(reconstructed * prime);
                }
            }
            Assert(reconstructed == factorization.Value, "factorization reconstructs original value");
            Assert(DiscreteMath.PollardRho(8051) is 83 or 97, "Pollard rho finds a non-trivial factor");
            Assert(DiscreteMath.EulerPhi(36) == 12, "Euler phi");
            Assert(DiscreteMath.DivisorCount(28) == 6, "divisor count");
            Assert(DiscreteMath.DivisorSum(28) == 56, "divisor sum");
            Assert(DiscreteMath.Moebius(30) == -1, "Mobius value");
            Assert(DiscreteMath.Moebius(12) == 0, "Mobius repeated factor");
            Assert(DiscreteMath.Liouville(12) == -1, "Liouville value");
        }

        private static void TestChineseRemainder()
        {
            CrtResult result = DiscreteMath.ChineseRemainderTheorem(new[] { 2L, 3L, 2L }, new[] { 3L, 5L, 7L });
            Assert(result.Exists && result.Solution == 23 && result.Modulus == 105, "Chinese remainder solution");
            Assert(DiscreteMath.TryChineseRemainderTheorem(new[] { 1L, 2L }, new[] { 2L, 4L }, out _, out _) == false, "inconsistent CRT should fail");
            CrtResult nonCoprime = DiscreteMath.ChineseRemainderTheorem(new[] { 2L, 4L }, new[] { 6L, 8L });
            Assert(nonCoprime.Exists && nonCoprime.Solution == 20 && nonCoprime.Modulus == 24, "compatible non-coprime CRT");
        }

        private static void TestCombinatorics()
        {
            Assert(DiscreteMath.Catalan(5) == 42, "Catalan number");
            Assert(DiscreteMath.Bell(5) == 52, "Bell number");
            Assert(DiscreteMath.StirlingFirst(4, 2) == 11, "signed Stirling number of the first kind");
            Assert(DiscreteMath.StirlingSecond(5, 2) == 15, "Stirling number of the second kind");
            Assert(DiscreteMath.Partition(5) == 7, "partition number");
            Assert(DiscreteMath.Derangement(5) == 44, "derangement count");
            Assert(DiscreteMath.Binomial(100, 50) == BigInteger.Parse("100891344545564193334812497256"), "exact BigInteger binomial");
            Assert(DiscreteMath.Multinomial(new[] { 2, 1 }) == 3, "multinomial coefficient");
        }

        private static void TestBaseConversion()
        {
            Assert(DiscreteMath.ToBase(new BigInteger(-255), 16) == "-FF", "negative hexadecimal formatting");
            Assert(DiscreteMath.FromBase("-ff", 16) == -255, "case-insensitive base parsing");
            Assert(DiscreteMath.ToBase(BigInteger.Zero, 2) == "0", "zero base formatting");
            Assert(DiscreteMath.DigitSum(-255, 16) == 30, "digit sum in base sixteen");
            BigInteger large = BigInteger.Parse("123456789012345678901234567890");
            Assert(DiscreteMath.FromBase(DiscreteMath.ToBase(large, 36), 36) == large, "large base conversion round trip");
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentOutOfRangeException>(() => DiscreteMath.Mod(1, 0), "zero modulus");
            AssertThrows<ArgumentOutOfRangeException>(() => DiscreteMath.Mod(1, -7), "negative modulus");
            AssertThrows<ArgumentOutOfRangeException>(() => DiscreteMath.Mod(BigInteger.One, new BigInteger(-7)),
                "negative BigInteger modulus");
            AssertThrows<ArgumentOutOfRangeException>(() => DiscreteMath.ModPow(2, -1, 7), "negative exponent");
            AssertThrows<InvalidOperationException>(() => DiscreteMath.ModInverse(6, 9), "missing inverse");
            AssertThrows<ArgumentOutOfRangeException>(() => DiscreteMath.Sieve(50000001), "sieve cap");
            AssertThrows<ArgumentOutOfRangeException>(() => DiscreteMath.Factorize(0), "factorization rejects zero");
            AssertThrows<ArgumentOutOfRangeException>(() => DiscreteMath.ToBase(BigInteger.One, 1), "invalid radix");
            AssertThrows<FormatException>(() => DiscreteMath.FromBase("2", 2), "invalid digit for radix");
        }
    }
}
