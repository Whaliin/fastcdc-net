// ============================================================================
// FastCdc.Engine.cs
//
// Internal rolling hash and boundary-detection algorithms for FastCDC.
// Contains the core Cut implementations for V2020 (unrolled 6-byte blocks with
// Gear & GearLs tables), V2016, and Ronomon formats.
//
// For the public Chunk/ChunkData API and streaming pumps, see FastCdc.cs.
// ============================================================================

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FastCdc;

public static partial class FastCdc
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static (ulong hash, int size) Cut(
		ReadOnlySpan<byte> source,
		in FastCdcOptions options,
		int sourceSize,
		bool eof)
	{
		return options.Format switch
		{
			FastCdcFormat.V2020 => CutV2020(source, in options, sourceSize, eof),
			FastCdcFormat.V2016 => CutV2016(source, in options, sourceSize, eof),
			FastCdcFormat.Ronomon => CutRonomon(source, in options, sourceSize, eof),
			_ => ThrowInvalidFormat(options.Format)
		};
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static (ulong hash, int size) ThrowInvalidFormat(FastCdcFormat format)
		=> throw new ArgumentOutOfRangeException(nameof(format), format, "Invalid FastCDC format.");

	private static (ulong hash, int size) CutV2020(
		ReadOnlySpan<byte> source,
		in FastCdcOptions options,
		int sourceSize,
		bool eof)
	{
		int minSize = options.MinSize;
		int maxSize = options.MaxSize;

		if (sourceSize > source.Length)
			sourceSize = source.Length;

		if (sourceSize <= minSize)
			return eof ? (0UL, sourceSize) : (0UL, 0);

		int center = options.CenterOffset;
		if (sourceSize > maxSize)
			sourceSize = maxSize;
		else if (sourceSize < center)
			center = sourceSize;

		int alignedCenter = center & ~1;
		int end = sourceSize & ~1;
		int scanStart = minSize & ~1;

		ref ulong gearRef = ref MemoryMarshal.GetReference(Table.Gear);
		ref ulong gearLsRef = ref MemoryMarshal.GetReference(Table.GearLs);
		ref byte sourceRef = ref MemoryMarshal.GetReference(source);

		ulong hash = 0UL;

		if (scanStart < alignedCenter)
		{
			int cutOffset = ScanRegionV2020(
				ref sourceRef,
				scanStart,
				alignedCenter,
				ref hash,
				options.MaskSLs,
				options.MaskS,
				ref gearRef,
				ref gearLsRef);

			if (cutOffset >= 0)
				return (hash, cutOffset);

			scanStart = alignedCenter;
		}

		if (scanStart < end)
		{
			int cutOffset = ScanRegionV2020(
				ref sourceRef,
				scanStart,
				end,
				ref hash,
				options.MaskLLs,
				options.MaskL,
				ref gearRef,
				ref gearLsRef);

			if (cutOffset >= 0)
				return (hash, cutOffset);
		}

		if ((sourceSize & 1) != 0)
		{
			byte lastByte = Unsafe.Add(ref sourceRef, (nint)(uint)(sourceSize - 1));
			hash = unchecked((hash << 1) + Unsafe.Add(ref gearRef, (nint)lastByte));
		}

		return (!eof && sourceSize < maxSize) ? (hash, 0) : (hash, sourceSize);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int ScanRegionV2020(
		ref byte sourceRef,
		int start,
		int end,
		ref ulong hash,
		ulong maskLs,
		ulong mask,
		ref ulong gearRef,
		ref ulong gearLsRef)
	{
		int offset = start;
		ulong currentHash = hash;

		unchecked
		{
			// Unrolled 6-byte block: requires 8 readable bytes in-bounds before 'end'
			// so that Unsafe.ReadUnaligned<ulong> never reads past the buffer end.
			while (offset + 8 <= end)
			{
				ulong block = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref sourceRef, (nint)(uint)offset));
				if (!BitConverter.IsLittleEndian)
					block = BinaryPrimitives.ReverseEndianness(block);

				ulong g0 = Unsafe.Add(ref gearLsRef, (nint)(byte)block);
				ulong g1 = Unsafe.Add(ref gearRef, (nint)(byte)(block >> 8));
				ulong g2 = Unsafe.Add(ref gearLsRef, (nint)(byte)(block >> 16));
				ulong g3 = Unsafe.Add(ref gearRef, (nint)(byte)(block >> 24));
				ulong g4 = Unsafe.Add(ref gearLsRef, (nint)(byte)(block >> 32));
				ulong g5 = Unsafe.Add(ref gearRef, (nint)(byte)(block >> 40));

				currentHash = (currentHash << 2) + g0;
				if ((currentHash & maskLs) == 0)
				{
					hash = currentHash;
					return offset;
				}
				currentHash += g1;
				if ((currentHash & mask) == 0)
				{
					hash = currentHash;
					return offset + 1;
				}

				currentHash = (currentHash << 2) + g2;
				if ((currentHash & maskLs) == 0)
				{
					hash = currentHash;
					return offset + 2;
				}
				currentHash += g3;
				if ((currentHash & mask) == 0)
				{
					hash = currentHash;
					return offset + 3;
				}

				currentHash = (currentHash << 2) + g4;
				if ((currentHash & maskLs) == 0)
				{
					hash = currentHash;
					return offset + 4;
				}
				currentHash += g5;
				if ((currentHash & mask) == 0)
				{
					hash = currentHash;
					return offset + 5;
				}

				offset += 6;
			}

			// 2-byte pair remainder loop for the remaining 2 to 6 bytes
			while (offset + 2 <= end)
			{
				byte b0 = Unsafe.Add(ref sourceRef, (nint)(uint)offset);
				byte b1 = Unsafe.Add(ref sourceRef, (nint)(uint)(offset + 1));

				ulong first = Unsafe.Add(ref gearLsRef, (nint)b0);
				ulong second = Unsafe.Add(ref gearRef, (nint)b1);

				currentHash = (currentHash << 2) + first;
				if ((currentHash & maskLs) == 0)
				{
					hash = currentHash;
					return offset;
				}
				currentHash += second;
				if ((currentHash & mask) == 0)
				{
					hash = currentHash;
					return offset + 1;
				}

				offset += 2;
			}
		}

		hash = currentHash;
		return -1;
	}

	private static (ulong hash, int size) CutV2016(
		ReadOnlySpan<byte> source,
		in FastCdcOptions options,
		int sourceSize,
		bool eof)
	{
		int minSize = options.MinSize;
		int maxSize = options.MaxSize;

		if (sourceSize > source.Length)
			sourceSize = source.Length;

		if (sourceSize <= minSize)
			return eof ? (0UL, sourceSize) : (0UL, 0);

		if (sourceSize > maxSize)
			sourceSize = maxSize;

		int center = options.CenterOffset;
		if (center > sourceSize)
			center = sourceSize;

		ref ulong gearRef = ref MemoryMarshal.GetReference(Table.Gear);
		ref byte sourceRef = ref MemoryMarshal.GetReference(source);

		ulong maskS = options.MaskS;
		ulong maskL = options.MaskL;

		ulong hash = 0UL;
		int i = minSize;

		unchecked
		{
			while (i < center)
			{
				byte b = Unsafe.Add(ref sourceRef, (nint)(uint)i);
				hash = (hash << 1) + Unsafe.Add(ref gearRef, (nint)b);
				if ((hash & maskS) == 0)
					return (hash, i);
				i++;
			}

			while (i < sourceSize)
			{
				byte b = Unsafe.Add(ref sourceRef, (nint)(uint)i);
				hash = (hash << 1) + Unsafe.Add(ref gearRef, (nint)b);
				if ((hash & maskL) == 0)
					return (hash, i);
				i++;
			}
		}

		return (!eof && sourceSize < maxSize) ? (hash, 0) : (hash, sourceSize);
	}

	private static (ulong hash, int size) CutRonomon(
		ReadOnlySpan<byte> source,
		in FastCdcOptions options,
		int sourceSize,
		bool eof)
	{
		int minSize = options.MinSize;
		int maxSize = options.MaxSize;

		if (sourceSize > source.Length)
			sourceSize = source.Length;

		if (sourceSize <= minSize)
			return eof ? (0UL, sourceSize) : (0UL, 0);

		if (sourceSize > maxSize)
			sourceSize = maxSize;

		int centerOffset = options.CenterOffset;
		int len1 = centerOffset > sourceSize ? sourceSize : centerOffset;
		int len2 = sourceSize;

		ref uint hashesRef = ref MemoryMarshal.GetReference(Table.Hashes);
		ref byte sourceRef = ref MemoryMarshal.GetReference(source);

		uint maskS = (uint)options.MaskS;
		uint maskL = (uint)options.MaskL;

		uint hash = 0u;
		int i = minSize;

		unchecked
		{
			while (i < len1)
			{
				byte b = Unsafe.Add(ref sourceRef, (nint)(uint)i);
				hash = (hash >> 1) + Unsafe.Add(ref hashesRef, (nint)b);
				i++;
				if ((hash & maskS) == 0)
					return (hash, i);
			}

			while (i < len2)
			{
				byte b = Unsafe.Add(ref sourceRef, (nint)(uint)i);
				hash = (hash >> 1) + Unsafe.Add(ref hashesRef, (nint)b);
				i++;
				if ((hash & maskL) == 0)
					return (hash, i);
			}
		}

		return (!eof && sourceSize < maxSize) ? (hash, 0) : (hash, sourceSize);
	}
}

