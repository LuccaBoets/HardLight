using Content.Shared.Humanoid;
using Content.Shared.Alert;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Content.Shared.Examine;
using Robust.Server.Containers;
using Content.Shared._Starlight;
using Content.Shared.Movement.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Damage;
using Content.Server.Chat.Managers;
using Robust.Shared.Player;
using Content.Shared.Chat;
using Robust.Shared.Timing;
using Content.Shared._HL.Traits.Physical;


namespace Content.Server._Starlight;

public sealed class ShadekinSystem : EntitySystem
{
    private const float BaseSlowdownMultiplier = 0.9f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private sealed class LightCone
    {
        public float Direction { get; set; }
        public float InnerWidth { get; set; }
        public float OuterWidth { get; set; }
    }
    private readonly Dictionary<string, List<LightCone>> lightMasks = new()
    {
        ["/Textures/Effects/LightMasks/cone.png"] = new List<LightCone>
    {
        new LightCone { Direction = 0, InnerWidth = 30, OuterWidth = 60 }
    },
        ["/Textures/Effects/LightMasks/double_cone.png"] = new List<LightCone>
    {
        new LightCone { Direction = 0, InnerWidth = 30, OuterWidth = 60 },
        new LightCone { Direction = 180, InnerWidth = 30, OuterWidth = 60 }
    }
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadekinComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<ShadekinComponent, EyeColorInitEvent>(OnEyeColorChange);
        SubscribeLocalEvent<ShadekinComponent, ShadekinAlertEvent>(OnShadekinAlert);
        SubscribeLocalEvent<ShadekinComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
    }

    private void OnInit(EntityUid uid, ShadekinComponent component, ComponentStartup args)
    {
        UpdateAlert(uid, component);
    }

    private void OnEyeColorChange(EntityUid uid, ShadekinComponent component, EyeColorInitEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            return;

        humanoid.EyeGlowing = false;
    }

    public void UpdateAlert(EntityUid uid, ShadekinComponent component)
    {
        _alerts.ShowAlert(uid, component.ShadekinAlert, (short) component.LightExposure);
    }

    private void OnShadekinAlert(Entity<ShadekinComponent> ent, ref ShadekinAlertEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<ActorComponent>(ent.Owner, out var actor))
        {
            var msg = Loc.GetString("shadekin-alert-" + ent.Comp.LightExposure);
            _chatManager.ChatMessageToOne(ChatChannel.Notifications, msg, msg, EntityUid.Invalid, false, actor.PlayerSession.Channel);
        }
        args.Handled = true;
    }

    private Angle GetAngle(EntityUid lightUid, SharedPointLightComponent lightComp, EntityUid targetUid)
    {
        var (lightPos, lightRot) = _transform.GetWorldPositionRotation(lightUid);
        lightPos += lightRot.RotateVec(lightComp.Offset);

        var (targetPos, targetRot) = _transform.GetWorldPositionRotation(targetUid);

        var mapDiff = targetPos - lightPos;

        var oppositeMapDiff = (-lightRot).RotateVec(mapDiff);
        var angle = oppositeMapDiff.ToWorldAngle();

        // HardLight: `angle == double.NaN` is always false; use IsNaN. Also reparenthesise
        // so the parent-child containment short-circuit applies in either direction
        // (previously the trailing `|| ContainsEntity(lightUid, targetUid)` always won
        // due to operator precedence regardless of angle / first containment check).
        if (double.IsNaN(angle)
            || _transform.ContainsEntity(targetUid, lightUid)
            || _transform.ContainsEntity(lightUid, targetUid))
        {
            angle = 0f;
        }

        return angle;
    }

    /// <summary>
    /// Return an illumination float value with is how many "energy" of light is hitting our ent.
    /// WARNING: This function might be expensive, Avoid calling it too much and CACHE THE RESULT!
    /// </summary>
    /// <param name="uid"></param>
    /// <returns></returns>
    public float GetLightExposure(EntityUid uid)
    {
        var illumination = 0f;

        var lightQuery = _lookup.GetEntitiesInRange<PointLightComponent>(Transform(uid).Coordinates, 20, LookupFlags.Uncontained);

        foreach (var light in lightQuery)
        {
            if (HasComp<DarkLightComponent>(light))
                continue;

            if (!light.Comp.Enabled
                || light.Comp.Radius < 1
                || light.Comp.Energy <= 0)
                continue;

            var (lightPos, lightRot) = _transform.GetWorldPositionRotation(light);
            lightPos += lightRot.RotateVec(light.Comp.Offset);

            if (!_examine.InRangeUnOccluded(light, uid, light.Comp.Radius, null))
                continue;

            Transform(uid).Coordinates.TryDistance(EntityManager, Transform(light).Coordinates, out var dist);

            var denom = dist / light.Comp.Radius;
            var attenuation = 1 - (denom * denom);
            var calculatedLight = 0f;

            if (light.Comp.MaskPath is not null)
            {
                var angleToTarget = GetAngle(light, light.Comp, uid);
                if (!lightMasks.TryGetValue(light.Comp.MaskPath, out var cones))
                {
                    calculatedLight = light.Comp.Energy * attenuation * attenuation;
                    illumination += calculatedLight;
                    continue;
                }

                foreach (var cone in cones)
                {
                    var coneLight = 0f;
                    var angleAttenuation = (float)Math.Min((float)Math.Max(cone.OuterWidth - angleToTarget, 0f), cone.InnerWidth) / cone.OuterWidth;

                    if (angleToTarget.Degrees - cone.Direction > cone.OuterWidth)
                        continue;
                    else if (angleToTarget.Degrees - cone.Direction > cone.InnerWidth
                        && angleToTarget.Degrees - cone.Direction < cone.OuterWidth)
                        coneLight = light.Comp.Energy * attenuation * attenuation * angleAttenuation;
                    else
                        coneLight = light.Comp.Energy * attenuation * attenuation;

                    calculatedLight = Math.Max(calculatedLight, coneLight);
                }
            }
            else
                calculatedLight = light.Comp.Energy * attenuation * attenuation;

            illumination += calculatedLight; //Math.Max(illumination, calculatedLight);
        }

        return illumination;
    }

    private void ToggleNightVision(EntityUid uid, float state)
    {
        if (state > 0)
            RemComp<NightVisionComponent>(uid);
        else
            EnsureComp<NightVisionComponent>(uid);
    }

    private void ApplyLightDamage(EntityUid uid, float state)
    {
        var threshold = TryComp<LightSensitivityComponent>(uid, out var sensitivity)
            ? sensitivity.BurnThreshold
            : 4;

        if (state < threshold)
            return;

        var multiplier = (int) state - threshold + 1;
        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Heat", multiplier);
        _damageable.TryChangeDamage(uid, damage, true, false);
    }

    private void OnRefreshMovementSpeedModifiers(EntityUid uid, ShadekinComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (TryComp<LightSensitivityComponent>(uid, out var sensitivity))
        {
            if (component.LightExposure < sensitivity.SlowdownThreshold)
                return;

            args.ModifySpeed(sensitivity.SpeedMultiplier, sensitivity.SpeedMultiplier);
            return;
        }

        if (component.LightExposure < 4)
            return;

        args.ModifySpeed(BaseSlowdownMultiplier, BaseSlowdownMultiplier);
    }

    private void ApplyDimLightHealing(EntityUid uid, ShadekinComponent component)
    {
        // Only fires in dim light (level 1). Total darkness is handled exclusively by ShadekinRegenerationSystem.
        if (component.LightExposure != 1)
            return;

        if (!_mobState.IsAlive(uid))
            return;

        if (!TryComp<DamageableComponent>(uid, out var damageable) || damageable.TotalDamage <= 0)
            return;

        var heal = new DamageSpecifier();
        heal.DamageDict.Add("Heat", -0.03f);
        heal.DamageDict.Add("Blunt", -0.03f);
        heal.DamageDict.Add("Slash", -0.03f);
        heal.DamageDict.Add("Piercing", -0.03f);
        _damageable.TryChangeDamage(uid, heal, true, false, damageable);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShadekinComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime < component.NextUpdate)
                continue;

            component.NextUpdate = _timing.CurTime + component.UpdateCooldown;

            var lightExposure = 0f;

            if (!_container.IsEntityInContainer(uid))
                lightExposure = GetLightExposure(uid);

            if (lightExposure >= 15f)
                component.LightExposure = 4;
            else if (lightExposure >= 10f)
                component.LightExposure = 3;
            else if (lightExposure >= 5f)
                component.LightExposure = 2;
            else if (lightExposure >= 0.8f)
                component.LightExposure = 1;
            else
                component.LightExposure = 0;

            ToggleNightVision(uid, component.LightExposure);
            ApplyLightDamage(uid, component.LightExposure);
            ApplyDimLightHealing(uid, component);
            _speed.RefreshMovementSpeedModifiers(uid);

            UpdateAlert(uid, component);
        }
    }
}
