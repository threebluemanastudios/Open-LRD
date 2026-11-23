using System;
using System.Collections.Generic;
using Complex = System.Numerics.Complex;

namespace OPEN_LRD.Skills
{
    internal enum WindowType
    {
        Rectangular,
        Hann,
        Hamming,
        Blackman
    }

    internal sealed record FirFilter(double[] Coefficients);

    internal static class TransformsAndSignals
    {
        private const int MaximumFftSize = 1 << 20;
        private const int MaximumDftSize = 8192;

        /// <summary>
        /// Computes the Discrete Fourier Transform of samples.
        /// </summary>
        public static Complex[] Dft(IReadOnlyList<double> samples)
        {
            ValidateRealSequence(samples, nameof(samples), MaximumDftSize);
            int length = samples.Count;
            Complex[] result = new Complex[length];
            for (int frequency = 0; frequency < length; frequency++)
            {
                Complex sum = Complex.Zero;
                for (int sample = 0; sample < length; sample++)
                {
                    double angle = -2.0 * Math.PI * frequency * sample / length;
                    sum += samples[sample] * new Complex(Math.Cos(angle), Math.Sin(angle));
                }
                result[frequency] = sum;
            }
        /// <summary>
        /// Computes the inverse Discrete Fourier Transform of spectrum.
        /// </summary>
            return result;
        }

        public static double[] InverseDft(IReadOnlyList<Complex> spectrum)
        {
            ValidateComplexSequence(spectrum, nameof(spectrum), MaximumDftSize);
            int length = spectrum.Count;
            double[] result = new double[length];
            for (int sample = 0; sample < length; sample++)
            {
                Complex sum = Complex.Zero;
                for (int frequency = 0; frequency < length; frequency++)
                {
                    double angle = 2.0 * Math.PI * frequency * sample / length;
                    sum += spectrum[frequency] * new Complex(Math.Cos(angle), Math.Sin(angle));
        /// <summary>
        /// Computes the Fast Fourier Transform of samples.
        /// </summary>
                }
                result[sample] = sum.Real / length;
            }
            return result;
        }

        public static Complex[] Fft(IReadOnlyList<Complex> samples)
        {
            ValidateComplexSequence(samples, nameof(samples), MaximumFftSize);
            if (!IsPowerOfTwo(samples.Count))
            {
                throw new ArgumentException("FFT length must be a power of two.", nameof(samples));
            }
        /// <summary>
        /// Computes the inverse Fast Fourier Transform of spectrum.
        /// </summary>
            Complex[] result = new Complex[samples.Count];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = samples[index];
            }
            TransformInPlace(result, inverse: false);
            return result;
        }

        public static Complex[] InverseFft(IReadOnlyList<Complex> spectrum)
        {
            ValidateComplexSequence(spectrum, nameof(spectrum), MaximumFftSize);
            if (!IsPowerOfTwo(spectrum.Count))
        /// <summary>
        /// Computes the real-input Fast Fourier Transform of samples.
        /// </summary>
            {
                throw new ArgumentException("FFT length must be a power of two.", nameof(spectrum));
            }
            Complex[] result = new Complex[spectrum.Count];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = spectrum[index];
            }
            TransformInPlace(result, inverse: true);
            return result;
        }

        /// <summary>
        /// Shifts zero-frequency bins of values.
        /// </summary>
        public static Complex[] RealFft(IReadOnlyList<double> samples)
        {
            ValidateRealSequence(samples, nameof(samples), MaximumFftSize);
            int length = NextPowerOfTwo(samples.Count);
            Complex[] padded = new Complex[length];
            for (int index = 0; index < samples.Count; index++)
            {
                padded[index] = new Complex(samples[index], 0.0);
            }
            Complex[] spectrum = Fft(padded);
            Complex[] result = new Complex[length / 2 + 1];
            Array.Copy(spectrum, result, result.Length);
            return result;
        }

        public static Complex[] FftShift(Complex[] values)
        /// <summary>
        /// Computes the next power of two greater than or equal to value.
        /// </summary>
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }
            if (values.Length == 0)
            {
                throw new ArgumentException("FFT shift input must not be empty.", nameof(values));
            }
            Complex[] shifted = new Complex[values.Length];
            int offset = (values.Length + 1) / 2;
            for (int index = 0; index < values.Length; index++)
            {
                shifted[index] = values[(index + offset) % values.Length];
            }
        /// <summary>
        /// Computes discrete convolution of signal with kernel.
        /// </summary>
            return shifted;
        }

        public static int NextPowerOfTwo(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            if (value > MaximumFftSize)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"FFT size must not exceed {MaximumFftSize}.");
        /// <summary>
        /// Computes convolution of signal with kernel using FFT.
        /// </summary>
            }
            int result = 1;
            while (result < value)
            {
                result <<= 1;
            }
            return result;
        }

        public static double[] Convolve(double[] signal, double[] kernel)
        {
            ValidateRealSequence(signal, nameof(signal), int.MaxValue);
            ValidateRealSequence(kernel, nameof(kernel), int.MaxValue);
            double[] result = new double[signal.Length + kernel.Length - 1];
            for (int signalIndex = 0; signalIndex < signal.Length; signalIndex++)
            {
                for (int kernelIndex = 0; kernelIndex < kernel.Length; kernelIndex++)
                {
                    result[signalIndex + kernelIndex] += signal[signalIndex] * kernel[kernelIndex];
                }
            }
            return result;
        }

        public static double[] ConvolveFft(double[] signal, double[] kernel)
        {
            ValidateRealSequence(signal, nameof(signal), MaximumFftSize);
            ValidateRealSequence(kernel, nameof(kernel), MaximumFftSize);
        /// <summary>
        /// Computes cross-correlation between first and second.
        /// </summary>
            int outputLength = checked(signal.Length + kernel.Length - 1);
            int transformLength = NextPowerOfTwo(outputLength);
            Complex[] first = new Complex[transformLength];
            Complex[] second = new Complex[transformLength];
            for (int index = 0; index < signal.Length; index++)
            {
                first[index] = new Complex(signal[index], 0.0);
            }
            for (int index = 0; index < kernel.Length; index++)
            {
                second[index] = new Complex(kernel[index], 0.0);
            }
            Complex[] firstSpectrum = Fft(first);
            Complex[] secondSpectrum = Fft(second);
            for (int index = 0; index < transformLength; index++)
            {
                firstSpectrum[index] *= secondSpectrum[index];
            }
            Complex[] convolution = InverseFft(firstSpectrum);
            double[] result = new double[outputLength];
            for (int index = 0; index < outputLength; index++)
        /// <summary>
        /// Computes autocorrelation of signal.
        /// </summary>
            {
                result[index] = convolution[index].Real;
            }
        /// <summary>
        /// Creates a window function array of length with type.
        /// </summary>
            return result;
        }

        public static double[] CrossCorrelate(double[] first, double[] second)
        {
            ValidateRealSequence(first, nameof(first), int.MaxValue);
            ValidateRealSequence(second, nameof(second), int.MaxValue);
            int outputLength = first.Length + second.Length - 1;
            double[] result = new double[outputLength];
            int firstLag = -(second.Length - 1);
            for (int output = 0; output < outputLength; output++)
            {
                int lag = firstLag + output;
                double sum = 0.0;
                for (int index = 0; index < first.Length; index++)
                {
                    int secondIndex = index - lag;
                    if (secondIndex >= 0 && secondIndex < second.Length)
                    {
                        sum += first[index] * second[secondIndex];
                    }
                }
                result[output] = sum;
            }
            return result;
        }

        /// <summary>
        /// Applies a window of type to signal.
        /// </summary>
        public static double[] AutoCorrelate(double[] signal)
        {
            ValidateRealSequence(signal, nameof(signal), int.MaxValue);
            return CrossCorrelate(signal, signal);
        }

        public static double[] CreateWindow(int length, WindowType type)
        {
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (!Enum.IsDefined(type))
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }
            double[] result = new double[length];
            if (type == WindowType.Rectangular || length == 1)
            {
                Array.Fill(result, 1.0);
                return result;
            }
            for (int index = 0; index < length; index++)
            {
                double phase = 2.0 * Math.PI * index / (length - 1.0);
                result[index] = type switch
                {
                    WindowType.Hann => 0.5 - 0.5 * Math.Cos(phase),
                    WindowType.Hamming => 0.54 - 0.46 * Math.Cos(phase),
                    WindowType.Blackman => 0.42 - 0.5 * Math.Cos(phase) + 0.08 * Math.Cos(2.0 * phase),
                    _ => 1.0
        /// <summary>
        /// Filters signal with a moving average of windowSize.
        /// </summary>
                };
            }
            return result;
        }

        public static double[] ApplyWindow(double[] signal, WindowType type)
        {
            ValidateRealSequence(signal, nameof(signal), int.MaxValue);
            double[] window = CreateWindow(signal.Length, type);
        /// <summary>
        /// Applies a one-pole low-pass filter with smoothing factor alpha to signal.
        /// </summary>
            double[] result = new double[signal.Length];
            for (int index = 0; index < signal.Length; index++)
            {
                result[index] = signal[index] * window[index];
            }
            return result;
        }

        public static double[] ApplyFirFilter(double[] signal, FirFilter filter)
        {
        /// <summary>
        /// Applies a one-pole high-pass filter with smoothing factor alpha to signal.
        /// </summary>
            ValidateRealSequence(signal, nameof(signal), int.MaxValue);
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }
            ValidateRealSequence(filter.Coefficients, nameof(filter), int.MaxValue);
            double[] output = new double[signal.Length];
            for (int sample = 0; sample < signal.Length; sample++)
            {
                double sum = 0.0;
        /// <summary>
        /// Applies a biquad filter with coefficients b0, b1, b2, a1, and a2 to signal.
        /// </summary>
                int available = Math.Min(sample + 1, filter.Coefficients.Length);
                for (int tap = 0; tap < available; tap++)
                {
                    sum += filter.Coefficients[tap] * signal[sample - tap];
                }
                output[sample] = sum;
            }
            return output;
        }

        public static double[] MovingAverageFilter(double[] signal, int windowSize)
        {
            ValidateRealSequence(signal, nameof(signal), int.MaxValue);
            if (windowSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(windowSize));
            }
            double[] coefficients = new double[windowSize];
            Array.Fill(coefficients, 1.0 / windowSize);
            return ApplyFirFilter(signal, new FirFilter(coefficients));
        }
        /// <summary>
        /// Computes Fourier series coefficients for function using terms and samplesPerPeriod.
        /// </summary>

        public static double[] OnePoleLowPass(double[] signal, double alpha)
        {
            ValidateRealSequence(signal, nameof(signal), int.MaxValue);
            ValidateAlpha(alpha);
            double[] result = new double[signal.Length];
            result[0] = signal[0];
            for (int index = 1; index < signal.Length; index++)
            {
                result[index] = alpha * signal[index] + (1.0 - alpha) * result[index - 1];
            }
            return result;
        }

        public static double[] OnePoleHighPass(double[] signal, double alpha)
        {
            ValidateRealSequence(signal, nameof(signal), int.MaxValue);
            ValidateAlpha(alpha);
            double[] result = new double[signal.Length];
            result[0] = 0.0;
            for (int index = 1; index < signal.Length; index++)
            {
                result[index] = alpha * (result[index - 1] + signal[index] - signal[index - 1]);
            }
            return result;
        }

        public static double[] BiquadFilter(double[] signal, double b0, double b1, double b2, double a1, double a2)
        {
            ValidateRealSequence(signal, nameof(signal), int.MaxValue);
        /// <summary>
        /// Reconstructs value at x over period from coefficients.
        /// </summary>
            EnsureFinite(b0, nameof(b0));
            EnsureFinite(b1, nameof(b1));
            EnsureFinite(b2, nameof(b2));
            EnsureFinite(a1, nameof(a1));
            EnsureFinite(a2, nameof(a2));
            if (Math.Abs(a2) >= 1.0 || 1.0 + a1 + a2 <= 0.0 || 1.0 - a1 + a2 <= 0.0)
            {
                throw new ArgumentException("Biquad denominator coefficients must define a stable filter.");
            }
            double[] output = new double[signal.Length];
            for (int index = 0; index < signal.Length; index++)
            {
                double x1 = index >= 1 ? signal[index - 1] : 0.0;
                double x2 = index >= 2 ? signal[index - 2] : 0.0;
                double y1 = index >= 1 ? output[index - 1] : 0.0;
                double y2 = index >= 2 ? output[index - 2] : 0.0;
                output[index] = b0 * signal[index] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            }
            return output;
        }

        public static Complex[] FourierSeriesCoefficients(Func<double, double> function, int terms, int samplesPerPeriod)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (terms < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(terms));
            }
            if (samplesPerPeriod < Math.Max(2, 2 * terms + 1))
            {
                throw new ArgumentOutOfRangeException(nameof(samplesPerPeriod), "Sample count must exceed the retained frequency range.");
            }
            if (samplesPerPeriod > MaximumFftSize)
            {
                throw new ArgumentOutOfRangeException(nameof(samplesPerPeriod));
            }
            double[] samples = new double[samplesPerPeriod];
            for (int index = 0; index < samples.Length; index++)
            {
                samples[index] = EvaluateFinite(function, 2.0 * Math.PI * index / samples.Length);
            }
            Complex[] spectrum = Dft(samples);
            Complex[] coefficients = new Complex[2 * terms + 1];
            for (int frequency = -terms; frequency <= terms; frequency++)
            {
                int source = frequency >= 0 ? frequency : samples.Length + frequency;
                coefficients[frequency + terms] = spectrum[source] / samples.Length;
            }
            return coefficients;
        }

        public static double FourierSeriesReconstruct(IReadOnlyList<Complex> coefficients, double x, double period)
        {
            if (coefficients == null)
            {
                throw new ArgumentNullException(nameof(coefficients));
            }
            if (coefficients.Count == 0 || coefficients.Count % 2 == 0)
            {
                throw new ArgumentException("Fourier coefficients must contain an odd, non-zero number of terms.", nameof(coefficients));
            }
            EnsureFinite(x, nameof(x));
            EnsurePositiveFinite(period, nameof(period));
            int terms = coefficients.Count / 2;
            Complex result = Complex.Zero;
            for (int index = 0; index < coefficients.Count; index++)
            {
                int frequency = index - terms;
                double angle = 2.0 * Math.PI * frequency * x / period;
                result += coefficients[index] * new Complex(Math.Cos(angle), Math.Sin(angle));
            }
            return result.Real;
        }

        private static void TransformInPlace(Complex[] values, bool inverse)
        {
            int length = values.Length;
            for (int index = 1, reversed = 0; index < length; index++)
            {
                int bit = length >> 1;
                for (; (reversed & bit) != 0; bit >>= 1)
                {
                    reversed ^= bit;
                }
                reversed ^= bit;
                if (index < reversed)
                {
                    (values[index], values[reversed]) = (values[reversed], values[index]);
                }
            }
            for (int size = 2; size <= length; size <<= 1)
            {
                double angle = (inverse ? 2.0 : -2.0) * Math.PI / size;
                Complex root = new Complex(Math.Cos(angle), Math.Sin(angle));
                for (int start = 0; start < length; start += size)
                {
                    Complex factor = Complex.One;
                    int half = size >> 1;
                    for (int offset = 0; offset < half; offset++)
                    {
                        Complex even = values[start + offset];
                        Complex odd = factor * values[start + offset + half];
                        values[start + offset] = even + odd;
                        values[start + offset + half] = even - odd;
                        factor *= root;
                    }
                }
                if (size == length)
                {
                    break;
                }
            }
            if (inverse)
            {
                for (int index = 0; index < length; index++)
                {
                    values[index] /= length;
                }
            }
        }

        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        private static void ValidateRealSequence(IReadOnlyList<double> values, string parameterName, int maximumLength)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (values.Count == 0 || values.Count > maximumLength)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Sequence must be non-empty and within the configured size limit.");
            }
            foreach (double value in values)
            {
                EnsureFinite(value, parameterName);
            }
        }

        private static void ValidateRealSequence(double[] values, string parameterName, int maximumLength)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (values.Length == 0 || values.Length > maximumLength)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Sequence must be non-empty and within the configured size limit.");
            }
            foreach (double value in values)
            {
                EnsureFinite(value, parameterName);
            }
        }

        private static void ValidateComplexSequence(IReadOnlyList<Complex> values, string parameterName, int maximumLength)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (values.Count == 0 || values.Count > maximumLength)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Sequence must be non-empty and within the configured size limit.");
            }
            foreach (Complex value in values)
            {
                if (!double.IsFinite(value.Real) || !double.IsFinite(value.Imaginary))
                {
                    throw new ArgumentOutOfRangeException(parameterName, "Complex samples must be finite.");
                }
            }
        }

        private static void ValidateAlpha(double alpha)
        {
            if (!double.IsFinite(alpha) || alpha <= 0.0 || alpha > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be in (0, 1].");
            }
        }

        private static double EvaluateFinite(Func<double, double> function, double value)
        {
            double result = function(value);
            if (!double.IsFinite(result))
            {
                throw new InvalidOperationException("Function returned a non-finite value.");
            }
            return result;
        }

        private static void EnsureFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
            }
        }

        private static void EnsurePositiveFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be positive and finite.");
            }
        }
    }
}
