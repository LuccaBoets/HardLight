using System.Numerics;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;
using Content.Server.Sprite; // HardLight
using Content.Shared._NF.SizeAttribute;
using Content.Shared.Nyanotrasen.Item.PseudoItem;
using Content.Shared.Humanoid;
using Content.Shared.Silicons.Borgs.Components; // HardLight

namespace Content.Server.SizeAttribute
{
    public sealed class SizeAttributeSystem : EntitySystem
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;
        [Dependency] private readonly AppearanceSystem _appearance = default!;
        [Dependency] private readonly ScaleVisualsSystem _scaleVisuals = default!; // HardLight
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SizeAttributeComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<TallWhitelistComponent, ComponentInit>(OnTallWhitelistInit); // HardLight
            SubscribeLocalEvent<ShortWhitelistComponent, ComponentInit>(OnShortWhitelistInit); // HardLight
        }

        private void OnComponentInit(EntityUid uid, SizeAttributeComponent component, ComponentInit args)
        {
            TryApplySizeAttribute(uid, component); // HardLight
        }

        // HardLight start
        private void OnTallWhitelistInit(EntityUid uid, TallWhitelistComponent _, ComponentInit args)
        {
            if (TryComp(uid, out SizeAttributeComponent? sizeAttribute))
                TryApplySizeAttribute(uid, sizeAttribute);
        }

        private void OnShortWhitelistInit(EntityUid uid, ShortWhitelistComponent _, ComponentInit args)
        {
            if (TryComp(uid, out SizeAttributeComponent? sizeAttribute))
                TryApplySizeAttribute(uid, sizeAttribute);
        }

        private void TryApplySizeAttribute(EntityUid uid, SizeAttributeComponent component)
        {
            if (component.Applied)
                return;

            if (component.Tall && TryComp<TallWhitelistComponent>(uid, out var tallComp))
            {
                var resolvedScale = ResolveScale(uid, tallComp.Scale, tallComp.BorgScale);
                Scale(uid,
                    resolvedScale,
                    tallComp.Density,
                    tallComp.DensityMultiplier,
                    tallComp.CosmeticOnly,
                    tallComp.BorgFixtureRadius);
                PseudoItem(uid, component, tallComp.PseudoItem, tallComp.Shape, tallComp.StoredOffset, tallComp.StoredRotation);
                component.Applied = true;
            }
            else if (component.Short && TryComp<ShortWhitelistComponent>(uid, out var shortComp))
            {
                var resolvedScale = ResolveScale(uid, shortComp.Scale, shortComp.BorgScale);
                Scale(uid,
                    resolvedScale,
                    shortComp.Density,
                    shortComp.DensityMultiplier,
                    shortComp.CosmeticOnly,
                    shortComp.BorgFixtureRadius);
                PseudoItem(uid, component, shortComp.PseudoItem, shortComp.Shape, shortComp.StoredOffset, shortComp.StoredRotation);
                component.Applied = true;
            }
        }

        private float ResolveScale(EntityUid uid, float defaultScale, float? borgScale)
        {
            if (_entityManager.HasComponent<BorgChassisComponent>(uid) && borgScale is { } overrideScale)
                return overrideScale;

            return defaultScale;
        }
        // HardLight end

        private void PseudoItem(EntityUid uid, SizeAttributeComponent _, bool active, List<Box2i>? shape, Vector2i? storedOffset, float storedRotation)
        {
            if (active)
            {
                var pseudoI = _entityManager.EnsureComponent<PseudoItemComponent>(uid);

                pseudoI.StoredRotation = storedRotation;
                pseudoI.StoredOffset = storedOffset ?? new(0, 17);
                pseudoI.Shape = shape ?? new List<Box2i>
                {
                    new Box2i(0, 0, 1, 4),
                    new Box2i(0, 2, 3, 4),
                    new Box2i(4, 0, 5, 4)
                };
            }
            else
            {
                _entityManager.RemoveComponent<PseudoItemComponent>(uid);
            }
        }

        // HardLight start
        public void Scale(
            EntityUid uid,
            float scale,
            float density,
            float densityMultiplier,
            bool cosmeticOnly,
            float? borgFixtureRadius)
        {
            if (scale <= 0f)
                return;

            var isBorg = _entityManager.HasComponent<BorgChassisComponent>(uid);

            // Borgs do not use humanoid visuals, so they need sprite scale visuals instead.
            if (isBorg)
                _scaleVisuals.SetSpriteScale(uid, new Vector2(scale, scale));

            var appearanceComponent = _entityManager.EnsureComponent<AppearanceComponent>(uid);
            _appearance.SetData(uid, HumanoidVisuals.Scale, new Vector2(scale, scale), appearanceComponent);

            if (!cosmeticOnly && _entityManager.TryGetComponent(uid, out FixturesComponent? manager))
            {
                foreach (var (id, fixture) in manager.Fixtures)
                {
                    if (!fixture.Hard)
                        continue;

                    switch (fixture.Shape)
                    {
                        case PhysShapeCircle circle:
                            var radius = circle.Radius * scale;
                            if (isBorg)
                            {
                                if (borgFixtureRadius is { } overrideRadius && overrideRadius > 0f)
                                    radius = overrideRadius * scale;
                            }

                            _physics.SetPositionRadius(uid, id, fixture, circle, circle.Position * scale, radius, manager);
                            break;
                        default:
                            // Skip unsupported shapes instead of crashing on initialization.
                            continue;
                    }

                    if (density > 0f)
                    {
                        _physics.SetDensity(uid, id, fixture, density);
                    }
                    else if (densityMultiplier > 0f && densityMultiplier != 1f)
                    {
                        _physics.SetDensity(uid, id, fixture, fixture.Density * densityMultiplier);
                    }
                }
            }
        }
        // HardLight end
    }

    [ByRefEvent]
    public readonly record struct ScaleEntityEvent(EntityUid Uid) { }
}
