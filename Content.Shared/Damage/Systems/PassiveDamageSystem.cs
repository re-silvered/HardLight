using Content.Shared.Damage.Components;
using Content.Shared.Mobs; // Hardlight
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Shared.Damage;

public sealed class PassiveDamageSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PassiveDamageComponent, MapInitEvent>(OnPendingMapInit);
    }

    private void OnPendingMapInit(EntityUid uid, PassiveDamageComponent component, MapInitEvent args)
    {
        component.NextDamage = _timing.CurTime + GetInterval(component);

        // Hardlight start
        foreach (var stack in component.Stacks)
        {
            stack.NextDamage = _timing.CurTime + GetInterval(stack);
        }

        component.NextUpdate = GetNextUpdate(component);
        // Hardlight end
    }

    // Every tick, attempt to damage entities
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var curTime = _timing.CurTime;

        // Go through every entity with the component
        var query = EntityQueryEnumerator<PassiveDamageComponent, DamageableComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var damage, out var mobState))
        {
            if (comp.NextUpdate > curTime)
                continue;

            // Hardlight start
            // Make sure they're up for a damage tick
            if (comp.NextDamage <= curTime)
            {
                comp.NextDamage = curTime + GetInterval(comp);

                if (CanApply(comp.Damage, comp.DamageCap, damage))
                    TryApply(uid, comp.AllowedStates, comp.Damage, damage, mobState);
            }

            foreach (var stack in comp.Stacks)
            {
                if (stack.NextDamage <= TimeSpan.Zero)
                    stack.NextDamage = curTime + GetInterval(stack);

                if (stack.NextDamage > curTime)
                    continue;

                stack.NextDamage = curTime + GetInterval(stack);

                if (!CanApply(stack.Damage, stack.DamageCap, damage))
                    continue;

                TryApply(uid, stack.AllowedStates, stack.Damage, damage, mobState);
            }

            comp.NextUpdate = GetNextUpdate(comp);
            // Hardlight end
        }
    }

    /// <summary>
    /// Hardlight: Checks if the entity is in an allowed state (alive, crit, dead) and applies damage if so.
    /// </summary>
    private void TryApply(
        EntityUid uid,
        List<MobState> allowedStates,
        DamageSpecifier passiveDamage,
        DamageableComponent damage,
        MobStateComponent mobState)
    {
        foreach (var allowedState in allowedStates)
        {
            if (allowedState == mobState.CurrentState)
                _damageable.TryChangeDamage(uid, passiveDamage, true, false, damage);
        }
    }

    private static bool CanApply(DamageSpecifier passiveDamage, FixedPoint2 damageCap, DamageableComponent damage)
    {
        return damageCap == 0 || !passiveDamage.AnyPositive() || damage.TotalDamage < damageCap;
    }

    private static TimeSpan GetInterval(PassiveDamageComponent component)
    {
        return TimeSpan.FromSeconds(Math.Max(0.1f, component.Interval));
    }

    // Hardlight start
    private static TimeSpan GetInterval(PassiveDamageStackEntry entry)
    {
        return TimeSpan.FromSeconds(Math.Max(0.1f, entry.Interval));
    }

    private static TimeSpan GetNextUpdate(PassiveDamageComponent component)
    {
        var nextUpdate = component.NextDamage;

        foreach (var stack in component.Stacks)
        {
            if (stack.NextDamage <= TimeSpan.Zero)
                return TimeSpan.Zero;

            if (stack.NextDamage < nextUpdate)
                nextUpdate = stack.NextDamage;
        }

        return nextUpdate;
    }
    // Hardlight end
}
