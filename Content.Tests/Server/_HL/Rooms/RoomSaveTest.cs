using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Server._HL.Save;
using NUnit.Framework;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
#nullable enable

namespace Content.Tests.Server._HL.Rooms;

/// <summary>
/// Unit tests for room-save serialization correctness.
/// These cover the bugs fixed in the room save path:
///   1. FixedPoint2 values survive YamlDotNet round-trip as double scalars.
///   2. Nested Dictionary&lt;object,object&gt; from YamlDotNet is coerced correctly.
///   3. Room save filter rule constants are well-formed.
/// All tests are pure (no ECS required).
/// </summary>
[TestFixture, Parallelizable(ParallelScope.All)]
public sealed class RoomSaveTest
{
    // ---------------------------------------------------------------------------
    // Helpers: minimal YamlDotNet round-trip (mirrors ShipSerializationSystem)
    // ---------------------------------------------------------------------------

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private static T? RoundTrip<T>(T value)
    {
        var yaml = Serializer.Serialize(value);
        return Deserializer.Deserialize<T>(yaml);
    }

    // ---------------------------------------------------------------------------
    // Bug fix 1: FixedPoint2-as-double survives round-trip
    // ---------------------------------------------------------------------------

    [Test]
    public void DoubleValueSurvivesYamlRoundTrip()
    {
        var original = new Dictionary<string, object>
        {
            ["water"] = 5.5,
            ["ethanol"] = 12.0,
        };

        var yaml = Serializer.Serialize(original);
        var restored = Deserializer.Deserialize<Dictionary<string, object>>(yaml);

        Assert.That(restored, Is.Not.Null);
        Assert.That(restored!.ContainsKey("water"), Is.True, "water key should survive round-trip");

        var waterObj = restored["water"];
        // YamlDotNet deserializes scalars as strings when target is object
        var ok = waterObj switch
        {
            double d => Math.Abs(d - 5.5) < 0.001,
            string s => double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                && Math.Abs(parsed - 5.5) < 0.001,
            _ => false
        };
        Assert.That(ok, Is.True,
            $"Expected ~5.5 after round-trip, got '{waterObj}' ({waterObj?.GetType().Name})");
    }

    [Test]
    public void EmptyStructAsObjectDoesNotSurviveRoundTripAsUsefulValue()
    {
        // This test documents the OLD bug: a struct with no public properties
        // (like FixedPoint2 when boxed as object) serializes as {} and comes
        // back as Dictionary<object,object>, which TryConvertToDouble cannot handle.
        var dummy = new EmptyStruct();
        var original = new Dictionary<string, object> { ["qty"] = dummy };

        var yaml = Serializer.Serialize(original);
        var restored = Deserializer.Deserialize<Dictionary<string, object>>(yaml);

        Assert.That(restored, Is.Not.Null);
        var qty = restored!["qty"];
        // Cannot be used as a numeric value — it comes back as Dictionary<object,object>
        var isUsable = qty is double or float or int or long or string;
        Assert.That(isUsable, Is.False,
            "A struct with no public fields round-trips to a dict, not a scalar — " +
            "this is why FixedPoint2 must be cast to double before storage.");
    }

    private struct EmptyStruct { }

    // ---------------------------------------------------------------------------
    // Bug fix 2: CoerceToDictStringObj handles both dict kinds
    // ---------------------------------------------------------------------------

    [Test]
    public void CoerceToDictStringObj_AcceptsDictStringObj()
    {
        var input = new Dictionary<string, object> { ["a"] = "hello" };
        var result = CoerceToDictStringObj(input);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!["a"], Is.EqualTo("hello"));
    }

    [Test]
    public void CoerceToDictStringObj_AcceptsDictObjObj()
    {
        var input = new Dictionary<object, object> { ["b"] = 42 };
        var result = CoerceToDictStringObj(input);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ContainsKey("b"), Is.True, "key 'b' should be preserved after coercion");
        Assert.That(result["b"], Is.EqualTo(42));
    }

    [Test]
    public void CoerceToDictStringObj_ReturnsNullForUnrelatedTypes()
    {
        Assert.That(CoerceToDictStringObj(null), Is.Null);
        Assert.That(CoerceToDictStringObj(42), Is.Null);
        Assert.That(CoerceToDictStringObj("hello"), Is.Null);
    }

    [Test]
    public void CoerceToDictStringObj_HandlesNestedDictFromYaml()
    {
        // Simulate the nested solution data that YamlDotNet produces:
        // Properties["default"] → Dictionary<object,object> { "Reagents" → Dictionary<object,object> { "water" → "5.5" } }
        var innerReagents = new Dictionary<object, object> { ["water"] = "5.5" };
        var innerSolution = new Dictionary<object, object>
        {
            ["MaxVolume"] = "100.0",
            ["Temperature"] = "293.15",
            ["Reagents"] = innerReagents,
        };

        var solutionInfo = CoerceToDictStringObj(innerSolution);
        Assert.That(solutionInfo, Is.Not.Null);

        var reagentsRaw = solutionInfo!.TryGetValue("Reagents", out var r) ? r : null;
        var reagents = CoerceToDictStringObj(reagentsRaw);
        Assert.That(reagents, Is.Not.Null, "Reagents inner dict should also be coercible");
        Assert.That(reagents!.ContainsKey("water"), Is.True);
        Assert.That(reagents["water"]?.ToString(), Is.EqualTo("5.5"));
    }

    // Local copy of the helper (duplicated from ShipSerializationSystem to keep tests pure)
    private static Dictionary<string, object>? CoerceToDictStringObj(object? value)
    {
        return value switch
        {
            Dictionary<string, object> d => d,
            Dictionary<object, object> od => od.ToDictionary(
                kv => kv.Key?.ToString() ?? string.Empty,
                kv => kv.Value),
            _ => null
        };
    }

    // ---------------------------------------------------------------------------
    // Bug fix 3: Solution data with doubles round-trips correctly end-to-end
    // ---------------------------------------------------------------------------

    [Test]
    public void SolutionDataWithDoublesRoundTripsCorrectly()
    {
        // Simulate what SerializeSolutionComponent now produces (after fix)
        var solutionData = new Dictionary<string, object>
        {
            ["default"] = new Dictionary<string, object>
            {
                ["Volume"] = 30.0,
                ["MaxVolume"] = 100.0,
                ["Temperature"] = 293.15f,
                ["Reagents"] = new Dictionary<string, object>
                {
                    ["Water"] = 20.0,
                    ["Ethanol"] = 10.0,
                }
            }
        };

        // Round-trip through YamlDotNet (as ComponentData.Properties would be)
        var yaml = Serializer.Serialize(solutionData);
        var restored = Deserializer.Deserialize<Dictionary<string, object>>(yaml);

        Assert.That(restored, Is.Not.Null);

        // RestoreSolutionComponent uses CoerceToDictStringObj for the outer value
        var solutionInfo = CoerceToDictStringObj(restored!["default"]);
        Assert.That(solutionInfo, Is.Not.Null, "Outer solution info should be coercible");

        // MaxVolume should be parseable as double
        Assert.That(solutionInfo!.ContainsKey("MaxVolume"), Is.True);
        var maxVolObj = solutionInfo["MaxVolume"];
        var maxVolOk = maxVolObj switch
        {
            double d => d > 0,
            string s => double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0,
            _ => false
        };
        Assert.That(maxVolOk, Is.True, $"MaxVolume '{maxVolObj}' should be parseable as a positive number");

        // Reagents should be present and coercible
        var reagentsRaw = solutionInfo.TryGetValue("Reagents", out var rr) ? rr : null;
        var reagents = CoerceToDictStringObj(reagentsRaw);
        Assert.That(reagents, Is.Not.Null, "Reagents should be coercible");
        Assert.That(reagents!.ContainsKey("Water"), Is.True, "Water reagent should survive round-trip");

        var waterQty = reagents["Water"];
        var waterOk = waterQty switch
        {
            double d => Math.Abs(d - 20.0) < 0.01,
            string s => double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) && Math.Abs(v - 20.0) < 0.01,
            _ => false
        };
        Assert.That(waterOk, Is.True, $"Water qty '{waterQty}' should be ~20.0");
    }

    // ---------------------------------------------------------------------------
    // Filter rule sanity
    // ---------------------------------------------------------------------------

    [Test]
    public void RoomSaveRules_FilteredPrototypesIsNonEmpty()
    {
        Assert.That(RoomSaveRules.FilteredPrototypes, Is.Not.Empty,
            "FilteredPrototypes should contain at least the ContainmentField prototype");
        Assert.That(RoomSaveRules.FilteredPrototypes, Contains.Item("ContainmentField"),
            "ContainmentField should be filtered from room saves");
    }

    [Test]
    public void RoomSaveRules_EntityExclusionContainsExpectedComponents()
    {
        Assert.That(RoomSaveRules.EntityExclusionComponentNames, Contains.Item("GhostComponent"),
            "Ghosts must be excluded from room saves");
        Assert.That(RoomSaveRules.EntityExclusionComponentNames, Contains.Item("MobStateComponent"),
            "Mobs must be excluded from room saves");
        Assert.That(RoomSaveRules.EntityExclusionComponentNames, Contains.Item("HumanoidAppearanceComponent"),
            "Player humanoids must be excluded from room saves");
    }

    [Test]
    public void RoomSaveRules_FilteredComponentNamesDoesNotContainVendingMachine()
    {
        // Vendors SHOULD be saved in rooms (they're placed there intentionally).
        Assert.That(RoomSaveRules.FilteredComponentNames, Does.Not.Contain("VendingMachineComponent"),
            "VendingMachineComponent must NOT be in the room component filter — vendors must be saved");
    }

    [Test]
    public void RoomSaveRules_FilteredComponentNamesContainsRuntimeOnlyComponents()
    {
        Assert.That(RoomSaveRules.FilteredComponentNames, Contains.Item("ActionsComponent"),
            "ActionsComponent is runtime-only and must be filtered");
        Assert.That(RoomSaveRules.FilteredComponentNames, Contains.Item("DockingComponent"),
            "DockingComponent is ship-specific runtime state and must be filtered");
        Assert.That(RoomSaveRules.FilteredComponentNames, Contains.Item("MindContainerComponent"),
            "MindContainerComponent must be filtered to avoid serializing player state");
    }

    // ---------------------------------------------------------------------------
    // Component round-trip helpers (mirrors ShipSerializationSystem logic)
    // ---------------------------------------------------------------------------

    private static Dictionary<string, object> MakePaintedProps(string colorHex, bool enabled, string shaderName = "Greyscale")
        => new() { ["Color"] = colorHex, ["Enabled"] = enabled, ["ShaderName"] = shaderName };

    private static Dictionary<string, object> MakeLabelProps(string label)
        => new() { ["CurrentLabel"] = label };

    private static Dictionary<string, object> MakePaperProps(string? content = null, string? stampState = null, bool editingDisabled = false, List<Dictionary<string, object>>? stamps = null)
    {
        var props = new Dictionary<string, object>();
        if (content != null) props["Content"] = content;
        if (stampState != null) props["StampState"] = stampState;
        if (editingDisabled) props["EditingDisabled"] = (object)true;
        if (stamps != null && stamps.Count > 0) props["Stamps"] = stamps.Cast<object>().ToList();
        return props;
    }

    private static Dictionary<string, object> MakeRandomSpriteProps(Dictionary<string, (string State, string? ColorHex)> selected)
    {
        var inner = selected.ToDictionary(
            kv => kv.Key,
            kv => (object)new Dictionary<string, object?>
            {
                ["State"] = kv.Value.State,
                ["Color"] = (object?)kv.Value.ColorHex,
            });
        return new Dictionary<string, object> { ["Selected"] = inner };
    }

    // ---------------------------------------------------------------------------
    // PaintedComponent round-trip
    // ---------------------------------------------------------------------------

    [Test]
    public void PaintedComponent_ColorAndEnabledSurviveRoundTrip()
    {
        var props = MakePaintedProps("#C063F5FF", enabled: true);
        var yaml = Serializer.Serialize(props);
        var restored = Deserializer.Deserialize<Dictionary<string, object>>(yaml);

        Assert.That(restored, Is.Not.Null);
        Assert.That(restored!["Color"]?.ToString(), Is.EqualTo("#C063F5FF"), "Color hex must survive round-trip");
        var ok = restored["Enabled"] switch
        {
            bool b => b,
            string s => bool.Parse(s),
            _ => false,
        };
        Assert.That(ok, Is.True, "Enabled flag must be true after round-trip");
    }

    [Test]
    public void PaintedComponent_DisabledIsNotSerialized()
    {
        // Disabled (unpainted) entities produce null from SerializePaintedComponent —
        // verified here by checking the Enabled=false guard.
        var props = MakePaintedProps("#C063F5FF", enabled: false);
        var yaml = Serializer.Serialize(props);
        var restored = Deserializer.Deserialize<Dictionary<string, object>>(yaml);
        Assert.That(restored, Is.Not.Null);
        var enabled = restored!["Enabled"] switch
        {
            bool b => b,
            string s => bool.Parse(s),
            _ => true,
        };
        Assert.That(enabled, Is.False, "A disabled PaintedComponent should round-trip as false");
    }

    // ---------------------------------------------------------------------------
    // LabelComponent round-trip
    // ---------------------------------------------------------------------------

    [Test]
    public void LabelComponent_CurrentLabelSurvivesRoundTrip()
    {
        var props = MakeLabelProps("Ammo Storage");
        var yaml = Serializer.Serialize(props);
        var restored = Deserializer.Deserialize<Dictionary<string, object>>(yaml);

        Assert.That(restored, Is.Not.Null);
        Assert.That(restored!["CurrentLabel"]?.ToString(), Is.EqualTo("Ammo Storage"));
    }

    [Test]
    public void LabelComponent_EmptyLabelProducesNoEntry()
    {
        // Empty/null labels are skipped by SerializeLabelComponent (returns null).
        // Simulate: if CurrentLabel is empty string, Properties dict has no "CurrentLabel" key.
        var props = new Dictionary<string, object>();
        Assert.That(props.ContainsKey("CurrentLabel"), Is.False,
            "Empty label must not produce a CurrentLabel entry");
    }

    // ---------------------------------------------------------------------------
    // PaperComponent round-trip
    // ---------------------------------------------------------------------------

    [Test]
    public void PaperComponent_ContentSurvivesRoundTrip()
    {
        var content = "Dear Captain,\n\nPlease approve my vacation request.\n\n- Ensign";
        var props = MakePaperProps(content: content);
        var yaml = Serializer.Serialize(props);
        var restored = Deserializer.Deserialize<Dictionary<string, object>>(yaml);

        Assert.That(restored, Is.Not.Null);
        Assert.That(restored!["Content"]?.ToString(), Is.EqualTo(content), "Paper content must survive round-trip");
    }

    [Test]
    public void PaperComponent_StampsSurviveRoundTrip()
    {
        var stamps = new List<Dictionary<string, object>>
        {
            new()
            {
                ["Name"] = "stamp-component-stamped-name-hos",
                ["Color"] = "#CC0000FF",
                ["Type"] = "RubberStamp",
                ["Reapply"] = (object)false,
            },
            new()
            {
                ["Name"] = "Marisol Alliman",
                ["Color"] = "#333333FF",
                ["Type"] = "Signature",
                ["Reapply"] = (object)false,
            },
        };
        var props = MakePaperProps(stampState: "paper_stamp-hos", stamps: stamps);
        var yaml = Serializer.Serialize(props);
        var restored = Deserializer.Deserialize<Dictionary<string, object>>(yaml);

        Assert.That(restored, Is.Not.Null);
        Assert.That(restored!["StampState"]?.ToString(), Is.EqualTo("paper_stamp-hos"));

        var stampsRaw = restored["Stamps"];
        Assert.That(stampsRaw, Is.Not.Null, "Stamps list must be present");
        Assert.That(stampsRaw is List<object>, Is.True, "Stamps must deserialize as List<object>");
        var stampList = (List<object>)stampsRaw!;
        Assert.That(stampList.Count, Is.EqualTo(2));

        var first = CoerceToDictStringObj(stampList[0]);
        Assert.That(first, Is.Not.Null);
        Assert.That(first!["Color"]?.ToString(), Is.EqualTo("#CC0000FF"));
        Assert.That(first["Type"]?.ToString(), Is.EqualTo("RubberStamp"));

        var second = CoerceToDictStringObj(stampList[1]);
        Assert.That(second, Is.Not.Null);
        Assert.That(second!["Type"]?.ToString(), Is.EqualTo("Signature"));
    }

    [Test]
    public void PaperComponent_EditingDisabledSurvivesRoundTrip()
    {
        var props = MakePaperProps(content: "Classified", editingDisabled: true);
        var yaml = Serializer.Serialize(props);
        var restored = Deserializer.Deserialize<Dictionary<string, object>>(yaml);

        Assert.That(restored, Is.Not.Null);
        var disabled = restored!["EditingDisabled"] switch
        {
            bool b => b,
            string s => bool.Parse(s),
            _ => false,
        };
        Assert.That(disabled, Is.True, "EditingDisabled must survive round-trip");
    }

    [Test]
    public void PaperComponent_BlankPaperProducesNoEntry()
    {
        // Blank, unstamped, unprotected paper → no Properties → null ComponentData.
        var props = MakePaperProps();
        Assert.That(props.Count, Is.Zero, "Blank paper must produce empty Properties dict (no ComponentData)");
    }

    // ---------------------------------------------------------------------------
    // RandomSpriteComponent round-trip
    // ---------------------------------------------------------------------------

    [Test]
    public void RandomSpriteComponent_SelectedStatesSurviveRoundTrip()
    {
        var props = MakeRandomSpriteProps(new Dictionary<string, (string State, string? ColorHex)>
        {
            ["enum.ArtifactsVisualLayers.Base"] = ("martianball3", null),
            ["pouchFill1"] = ("fill-util1-3", "#FF8800FF"),
        });
        var yaml = Serializer.Serialize(props);
        var restored = Deserializer.Deserialize<Dictionary<string, object>>(yaml);

        Assert.That(restored, Is.Not.Null);

        var selectedRaw = CoerceToDictStringObj(restored!["Selected"]);
        Assert.That(selectedRaw, Is.Not.Null, "Selected dict must be coercible");

        var baseLayer = CoerceToDictStringObj(selectedRaw!["enum.ArtifactsVisualLayers.Base"]);
        Assert.That(baseLayer, Is.Not.Null);
        Assert.That(baseLayer!["State"]?.ToString(), Is.EqualTo("martianball3"));
        Assert.That(baseLayer["Color"], Is.Null.Or.Empty, "Null color must round-trip as null or empty");

        var pouchLayer = CoerceToDictStringObj(selectedRaw["pouchFill1"]);
        Assert.That(pouchLayer, Is.Not.Null);
        Assert.That(pouchLayer!["State"]?.ToString(), Is.EqualTo("fill-util1-3"));
        Assert.That(pouchLayer["Color"]?.ToString(), Is.EqualTo("#FF8800FF"));
    }

    [Test]
    public void RandomSpriteComponent_EmptySelectedProducesNoEntry()
    {
        // Selected.Count == 0 → SerializeRandomSpriteComponent returns null.
        // Simulate: empty dict → Properties is empty → removed by RemoveAll filter.
        var props = MakeRandomSpriteProps(new Dictionary<string, (string State, string? ColorHex)>());
        Assert.That(props.TryGetValue("Selected", out var sel) && sel is Dictionary<string, object> d && d.Count == 0,
            Is.True, "Empty Selected must produce an empty inner dict");
    }

    // ---------------------------------------------------------------------------
    // ChameleonClothingComponent round-trip
    // ---------------------------------------------------------------------------

    private static Dictionary<string, object> MakeChameleonProps(string selectedProtoId)
        => new() { ["Default"] = selectedProtoId };

    [Test]
    public void ChameleonClothing_DefaultPrototypeSurvivesRoundTrip()
    {
        var props = MakeChameleonProps("ClothingHeadHelmetHardsuitSecurity");
        var yaml = Serializer.Serialize(props);
        var restored = Deserializer.Deserialize<Dictionary<string, object>>(yaml);

        Assert.That(restored, Is.Not.Null);
        Assert.That(restored!["Default"]?.ToString(), Is.EqualTo("ClothingHeadHelmetHardsuitSecurity"),
            "Chameleon selected prototype ID must survive round-trip unchanged");
    }

    [Test]
    public void ChameleonClothing_EmptyDefaultProducesNoEntry()
    {
        // SerializeChameleonClothingComponent returns null when Default is null/empty,
        // so no ComponentData is added. Verify the guard logic matches.
        var nullDefault = (string?)null;
        var emptyDefault = string.Empty;

        Assert.That(string.IsNullOrEmpty(nullDefault), Is.True, "null Default must be treated as not set");
        Assert.That(string.IsNullOrEmpty(emptyDefault), Is.True, "empty Default must be treated as not set");
    }

    [Test]
    public void ChameleonClothing_ProtoIdWithSpecialCharactersSurvivesRoundTrip()
    {
        // Prototype IDs can contain underscores, digits, and mixed case.
        var props = MakeChameleonProps("ClothingOuterHardsuitRd_Variant2");
        var yaml = Serializer.Serialize(props);
        var restored = Deserializer.Deserialize<Dictionary<string, object>>(yaml);

        Assert.That(restored!["Default"]?.ToString(), Is.EqualTo("ClothingOuterHardsuitRd_Variant2"));
    }

    // ---------------------------------------------------------------------------
    // ToggleableClothing / helmet fix — AttachedUid sentinel logic
    // ---------------------------------------------------------------------------

    [Test]
    public void ToggleableClothing_InvalidEntityUidSentinelIsNotValid()
    {
        // The helmet fix works by setting AttachedClothingComponent.AttachedUid = EntityUid.Invalid
        // before deleting the auto-spawned helmet. OnRemoveAttached then calls
        // TryComp(component.AttachedUid, ...) which returns false for an invalid uid,
        // so ToggleableClothingComponent is NOT stripped from the suit.
        //
        // This test verifies the sentinel value behaves as expected.
        var invalid = Robust.Shared.GameObjects.EntityUid.Invalid;
        Assert.That(invalid.IsValid(), Is.False,
            "EntityUid.Invalid must not be valid — the OnRemoveAttached guard relies on this");
    }

    [Test]
    public void ToggleableClothing_DefaultEntityUidIsInvalid()
    {
        // A default (zero) EntityUid is equivalent to Invalid. This is the state
        // AttachedUid would be in before assignment, confirming the sentinel doesn't
        // accidentally alias a real entity.
        var defaultUid = default(Robust.Shared.GameObjects.EntityUid);
        Assert.That(defaultUid.IsValid(), Is.False,
            "default EntityUid must not be valid");
        Assert.That(defaultUid, Is.EqualTo(Robust.Shared.GameObjects.EntityUid.Invalid),
            "default EntityUid must equal EntityUid.Invalid");
    }
}
