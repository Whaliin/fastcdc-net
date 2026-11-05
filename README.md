# FastCdc

A high-performance .NET implementation of the [FastCDC algorithm](https://www.usenix.org/conference/atc16/technical-sessions/presentation/xia) for content-defined chunking, providing bit-for-bit compatibility with Rust's [`fastcdc-rs`](https://github.com/nlfiedler/fastcdc-rs) (`fastcdc::ronomon`).

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

- **In-Memory Slicing**: `Chunk(ReadOnlySpan<byte>)` uses a `ref struct` enumerator without heap allocations.
- **Streaming**:
  - `Chunk(Stream)`: Sequential synchronous stream reading using buffer pooling.
  - `ChunkAsync(Stream)`: Asynchronous stream reading for non-blocking I/O.
  - `ChunkAsync(PipeReader)`: Direct reading from `System.IO.Pipelines` without intermediate buffer copies.
- **64-bit Offsets**: Supports streams and files of any size without 32-bit integer overflow.
- **Compact Layout**: 16-byte `Chunk` value type with zero padding bytes.
- **Arithmetic Safety**: Arithmetic operations are explicitly unchecked, preventing runtime overflow exceptions in checked compilation contexts.

---

## Usage

### 1. In-Memory Slices

Pass a byte span to `FastCdc.Chunk`:

```csharp
using FastCdc;

byte[] data = File.ReadAllBytes("file.bin");

// Defaults to FastCdcOptions.Default16KB (~16 KB average chunks)
foreach (Chunk chunk in FastCdc.Chunk(data))
{
    Console.WriteLine($"Chunk @ {chunk.Offset} (Length: {chunk.Length}, Hash: 0x{chunk.Hash:X8})");
}
```

### 2. Synchronous Streams

Read large files sequentially without loading the entire payload into memory:

```csharp
using FastCdc;

using FileStream stream = File.OpenRead("largefile.iso");

foreach (Chunk chunk in FastCdc.Chunk(stream, FastCdcOptions.Default32KB))
{
    // Process chunk
}
```

### 3. Asynchronous Streams

Read streams asynchronously using `IAsyncEnumerable<Chunk>`:

```csharp
using FastCdc;

await using FileStream stream = File.OpenRead("video.mp4");

await foreach (Chunk chunk in FastCdc.ChunkAsync(stream, FastCdcOptions.Default64KB))
{
    // Process chunk
}
```

### 4. Pipelines (`PipeReader`)

Chunk directly from a `System.IO.Pipelines.PipeReader`:

```csharp
using FastCdc;
using System.IO.Pipelines;

await foreach (Chunk chunk in FastCdc.ChunkAsync(pipeReader, FastCdcOptions.Default16KB))
{
    // Process chunk
}
```

---

## Configuration Options

Configure chunk size bounds using presets or custom values:

```csharp
// Presets
FastCdcOptions.Default16KB // min: 8 KB,  avg: 16 KB, max: 32 KB
FastCdcOptions.Default32KB // min: 16 KB, avg: 32 KB, max: 64 KB
FastCdcOptions.Default64KB // min: 32 KB, avg: 64 KB, max: 128 KB

// Custom bounds
FastCdcOptions options = new(minSize: 4096, avgSize: 8192, maxSize: 16384);
```

---

## Benchmarks

Measurements conducted using BenchmarkDotNet on an **Intel Core i5-12600K (.NET 10)**:

| Fixture | Size | Method | Throughput | Allocated |
| :--- | :--- | :--- | :--- | :--- |
| `tsunami.tiff` | **3.07 MB** | In-Memory `Span` | 4.40 GB/s (698 μs) | 0 B |
| `tsunami.tiff` | **3.07 MB** | `Stream` (Sync) | 3.71 GB/s (827 μs) | 184 B |
| `tsunami.tiff` | **3.07 MB** | `Stream` (Async) | 3.76 GB/s (815 μs) | 272 B |
| `shakespeare.txt` | **5.64 MB** | In-Memory `Span` | 4.17 GB/s (1,350 μs) | 0 B |
| `shakespeare.txt` | **5.64 MB** | `Stream` (Sync) | 3.39 GB/s (1,665 μs) | 184 B |
| `shakespeare.txt` | **5.64 MB** | `Stream` (Async) | 3.70 GB/s (1,522 μs) | 272 B |

Run the benchmarks locally:

```bash
dotnet run --project benchmarks/FastCdc.Benchmark -c Release
```

---

## Acknowledgments

- Originally forked from [AlpaGit/fastcdc-net](https://github.com/AlpaGit/fastcdc-net), based on [fastcdc-rs](https://github.com/nlfiedler/fastcdc-rs) (specifically its `ronomon` variant), ensuring cross-language chunk boundary and hash compatibility.
- Modernized for .NET 10 and co-developed with **Antigravity (Google DeepMind)**.

---

## License

This project is licensed under the [MIT License](LICENSE).
