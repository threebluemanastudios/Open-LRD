using System;
using System.Collections.Generic;
using System.Numerics;

namespace OPEN_LRD.Skills
{
    internal sealed record PrimeFactorization(long Value, IReadOnlyList<(long Prime, int Exponent)> Factors);

    internal sealed record BigIntegerPrimeFactorization(BigInteger Value, IReadOnlyList<(BigInteger Prime, int Exponent)> Factors);

    internal sealed record CongruenceSolution(bool Exists, long Solution, long Modulus);

    internal sealed record CrtResult(bool Exists, long Solution, long Modulus);

    internal static class DiscreteMath
    {
        private const int MaximumSieveLimit = 50000000;
        private const int MaximumSegmentLength = 5000000;
        private const int MaximumFactorizationIterations = 500000;
        private static readonly ulong[] DeterministicWitnesses = { 2, 325, 9375, 28178, 450775, 9780504, 1795265022 };

        public static long Mod(long value, long modulus)
        {
            ValidateModulus(modulus);
            return (long)PositiveMod(value, modulus);
        }

        public static BigInteger Mod(BigInteger value, BigInteger modulus)
        {
            ValidateModulus(modulus);
            BigInteger result = value % modulus;
            return result.Sign < 0 ? result + modulus : result;
        }

        public static long ModPow(long value, long exponent, long modulus)
        {
            if (exponent < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exponent), "Exponent must be non-negative.");
            }
            ValidateModulus(modulus);
            return (long)BigInteger.ModPow(PositiveMod(value, modulus), exponent, modulus);
        }

        public static BigInteger ModPow(BigInteger value, BigInteger exponent, BigInteger modulus)
        {
            if (exponent < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exponent), "Exponent must be non-negative.");
            }
            ValidateModulus(modulus);
            return BigInteger.ModPow(Mod(value, modulus), exponent, modulus);
        }

        public static long ModInverse(long value, long modulus)
        {
            if (!TryModInverse(value, modulus, out long inverse))
            {
                throw new InvalidOperationException("A modular inverse does not exist for these values.");
            }
            return inverse;
        }

        public static BigInteger ModInverse(BigInteger value, BigInteger modulus)
        {
            if (!TryModInverse(value, modulus, out BigInteger inverse))
            {
                throw new InvalidOperationException("A modular inverse does not exist for these values.");
            }
            return inverse;
        }

        public static bool TryModInverse(long value, long modulus, out long inverse)
        {
            ValidateModulus(modulus);
            bool exists = TryModInverse(new BigInteger(value), new BigInteger(modulus), out BigInteger result);
            if (!exists)
            {
                inverse = 0;
                return false;
            }
            inverse = (long)result;
            return true;
        }

        public static bool TryModInverse(BigInteger value, BigInteger modulus, out BigInteger inverse)
        {
            ValidateModulus(modulus);
            BigInteger gcd = ExtendedGcdBig(PositiveMod(value, modulus), modulus, out BigInteger x, out _);
            if (gcd != BigInteger.One)
            {
                inverse = BigInteger.Zero;
                return false;
            }
            inverse = PositiveMod(x, modulus);
            return true;
        }

        public static long ExtendedGcd(long a, long b, out long x, out long y)
        {
            BigInteger gcd = ExtendedGcdBig(a, b, out BigInteger bigX, out BigInteger bigY);
            x = checked((long)bigX);
            y = checked((long)bigY);
            return checked((long)gcd);
        }

        public static BigInteger ExtendedGcd(BigInteger a, BigInteger b, out BigInteger x, out BigInteger y)
        {
            return ExtendedGcdBig(a, b, out x, out y);
        }

        public static bool IsCoprime(long a, long b)
        {
            return BigInteger.GreatestCommonDivisor(a, b) == BigInteger.One;
        }

        public static CongruenceSolution SolveLinearCongruence(long a, long b, long modulus)
        {
            ValidateModulus(modulus);
            BigInteger bigModulus = modulus;
            BigInteger gcd = BigInteger.GreatestCommonDivisor(a, bigModulus);
            if (b % (long)gcd != 0)
            {
                return new CongruenceSolution(false, 0, 0);
            }
            BigInteger reducedModulus = bigModulus / gcd;
            BigInteger solution = reducedModulus == BigInteger.One
                ? BigInteger.Zero
                : PositiveMod((BigInteger)b / gcd * ModInverse((BigInteger)a / gcd, reducedModulus), reducedModulus);
            return new CongruenceSolution(true, checked((long)solution), checked((long)reducedModulus));
        }

        public static int[] Sieve(int limit)
        {
            if (limit < 0 || limit > MaximumSieveLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), $"Sieve limit must be in [0, {MaximumSieveLimit}].");
            }
            if (limit < 2)
            {
                return Array.Empty<int>();
            }
            bool[] composite = new bool[limit + 1];
            for (int prime = 2; (long)prime * prime <= limit; prime++)
            {
                if (composite[prime])
                {
                    continue;
                }
                for (long multiple = (long)prime * prime; multiple <= limit; multiple += prime)
                {
                    composite[(int)multiple] = true;
                }
            }
            List<int> primes = new List<int>();
            for (int value = 2; value <= limit; value++)
            {
                if (!composite[value])
                {
                    primes.Add(value);
                }
            }
            return primes.ToArray();
        }

        public static long[] SegmentedSieve(long lower, long upper)
        {
            if (upper < lower)
            {
                throw new ArgumentOutOfRangeException(nameof(upper), "Upper bound must not be below lower bound.");
            }
            if (upper < 2)
            {
                return Array.Empty<long>();
            }
            lower = Math.Max(lower, 2);
            if ((BigInteger)upper - lower > MaximumSegmentLength)
            {
                throw new ArgumentOutOfRangeException(nameof(upper), $"Segment length must not exceed {MaximumSegmentLength}.");
            }
            long root = (long)Math.Sqrt(upper);
            if (root > MaximumSieveLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(upper), "The upper bound requires an impractically large base sieve.");
            }
            int[] basePrimes = Sieve((int)root);
            bool[] composite = new bool[checked((int)(upper - lower + 1))];
            foreach (int prime in basePrimes)
            {
                long first = Math.Max((long)prime * prime, lower + ((prime - lower % prime) % prime));
                for (long value = first; value <= upper; value += prime)
                {
                    composite[(int)(value - lower)] = true;
                }
            }
            List<long> result = new List<long>();
            for (int index = 0; index < composite.Length; index++)
            {
                if (!composite[index])
                {
                    result.Add(lower + index);
                }
            }
            return result.ToArray();
        }

        public static bool IsPrime(long value)
        {
            return IsPrime64(value);
        }

        public static bool IsProbablePrime(long value, int rounds, Random random)
        {
            return IsProbablePrime(new BigInteger(value), rounds, random);
        }

        public static bool IsProbablePrime(BigInteger value, int rounds, Random random)
        {
            if (rounds < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(rounds));
            }
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }
            if (value < 2)
            {
                return false;
            }
            if (value == 2 || value == 3)
            {
                return true;
            }
            if (value.IsEven)
            {
                return false;
            }
            foreach (int smallPrime in new[] { 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
            {
                if (value == smallPrime)
                {
                    return true;
                }
                if (value % smallPrime == 0)
                {
                    return false;
                }
            }
            BigInteger d = value - 1;
            int powersOfTwo = 0;
            while (d.IsEven)
            {
                d >>= 1;
                powersOfTwo++;
            }
            for (int round = 0; round < rounds; round++)
            {
                BigInteger witness = 2 + RandomBelow(value - 4, random);
                if (!MillerRabinRound(value, d, powersOfTwo, witness))
                {
                    return false;
                }
            }
            return true;
        }

        public static PrimeFactorization Factorize(long value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Factorization requires a positive integer.");
            }
            BigIntegerPrimeFactorization factorization = Factorize(new BigInteger(value));
            List<(long Prime, int Exponent)> factors = new List<(long Prime, int Exponent)>();
            foreach ((BigInteger prime, int exponent) in factorization.Factors)
            {
                factors.Add((checked((long)prime), exponent));
            }
            return new PrimeFactorization(value, factors.AsReadOnly());
        }

        public static BigIntegerPrimeFactorization Factorize(BigInteger value)
        {
            if (value <= BigInteger.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Factorization requires a positive integer.");
            }
            List<BigInteger> factors = new List<BigInteger>();
            FactorRecursive(value, factors);
            factors.Sort();
            List<(BigInteger Prime, int Exponent)> grouped = new List<(BigInteger Prime, int Exponent)>();
            foreach (BigInteger factor in factors)
            {
                if (grouped.Count > 0 && grouped[^1].Prime == factor)
                {
                    (BigInteger prime, int exponent) = grouped[^1];
                    grouped[^1] = (prime, exponent + 1);
                }
                else
                {
                    grouped.Add((factor, 1));
                }
            }
            return new BigIntegerPrimeFactorization(value, grouped.AsReadOnly());
        }

        public static long PollardRho(long value)
        {
            if (value <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            if (IsPrime(value))
            {
                throw new InvalidOperationException("Pollard rho requires a composite value.");
            }
            BigInteger factor = PollardRho(new BigInteger(value));
            if (factor <= 1 || factor >= value)
            {
                throw new InvalidOperationException("Pollard rho did not find a non-trivial factor.");
            }
            return (long)factor;
        }

        public static long EulerPhi(long value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            BigInteger result = value;
            foreach ((long prime, _) in Factorize(value).Factors)
            {
                result = result / prime * (prime - 1);
            }
            return checked((long)result);
        }

        public static long DivisorCount(long value)
        {
            BigInteger result = BigInteger.One;
            foreach ((long _, int exponent) in Factorize(value).Factors)
            {
                result *= exponent + 1;
            }
            return checked((long)result);
        }

        public static long DivisorSum(long value)
        {
            BigInteger result = BigInteger.One;
            foreach ((long prime, int exponent) in Factorize(value).Factors)
            {
                BigInteger power = BigInteger.One;
                BigInteger sum = BigInteger.One;
                for (int count = 0; count < exponent; count++)
                {
                    power *= prime;
                    sum += power;
                }
                result *= sum;
            }
            return checked((long)result);
        }

        public static int Moebius(long value)
        {
            foreach ((long _, int exponent) in Factorize(value).Factors)
            {
                if (exponent > 1)
                {
                    return 0;
                }
            }
            return Factorize(value).Factors.Count % 2 == 0 ? 1 : -1;
        }

        public static int Liouville(long value)
        {
            int totalExponent = 0;
            foreach ((long _, int exponent) in Factorize(value).Factors)
            {
                totalExponent += exponent;
            }
            return totalExponent % 2 == 0 ? 1 : -1;
        }

        public static CrtResult ChineseRemainderTheorem(long[] remainders, long[] moduli)
        {
            bool exists = TryChineseRemainderTheorem(remainders, moduli, out long solution, out long combinedModulus);
            return new CrtResult(exists, solution, combinedModulus);
        }

        public static bool TryChineseRemainderTheorem(long[] remainders, long[] moduli, out long solution, out long combinedModulus)
        {
            if (remainders == null)
            {
                throw new ArgumentNullException(nameof(remainders));
            }
            if (moduli == null)
            {
                throw new ArgumentNullException(nameof(moduli));
            }
            if (remainders.Length == 0 || remainders.Length != moduli.Length)
            {
                throw new ArgumentException("Remainder and modulus arrays must have equal positive length.");
            }
            BigInteger current = BigInteger.Zero;
            BigInteger currentModulus = BigInteger.One;
            for (int index = 0; index < remainders.Length; index++)
            {
                if (moduli[index] <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(moduli), "Moduli must be positive.");
                }
                BigInteger nextModulus = moduli[index];
                BigInteger nextRemainder = PositiveMod(remainders[index], nextModulus);
                BigInteger gcd = BigInteger.GreatestCommonDivisor(currentModulus, nextModulus);
                BigInteger difference = nextRemainder - current;
                if (difference % gcd != 0)
                {
                    solution = 0;
                    combinedModulus = 0;
                    return false;
                }
                BigInteger reducedNextModulus = nextModulus / gcd;
                BigInteger multiplier = reducedNextModulus == BigInteger.One
                    ? BigInteger.Zero
                    : PositiveMod((difference / gcd) * ModInverse(currentModulus / gcd, reducedNextModulus), reducedNextModulus);
                current += currentModulus * multiplier;
                currentModulus *= reducedNextModulus;
                current = PositiveMod(current, currentModulus);
            }
            if (current < long.MinValue || current > long.MaxValue || currentModulus > long.MaxValue)
            {
                solution = 0;
                combinedModulus = 0;
                return false;
            }
            solution = (long)current;
            combinedModulus = (long)currentModulus;
            return true;
        }

        public static BigInteger Catalan(int n)
        {
            ValidateNonNegative(n, nameof(n));
            if (n > 5000)
            {
                throw new ArgumentOutOfRangeException(nameof(n), "Catalan index is limited to 5000.");
            }
            return Binomial(2 * n, n) / (n + 1);
        }

        public static BigInteger Bell(int n)
        {
            ValidateNonNegative(n, nameof(n));
            if (n > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(n), "Bell index is limited to 1000.");
            }
            BigInteger[] row = { BigInteger.One };
            for (int size = 1; size <= n; size++)
            {
                BigInteger[] next = new BigInteger[size + 1];
                next[0] = row[^1];
                for (int index = 1; index <= size; index++)
                {
                    next[index] = next[index - 1] + row[index - 1];
                }
                row = next;
            }
            return row[0];
        }

        public static BigInteger StirlingFirst(int n, int k)
        {
            ValidateCombinatoricsIndices(n, k);
            BigInteger[] row = new BigInteger[k + 1];
            row[0] = BigInteger.One;
            for (int size = 1; size <= n; size++)
            {
                int limit = Math.Min(size, k);
                for (int index = limit; index >= 1; index--)
                {
                    row[index] = row[index - 1] - (size - 1) * row[index];
                }
                row[0] = BigInteger.Zero;
            }
            return row[k];
        }

        public static BigInteger StirlingSecond(int n, int k)
        {
            ValidateCombinatoricsIndices(n, k);
            BigInteger[] row = new BigInteger[k + 1];
            row[0] = BigInteger.One;
            for (int size = 1; size <= n; size++)
            {
                int limit = Math.Min(size, k);
                for (int index = limit; index >= 1; index--)
                {
                    row[index] = row[index - 1] + index * row[index];
                }
                row[0] = BigInteger.Zero;
            }
            return row[k];
        }

        public static BigInteger Partition(int n)
        {
            ValidateNonNegative(n, nameof(n));
            if (n > 10000)
            {
                throw new ArgumentOutOfRangeException(nameof(n), "Partition index is limited to 10000.");
            }
            BigInteger[] values = new BigInteger[n + 1];
            values[0] = BigInteger.One;
            for (int part = 1; part <= n; part++)
            {
                for (int total = part; total <= n; total++)
                {
                    values[total] += values[total - part];
                }
            }
            return values[n];
        }

        public static BigInteger Derangement(int n)
        {
            ValidateNonNegative(n, nameof(n));
            if (n > 10000)
            {
                throw new ArgumentOutOfRangeException(nameof(n), "Derangement index is limited to 10000.");
            }
            if (n == 0)
            {
                return BigInteger.One;
            }
            BigInteger previous = BigInteger.One;
            BigInteger current = BigInteger.Zero;
            for (int size = 2; size <= n; size++)
            {
                BigInteger next = (size - 1) * (current + previous);
                previous = current;
                current = next;
            }
            return current;
        }

        public static BigInteger Binomial(int n, int k)
        {
            ValidateCombinatoricsIndices(n, k);
            k = Math.Min(k, n - k);
            BigInteger result = BigInteger.One;
            for (int index = 1; index <= k; index++)
            {
                result = result * (n - k + index) / index;
            }
            return result;
        }

        public static BigInteger Multinomial(int[] counts)
        {
            if (counts == null)
            {
                throw new ArgumentNullException(nameof(counts));
            }
            int total = 0;
            foreach (int count in counts)
            {
                ValidateNonNegative(count, nameof(counts));
                total = checked(total + count);
            }
            BigInteger result = BigInteger.One;
            int assigned = 0;
            foreach (int count in counts)
            {
                result *= Binomial(assigned + count, count);
                assigned += count;
            }
            return result;
        }

        public static string ToBase(BigInteger value, int radix)
        {
            ValidateRadix(radix);
            if (value.IsZero)
            {
                return "0";
            }
            const string digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            bool negative = value.Sign < 0;
            BigInteger remaining = BigInteger.Abs(value);
            List<char> output = new List<char>();
            while (remaining > BigInteger.Zero)
            {
                BigInteger quotient = BigInteger.DivRem(remaining, radix, out BigInteger remainder);
                output.Add(digits[(int)remainder]);
                remaining = quotient;
            }
            output.Reverse();
            return (negative ? "-" : string.Empty) + new string(output.ToArray());
        }

        public static BigInteger FromBase(string digits, int radix)
        {
            ValidateRadix(radix);
            if (digits == null)
            {
                throw new ArgumentNullException(nameof(digits));
            }
            if (digits.Length == 0)
            {
                throw new FormatException("Digit string must not be empty.");
            }
            int sign = 1;
            int index = 0;
            if (digits[0] == '+' || digits[0] == '-')
            {
                sign = digits[0] == '-' ? -1 : 1;
                index++;
            }
            if (index == digits.Length)
            {
                throw new FormatException("Digit string must contain at least one digit.");
            }
            BigInteger value = BigInteger.Zero;
            for (; index < digits.Length; index++)
            {
                char character = char.ToUpperInvariant(digits[index]);
                int digit = character is >= '0' and <= '9'
                    ? character - '0'
                    : character is >= 'A' and <= 'Z' ? character - 'A' + 10 : -1;
                if (digit < 0 || digit >= radix)
                {
                    throw new FormatException($"'{digits[index]}' is not a valid base-{radix} digit.");
                }
                value = value * radix + digit;
            }
            return sign < 0 ? -value : value;
        }

        public static int DigitSum(long value, int radix)
        {
            ValidateRadix(radix);
            BigInteger remaining = BigInteger.Abs(new BigInteger(value));
            int sum = 0;
            while (remaining > BigInteger.Zero)
            {
                remaining = BigInteger.DivRem(remaining, radix, out BigInteger remainder);
                sum = checked(sum + (int)remainder);
            }
            return sum;
        }

        private static bool IsPrime64(long value)
        {
            if (value < 2)
            {
                return false;
            }
            foreach (int prime in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
            {
                if (value == prime)
                {
                    return true;
                }
                if (value % prime == 0)
                {
                    return false;
                }
            }
            ulong n = (ulong)value;
            ulong d = n - 1;
            int powersOfTwo = 0;
            while ((d & 1UL) == 0)
            {
                d >>= 1;
                powersOfTwo++;
            }
            foreach (ulong witness in DeterministicWitnesses)
            {
                ulong basis = witness % n;
                if (basis == 0)
                {
                    continue;
                }
                ulong result = (ulong)BigInteger.ModPow(basis, d, n);
                if (result == 1 || result == n - 1)
                {
                    continue;
                }
                bool probablyPrime = false;
                for (int round = 1; round < powersOfTwo; round++)
                {
                    result = (ulong)((BigInteger)result * result % n);
                    if (result == n - 1)
                    {
                        probablyPrime = true;
                        break;
                    }
                }
                if (!probablyPrime)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool MillerRabinRound(BigInteger value, BigInteger oddPart, int powersOfTwo, BigInteger witness)
        {
            BigInteger result = BigInteger.ModPow(witness, oddPart, value);
            if (result == BigInteger.One || result == value - 1)
            {
                return true;
            }
            for (int round = 1; round < powersOfTwo; round++)
            {
                result = result * result % value;
                if (result == value - 1)
                {
                    return true;
                }
            }
            return false;
        }

        private static BigInteger RandomBelow(BigInteger maximumInclusive, Random random)
        {
            byte[] bytes = new byte[maximumInclusive.ToByteArray(isUnsigned: true, isBigEndian: false).Length];
            while (true)
            {
                random.NextBytes(bytes);
                BigInteger candidate = new BigInteger(bytes, isUnsigned: true, isBigEndian: false);
                if (candidate <= maximumInclusive)
                {
                    return candidate;
                }
            }
        }

        private static void FactorRecursive(BigInteger value, List<BigInteger> factors)
        {
            if (value == BigInteger.One)
            {
                return;
            }
            if (IsProbablePrime(value, 20, new Random((int)(value % int.MaxValue))))
            {
                factors.Add(value);
                return;
            }
            BigInteger divisor = PollardRho(value);
            FactorRecursive(divisor, factors);
            FactorRecursive(value / divisor, factors);
        }

        private static BigInteger PollardRho(BigInteger value)
        {
            if (value.IsEven)
            {
                return 2;
            }
            if (value % 3 == 0)
            {
                return 3;
            }
            for (int constant = 1; constant < 128; constant++)
            {
                BigInteger x = 2;
                BigInteger y = 2;
                BigInteger divisor = BigInteger.One;
                for (int iteration = 0; iteration < MaximumFactorizationIterations && divisor == BigInteger.One; iteration++)
                {
                    x = (x * x + constant) % value;
                    y = (y * y + constant) % value;
                    y = (y * y + constant) % value;
                    divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(x - y), value);
                }
                if (divisor > BigInteger.One && divisor < value)
                {
                    return divisor;
                }
            }
            throw new InvalidOperationException("Pollard rho failed to find a factor within the configured iteration bound.");
        }

        private static BigInteger ExtendedGcdBig(BigInteger a, BigInteger b, out BigInteger x, out BigInteger y)
        {
            BigInteger oldR = BigInteger.Abs(a);
            BigInteger r = BigInteger.Abs(b);
            BigInteger oldS = BigInteger.One;
            BigInteger s = BigInteger.Zero;
            BigInteger oldT = BigInteger.Zero;
            BigInteger t = BigInteger.One;
            while (r != BigInteger.Zero)
            {
                BigInteger quotient = oldR / r;
                (oldR, r) = (r, oldR - quotient * r);
                (oldS, s) = (s, oldS - quotient * s);
                (oldT, t) = (t, oldT - quotient * t);
            }
            x = a.Sign < 0 ? -oldS : oldS;
            y = b.Sign < 0 ? -oldT : oldT;
            return oldR;
        }

        private static BigInteger PositiveMod(BigInteger value, BigInteger modulus)
        {
            BigInteger result = value % modulus;
            return result.Sign < 0 ? result + modulus : result;
        }

        private static void ValidateModulus(long modulus)
        {
            if (modulus <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(modulus), "Modulus must be positive.");
            }
        }

        private static void ValidateModulus(BigInteger modulus)
        {
            if (modulus <= BigInteger.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(modulus), "Modulus must be positive.");
            }
        }

        private static void ValidateCombinatoricsIndices(int n, int k)
        {
            ValidateNonNegative(n, nameof(n));
            ValidateNonNegative(k, nameof(k));
            if (k > n)
            {
                throw new ArgumentOutOfRangeException(nameof(k), "k must not exceed n.");
            }
            if (n > 10000)
            {
                throw new ArgumentOutOfRangeException(nameof(n), "Combinatorics index is limited to 10000.");
            }
        }

        private static void ValidateNonNegative(int value, string parameterName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateRadix(int radix)
        {
            if (radix < 2 || radix > 36)
            {
                throw new ArgumentOutOfRangeException(nameof(radix), "Radix must be in [2, 36].");
            }
        }
    }
}
