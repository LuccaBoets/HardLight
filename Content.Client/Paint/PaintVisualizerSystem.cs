using System.Linq;
using Robust.Client.GameObjects;
using static Robust.Client.GameObjects.SpriteComponent;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Content.Shared.Paint;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Client.Paint
{
    public sealed class PaintedVisualizerSystem : VisualizerSystem<PaintedComponent>
    {
        private const string GreyscaleShader = "Greyscale"; // HardLight
        private const string DisplacedStencilDrawShader = "DisplacedStencilDraw"; // HardLight
        private const string DisplacedGreyscaleShader = "DisplacedGreyscale"; // HardLight

        /// <summary>
        /// Visualizer for Paint which applies a shader and colors the entity.
        /// </summary>

        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly IPrototypeManager _protoMan = default!;
        [Dependency] private readonly SpriteSystem _sprite = default!;

        public ShaderInstance? Shader; // in Robust.Client.Graphics so cannot move to shared component.

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<PaintedComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
            SubscribeLocalEvent<PaintedComponent, HeldVisualsUpdatedEvent>(OnHeldVisualsUpdated);
            SubscribeLocalEvent<PaintedComponent, ComponentShutdown>(OnShutdown);
            SubscribeLocalEvent<PaintedComponent, EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
        }

        protected override void OnAppearanceChange(EntityUid uid, PaintedComponent component, ref AppearanceChangeEvent args)
        {
            ApplyPaintToSprite(uid, component, args.Sprite);
        }
        private void OnAfterAutoHandleState(EntityUid uid, PaintedComponent component, ref AfterAutoHandleStateEvent args)
        {
            if (!TryComp(uid, out SpriteComponent? sprite))
                return;

            ApplyPaintToSprite(uid, component, sprite);
        }
        private void ApplyPaintToSprite(EntityUid uid, PaintedComponent component, SpriteComponent? sprite)
        {
            if (sprite == null)
                return;

            if (!_appearance.TryGetData<bool>(uid, PaintVisuals.Painted, out var isPainted) || !isPainted)
                return;

            foreach (var spriteLayer in sprite.AllLayers)
            {
                if (spriteLayer is not Layer layer)
                    continue;

                var paintShaderPrototype = GetPaintShaderPrototype(component.ShaderName, layer);
                var canApplyPaint = layer.Shader == null
                    || layer.ShaderPrototype == component.ShaderName
                    || layer.ShaderPrototype == paintShaderPrototype
                    || layer.ShaderPrototype == DisplacedStencilDrawShader
                    || layer.ShaderPrototype == DisplacedGreyscaleShader;

                if (!canApplyPaint)
                    continue;

                // ShaderPrototype sadly in Robust.Client, cannot move to shared component.
                Shader = _protoMan.Index<ShaderPrototype>(paintShaderPrototype).Instance();
                layer.Shader = Shader;
                layer.Color = component.Color;
            }
        }

        private void OnHeldVisualsUpdated(EntityUid uid, PaintedComponent component, HeldVisualsUpdatedEvent args)
        {
            if (args.RevealedLayers.Count == 0)
                return;

            if (!TryComp(args.User, out SpriteComponent? sprite))
                return;

            foreach (var revealed in args.RevealedLayers)
            {
                if (!_sprite.LayerMapTryGet((args.User, sprite), revealed, out var layer, false) || // HardLight
                    sprite[layer] is not Layer spriteLayer || // Added spriteLayer
                    spriteLayer.CopyToShaderParameters != null)
                    continue;

                sprite.LayerSetShader(layer, GetPaintShaderPrototype(component.ShaderName, spriteLayer)); // HardLight: Added GetPaintShaderPrototype & spriteLayer
                _sprite.LayerSetColor((args.User, sprite), layer, component.Color);
            }
        }

        private void OnEquipmentVisualsUpdated(EntityUid uid, PaintedComponent component, EquipmentVisualsUpdatedEvent args)
        {
            if (args.RevealedLayers.Count == 0)
                return;

            if (!TryComp(args.Equipee, out SpriteComponent? sprite))
                return;

            foreach (var revealed in args.RevealedLayers)
            {
                if (!_sprite.LayerMapTryGet((args.Equipee, sprite), revealed, out var layer, false) || // HardLight
                    sprite[layer] is not Layer spriteLayer || // Added spriteLayer
                    spriteLayer.CopyToShaderParameters != null)
                    continue;

                sprite.LayerSetShader(layer, GetPaintShaderPrototype(component.ShaderName, spriteLayer)); // HardLight: Added GetPaintShaderPrototype & spriteLayer
                _sprite.LayerSetColor((args.Equipee, sprite), layer, component.Color);
            }
        }

        // HardLight: Preserve displacement-aware clothing rendering by using a combined shader
        // instead of replacing the displacement shader with plain greyscale paint.
        private static string GetPaintShaderPrototype(string defaultShader, Layer layer)
        {
            if (defaultShader == GreyscaleShader && layer.ShaderPrototype == DisplacedStencilDrawShader)
                return DisplacedGreyscaleShader;

            return defaultShader;
        }

        private void OnShutdown(EntityUid uid, PaintedComponent component, ref ComponentShutdown args)
        {
            if (!TryComp(uid, out SpriteComponent? sprite))
                return;

            component.BeforeColor = sprite.Color;
            Shader = _protoMan.Index<ShaderPrototype>(component.ShaderName).Instance();

            if (!Terminating(uid))
            {
                foreach (var spriteLayer in sprite.AllLayers)
                {
                    if (spriteLayer is not Layer layer)
                        continue;

                    if (layer.Shader == Shader) // If shader isn't same as one in component we need to ignore it.
                    {
                        layer.Shader = null;
                        if (layer.Color == component.Color) // If color isn't the same as one in component we don't want to change it.
                            layer.Color = component.BeforeColor;
                    }
                }
            }
        }
    }
}
