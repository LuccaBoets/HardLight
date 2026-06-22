
using System.Linq;
using System.Numerics;
using Content.Server._Common.Consent;
using Content.Server.Sprite;
using Content.Shared._Common.Consent;
using Content.Shared.Body.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using static Robust.Shared.Prototypes.EntityPrototype;

namespace Content.Server.Body.Systems;

public sealed class SizeManipulationSystem : EntitySystem
{

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ConsentSystem _consent = default!; 
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ScaleVisualsSystem _scaleVisuals = default!; // HardLight
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!; // HardLight

    private static readonly ProtoId<ConsentTogglePrototype> SizeManipulationConsent = "SizeManipulation";

    public override void Initialize()
    {
        base.Initialize();
    }

    private static float GetDensity(Fixture fixture, float oldScale, float newScale)
    {
        float oldDestinyMultiplier;
        if (oldScale > 1)
        {
            oldDestinyMultiplier = 1.25f;
        }
        else
        {
            oldDestinyMultiplier = 0.7f;
        }

        float originalDensity = fixture.Density / oldDestinyMultiplier;

        float newDestinyMultiplier;
        if (newScale > 1)
        {
            newDestinyMultiplier = 1.25f;
        }
        else
        {
            newDestinyMultiplier = 0.7f;
        }

        return originalDensity * newDestinyMultiplier;
    }

    public void ApplyEffects(float scale, float oldScale, SizeAffectedComponent sizeComp, EntityUid target)
    {
        var scaleEffects = _prototypeManager.EnumeratePrototypes<SizeManipulationPrototype>().ToList();

        // Get old components
        List<KeyValuePair<string, ComponentRegistryEntry>>? oldComponents = default;
        foreach (var scaleEffect in scaleEffects)
        {
            if (oldScale > scaleEffect.MinScale && oldScale <= scaleEffect.MaxScale)
            {
                oldComponents = scaleEffect.Components.ToList();
                break;
            }
        }

        // Apply new components with copy (override)
        foreach (var scaleEffect in scaleEffects)
        {
            var components = scaleEffect.Components;
            if (scale > scaleEffect.MinScale && scale <= scaleEffect.MaxScale)
            {
                Logger.Debug($"SizeManipulationSystem: size effect: {scaleEffect.ID}, scale range: {scaleEffect.MinScale}-{scaleEffect.MaxScale}");

                foreach (var componentKeyPair in components)
                {
                    if (oldComponents != null && oldComponents.Any(x => x.Key == componentKeyPair.Key))
                    {
                        oldComponents.RemoveAll(x => x.Key == componentKeyPair.Key);
                    }

                    var component = componentKeyPair.Value.Component;
                    CopyComp(component.Owner, target, component);
                }
                break;
            }
        }

        // Remove any left over old components
        if (oldComponents != null)
        {
            foreach (var oldComponent in oldComponents)
            {
                var type = oldComponent.Value.Component.GetType();
                RemCompDeferred(target, type);
            }
        }
    }

    /// <summary>
    /// Applies a size change to the target entity
    /// </summary>
    public bool TryChangeSize(EntityUid target, SizeManipulatorMode mode, EntityUid? user = null, bool safetyDisabled = false)
    {
        // Only allow size manipulation on mobs (living entities)
        if (!HasComp<MobStateComponent>(target))
        {
            Logger.Debug($"SizeManipulation: Target {ToPrettyString(target)} is not a mob, ignoring");
            return false;
        }

        // Check consent
        if (!_consent.HasConsent(target, SizeManipulationConsent))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("size-manipulator-consent-denied"), target, user.Value);

            Logger.Debug($"SizeManipulation: Consent denied for {ToPrettyString(target)}");
            return false;
        }

        var sizeComp = EnsureComp<SizeAffectedComponent>(target);

        Logger.Debug($"SizeManipulation: TryChangeSize called on {ToPrettyString(target)}, mode: {mode}, current scale: {sizeComp.ScaleMultiplier}, safety disabled: {safetyDisabled}");

        // If safety is disabled, double the max limit
        var maxScale = safetyDisabled ? sizeComp.MaxScale * 2.0f : sizeComp.MaxScale;

        // Hardlight Start

        float newScale;
        var densityMultiplier = 0f;
        if (mode == SizeManipulatorMode.Grow)
        {
            densityMultiplier = 1.25f;

            newScale = sizeComp.ScaleMultiplier + sizeComp.ScaleChangeAmount;
            if (newScale > maxScale)
            {
                if (user != null)
                    _popup.PopupEntity(Loc.GetString("size-manipulator-max-size"), target, user.Value);
                return false;
            }
        }
        else
        {
            densityMultiplier = 0.75f;

            newScale = sizeComp.ScaleMultiplier - sizeComp.ScaleChangeAmount;
            if (newScale < sizeComp.MinScale)
            {
                if (user != null)
                    _popup.PopupEntity(Loc.GetString("size-manipulator-min-size"), target, user.Value);
                return false;
            }
        }


        // Fix floating point problem
        newScale = MathF.Round(newScale, 2);

        float oldScale = sizeComp.ScaleMultiplier;
        this.ApplyEffects(newScale, oldScale, sizeComp, target);

        // Update the component's scale multiplier
        sizeComp.ScaleMultiplier = newScale;
        Dirty(target, sizeComp);

        // Set Scale
        _scaleVisuals.SetSpriteScale(target, new Vector2(newScale, newScale));

        if (_entityManager.TryGetComponent(target, out FixturesComponent? manager))
        {
            foreach (var (id, fixture) in manager.Fixtures)
            {
                if (!fixture.Hard)
                    continue;

                switch (fixture.Shape)
                {
                    case PhysShapeCircle circle:

                        if (sizeComp.BaseRadius == -1f)
                        {
                            sizeComp.BaseRadius = circle.Radius;
                        }

                        // Calc new radius of the entity
                        var radius = MathF.Round(sizeComp.BaseRadius * newScale, 4);

                        // Set Radius
                        _physics.SetPositionRadius(target, id, fixture, circle, circle.Position * newScale, radius, manager);
                        break;
                    default:
                        // Skip unsupported shapes instead of crashing on initialization.
                        continue;
                }

                if (densityMultiplier > 0f && densityMultiplier != 1f)
                {
                    // TODO: fix, use sizeComp variable
                    float newDensity = GetDensity(fixture, oldScale, newScale);
                    _physics.SetDensity(target, id, fixture, newDensity);
                }
            }
        }

        // Hardlight End

        Logger.Debug($"SizeManipulation: Set scale multiplier to {newScale} for {ToPrettyString(target)}");

        var message = mode == SizeManipulatorMode.Grow
            ? Loc.GetString("size-manipulator-target-grow")
            : Loc.GetString("size-manipulator-target-shrink");

        _popup.PopupEntity(message, target, PopupType.Medium);

        return true;
    }
}
