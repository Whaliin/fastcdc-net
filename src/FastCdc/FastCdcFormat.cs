namespace FastCdc;

/// <summary>
/// Specifies the FastCDC algorithm variant used for chunk boundary detection.
/// </summary>
public enum FastCdcFormat
{
	/// <summary>
	/// The 2020 FastCDC algorithm using a 64-bit Gear hash rolling two bytes per step.
	/// Produces identical chunk boundaries to <see cref="V2016"/> for even size parameters
	/// with higher throughput. Matches <c>fastcdc-rs::v2020</c>.
	/// </summary>
	V2020 = 0,

	/// <summary>
	/// The 2016 FastCDC algorithm using a 64-bit Gear hash rolling one byte per step.
	/// Matches <c>fastcdc-rs::v2016</c> and standard Go/C implementations.
	/// </summary>
	V2016 = 1,

	/// <summary>
	/// The legacy 31-bit right-shift FastCDC variant.
	/// Matches <c>fastcdc-rs::ronomon</c> and FastCdc v1.x/v2.x when used with <see cref="NormalizationLevel.Level1"/>.
	/// </summary>
	/// <remarks>
	/// Upstream <c>fastcdc-rs::ronomon</c> hardcodes 1-bit normalization (<see cref="NormalizationLevel.Level1"/>).
	/// Other <see cref="NormalizationLevel"/> values are supported by shifting the 31-bit masks by the specified level,
	/// but will produce boundaries that differ from upstream <c>ronomon</c>.
	/// </remarks>
	Ronomon = 2,
}

