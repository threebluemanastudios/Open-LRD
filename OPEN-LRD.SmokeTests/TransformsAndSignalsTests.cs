using System;
using Complex = System.Numerics.Complex;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class TransformsAndSignalsTests
    {
        public static void RunAll()
        {
            Run("DFT, FFT, and inverse transforms", TestTransforms);
            Run("Frequency shifts and real FFT", TestConvenienceTransforms);
            Run("Convolution and correlation", TestConvolutionAndCorrelation);
            Run("Signal windows", TestWindows);
            Run("FIR and recursive filters", TestFilters);
            Run("Fourier series reconstruction", TestFourierSeries);
            Run("Transforms and signals validation", TestValidation);
        }

        private static void TestTransforms()
        {
            Complex[] deltaSpectrum = TransformsAndSignals.Dft(new[] { 1.0, 0.0, 0.0, 0.0 });
            foreach (Complex value in deltaSpectrum)
            {
                AssertComplexEqual(value, Complex.One, "DFT of an impulse");
            }

            Complex[] samples =
            {
                new Complex(1.0, 1.0),
                new Complex(2.0, -1.0),
                new Complex(-1.0, 0.5),
                new Complex(0.0, 2.0)
            };
            Complex[] fft = TransformsAndSignals.Fft(samples);
            double[] realSamples = { 1.0, 2.0, -1.0, 0.0 };
            Complex[] realFft = TransformsAndSignals.Fft(new[]
            {
                new Complex(realSamples[0], 0.0),
                new Complex(realSamples[1], 0.0),
                new Complex(realSamples[2], 0.0),
                new Complex(realSamples[3], 0.0)
            });
            Complex[] dft = TransformsAndSignals.Dft(realSamples);
            for (int index = 0; index < realFft.Length; index++)
            {
                AssertComplexEqual(realFft[index], dft[index], "FFT/DFT comparison", 1e-7);
            }

            Complex[] roundTrip = TransformsAndSignals.InverseFft(fft);
            for (int index = 0; index < samples.Length; index++)
            {
                AssertComplexEqual(roundTrip[index], samples[index], "inverse FFT round trip");
            }

            double inputEnergy = 0.0;
            double spectrumEnergy = 0.0;
            foreach (Complex value in samples)
            {
                inputEnergy += value.Magnitude * value.Magnitude;
            }
            foreach (Complex value in fft)
            {
                spectrumEnergy += value.Magnitude * value.Magnitude / fft.Length;
            }
            AssertEqual(spectrumEnergy, inputEnergy, "Parseval identity", 1e-8);

            double[] sinusoid = new double[8];
            for (int index = 0; index < sinusoid.Length; index++)
            {
                sinusoid[index] = Math.Sin(2.0 * Math.PI * 2.0 * index / sinusoid.Length);
            }
            Complex[] sinusoidSpectrum = TransformsAndSignals.Dft(sinusoid);
            AssertEqual(sinusoidSpectrum[2].Magnitude, 4.0, "sinusoid frequency-bin magnitude");
            AssertEqual(sinusoidSpectrum[6].Magnitude, 4.0, "sinusoid conjugate frequency-bin magnitude");
        }

        private static void TestConvenienceTransforms()
        {
            Assert(TransformsAndSignals.NextPowerOfTwo(1) == 1, "next power of two of one");
            Assert(TransformsAndSignals.NextPowerOfTwo(9) == 16, "next power of two");
            Complex[] shifted = TransformsAndSignals.FftShift(new[] { Complex.Zero, Complex.One, new Complex(2.0, 0.0), new Complex(3.0, 0.0) });
            Assert(shifted[0].Real == 2.0 && shifted[1].Real == 3.0 && shifted[2] == Complex.Zero && shifted[3] == Complex.One, "FFT shift order");
            Complex[] realSpectrum = TransformsAndSignals.RealFft(new[] { 1.0, 2.0, 3.0 });
            Assert(realSpectrum.Length == 3, "real FFT pads odd input and returns non-negative frequencies");
            AssertComplexEqual(realSpectrum[0], new Complex(6.0, 0.0), "real FFT DC component");
        }

        private static void TestConvolutionAndCorrelation()
        {
            double[] signal = { 1.0, 2.0, 3.0 };
            double[] kernel = { 1.0, 1.0 };
            double[] direct = TransformsAndSignals.Convolve(signal, kernel);
            double[] fft = TransformsAndSignals.ConvolveFft(signal, kernel);
            double[] expected = { 1.0, 3.0, 5.0, 3.0 };
            for (int index = 0; index < expected.Length; index++)
            {
                AssertEqual(direct[index], expected[index], "direct convolution");
                AssertEqual(fft[index], expected[index], "FFT convolution", 1e-8);
            }
            double[] correlation = TransformsAndSignals.CrossCorrelate(signal, new[] { 1.0, 2.0 });
            Assert(correlation.Length == 4, "full correlation length");
            AssertEqual(correlation[0], 2.0, "negative-lag correlation");
            AssertEqual(correlation[1], 5.0, "zero-lag correlation");
            AssertEqual(correlation[2], 8.0, "positive-lag correlation");
            double[] auto = TransformsAndSignals.AutoCorrelate(new[] { 1.0, 2.0 });
            Assert(auto.Length == 3 && auto[1] == 5.0, "autocorrelation");
        }

        private static void TestWindows()
        {
            double[] hann = TransformsAndSignals.CreateWindow(5, WindowType.Hann);
            AssertEqual(hann[0], 0.0, "Hann start endpoint");
            AssertEqual(hann[2], 1.0, "Hann center");
            AssertEqual(hann[4], 0.0, "Hann end endpoint");
            AssertEqual(hann[1], hann[3], "Hann symmetry");
            AssertEqual(TransformsAndSignals.CreateWindow(5, WindowType.Hamming)[0], 0.08, "Hamming endpoint");
            AssertEqual(TransformsAndSignals.CreateWindow(5, WindowType.Rectangular)[3], 1.0, "rectangular window");
            double[] applied = TransformsAndSignals.ApplyWindow(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 }, WindowType.Hann);
            AssertEqual(applied[2], 3.0, "window application");
        }

        private static void TestFilters()
        {
            double[] signal = { 1.0, 2.0, 3.0 };
            double[] fir = TransformsAndSignals.ApplyFirFilter(signal, new FirFilter(new[] { 0.5, 0.5 }));
            AssertEqual(fir[0], 0.5, "FIR first output");
            AssertEqual(fir[1], 1.5, "FIR second output");
            AssertEqual(fir[2], 2.5, "FIR third output");
            double[] movingAverage = TransformsAndSignals.MovingAverageFilter(signal, 2);
            AssertEqual(movingAverage[2], 2.5, "moving-average filter");

            double[] lowPass = TransformsAndSignals.OnePoleLowPass(new[] { 1.0, 0.0, 0.0 }, 0.5);
            AssertEqual(lowPass[1], 0.5, "one-pole low-pass");
            AssertEqual(lowPass[2], 0.25, "one-pole low-pass state");
            double[] highPass = TransformsAndSignals.OnePoleHighPass(new[] { 1.0, 0.0, 0.0 }, 0.5);
            AssertEqual(highPass[1], -0.5, "one-pole high-pass");
            AssertEqual(highPass[2], -0.25, "one-pole high-pass state");
            double[] biquad = TransformsAndSignals.BiquadFilter(signal, 1.0, 0.0, 0.0, 0.0, 0.0);
            AssertEqual(biquad[1], signal[1], "identity biquad");
        }

        private static void TestFourierSeries()
        {
            Complex[] coefficients = TransformsAndSignals.FourierSeriesCoefficients(
                value => Math.Sin(value) >= 0.0 ? 1.0 : -1.0,
                15,
                1024);
            double reconstructed = TransformsAndSignals.FourierSeriesReconstruct(coefficients, Math.PI / 2.0, 2.0 * Math.PI);
            Assert(reconstructed > 0.9 && reconstructed < 1.1, "Fourier series approximates square wave away from discontinuity");
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentOutOfRangeException>(() => TransformsAndSignals.NextPowerOfTwo(0), "non-positive FFT size");
            AssertThrows<ArgumentException>(() => TransformsAndSignals.Fft(new[] { Complex.One, Complex.One, Complex.One }), "FFT requires power-of-two length");
            AssertThrows<ArgumentOutOfRangeException>(() => TransformsAndSignals.CreateWindow(0, WindowType.Hann), "window length must be positive");
            AssertThrows<ArgumentOutOfRangeException>(() => TransformsAndSignals.OnePoleLowPass(new[] { 1.0 }, 0.0), "filter alpha must be positive");
            AssertThrows<ArgumentException>(() => TransformsAndSignals.BiquadFilter(new[] { 1.0 }, 1.0, 0.0, 0.0, 0.0, 1.0), "unstable biquad");
        }
    }
}
