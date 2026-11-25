using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using static RcBench.Cfg;
using RcBench;

// ---------------- Measurements ----------------

Console.WriteLine("== Phase 0 benchmark ==");
Console.WriteLine($"Process arch: {RuntimeInformation.ProcessArchitecture}, cores: {Environment.ProcessorCount}");
Console.WriteLine($"AVX2: {Avx2.IsSupported}, FMA: {Fma.IsSupported}, AVX-512F: {Avx512F.IsSupported}");

var rng = new Random(12345); // fixed seed: reproducible weights on this machine

// 1) Kernel correctness: AVX2 vs scalar on a realistic shape.
{
    QMat m = new QMat(Ffn, Hidden, rng);
    float[] x = new float[Hidden];
    for (int i = 0; i < Hidden; i++) x[i] = (float)(rng.NextDouble() * 2 - 1);
    float[] ys = new float[Ffn], ya = new float[Ffn], yp = new float[Ffn];
    Kernels.MatVecScalar(m, x, ys);
    Kernels.MatVecAvx2(m, x, ya);
    Kernels.MatVecAvx2Parallel(m, x, yp, Environment.ProcessorCount);
    double maxErr = 0, maxRel = 0;
    for (int i = 0; i < Ffn; i++)
    {
        maxErr = Math.Max(maxErr, Math.Abs(ys[i] - ya[i]));
        maxRel = Math.Max(maxRel, Math.Abs(ys[i]) > 1e-6 ? Math.Abs((ys[i] - ya[i]) / ys[i]) : 0);
    }
    bool parallelEqual = true;
    for (int i = 0; i < Ffn; i++) if (BitConverter.SingleToInt32Bits(ya[i]) != BitConverter.SingleToInt32Bits(yp[i])) { parallelEqual = false; break; }
    Console.WriteLine($"Correctness (Ffn x Hidden): max abs err AVX2 vs scalar = {maxErr:E3}, max rel err = {maxRel:E3}");
    Console.WriteLine($"Parallel AVX2 bit-identical to serial AVX2: {parallelEqual}");
}

// 2) Build model.
Console.WriteLine("Building model (random int8 weights)...");
var sw = Stopwatch.StartNew();
Model model = new Model(rng);
sw.Stop();
Console.WriteLine($"Params: {model.ParamCount() / 1e9:F3} B, int8 weight bytes: {model.WeightBytes() / 1e9:F3} GB, build {sw.Elapsed.TotalSeconds:F1}s");
Console.WriteLine($"Process working set: {Environment.WorkingSet / 1e9:F2} GB");
if (Environment.GetEnvironmentVariable("CTX_ONLY") == "1")
{
    Model.VecAttn = true;
    foreach (int pos in new[] { 1023, 4095, 8191, 15999 })
    {
        Model.AttnTicks = 0; Model.TotalTicks = 0;
        DecodeBench("vec attn, parallel matvec, ctx sweep", (m, x, y) => Kernels.MatVecAvx2Parallel(m, x, y, Environment.ProcessorCount), pos, 4);
        Console.WriteLine($"  attention {100.0 * Model.AttnTicks / Model.TotalTicks:F1}% of step at pos {pos}, {Model.AttnTicks / (double)Stopwatch.Frequency / 4 * 1e3:F1} ms/token");
    }
    Console.WriteLine($"Peak working set: {Environment.WorkingSet / 1e9:F2} GB");
    return;
}

// 3) Matvec throughput per kernel on the largest matrix (Lm: Vocab x Hidden) and FFN.
void MatBench(string name, QMat m, Model.MatVec mv, int iters)
{
    float[] x = new float[m.Cols];
    for (int i = 0; i < x.Length; i++) x[i] = 0.01f * (i % 17);
    float[] y = new float[m.Rows];
    mv(m, x, y); // warm up
    var t = Stopwatch.StartNew();
    for (int i = 0; i < iters; i++) mv(m, x, y);
    t.Stop();
    double secPer = t.Elapsed.TotalSeconds / iters;
    double gbps = m.W.Length / secPer / 1e9;
    Console.WriteLine($"  {name,-28} {secPer * 1e3,8:F3} ms/matvec  {gbps,6:F1} GB/s (int8 weights)");
}

Console.WriteLine("Matvec throughput:");
MatBench("Ffn  W1 (6912x2560) scalar", model.W1[0], Kernels.MatVecScalar, 5);
MatBench("Ffn  W1 (6912x2560) AVX2", model.W1[0], Kernels.MatVecAvx2, 30);
MatBench("Ffn  W1 parallel AVX2", model.W1[0], (m, x, y) => Kernels.MatVecAvx2Parallel(m, x, y, Environment.ProcessorCount), 30);
MatBench("Lm   (32000x2560) AVX2", model.Lm, Kernels.MatVecAvx2, 10);
MatBench("Lm   (32000x2560) parallel AVX2", model.Lm, (m, x, y) => Kernels.MatVecAvx2Parallel(m, x, y, Environment.ProcessorCount), 20);

// 4) Decode throughput: full forward per token.
void DecodeBench(string name, Model.MatVec mv, int startPos, int steps)
{
    int token = 1;
    // fill positions below startPos cheaply by reusing decode (time not counted) only when startPos==0
    var t = Stopwatch.StartNew();
    for (int s = 0; s < steps; s++)
    {
        token = model.DecodeStep(token, startPos + s, mv);
    }
    t.Stop();
    double tps = steps / t.Elapsed.TotalSeconds;
    Console.WriteLine($"  {name,-40} pos {startPos,5}..{startPos + steps - 1,-5} {tps,7:F2} tok/s  ({t.Elapsed.TotalSeconds / steps * 1e3:F1} ms/token)");
}

Console.WriteLine("Decode throughput (single token, KV cache, full forward):");
// Warm-up pass for JIT tiering
model.DecodeStep(1, 0, Kernels.MatVecAvx2);
DecodeBench("AVX2 serial", Kernels.MatVecAvx2, 0, 32);
DecodeBench("AVX2 parallel rows", (m, x, y) => Kernels.MatVecAvx2Parallel(m, x, y, Environment.ProcessorCount), 0, 32);
DecodeBench("Scalar reference", Kernels.MatVecScalar, 0, 4);

// Long-context: fill KV by decoding 1024 tokens (not timed as decode), then time 32 at pos 1024.
Console.WriteLine("Filling KV cache to 1024 positions (untimed)...");
{
    int token = 1;
    for (int p = 0; p < 1024; p++) token = model.DecodeStep(token, p, (m, x, y) => Kernels.MatVecAvx2Parallel(m, x, y, Environment.ProcessorCount));
}
Model.AttnTicks = 0; Model.TotalTicks = 0;
DecodeBench("AVX2 parallel rows, long ctx", (m, x, y) => Kernels.MatVecAvx2Parallel(m, x, y, Environment.ProcessorCount), 1024, 32);
Console.WriteLine($"  Breakdown at pos 1024: attention {100.0 * Model.AttnTicks / Model.TotalTicks:F1}% of step, attention {Model.AttnTicks / (double)Stopwatch.Frequency / 32 * 1e3:F1} ms/token, non-attention {(Model.TotalTicks - Model.AttnTicks) / (double)Stopwatch.Frequency / 32 * 1e3:F1} ms/token");

Model.VecAttn = true;
Model.AttnTicks = 0; Model.TotalTicks = 0;
DecodeBench("AVX2 parallel rows + AVX2 attention, long ctx", (m, x, y) => Kernels.MatVecAvx2Parallel(m, x, y, Environment.ProcessorCount), 1024, 32);
Console.WriteLine($"  Breakdown at pos 1024 (vec attn): attention {100.0 * Model.AttnTicks / Model.TotalTicks:F1}% of step, attention {Model.AttnTicks / (double)Stopwatch.Frequency / 32 * 1e3:F1} ms/token, non-attention {(Model.TotalTicks - Model.AttnTicks) / (double)Stopwatch.Frequency / 32 * 1e3:F1} ms/token");
Console.WriteLine($"Peak working set: {Environment.WorkingSet / 1e9:F2} GB");
Console.WriteLine("== done ==");
