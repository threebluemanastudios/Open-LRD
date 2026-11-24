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
            return result;
        }

        /// <summary>
        /// Computes the inverse Discrete Fourier Transform of spectrum.
        /// </summary>
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
                }
                result[sample] = sum.Real / length;
            }
            return result;
        }

        /// <summary>
        /// Computes the Fast Fourier Transform of samples using an iterative radix-2 algorithm.
        /// </summary>
        /// <param name="samples">The complex samples. The count must be a power of two.</param>
        /// <returns>The complex spectrum of the same length as samples.</returns>
        /// <exception cref="ArgumentException">Thrown when the sample count is not a power of two.</exception>
        public static Complex[] Fft(IReadOnlyList<Complex> samples)
        {
            ValidateComplexSequence(samples, nameof(samples), MaximumFftSize);
            if (!IsPowerOfTwo(samples.Count))
            {
                throw new ArgumentException("FFT length must be a power of two.", nameof(samples));
            }
            Complex[] result = new Complex[samples.Count];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = samples[index];
            }
            TransformInPlace(result, inverse: false);
            return result;
        }

        /// <summary>
        /// Computes the inverse Fast Fourier Transform of spectrum.
        /// </summary>
        public static Complex[] InverseFft(IReadOnlyList<Complex> spectrum)
        {
            ValidateComplexSequence(spectrum, nameof(spectrum), MaximumFftSize);
            if (!IsPowerOfTwo(spectrum.Count))
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
        /// Computes the FFT of a real-valued sequence, zero-padded to a power of two, returning the non-negative frequency bins.
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

        /// <summary>
        /// Rearranges FFT output so that the zero-frequency component is centered.
        /// </summary>
        /// <param name="values">The spectrum to shift. Must not be empty.</param>
        /// <returns>A new array with the two halves of values swapped.</returns>
        /// <exception cref="ArgumentNullException">Thrown when values is null.</exception>
        /// <exception cref="ArgumentException">Thrown when values is empty.</exception>
        public static Complex[] FftShift(Complex[] values)
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
            return shifted;
        }

        /// <summary>
        /// Computes the next power of two greater than or equal to value.
        /// </summary>
        public static int NextPowerOfTwo(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            if (value > MaximumFftSize)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"FFT size must not exceed {MaximumFftSize}.");
            }
            int result = 1;
            while (result < value)
            {
                result <<= 1;
            }
            return result;
        }

        /// <summary>
        /// Computes the full linear convolution of signal and kernel by direct summation.
        /// </summary>
        /// <param name="signal">The input signal.</param>
        /// <param name="kernel">The convolution kernel.</param>
        /// <returns>The convolution with length signal.Length + kernel.Length - 1.</returns>
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

        /// <summary>
        /// Computes the full linear convolution of signal and kernel using the FFT.
        /// </summary>
        /// <param name="signal">The input signal.</param>
        /// <param name="kernel">The convolution kernel.</param>
        /// <returns>The convolution with length signal.Length + kernel.Length - 1.</returns>
        public static double[] ConvolveFft(double[] signal, double[] kernel)
        {
            ValidateRealSequence(signal, nameof(signal), MaximumFftSize);
            ValidateRealSequence(kernel, nameof(kernel), MaximumFftSize);
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
            {
                result[index] = convolution[index].Real;
            }
            return result;
        }

        /// <summary>
        /// Computes cross-correlation between first and second.
        /// </summary>
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
        /// Computes the auto-correlation of signal by cross-correlating it with itself.
        /// </summary>
        public static double[] AutoCorrelate(double[] signal)
        {
            ValidateRealSequence(signal, nameof(signal), int.MaxValue);
            return CrossCorrelate(signal, signal);
        }

        /// <summary>
        /// Creates a tapering window of the specified length and type.
        /// </summary>
        /// <param name="length">The number of window samples. Must be positive.</param>
        /// <param name="type">The window function to generate.</param>
        /// <returns>The window coefficients.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when length is not positive or type is not defined.</exception>
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
                };
            }
            return result;
        }

        /// <summary>
        /// Multiplies signal element-wise by a window of the specified type.
        /// </summary>
        /// <param name="signal">The input signal.</param>
        /// <param name="type">The window function to apply.</param>
        /// <returns>The windowed signal.</returns>
        public static double[] ApplyWindow(double[] signal, WindowType type)
        {
            ValidateRealSequence(signal, nameof(signal), int.MaxValue);
            double[] window = CreateWindow(signal.Length, type);
            double[] result = new double[signal.Length];
            for (int index = 0; index < signal.Length; index++)
            {
                result[index] = signal[index] * window[index];
            }
            return result;
        }

        /// <summary>
        /// Applies a causal finite impulse response (FIR) filter to signal.
        /// </summary>
        /// <param name="signal">The input signal.</param>
        /// <param name="filter">The filter whose coefficients are applied.</param>
        /// <returns>The filtered signal, with the same length as signal.</returns>
        /// <exception cref="ArgumentNullException">Thrown when filter is null.</exception>
        public static double[] ApplyFirFilter(double[] signal, FirFilter filter)
        {
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
                int available = Math.Min(sample + 1, filter.Coefficients.Length);
                for (int tap = 0; tap < available; tap++)
                {
                    sum += filter.Coefficients[tap] * signal[sample - tap];
                }
                output[sample] = sum;
            }
            return output;
        }

        /// <summary>
        /// Smooths signal with a causal moving average over the specified window size.
        /// </summary>
        /// <param name="signal">The input signal.</param>
        /// <param name="windowSize">The number of samples averaged. Must be at least one.</param>
        /// <returns>The smoothed signal, with the same length as signal.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when windowSize is less than one.</exception>
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
        /// Applies a one-pole low-pass (exponential smoothing) filter with smoothing factor alpha to signal.
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

        /// <summary>
        /// Applies a one-pole high-pass filter with smoothing factor alpha to signal.
        /// </summary>
        /// <param name="signal">The input signal.</param>
        /// <param name="alpha">The filter coefficient in (0, 1].</param>
        /// <returns>The high-pass filtered signal.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when alpha is outside (0, 1].</exception>
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

        /// <summary>
        /// Applies a second-order (biquad) IIR filter with the given numerator and denominator coefficients to signal.
        /// </summary>
        /// <param name="signal">The input signal.</param>
        /// <param name="b0">The feedforward coefficient for the current sample.</param>
        /// <param name="b1">The feedforward coefficient for the previous sample.</param>
        /// <param name="b2">The feedforward coefficient for the sample two steps back.</param>
        /// <param name="a1">The feedback coefficient for the previous output.</param>
        /// <param name="a2">The feedback coefficient for the output two steps back.</param>
        /// <returns>The filtered signal.</returns>
        /// <exception cref="ArgumentException">Thrown when the denominator coefficients do not define a stable filter.</exception>
        public static double[] BiquadFilter(double[] signal, double b0, double b1, double b2, double a1, double a2)
        {
            ValidateRealSequence(signal, nameof(signal), int.MaxValue);
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

        /// <summary>
        /// Estimates the complex Fourier series coefficients of a periodic function over one period by sampling and DFT.
        /// </summary>
        /// <param name="function">The periodic function with period 2π.</param>
        /// <param name="terms">The number of harmonics retained on each side of zero frequency.</param>
        /// <param name="samplesPerPeriod">The number of samples taken over one period. Must exceed the retained frequency range.</param>
        /// <returns>The coefficients for frequencies -terms through terms, in that order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when function is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when terms or samplesPerPeriod are out of range.</exception>
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

        /// <summary>
        /// Reconstructs the value of a Fourier series at position x from its centered coefficients.
        /// </summary>
        /// <param name="coefficients">The coefficients for frequencies -terms through terms. The count must be odd.</param>
        /// <param name="x">The position at which to evaluate the series.</param>
        /// <param name="period">The positive period of the series.</param>
        /// <returns>The real part of the reconstructed series value at x.</returns>
        /// <exception cref="ArgumentNullException">Thrown when coefficients is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the coefficient count is zero or even.</exception>
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

        /// <summary>
        /// Performs an in-place iterative radix-2 FFT (or inverse FFT with 1/N scaling) on values, whose length must be a power of two.
        /// </summary>
        /// <param name="values">The array to transform in place.</param>
        /// <param name="inverse">true to compute the inverse transform; false for the forward transform.</param>
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

        /// <summary>
        /// Determines whether a value is a positive power of two.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>true if value is a positive power of two; otherwise false.</returns>
        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        /// <summary>
        /// Validates that a real-valued sequence is non-null, non-empty, within the size limit, and contains only finite values.
        /// </summary>
        /// <param name="values">The sequence to validate.</param>
        /// <param name="parameterName">The parameter name used in exception messages.</param>
        /// <param name="maximumLength">The maximum permitted length.</param>
        /// <exception cref="ArgumentNullException">Thrown when values is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is out of range or a value is not finite.</exception>
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

        /// <summary>
        /// Validates that a real-valued array is non-null, non-empty, within the size limit, and contains only finite values.
        /// </summary>
        /// <param name="values">The array to validate.</param>
        /// <param name="parameterName">The parameter name used in exception messages.</param>
        /// <param name="maximumLength">The maximum permitted length.</param>
        /// <exception cref="ArgumentNullException">Thrown when values is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is out of range or a value is not finite.</exception>
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

        /// <summary>
        /// Validates that a complex sequence is non-null, non-empty, within the size limit, and contains only finite values.
        /// </summary>
        /// <param name="values">The sequence to validate.</param>
        /// <param name="parameterName">The parameter name used in exception messages.</param>
        /// <param name="maximumLength">The maximum permitted length.</param>
        /// <exception cref="ArgumentNullException">Thrown when values is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is out of range or a component is not finite.</exception>
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

        /// <summary>
        /// Validates that a smoothing factor is finite and lies in (0, 1].
        /// </summary>
        /// <param name="alpha">The smoothing factor to validate.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when alpha is outside (0, 1].</exception>
        private static void ValidateAlpha(double alpha)
        {
            if (!double.IsFinite(alpha) || alpha <= 0.0 || alpha > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be in (0, 1].");
            }
        }

        /// <summary>
        /// Evaluates a function at value and verifies that the result is finite.
        /// </summary>
        /// <param name="function">The function to evaluate.</param>
        /// <param name="value">The input value.</param>
        /// <returns>The finite function result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the function returns a non-finite value.</exception>
        private static double EvaluateFinite(Func<double, double> function, double value)
        {
            double result = function(value);
            if (!double.IsFinite(result))
            {
                throw new InvalidOperationException("Function returned a non-finite value.");
            }
            return result;
        }

        /// <summary>
        /// Ensures a value is finite (not NaN or infinity).
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="parameterName">The parameter name used in exception messages.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not finite.</exception>
        private static void EnsureFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
            }
        }

        /// <summary>
        /// Ensures a value is finite and strictly positive.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="parameterName">The parameter name used in exception messages.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not positive and finite.</exception>
        private static void EnsurePositiveFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be positive and finite.");
            }
        }
    }
}
