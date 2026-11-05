# FastCdc

A .NET implementation of the [FastCDC algorithm](https://www.usenix.org/conference/atc16/technical-sessions/presentation/xia) ([2020 paper](https://ieeexplore.ieee.org/document/9055082)) for content-defined chunking, providing bit-for-bit compatibility with Rust's [`fastcdc-rs`](https://github.com/nlfiedler/fastcdc-rs) across `v2020`, `v2016`, and `ronomon` formats.

[![CI](https://github.com/Whaliin/fastcdc-net/actions/workflows/ci.yml/badge.svg)](https://github.com/Whaliin/fastcdc-net/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/FastCdc.svg)](https://www.nuget.org/packages/FastCdc)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## Installation

```bash
dotnet add package FastCdc
```

---

## Features

- **Multiple FastCDC Formats**:
  - `FastCdcFormat.V2020` (default): 64-bit Gear hash rolling two bytes per step (`fastcdc-rs::v2020`).
  - `FastCdcFormat.V2016`: 64-bit Gear hash rolling one byte per step (`fastcdc-rs::v2016`, Go/C Destor).
  - `FastCdcFormat.Ronomon`: Legacy 31-bit right-shift variant (`fastcdc-rs::ronomon`, FastCdc v1.x/v2.x).
- **Configurable Normalization**: Supports `NormalizationLevel.Level0` through `NormalizationLevel.Level3`.
- **Metadata & Single-Pass Payload Extraction**:
  - `Chunk` / `ChunkAsync`: Yields 24-byte `Chunk(long Offset, ulong Hash, int Length)` metadata records.
  - `ChunkData` / `ChunkDataAsync`: Yields `ChunkData` records containing both chunk metadata and the underlying `ReadOnlyMemory<byte>` payload in a single pass.
- **In-Memory & Streaming Overloads**:
  - `ReadOnlySpan<byte>` and `ReadOnlyMemory<byte>` enumerators with zero heap allocations.
  - Synchronous and asynchronous `Stream` readers backed by `ArrayPool<byte>`.
  - `PipeReader` support for `System.IO.Pipelines` without intermediate copies on contiguous segments.

---

## Usage

### 1. In-Memory Slicing

Pass a `ReadOnlySpan<byte>` to `FastCdc.Chunk`, or a `ReadOnlyMemory<byte>` to `FastCdc.ChunkData`:

```csharp
using FastCdc;

byte[] data = File.ReadAllBytes("file.bin");

// Defaults to FastCdcOptions.Default16KB (V2020, Level1, ~16 KB average chunks)
foreach (Chunk chunk in FastCdc.Chunk(data))
{
    Console.WriteLine($"Chunk @ {chunk.Offset} (Length: {chunk.Length}, Hash: 0x{chunk.Hash:X16})");
}
```

### 2. Single-Pass Payload Extraction (`ChunkData` / `ChunkDataAsync`)

Use `ChunkData` or `ChunkDataAsync` to inspect or hash each chunk's byte payload in a single pass without seeking or re-reading the source stream:

```csharp
using FastCdc;
using System.Security.Cryptography;

await using FileStream stream = File.OpenRead("archive.tar");

await foreach (ChunkData chunk in FastCdc.ChunkDataAsync(stream, FastCdcOptions.Default32KB))
{
    // chunk.Memory / chunk.Span is backed by a pooled buffer valid for the current iteration.
    // Call chunk.ToArray() if you need to retain the bytes after advancing the enumerator.
    byte[] sha256 = SHA256.HashData(chunk.Span);
}
```

### 3. Synchronous & Asynchronous Streams

Read large files sequentially without loading the entire payload into memory:

```csharp
using FastCdc;

await using FileStream stream = File.OpenRead("video.mp4");

await foreach (Chunk chunk in FastCdc.ChunkAsync(stream, FastCdcOptions.Default64KB))
{
    // Process chunk metadata
}
```

### 4. Pipelines (`PipeReader`)

Chunk directly from a `System.IO.Pipelines.PipeReader`:

```csharp
using FastCdc;
using System.IO.Pipelines;

await foreach (Chunk chunk in FastCdc.ChunkAsync(pipeReader, FastCdcOptions.Default16KB))
{
    // Process chunk metadata
}
```

---

## Configuration Options

Configure chunk size bounds, algorithm format, and normalization level via `FastCdcOptions`:

```csharp
// Presets (V2020, NormalizationLevel.Level1)
FastCdcOptions.Default16KB // min: 8 KB,  avg: 16 KB, max: 32 KB
FastCdcOptions.Default32KB // min: 16 KB, avg: 32 KB, max: 64 KB
FastCdcOptions.Default64KB // min: 32 KB, avg: 64 KB, max: 128 KB

// Custom bounds, format, and normalization level
FastCdcOptions options = new(
    minSize: 4096,
    avgSize: 16384,
    maxSize: 65534,
    format: FastCdcFormat.V2020,
    normalization: NormalizationLevel.Level2);

// Legacy v1/v2 Ronomon compatibility
FastCdcOptions ronomonOptions = new(
    minSize: 8192,
    avgSize: 16384,
    maxSize: 32768,
    format: FastCdcFormat.Ronomon);
```

---

## Benchmarks

Measurements conducted on an **Intel Core i5-12600K 3.70GHz** comparing **.NET 10 (`FastCdc` v3.0.0)** via BenchmarkDotNet against **Rust ([`fastcdc-rs`](https://github.com/nlfiedler/fastcdc-rs) v3.1)** via Criterion with identical chunking parameters (`target-cpu=x86-64-v3`).

### 1. In-Memory Throughput (.NET 10 vs. Rust `fastcdc-rs`)

#### `shakespeare.txt` (5.44 MB text)

| Implementation | Variant / Average Chunk Size | Time | Throughput | vs. Rust |
| :--- | :--- | ---: | ---: | :---: |
| **.NET 10 (`FastCdc`)** | **`V2020` ~16 KB** | **1,058.0 μs** | **5.14 GB/s** | **+32.5% faster** |
| Rust (`fastcdc-rs`) | `V2020` ~16 KB | 1,402.1 μs | 3.88 GB/s | baseline |
| **.NET 10 (`FastCdc`)** | **`V2020` ~32 KB** | **987.3 μs** | **5.51 GB/s** | **+31.5% faster** |
| Rust (`fastcdc-rs`) | `V2020` ~32 KB | 1,298.5 μs | 4.19 GB/s | baseline |
| **.NET 10 (`FastCdc`)** | **`V2020` ~64 KB** | **1,090.1 μs** | **4.99 GB/s** | **+27.6% faster** |
| Rust (`fastcdc-rs`) | `V2020` ~64 KB | 1,391.5 μs | 3.91 GB/s | baseline |
| **.NET 10 (`FastCdc`)** | `V2016` ~16 KB | 1,412.0 μs | 3.85 GB/s | +7.1% faster |
| Rust (`fastcdc-rs`) | `V2016` ~16 KB | 1,512.1 μs | 3.60 GB/s | baseline |
| **.NET 10 (`FastCdc`)** | `Ronomon` ~16 KB | 1,489.1 μs | 3.65 GB/s | parity |
| Rust (`fastcdc-rs`) | `Ronomon` ~16 KB | 1,475.9 μs | 3.69 GB/s | baseline |

#### `tsunami.tiff` (3.07 MB binary)

| Implementation | Variant / Average Chunk Size | Time | Throughput | vs. Rust |
| :--- | :--- | ---: | ---: | :---: |
| **.NET 10 (`FastCdc`)** | **`V2020` ~16 KB** | **601.9 μs** | **5.10 GB/s** | **+28.8% faster** |
| Rust (`fastcdc-rs`) | `V2020` ~16 KB | 775.4 μs | 3.96 GB/s | baseline |
| **.NET 10 (`FastCdc`)** | **`V2020` ~32 KB** | **580.2 μs** | **5.29 GB/s** | **+32.8% faster** |
| Rust (`fastcdc-rs`) | `V2020` ~32 KB | 770.3 μs | 3.99 GB/s | baseline |
| **.NET 10 (`FastCdc`)** | **`V2020` ~64 KB** | **599.0 μs** | **5.13 GB/s** | **+28.6% faster** |
| Rust (`fastcdc-rs`) | `V2020` ~64 KB | 770.3 μs | 3.99 GB/s | baseline |
| **.NET 10 (`FastCdc`)** | `V2016` ~16 KB | 820.7 μs | 3.74 GB/s | +5.0% faster |
| Rust (`fastcdc-rs`) | `V2016` ~16 KB | 861.9 μs | 3.56 GB/s | baseline |
| **.NET 10 (`FastCdc`)** | `Ronomon` ~16 KB | 799.2 μs | 3.84 GB/s | parity |
| Rust (`fastcdc-rs`) | `Ronomon` ~16 KB | 777.6 μs | 3.95 GB/s | baseline |

---

### 2. .NET 10 Streaming & Allocation Profile (`FastCdc` v3.0.0)

| Fixture | Size | Target Avg | Method | Time | Throughput | Allocated |
| :--- | ---: | ---: | :--- | ---: | ---: | ---: |
| `tsunami.tiff` | 3.07 MB | 16 KB | In-Memory `Span` | 601.9 μs | 5.10 GB/s | 0 B |
| `tsunami.tiff` | 3.07 MB | 16 KB | `Stream` (Sync) | 695.6 μs | 4.41 GB/s | 232 B |
| `tsunami.tiff` | 3.07 MB | 16 KB | `Stream` (Async) | 668.7 μs | 4.59 GB/s | 320 B |
| `tsunami.tiff` | 3.07 MB | 32 KB | `Stream` (Async) | 664.4 μs | 4.62 GB/s | 320 B |
| `tsunami.tiff` | 3.07 MB | 64 KB | `Stream` (Async) | 664.4 μs | 4.62 GB/s | 320 B |
| `shakespeare.txt` | 5.44 MB | 16 KB | In-Memory `Span` | 1,058.0 μs | 5.14 GB/s | 0 B |
| `shakespeare.txt` | 5.44 MB | 16 KB | `Stream` (Sync) | 1,207.6 μs | 4.51 GB/s | 232 B |
| `shakespeare.txt` | 5.44 MB | 16 KB | `Stream` (Async) | 1,228.6 μs | 4.43 GB/s | 320 B |
| `shakespeare.txt` | 5.44 MB | 32 KB | `Stream` (Async) | 1,177.8 μs | 4.62 GB/s | 320 B |
| `shakespeare.txt` | 5.44 MB | 64 KB | `Stream` (Async) | 1,238.4 μs | 4.39 GB/s | 320 B |

Run the benchmarks locally:

```bash
dotnet run --project benchmarks/FastCdc.Benchmark -c Release
```

---

## Acknowledgments

- Originally forked from [AlpaGit/fastcdc-net](https://github.com/AlpaGit/fastcdc-net), based on [fastcdc-rs](https://github.com/nlfiedler/fastcdc-rs), ensuring cross-language chunk boundary and hash compatibility across `v2020`, `v2016`, and `ronomon`.
- Modernized for .NET 10 and co-developed with **Antigravity (Google DeepMind)**.

---

## License

This project is licensed under the [MIT License](LICENSE).
