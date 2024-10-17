﻿// -----------------------------------------------------------------------
// <copyright file="LzmaDecoder.cs" company="Altavec">
// Copyright (c) Altavec. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Altavec.IO.Compression;

/// <summary>
/// The LZMA decoder.
/// </summary>
internal class LzmaDecoder : ICoder, ISetDecoderProperties
{
    private readonly LZ.OutWindow outWindow = new();
    private readonly RangeCoder.Decoder rangeDecoder = new();

    private readonly RangeCoder.BitDecoder[] isMatchDecoders = new RangeCoder.BitDecoder[LzmaBase.NumStates << LzmaBase.NumPosStatesBitsMax];
    private readonly RangeCoder.BitDecoder[] isRepDecoders = new RangeCoder.BitDecoder[LzmaBase.NumStates];
    private readonly RangeCoder.BitDecoder[] isRepG0Decoders = new RangeCoder.BitDecoder[LzmaBase.NumStates];
    private readonly RangeCoder.BitDecoder[] isRepG1Decoders = new RangeCoder.BitDecoder[LzmaBase.NumStates];
    private readonly RangeCoder.BitDecoder[] isRepG2Decoders = new RangeCoder.BitDecoder[LzmaBase.NumStates];
    private readonly RangeCoder.BitDecoder[] isRep0LongDecoders = new RangeCoder.BitDecoder[LzmaBase.NumStates << LzmaBase.NumPosStatesBitsMax];

    private readonly RangeCoder.BitTreeDecoder[] posSlotDecoder = new RangeCoder.BitTreeDecoder[LzmaBase.NumLenToPosStates];
    private readonly RangeCoder.BitDecoder[] posDecoders = new RangeCoder.BitDecoder[LzmaBase.NumFullDistances - LzmaBase.EndPosModelIndex];

    private readonly LenDecoder lenDecoder = new();
    private readonly LenDecoder repLenDecoder = new();

    private readonly LiteralDecoder literalDecoder = new();

    private readonly RangeCoder.BitTreeDecoder posAlignDecoder = new(LzmaBase.NumAlignBits);

    private uint dictionarySize;
    private uint dictionarySizeCheck;

    private uint posStateMask;

    private bool solid;

    /// <summary>
    /// Initializes a new instance of the <see cref="LzmaDecoder"/> class.
    /// </summary>
    public LzmaDecoder()
    {
        this.dictionarySize = uint.MaxValue;
        for (var i = 0; i < LzmaBase.NumLenToPosStates; i++)
        {
            this.posSlotDecoder[i] = new(LzmaBase.NumPosSlotBits);
        }
    }

    /// <inheritdoc/>
    public void Code(Stream inStream, Stream outStream, long inSize = -1, long outSize = -1, Action<long, long>? progress = null)
    {
        this.Init(inStream, outStream);

        LzmaBase.State state = default;
        state.Init();
        var rep0 = 0U;
        var rep1 = 0U;
        var rep2 = 0U;
        var rep3 = 0U;

        var nowPos64 = 0UL;
        var outSize64 = (ulong)outSize;
        if (nowPos64 < outSize64)
        {
            if (this.isMatchDecoders[state.Index << LzmaBase.NumPosStatesBitsMax].Decode(this.rangeDecoder) is not 0U)
            {
                throw new System.Data.DataException();
            }

            state.UpdateChar();
            var b = this.literalDecoder.DecodeNormal(this.rangeDecoder, 0, 0);
            this.outWindow.PutByte(b);
            nowPos64++;
        }

        while (nowPos64 < outSize64)
        {
            var posState = (uint)nowPos64 & this.posStateMask;
            if (this.isMatchDecoders[(state.Index << LzmaBase.NumPosStatesBitsMax) + posState].Decode(this.rangeDecoder) is 0)
            {
                var prevByte = this.outWindow.GetByte(0);
                var b = state.IsCharState()
                    ? this.literalDecoder.DecodeNormal(this.rangeDecoder, (uint)nowPos64, prevByte)
                    : this.literalDecoder.DecodeWithMatchByte(this.rangeDecoder, (uint)nowPos64, prevByte, this.outWindow.GetByte(rep0));
                this.outWindow.PutByte(b);
                state.UpdateChar();
                nowPos64++;
            }
            else
            {
                uint len;
                if (this.isRepDecoders[state.Index].Decode(this.rangeDecoder) is 1U)
                {
                    if (this.isRepG0Decoders[state.Index].Decode(this.rangeDecoder) is 0U)
                    {
                        if (this.isRep0LongDecoders[(state.Index << LzmaBase.NumPosStatesBitsMax) + posState].Decode(this.rangeDecoder) is 0U)
                        {
                            state.UpdateShortRep();
                            this.outWindow.PutByte(this.outWindow.GetByte(rep0));
                            nowPos64++;
                            continue;
                        }
                    }
                    else
                    {
                        uint distance;
                        if (this.isRepG1Decoders[state.Index].Decode(this.rangeDecoder) is 0U)
                        {
                            distance = rep1;
                        }
                        else
                        {
                            if (this.isRepG2Decoders[state.Index].Decode(this.rangeDecoder) is 0U)
                            {
                                distance = rep2;
                            }
                            else
                            {
                                distance = rep3;
                                rep3 = rep2;
                            }

                            rep2 = rep1;
                        }

                        rep1 = rep0;
                        rep0 = distance;
                    }

                    len = this.repLenDecoder.Decode(this.rangeDecoder, posState) + LzmaBase.MatchMinLen;
                    state.UpdateRep();
                }
                else
                {
                    rep3 = rep2;
                    rep2 = rep1;
                    rep1 = rep0;
                    len = LzmaBase.MatchMinLen + this.lenDecoder.Decode(this.rangeDecoder, posState);
                    state.UpdateMatch();
                    var posSlot = this.posSlotDecoder[LzmaBase.GetLenToPosState(len)].Decode(this.rangeDecoder);
                    if (posSlot >= LzmaBase.StartPosModelIndex)
                    {
                        var numDirectBits = (int)((posSlot >> 1) - 1);
                        rep0 = (2 | (posSlot & 1)) << numDirectBits;
                        if (posSlot < LzmaBase.EndPosModelIndex)
                        {
                            rep0 += RangeCoder.BitTreeDecoder.ReverseDecode(this.posDecoders, rep0 - posSlot - 1, this.rangeDecoder, numDirectBits);
                        }
                        else
                        {
                            rep0 += this.rangeDecoder.DecodeDirectBits(numDirectBits - LzmaBase.NumAlignBits) << LzmaBase.NumAlignBits;
                            rep0 += this.posAlignDecoder.ReverseDecode(this.rangeDecoder);
                        }
                    }
                    else
                    {
                        rep0 = posSlot;
                    }
                }

                if (rep0 >= this.outWindow.TrainSize + nowPos64 || rep0 >= this.dictionarySizeCheck)
                {
                    if (rep0 is uint.MaxValue)
                    {
                        break;
                    }

                    throw new InvalidDataException();
                }

                this.outWindow.CopyBlock(rep0, len);
                nowPos64 += len;
            }
        }

        this.outWindow.Flush();
        this.outWindow.ReleaseStream();
        this.rangeDecoder.ReleaseStream();
    }

    /// <inheritdoc/>
    public void SetDecoderProperties(byte[] properties)
    {
        if (properties.Length < 5)
        {
            throw new ArgumentOutOfRangeException(nameof(properties));
        }

        var lc = properties[0] % 9;
        var remainder = properties[0] / 9;
        var lp = remainder % 5;
        var pb = remainder / 5;
        if (pb > LzmaBase.NumPosStatesBitsMax)
        {
            throw new InvalidDataException();
        }

        var currentDictionarySize = 0U;
        for (var i = 0; i < 4; i++)
        {
            currentDictionarySize += ((uint)properties[1 + i]) << (i * 8);
        }

        this.SetDictionarySize(currentDictionarySize);
        this.SetLiteralProperties(lp, lc);
        this.SetPosBitsProperties(pb);
    }

    /// <summary>
    /// Trains this instance with the stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns><see langword="true"/> if the training was successful; otherwise <see langword="false"/>.</returns>
    public bool Train(Stream stream)
    {
        this.solid = true;
        return this.outWindow.Train(stream);
    }

    private void SetDictionarySize(uint dictionarySize)
    {
        if (this.dictionarySize != dictionarySize)
        {
            this.dictionarySize = dictionarySize;
            this.dictionarySizeCheck = Math.Max(this.dictionarySize, 1);
            var blockSize = Math.Max(this.dictionarySizeCheck, 1 << 12);
            this.outWindow.Create(blockSize);
        }
    }

    private void SetLiteralProperties(int lp, int lc)
    {
        if (lp > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(lp));
        }

        if (lc > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(lc));
        }

        this.literalDecoder.Create(lp, lc);
    }

    private void SetPosBitsProperties(int pb)
    {
        if (pb > LzmaBase.NumPosStatesBitsMax)
        {
            throw new InvalidDataException();
        }

        var numPosStates = 1U << pb;
        this.lenDecoder.Create(numPosStates);
        this.repLenDecoder.Create(numPosStates);
        this.posStateMask = numPosStates - 1;
    }

    private void Init(Stream inStream, Stream outStream)
    {
        this.rangeDecoder.Init(inStream);
        this.outWindow.Init(outStream, this.solid);

        for (var i = 0U; i < LzmaBase.NumStates; i++)
        {
            for (var j = 0U; j <= this.posStateMask; j++)
            {
                var index = (i << LzmaBase.NumPosStatesBitsMax) + j;
                this.isMatchDecoders[index].Init();
                this.isRep0LongDecoders[index].Init();
            }

            this.isRepDecoders[i].Init();
            this.isRepG0Decoders[i].Init();
            this.isRepG1Decoders[i].Init();
            this.isRepG2Decoders[i].Init();
        }

        this.literalDecoder.Init();
        for (var i = 0U; i < LzmaBase.NumLenToPosStates; i++)
        {
            this.posSlotDecoder[i].Init();
        }

        for (var i = 0U; i < LzmaBase.NumFullDistances - LzmaBase.EndPosModelIndex; i++)
        {
            this.posDecoders[i].Init();
        }

        this.lenDecoder.Init();
        this.repLenDecoder.Init();
        this.posAlignDecoder.Init();
    }

    private sealed class LenDecoder
    {
        private readonly RangeCoder.BitTreeDecoder[] lowCoder = new RangeCoder.BitTreeDecoder[LzmaBase.NumPosStatesMax];
        private readonly RangeCoder.BitTreeDecoder[] midCoder = new RangeCoder.BitTreeDecoder[LzmaBase.NumPosStatesMax];
        private readonly RangeCoder.BitTreeDecoder highCoder = new(LzmaBase.NumHighLenBits);
        private RangeCoder.BitDecoder firstChoice = default;
        private RangeCoder.BitDecoder secondChoice = default;
        private uint numPosStates;

        public void Create(uint numPosStates)
        {
            for (var posState = this.numPosStates; posState < numPosStates; posState++)
            {
                this.lowCoder[posState] = new(LzmaBase.NumLowLenBits);
                this.midCoder[posState] = new(LzmaBase.NumMidLenBits);
            }

            this.numPosStates = numPosStates;
        }

        public void Init()
        {
            this.firstChoice.Init();
            for (var posState = 0U; posState < this.numPosStates; posState++)
            {
                this.lowCoder[posState].Init();
                this.midCoder[posState].Init();
            }

            this.secondChoice.Init();
            this.highCoder.Init();
        }

        public uint Decode(RangeCoder.Decoder rangeDecoder, uint posState)
        {
            if (this.firstChoice.Decode(rangeDecoder) is 0U)
            {
                return this.lowCoder[posState].Decode(rangeDecoder);
            }

            var symbol = LzmaBase.NumLowLenSymbols;
            if (this.secondChoice.Decode(rangeDecoder) is 0U)
            {
                symbol += this.midCoder[posState].Decode(rangeDecoder);
            }
            else
            {
                symbol += LzmaBase.NumMidLenSymbols;
                symbol += this.highCoder.Decode(rangeDecoder);
            }

            return symbol;
        }
    }

    private sealed class LiteralDecoder
    {
        private Decoder2[]? coders;
        private int numPrevBits;
        private int numPosBits;
        private uint posMask;

        public void Create(int numPosBits, int numPrevBits)
        {
            if (this.coders is not null
                && this.numPrevBits == numPrevBits
                && this.numPosBits == numPosBits)
            {
                return;
            }

            this.numPosBits = numPosBits;
            this.posMask = (1U << numPosBits) - 1;
            this.numPrevBits = numPrevBits;
            var numStates = 1U << (this.numPrevBits + this.numPosBits);
            this.coders = new Decoder2[numStates];
            for (var i = 0; i < numStates; i++)
            {
                this.coders[i] = new Decoder2();
            }
        }

        public void Init()
        {
            if (this.coders is null)
            {
                return;
            }

            var numStates = 1U << (this.numPrevBits + this.numPosBits);
            for (var i = 0U; i < numStates; i++)
            {
                this.coders[i].Init();
            }
        }

        public byte DecodeNormal(RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte) => this.coders is not null
                ? this.coders[this.GetState(pos, prevByte)].DecodeNormal(rangeDecoder)
                : throw new InvalidOperationException();

        public byte DecodeWithMatchByte(RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte, byte matchByte) => this.coders is not null
                ? this.coders[this.GetState(pos, prevByte)].DecodeWithMatchByte(rangeDecoder, matchByte)
                : throw new InvalidOperationException();

        private uint GetState(uint pos, byte prevByte) => ((pos & this.posMask) << this.numPrevBits) + (uint)(prevByte >> (8 - this.numPrevBits));

        private readonly struct Decoder2
        {
            private readonly RangeCoder.BitDecoder[] secoders;

            public Decoder2() => this.secoders = new RangeCoder.BitDecoder[0x300];

            public void Init()
            {
                for (var i = 0; i < 0x300; i++)
                {
                    this.secoders[i].Init();
                }
            }

            public byte DecodeNormal(RangeCoder.Decoder rangeDecoder)
            {
                var symbol = 1U;
                do
                {
                    symbol = (symbol << 1) | this.secoders[symbol].Decode(rangeDecoder);
                }
                while (symbol < 0x100);

                return (byte)symbol;
            }

            public byte DecodeWithMatchByte(RangeCoder.Decoder rangeDecoder, byte matchByte)
            {
                var symbol = 1U;
                do
                {
                    var matchBit = (uint)(matchByte >> 7) & 1U;
                    matchByte <<= 1;
                    var bit = this.secoders[((1 + matchBit) << 8) + symbol].Decode(rangeDecoder);
                    symbol = (symbol << 1) | bit;
                    if (matchBit != bit)
                    {
                        while (symbol < 0x100)
                        {
                            symbol = (symbol << 1) | this.secoders[symbol].Decode(rangeDecoder);
                        }

                        break;
                    }
                }
                while (symbol < 0x100);

                return (byte)symbol;
            }
        }
    }
}