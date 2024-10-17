// -----------------------------------------------------------------------
// <copyright file="ICoder.cs" company="Altavec">
// Copyright (c) Altavec. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Altavec.IO.Compression;

/// <summary>
/// The coder.
/// </summary>
internal interface ICoder
{
    /// <summary>
    /// Codes streams.
    /// </summary>
    /// <param name="inStream">input Stream.</param>
    /// <param name="outStream">output Stream.</param>
    /// <param name="inSize">input Size. -1 if unknown.</param>
    /// <param name="outSize">output Size. -1 if unknown.</param>
    /// <param name="progress">callback progress reference.</param>
    void Code(Stream inStream, Stream outStream, long inSize = -1, long outSize = -1, Action<long, long>? progress = null);
}