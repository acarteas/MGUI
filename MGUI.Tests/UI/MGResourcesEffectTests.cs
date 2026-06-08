using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using MGUI.Core.UI;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.Tests.UI;

public class MGResourcesEffectTests
{
    [Fact]
    public void AddEffect_RegistersCallerOwnedInstance()
    {
        MGResources resources = CreateResources();
        Effect effect = CreateEffect();

        resources.AddEffect("Hud", effect);

        Assert.Same(effect, resources.Effects["Hud"]);
        Assert.True(resources.TryGetEffect("Hud", out Effect result));
        Assert.Same(effect, result);
    }

    [Fact]
    public void AddEffect_ThrowsForDuplicateName()
    {
        MGResources resources = CreateResources();
        Effect first = CreateEffect();
        Effect replacement = CreateEffect();
        int addedCount = 0;
        resources.OnEffectAdded += (_, _) => addedCount++;
        resources.AddEffect("Hud", first);

        Assert.Throws<ArgumentException>(() => resources.AddEffect("Hud", replacement));
        Assert.Same(first, resources.Effects["Hud"]);
        Assert.Equal(1, addedCount);
    }

    [Fact]
    public void AddOrReplaceEffect_ReplacesRegistrationWithoutDisposingEitherEffect()
    {
        MGResources resources = CreateResources();
        Effect first = CreateEffect();
        Effect replacement = CreateEffect();
        resources.AddEffect("Hud", first);

        resources.AddOrReplaceEffect("Hud", replacement);

        Assert.Same(replacement, resources.Effects["Hud"]);
    }

    [Fact]
    public void AddOrReplaceEffect_RaisesRemovedThenAdded()
    {
        MGResources resources = CreateResources();
        Effect first = CreateEffect();
        Effect replacement = CreateEffect();
        resources.AddEffect("Hud", first);
        List<(string Event, Effect Effect)> events = new();
        resources.OnEffectRemoved += (_, args) => events.Add(("Removed", args.Effect));
        resources.OnEffectAdded += (_, args) => events.Add(("Added", args.Effect));

        resources.AddOrReplaceEffect("Hud", replacement);

        Assert.Equal(new[] { "Removed", "Added" }, events.ConvertAll(x => x.Event));
        Assert.Same(first, events[0].Effect);
        Assert.Same(replacement, events[1].Effect);
    }

    [Fact]
    public void AddOrReplaceEffect_WithNewName_RaisesOnlyAdded()
    {
        MGResources resources = CreateResources();
        int removedCount = 0;
        int addedCount = 0;
        resources.OnEffectRemoved += (_, _) => removedCount++;
        resources.OnEffectAdded += (_, _) => addedCount++;

        resources.AddOrReplaceEffect("Hud", CreateEffect());

        Assert.Equal(0, removedCount);
        Assert.Equal(1, addedCount);
    }

    [Fact]
    public void AddOrReplaceEffect_WithNullReplacement_PreservesExistingRegistration()
    {
        MGResources resources = CreateResources();
        Effect first = CreateEffect();
        resources.AddEffect("Hud", first);
        int eventCount = 0;
        resources.OnEffectRemoved += (_, _) => eventCount++;
        resources.OnEffectAdded += (_, _) => eventCount++;

        Assert.Throws<ArgumentNullException>(() => resources.AddOrReplaceEffect("Hud", null));

        Assert.Same(first, resources.Effects["Hud"]);
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void RemoveEffect_RemovesRegistrationAndReturnsRegisteredInstanceInEvent()
    {
        MGResources resources = CreateResources();
        Effect effect = CreateEffect();
        Effect removed = null!;
        resources.OnEffectRemoved += (_, args) => removed = args.Effect;
        resources.AddEffect("Hud", effect);

        bool result = resources.RemoveEffect("Hud");

        Assert.True(result);
        Assert.Same(effect, removed);
        Assert.False(resources.TryGetEffect("Hud", out _));
        Assert.False(resources.RemoveEffect("Hud"));
    }

    [Fact]
    public void AddEffect_RejectsNullWithoutTakingLifetimeResponsibility()
    {
        MGResources resources = CreateResources();
        int addedCount = 0;
        resources.OnEffectAdded += (_, _) => addedCount++;

        Assert.Throws<ArgumentNullException>(() => resources.AddEffect("Hud", null));
        Assert.Empty(resources.Effects);
        Assert.Equal(0, addedCount);
    }

    [Fact]
    public void TryGetEffect_WithUnknownOrNullName_ReturnsFalseAndNull()
    {
        MGResources resources = CreateResources();

        Assert.False(resources.TryGetEffect("Missing", out Effect missing));
        Assert.Null(missing);
        Assert.False(resources.TryGetEffect(null, out Effect nullName));
        Assert.Null(nullName);
    }

    [Fact]
    public void RemoveEffect_WithUnknownOrNullName_ReturnsFalseWithoutEvent()
    {
        MGResources resources = CreateResources();
        int removedCount = 0;
        resources.OnEffectRemoved += (_, _) => removedCount++;

        Assert.False(resources.RemoveEffect("Missing"));
        Assert.False(resources.RemoveEffect(null));
        Assert.Equal(0, removedCount);
    }

    private static MGResources CreateResources() => new(new MGTheme("TestFont"));

    private static Effect CreateEffect()
    {
        return (Effect)RuntimeHelpers.GetUninitializedObject(typeof(Effect));
    }
}
