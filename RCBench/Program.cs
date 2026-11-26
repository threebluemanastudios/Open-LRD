using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using OPEN_LRD.Reasoning.Inference;

// Phase 0/1 benchmark driver. Compiles the production sources in OPEN-LRD/Reasoning/Inference.
// Env: ATTN_KERNEL = scalar | fused | int8 (default scalar). CTX_ONLY=1 runs only the 16k context sweep.

ModelConfig cfg = ModelConfig.Benchmark;
string kernelEnv = Environment.GetEnvironmentVariable("ATTN_KERNEL") ?? "scalar";
AttnKernel envKernel = kernelEnv switch
{
    "fused" => AttnKernel.FusedFp32,
    "int8" => AttnKernel.FusedInt8,
    _ => AttnKernel.Scalar,
};
KvMode kvMode = envKernel == AttnKernel.FusedInt8 ? KvMode.Int8 : KvMode.Fp32;
int procs = Environment.ProcessorCount;

Console.WriteLine("== Phase 1 benchmark (production inference code) ==");
Console.WriteLine($"Process arch: {RuntimeInformation.ProcessArchitecture}, cores: {procs}");
Console.WriteLine($"AVX2: {Kernels.SimdSupported}");
var rng = new Random(12345);

// 1) Kernel correctness on the FFN shape.
{
    QMat m = QMat.Random(cfg.Ffn, cfg.Hidden, rng);
    float[] x = new float[cfg.Hidden];
    for (int i = 0; i < x.Length; i++) x[i] = (float)(rng.NextDouble() * 2 - 1);
    float[] ys = new float[cfg.Ffn], ya = new float[cfg.Ffn], yp = new float[cfg.Ffn];
    Kernels.MatVecScalar(m, x, ys);
    Kernels.MatVecAvx2(m, x, ya);
    new MatVecEngine(procs).Run(m, x, yp);
    double maxErr = 0, maxRef = 0;
    bool identical = true;
    for (int i = 0; i < cfg.Ffn; i++)
    {
        maxErr = Math.Max(maxErr, Math.Abs(ys[i] - ya[i]));
        maxRef = Math.Max(maxRef, Math.Abs(ys[i]));
        if (BitConverter.SingleToInt32Bits(ya[i]) != BitConverter.SingleToInt32Bits(yp[i])) identical = false;
    }
    Console.WriteLine($"Correctness (FFN shape): max abs err AVX2 vs scalar = {maxErr:E3} (max output {maxRef:E3}), parallel bit-identical to serial: {identical}");
}

// 2) Build the decoder with random int8 weights.
Console.WriteLine("Building decoder (random int8 weights)...");
var sw = Stopwatch.StartNew();
DecoderWeights weights = DecoderWeights.Random(cfg, rng);
Decoder dec = new Decoder(weights, kvMode, procs);
sw.Stop();
long paramCount = (long)cfg.Vocab * cfg.Hidden;
long weightBytes = weights.Embedding.W.Length + weights.Embedding.Scale.Length * 4L;
for (int l = 0; l < cfg.Layers; l++)
{
    foreach (QMat m in new[] { weights.Wq[l], weights.Wk[l], weights.Wv[l], weights.Wo[l], weights.W1[l], weights.W3[l], weights.W2[l] })
    {
        paramCount += (long)m.Rows * m.Cols;
        weightBytes += m.W.Length + m.Scale.Length * 4L;
    }
}
Console.WriteLine($"Params: {paramCount / 1e9:F3} B, int8 weight bytes: {weightBytes / 1e9:F3} GB, build {sw.Elapsed.TotalSeconds:F1}s");
Console.WriteLine($"KV mode: {kvMode}, KV bytes {dec.Cache.Bytes / 1e9:F2} GB");
Console.WriteLine($"Process working set: {Environment.WorkingSet / 1e9:F2} GB");

void DecodeBench(string name, MatVecKernel mv, AttnKernel ak, int startPos, int steps)
{
    dec.MatVecKernel = mv;
    dec.AttentionKernel = ak;
    dec.PrimeCacheForBenchmark(startPos, rng);
    long a0 = dec.AttentionTicks, f0 = dec.ForwardTicks, m0 = dec.MatVecTicks;
    var t = Stopwatch.StartNew();
    for (int s = 0; s < steps; s++) dec.Forward(1, startPos + s);
    t.Stop();
    double secs = t.Elapsed.TotalSeconds;
    double attnMs = (dec.AttentionTicks - a0) / (double)Stopwatch.Frequency / steps * 1e3;
    double fwdMs = (dec.ForwardTicks - f0) / (double)Stopwatch.Frequency / steps * 1e3;
    double mvMs = (dec.MatVecTicks - m0) / (double)Stopwatch.Frequency / steps * 1e3;
    if (Environment.GetEnvironmentVariable("CTX_ONLY") == "1") Console.WriteLine($"      matvec {mvMs:F1} ms, attention {attnMs:F1} ms, other {fwdMs - attnMs - mvMs:F1} ms");
    Console.WriteLine($"  {name,-46} pos {startPos,5}..{startPos + steps - 1,-5} {steps / secs,7:F2} tok/s  ({secs / steps * 1e3:F1} ms/token)");
    if (ak != AttnKernel.Scalar || fwdMs > 0)
        Console.WriteLine($"      attention {attnMs:F1} ms/token ({100.0 * attnMs / fwdMs:F1}% of forward), non-attention {fwdMs - attnMs:F1} ms/token");
}

if (Environment.GetEnvironmentVariable("CTX_ONLY") == "1")
{
    AttnKernel ak = envKernel == AttnKernel.Scalar ? AttnKernel.FusedFp32 : envKernel;
    dec.MatVecKernel = MatVecKernel.Avx2Parallel;
    dec.AttentionKernel = ak;
    dec.PrimeCacheForBenchmark(64, rng);
    for (int w = 0; w < 8; w++) dec.Forward(1, 64 + w);   // JIT warm-up, untimed
    dec.Reset();
    foreach (int pos in new[] { 1023, 4095, 8191, 15999 })
        DecodeBench($"{ak} attn, parallel matvec, ctx sweep", MatVecKernel.Avx2Parallel, ak, pos, 4);
    Console.WriteLine($"GC gen0/1/2 collections: {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");
    Console.WriteLine($"Peak working set: {Environment.WorkingSet / 1e9:F2} GB");
    return;
}

// 3) Matvec throughput on the FFN and LM-head shapes.
void MatBench(string name, QMat m, MatFn run, int iters)
{
    float[] x = new float[m.Cols];
    for (int i = 0; i < x.Length; i++) x[i] = 0.01f * (i % 17);
    float[] y = new float[m.Rows];
    run(m, x, y);
    var t = Stopwatch.StartNew();
    for (int i = 0; i < iters; i++) run(m, x, y);
    t.Stop();
    double secPer = t.Elapsed.TotalSeconds / iters;
    Console.WriteLine($"  {name,-34} {secPer * 1e3,8:F3} ms/matvec  {m.W.Length / secPer / 1e9,6:F1} GB/s (int8 weights)");
}

Console.WriteLine("Matvec throughput:");
QMat ffn = weights.W1[0];
var engine = new MatVecEngine(procs);
MatBench($"FFN ({cfg.Ffn}x{cfg.Hidden}) scalar", ffn, Kernels.MatVecScalar, 5);
MatBench($"FFN ({cfg.Ffn}x{cfg.Hidden}) AVX2", ffn, Kernels.MatVecAvx2, 30);
MatBench($"FFN ({cfg.Ffn}x{cfg.Hidden}) parallel AVX2", ffn, engine.Run, 30);
MatBench($"LM ({cfg.Vocab}x{cfg.Hidden}) AVX2", weights.Embedding, Kernels.MatVecAvx2, 10);
MatBench($"LM ({cfg.Vocab}x{cfg.Hidden}) parallel AVX2", weights.Embedding, engine.Run, 20);

if (Environment.GetEnvironmentVariable("BATCH_BENCH") == "1")
{
    // Batched decode over paged int8 sequences, each primed to 512 positions. Step time per batch size.
    const int ctxLen = 512;
    float[] kk = new float[cfg.KvDim], vv = new float[cfg.KvDim];
    var br = new Random(31);
    for (int i = 0; i < kk.Length; i++) { kk[i] = (float)(br.NextDouble() * 2 - 1); vv[i] = (float)(br.NextDouble() * 2 - 1); }
    foreach (int B in new[] { 1, 4, 8 })
    {
        KvBlockPool bpool = new KvBlockPool(cfg, KvMode.Int8, 256, B * 3 + 2);
        var list = new List<PagedKvSequence>();
        for (int b = 0; b < B; b++)
        {
            PagedKvSequence sq = new PagedKvSequence(bpool);
            for (int p = 0; p < ctxLen; p++)
                for (int l = 0; l < cfg.Layers; l++) sq.Write(l, p, kk, vv);
            list.Add(sq);
        }
        BatchDecoder bd = new BatchDecoder(weights, list, procs);
        int[] toks = new int[B];
        for (int b = 0; b < B; b++) toks[b] = 1 + b;
        // Each timed step appends one position; reset lengths between runs by clearing to ctxLen afterwards is not needed for a 3-step timing.
        bd.Forward(toks);   // warm-up
        var sw2 = Stopwatch.StartNew();
        const int steps = 3;
        for (int s2 = 0; s2 < steps; s2++) bd.Forward(toks);
        sw2.Stop();
        double stepMs = sw2.Elapsed.TotalMilliseconds / steps;
        Console.WriteLine($"  batch {B,2}: {stepMs,8:F1} ms per step, {stepMs / B,7:F1} ms per token, {B * 1000.0 / stepMs,6:F2} tok/s total");
        foreach (var sq in list) sq.Clear();
    }
    return;
}
if (Environment.GetEnvironmentVariable("PREFILL_BENCH") == "1")
{
    // Sequential vs batched paged int8 prefill, 512 tokens, same weights.
    int[] toks = new int[512];
    var pr3 = new Random(41);
    for (int i = 0; i < toks.Length; i++) toks[i] = pr3.Next(cfg.Vocab);
    foreach (int chunk in new[] { 1, 16, 64, 128 })
    {
        KvBlockPool bp = new KvBlockPool(cfg, KvMode.Int8, 256, 8);
        Decoder d = new Decoder(weights, new PagedKvSequence(bp), procs);
        var sw3 = Stopwatch.StartNew();
        float[] lg = d.PrefillBatched(toks, chunk);
        sw3.Stop();
        Console.WriteLine($"  batched prefill, chunk {chunk,3}: {sw3.Elapsed.TotalSeconds,6:F2} s  ({sw3.Elapsed.TotalSeconds / toks.Length * 1e3:F1} ms/token), logit0 {lg[0]:F6}");
    }
    // Disk tier: index with a one-entry RAM budget spills to a temp directory; the second prefill restores from disk.
    string kvDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bench-kvb-" + Guid.NewGuid().ToString("N"));
    KvBlockPool dp = new KvBlockPool(cfg, KvMode.Int8, 256, 8);
    PrefixCache spill = new PrefixCache(cfg, KvMode.Int8, 256, 1, kvDir);
    PagedPrefixIndex dIndex = new PagedPrefixIndex(dp, 1, spill);
    Decoder dd1 = new Decoder(weights, new PagedKvSequence(dp), procs);
    var swd1 = Stopwatch.StartNew();
    float[] ld1 = dd1.PrefillBatched(toks, 64, dIndex);
    swd1.Stop();
    PagedKvSequence dseq2 = new PagedKvSequence(dp);
    Decoder dd2 = new Decoder(weights, dseq2, procs);
    var swd2 = Stopwatch.StartNew();
    float[] ld2 = dd2.PrefillBatched(toks, 64, dIndex);
    swd2.Stop();
    Console.WriteLine($"  disk tier: first prefill {swd1.Elapsed.TotalSeconds:F2} s; second (blocks restored from disk, {dIndex.Stats.DiskHits} disk hits) {swd2.Elapsed.TotalSeconds:F2} s, logits identical to first: {ld2.AsSpan().SequenceEqual(ld1)}");
    System.IO.Directory.Delete(kvDir, recursive: true);

    KvBlockPool sp = new KvBlockPool(cfg, KvMode.Int8, 256, 8);
    Decoder sd = new Decoder(weights, new PagedKvSequence(sp), procs);
    var sw4 = Stopwatch.StartNew();
    float[] ls = sd.Prefill(toks);
    sw4.Stop();
    Console.WriteLine($"  sequential prefill (Forward per token): {sw4.Elapsed.TotalSeconds:F2} s, logit0 {ls[0]:F6}");
    return;
}
if (Environment.GetEnvironmentVariable("PAGED_BENCH") == "1")
{
    // Paged vs contiguous int8 prefill, 512 tokens (sequential forward passes).
    int[] toks = new int[512];
    var pr2 = new Random(21);
    for (int i = 0; i < toks.Length; i++) toks[i] = pr2.Next(cfg.Vocab);
    Decoder contig = new Decoder(weights, KvMode.Int8, procs);
    var t1 = Stopwatch.StartNew();
    float[] lc = contig.Prefill(toks);
    t1.Stop();
    KvBlockPool pool = new KvBlockPool(cfg, KvMode.Int8, 256, 8);
    Decoder paged = new Decoder(weights, new PagedKvSequence(pool), procs);
    var t2 = Stopwatch.StartNew();
    float[] lp = paged.Prefill(toks, new PagedPrefixIndex(pool, 4)) ;
    t2.Stop();
    bool same = true;
    for (int i = 0; i < lc.Length; i++) if (BitConverter.SingleToInt32Bits(lc[i]) != BitConverter.SingleToInt32Bits(lp[i])) { same = false; break; }
    Console.WriteLine($"  contiguous int8 prefill 512 tokens: {t1.Elapsed.TotalSeconds:F2} s");
    Console.WriteLine($"  paged int8 prefill 512 tokens (block 256): {t2.Elapsed.TotalSeconds:F2} s, logits identical: {same}");
    return;
}
if (Environment.GetEnvironmentVariable("PREFIX_BENCH") == "1")
{
    // Prefix reuse: prefill a 1024-token prompt cold, then again with a warm RAM prefix cache, then with a 512-token extension.
    int[] prompt = new int[1024];
    var pr = new Random(9);
    for (int i = 0; i < prompt.Length; i++) prompt[i] = pr.Next(cfg.Vocab);
    var pc = new PrefixCache(cfg, KvMode.Int8, 256, 4L << 30);
    Decoder pdec = new Decoder(weights, KvMode.Int8, procs);
    void Time(string tag, int[] toks, PrefixCache? c)
    {
        var t = Stopwatch.StartNew();
        pdec.Prefill(toks, c);
        t.Stop();
        Console.WriteLine($"  {tag}: {t.Elapsed.TotalSeconds:F2} s for {toks.Length} tokens");
    }
    Time("cold prefill (no cache)", prompt, null);
    Time("prefill, cache empty, stores blocks", prompt, pc);
    Time("prefill, cache warm (4 blocks hit)", prompt, pc);
    int[] ext = new int[1536];
    Array.Copy(prompt, ext, prompt.Length);
    for (int i = 1024; i < ext.Length; i++) ext[i] = pr.Next(cfg.Vocab);
    Time("prefill 1536 tokens, 1024 cached", ext, pc);
    Console.WriteLine($"  cache stats: {pc.Stats}, hot blocks {pc.HotCount}, hot MB {pc.HotBytes / 1e6:F0}");
    return;
}
if (Environment.GetEnvironmentVariable("MATVEC_ONLY") == "1") return;
if (Environment.GetEnvironmentVariable("INTERACT") == "1")
{
    // Interaction test: matvec-only timing, before and after touching the full fp32 KV footprint.
    float[] xi = new float[cfg.Hidden]; for (int i = 0; i < xi.Length; i++) xi[i] = 0.01f * (i % 17);
    float[] yi = new float[cfg.Hidden];
    var eng = new MatVecEngine(procs);
    void Time(string tag)
    {
        for (int w = 0; w < 20; w++) eng.Run(weights.Wq[0], xi, yi);
        var tt = Stopwatch.StartNew();
        for (int w = 0; w < 200; w++) for (int l = 0; l < cfg.Layers; l++) eng.Run(weights.Wq[l], xi, yi);
        tt.Stop();
        Console.WriteLine($"  {tag}: {tt.Elapsed.TotalMilliseconds / 200 / cfg.Layers:F3} ms per Wq matvec");
    }
    Time("matvec only, before KV use");
    dec.MatVecKernel = MatVecKernel.Avx2Parallel;
    dec.AttentionKernel = AttnKernel.FusedFp32;
    dec.PrimeCacheForBenchmark(16000, rng);
    for (int w = 0; w < 4; w++) dec.Forward(1, 16000 + w);
    dec.Reset();
    Time("matvec only, after fp32 KV is populated");
    return;
}

// 4) Decode throughput.
Console.WriteLine("Decode throughput (full forward, KV cache):");
dec.Forward(1, 0);
dec.Reset();
DecodeBench("AVX2 serial matvec, scalar attention", MatVecKernel.Avx2Serial, AttnKernel.Scalar, 0, 32);
DecodeBench("AVX2 parallel matvec, scalar attention", MatVecKernel.Avx2Parallel, AttnKernel.Scalar, 0, 32);
DecodeBench("Scalar matvec, scalar attention", MatVecKernel.Scalar, AttnKernel.Scalar, 0, 4);
DecodeBench("AVX2 parallel matvec, scalar attention", MatVecKernel.Avx2Parallel, AttnKernel.Scalar, 1024, 32);
AttnKernel fused = kvMode == KvMode.Int8 ? AttnKernel.FusedInt8 : AttnKernel.FusedFp32;
DecodeBench($"AVX2 parallel matvec, {fused} attention", MatVecKernel.Avx2Parallel, fused, 1024, 32);
Console.WriteLine($"Peak working set: {Environment.WorkingSet / 1e9:F2} GB");
Console.WriteLine("== done ==");

delegate void MatFn(QMat m, ReadOnlySpan<float> x, Span<float> y);
