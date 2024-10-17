﻿// -----------------------------------------------------------------------
// <copyright file="BitEncoder.cs" company="Altavec">
// Copyright (c) Altavec. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Altavec.IO.Compression.RangeCoder;

/// <summary>
/// The bit encoder.
/// </summary>
internal struct BitEncoder
{
    /// <summary>
    /// The number of bit price shift bits.
    /// </summary>
    public const int NumBitPriceShiftBits = 6;

    private const int NumBitModelTotalBits = 11;
    private const uint BitModelTotal = 1 << NumBitModelTotalBits;

    private const int NumMoveBits = 5;
    private const int NumMoveReducingBits = 2;

    private static readonly uint[] ProbPrices = GetPropPrices();

    private uint prob;

    /// <summary>
    /// Initializes this instance.
    /// </summary>
    public void Init() => this.prob = BitModelTotal >> 1;

    /// <summary>
    /// Updates the model.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    public void UpdateModel(uint symbol)
    {
        if (symbol is 0U)
        {
            this.prob += (BitModelTotal - this.prob) >> NumMoveBits;
        }
        else
        {
            this.prob -= this.prob >> NumMoveBits;
        }
    }

    /// <summary>
    /// Encodes the symbol.
    /// </summary>
    /// <param name="encoder">The encoder.</param>
    /// <param name="symbol">The symbol.</param>
    public void Encode(Encoder encoder, uint symbol)
    {
        var newBound = (encoder.Range >> NumBitModelTotalBits) * this.prob;
        if (symbol is 0)
        {
            encoder.Range = newBound;
            this.prob += (BitModelTotal - this.prob) >> NumMoveBits;
        }
        else
        {
            encoder.Low += newBound;
            encoder.Range -= newBound;
            this.prob -= this.prob >> NumMoveBits;
        }

        if (encoder.Range < Encoder.TopValue)
        {
            encoder.Range <<= 8;
            encoder.ShiftLow();
        }
    }

    /// <summary>
    /// Gets the price.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The price.</returns>
    public readonly uint GetPrice(uint symbol) => ProbPrices[(((this.prob - symbol) ^ (-(int)symbol)) & (BitModelTotal - 1)) >> NumMoveReducingBits];

    /// <summary>
    /// GEts the zero price.
    /// </summary>
    /// <returns>The zero price.</returns>
    public readonly uint GetPrice0() => ProbPrices[this.prob >> NumMoveReducingBits];

    /// <summary>
    /// Gets the one price.
    /// </summary>
    /// <returns>The one price.</returns>
    public readonly uint GetPrice1() => ProbPrices[(BitModelTotal - this.prob) >> NumMoveReducingBits];

    private static uint[] GetPropPrices()
    {
        const int NumBits = NumBitModelTotalBits - NumMoveReducingBits;
        var propPrices = new uint[BitModelTotal >> NumMoveReducingBits];
        for (var i = NumBits - 1; i >= 0; i--)
        {
            var start = 1U << (NumBits - i - 1);
            var end = 1U << (NumBits - i);
            for (var j = start; j < end; j++)
            {
                propPrices[j] = ((uint)i << NumBitPriceShiftBits) + (((end - j) << NumBitPriceShiftBits) >> (NumBits - i - 1));
            }
        }

        return propPrices;
    }
}