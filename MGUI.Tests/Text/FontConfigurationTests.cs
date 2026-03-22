using System;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Xunit;

namespace MGUI.Tests.Text;

public class FontConfigurationTests
{
    [Fact]
    public void FontManager_GetFontFamilyOrDefault_WithNoRegisteredFonts_ThrowsHelpfulException()
    {
        FontManager manager = new();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => manager.GetFontFamilyOrDefault("AnyFamily"));
        Assert.Contains("Register at least one FontSet", ex.Message);
    }

    [Fact]
    public void FontManager_GetFontFamilyOrDefault_WithOnlyDefaultFamilyConfigured_StillThrowsWhenRegistryIsEmpty()
    {
        FontManager manager = new("ConfiguredDefault");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => manager.GetFontFamilyOrDefault("MissingFamily"));
        Assert.Contains("Register at least one FontSet", ex.Message);
    }

    [Fact]
    public void FontManager_TryGetFont_WithNoRegisteredFonts_ReturnsFalse()
    {
        FontManager manager = new();

        bool found = manager.TryGetFont("AnyFamily", CustomFontStyles.Normal, 12, true, out _, out _, out _, out _, out _);

        Assert.False(found);
    }

    [Fact]
    public void SpriteFontTextEngine_ResolveFont_WithEmptyFontManager_ThrowsHelpfulException()
    {
        FontManager manager = new();
        SpriteFontTextEngine engine = new(manager);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => engine.ResolveFont(FontSpec.Normal("AnyFamily", 12)));
        Assert.Contains("Register at least one FontSet", ex.Message);
    }
}
