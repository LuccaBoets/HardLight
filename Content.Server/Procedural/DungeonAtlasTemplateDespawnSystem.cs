using Robust.Shared.GameObjects;
using TimedDespawnComponent = Robust.Shared.Spawners.TimedDespawnComponent;

namespace Content.Server.Procedural;

/// <summary>
/// Ensures dungeon atlas template entities despawn after a fixed delay.
/// </summary>
public sealed class DungeonAtlasTemplateDespawnSystem : EntitySystem
{
    private static readonly TimeSpan DespawnDelay = TimeSpan.FromMinutes(30);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DungeonAtlasTemplateComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<DungeonAtlasTemplateComponent> ent, ref ComponentInit args)
    {
        var despawn = EnsureComp<TimedDespawnComponent>(ent);
        despawn.Lifetime = (float) DespawnDelay.TotalSeconds;
    }
}
