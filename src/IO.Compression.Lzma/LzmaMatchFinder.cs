// -----------------------------------------------------------------------
// <copyright file="LzmaMatchFinder.cs" company="Altavec">
// Copyright (c) Altavec. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Altavec.IO.Compression;

/// <summary>
/// The LZMA match finders.
/// </summary>
public enum LzmaMatchFinder
{
    /// <summary>
    /// Binary Tree with 2.
    /// </summary>
    BT2,

    /// <summary>
    /// Binary Tree with 4.
    /// </summary>
    BT4,
}