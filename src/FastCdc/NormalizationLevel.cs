namespace FastCdc;

/// <summary>
/// Specifies the chunk size normalization level for FastCDC.
/// Higher normalization levels cluster chunk sizes more tightly around the target average size.
/// </summary>
public enum NormalizationLevel
{
	/// <summary>
	/// No chunk size normalization; MaskS and MaskL are identical to the average size mask.
	/// </summary>
	Level0 = 0,

	/// <summary>
	/// Normalization level 1 (default); shifts MaskS and MaskL by 1 bit relative to the average size mask.
	/// </summary>
	Level1 = 1,

	/// <summary>
	/// Normalization level 2; shifts MaskS and MaskL by 2 bits relative to the average size mask.
	/// </summary>
	Level2 = 2,

	/// <summary>
	/// Normalization level 3; shifts MaskS and MaskL by 3 bits relative to the average size mask.
	/// </summary>
	Level3 = 3,
}

