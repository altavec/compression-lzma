// -----------------------------------------------------------------------
// <copyright file="IWriteCoderProperties.cs" company="Altavec">
// Copyright (c) Altavec. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Altavec.IO.Compression;

/// <summary>
/// Interface for writing the coder properties.
/// </summary>
internal interface IWriteCoderProperties
{
    /// <summary>
    /// Writes the coder properties.
    /// </summary>
    /// <param name="outStream">The stream to write to.</param>
    void WriteCoderProperties(Stream outStream);
}