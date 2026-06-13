
using System.Numerics;
using Content.Server._Common.Consent;
using Content.Server.SizeAttribute;
using Content.Shared._Common.Consent;
using Content.Shared.Body.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Body.Systems;

public sealed class SizeManipulationSystem : EntitySystem
{

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ConsentSystem _consent = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SizeAttributeSystem _sizeAttribute = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private static readonly ProtoId<ConsentTogglePrototype> SizeManipulationConsent = "SizeManipulation";

    public override void Initialize()
    {
        base.Initialize();
        //SubscribeLocalEvent<SizeAffectedComponent, GetSizeModifierEvent>(OnGetSizeModifier);
        //SubscribeLocalEvent<SizeAffectedComponent, ComponentStartup>(OnComponentStartup);
    }

    /// <summary>
    /// When a SizeAffectedComponent is added or initialized (e.g., on client connect),
    /// trigger a size recalculation to ensure visual state is correct
    /// </summary>
    //private void OnComponentStartup(EntityUid uid, SizeAffectedComponent component, ComponentStartup args)
    //{
    //    // Only recalculate if the entity has a non-default scale
    //    if (Math.Abs(component.ScaleMultiplier - 1.0f) > 0.01f)
    //    {
    //        //var recalcEvent = new RequestSizeRecalcEvent();
    //        //RaiseLocalEvent(uid, ref recalcEvent);
    //    }
    //}

    ///// <summary>
    ///// Contributes the size gun's modifier when collecting all size modifiers
    ///// </summary>
    //private void OnGetSizeModifier(EntityUid uid, SizeAffectedComponent component)
    //{
    //    // Only contribute if the scale has been changed from default
    //    if (Math.Abs(component.ScaleMultiplier - 1.0f) > 0.01f)
    //    {
    //        //component.ScaleMultiplier
    //        //args.Modifiers.Add(new SizeModifier
    //        //{
    //        //    Source = "SizeGun",
    //        //    Scale = component.ScaleMultiplier,
    //        //    Priority = 10 // Medium priority - applied after base but before temporary effects
    //        //});
    //    }
    //}

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

        // Update the component's scale multiplier
        sizeComp.ScaleMultiplier = newScale;
        Dirty(target, sizeComp);

        // Set Scale
        var appearanceComponent = _entityManager.EnsureComponent<AppearanceComponent>(target);
        _appearance.SetData(target, HumanoidVisuals.Scale, new Vector2(newScale, newScale), appearanceComponent);

        if (_entityManager.TryGetComponent(target, out FixturesComponent? manager))
        {
            foreach (var (id, fixture) in manager.Fixtures)
            {
                if (!fixture.Hard)
                    continue;

                switch (fixture.Shape)
                {
                    case PhysShapeCircle circle:
                        Logger.Debug($"circle.Radius:  {circle.Radius} for {ToPrettyString(target)}");

                        var radius = newScale * 0.35f;
                        //if (isBorg)
                        //{
                        //    if (borgFixtureRadius is { } overrideRadius && overrideRadius > 0f)
                        //        radius = overrideRadius * newScale;
                        //}

                        // Set Radius
                        _physics.SetPositionRadius(target, id, fixture, circle, circle.Position * newScale, radius, manager);
                        break;
                    default:
                        // Skip unsupported shapes instead of crashing on initialization.
                        continue;
                }

                if (densityMultiplier > 0f && densityMultiplier != 1f)
                {
                    _physics.SetDensity(target, id, fixture, fixture.Density * densityMultiplier);
                }
            }
        }

        Logger.Debug($"SizeManipulation: Set scale multiplier to {newScale} for {ToPrettyString(target)}");


        var message = mode == SizeManipulatorMode.Grow
            ? Loc.GetString("size-manipulator-target-grow")
            : Loc.GetString("size-manipulator-target-shrink");

        _popup.PopupEntity(message, target, PopupType.Medium);

        return true;
    }
}
