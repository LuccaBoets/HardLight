using Content.Shared._HL.Aphrodisiac;
using Content.Shared.StatusEffect;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._HL.Aphrodisiac;

public sealed class AphrodisiacOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _aphrodisiacShader;

    public float CurrentAphrodisiacPower = 0.0f;

    private const float VisualThreshold = 0.0f;
    private const float PowerDivisor = 5.0f;

    private float _visualScale = 0;

    public AphrodisiacOverlay()
    {
        IoCManager.InjectDependencies(this);
        _aphrodisiacShader = _prototypeManager.Index(AphrodisiacShaderId).InstanceUnique();
    }

    private static readonly ProtoId<ShaderPrototype> AphrodisiacShaderId = "Aphrodisiac";

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var playerEntity = _playerManager.LocalEntity;

        if (playerEntity == null)
            return;

        if (!_entityManager.HasComponent<AphrodisiacStatusEffectComponent>(playerEntity)
            || !_entityManager.TryGetComponent<StatusEffectsComponent>(playerEntity, out var status))
            return;

        var statusSys = _sysMan.GetEntitySystem<StatusEffectsSystem>();
        if (!statusSys.TryGetTime(playerEntity.Value, SharedAphrodisiacSystem.AphrodisiacKey, out var time, status))
            return;

        var curTime = _timing.CurTime;
        var power = (float) (time.Value.Item2 - curTime).TotalSeconds;

        CurrentAphrodisiacPower += 1f * (power * 0.5f - CurrentAphrodisiacPower) * args.DeltaSeconds / (power + 1);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        var visualPower = CurrentAphrodisiacPower;

        _visualScale = AphrodisiacPowerToVisual(visualPower);
        return _visualScale > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _aphrodisiacShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _aphrodisiacShader.SetParameter("aphrodisiacPower", _visualScale);
        _aphrodisiacShader.SetParameter("alpha", 0.6f);
        _aphrodisiacShader.SetParameter("outerMultiplier", 8.0f);
        _aphrodisiacShader.SetParameter("minimumInnerOut", 0.3f);
        _aphrodisiacShader.SetParameter("minimumInnerIn", 0.1f);
        _aphrodisiacShader.SetParameter("pulseSpeed", 1.0f);
        handle.UseShader(_aphrodisiacShader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }

    /// <summary>
    ///     Converts the # of seconds the aphrodisiac effect lasts for (aphrodisiac power) to a percentage
    ///     used by the actual shader.
    /// </summary>
    private float AphrodisiacPowerToVisual(float aphrodisiacPower)
    {
        return Math.Clamp((aphrodisiacPower - VisualThreshold) / PowerDivisor, 0.0f, 1.0f);
    }
}