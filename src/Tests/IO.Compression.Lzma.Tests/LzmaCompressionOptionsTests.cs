// -----------------------------------------------------------------------
// <copyright file="LzmaCompressionOptionsTests.cs" company="Altavec">
// Copyright (c) Altavec. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Altavec.IO.Compression.Tests;

public class LzmaCompressionOptionsTests
{
    [Fact]
    public void FromDefaultOptions()
    {
        var defaultProperties = LzmaEncoderTests.GetDefaultProperties();
        var defaultFromOptions = new LzmaCompressionOptions().ToDictionary();

        Assert.Equal(defaultProperties, defaultFromOptions);
    }
}