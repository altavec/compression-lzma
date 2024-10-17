// -----------------------------------------------------------------------
// <copyright file="ISetCoderProperties.cs" company="Altavec">
// Copyright (c) Altavec. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Altavec.IO.Compression;

/// <summary>
/// Interface for setting coder properties.
/// </summary>
internal interface ISetCoderProperties
{
    /// <summary>
    /// Sets the coder properties.
    /// </summary>
    /// <param name="propIDs">The property IDs.</param>
    /// <param name="properties">The properties.</param>
    void SetCoderProperties(CoderPropID[] propIDs, object[] properties);
}