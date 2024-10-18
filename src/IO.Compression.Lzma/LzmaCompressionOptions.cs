﻿// -----------------------------------------------------------------------
// <copyright file="LzmaCompressionOptions.cs" company="Altavec">
// Copyright (c) Altavec. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Altavec.IO.Compression;

/// <summary>
/// Provides compression options to be used with <see cref="LzmaStream"/>.
/// </summary>
public sealed class LzmaCompressionOptions
{
    private const int DefaultDictionary = 23;
    private const int DefaultFastBytes = 128;
    private const int DefaultAlgorithm = 2;
    private const int DefaultLiteralContextBits = 3;
    private const int DefaultLiteralPosBits = 0;
    private const int DefaultPosBits = 2;
    private const LzmaMatchFinder DefaultMatchFinder = LzmaMatchFinder.BT4;

    /// <summary>
    /// Gets or sets the dictionary.
    /// </summary>
    public int Dictionary { get; set; } = DefaultDictionary;

    /// <summary>
    /// Gets or sets the number of fast bytes.
    /// </summary>
    public int FastBytes { get; set; } = DefaultFastBytes;

    /// <summary>
    /// Gets or sets the number of literal context bits.
    /// </summary>
    public int LiteralContextBits { get; set; } = DefaultLiteralContextBits;

    /// <summary>
    /// Gets or sets the number of literal pos bits.
    /// </summary>
    public int LiteralPosBits { get; set; } = DefaultLiteralPosBits;

    /// <summary>
    /// Gets or sets the number of pos bits.
    /// </summary>
    public int PosBits { get; set; } = DefaultPosBits;

    /// <summary>
    /// Gets or sets the match finder.
    /// </summary>
    public LzmaMatchFinder MatchFinder { get; set; } = DefaultMatchFinder;

    /// <summary>
    /// Gets or sets a value indicating whether to write the end of stream marker.
    /// </summary>
    public bool EndMarker { get; set; } = false;

    /// <summary>
    /// Creates the encoder.
    /// </summary>
    /// <returns>The created encoder.</returns>
    internal LzmaEncoder CreateEncoder()
    {
        CoderPropId[] propIDs =
            [
                CoderPropId.DictionarySize,
                CoderPropId.PosStateBits,
                CoderPropId.LitContextBits,
                CoderPropId.LitPosBits,
                CoderPropId.Algorithm,
                CoderPropId.NumFastBytes,
                CoderPropId.MatchFinder,
                CoderPropId.EndMarker,
            ];

        object[] properties =
        [
                1 << this.Dictionary,
                this.PosBits,
                this.LiteralContextBits,
                this.LiteralPosBits,
                DefaultAlgorithm,
                this.FastBytes,
                this.MatchFinder.ToString(),
                this.EndMarker,
            ];

        var encoder = new LzmaEncoder();
        encoder.SetCoderProperties(propIDs, properties);

        return encoder;
    }
}