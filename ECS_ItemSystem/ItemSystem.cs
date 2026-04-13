using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct ItemSystem : ISystem
{
    private ComponentLookup<LocalTransform> _transformLookup;
    private ComponentLookup<PlayerTag> _playerLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ItemComponent>();

        _transformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
        _playerLookup = state.GetComponentLookup<PlayerTag>(isReadOnly: true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _transformLookup.Update(ref state);
        _playerLookup.Update(ref state);

        var playerPositions = new NativeList<float3>(Allocator.TempJob);
        foreach (var (_, transform) in
                 SystemAPI.Query<RefRO<PlayerTag>, RefRO<LocalTransform>>())
        {
            playerPositions.Add(transform.ValueRO.Position);
        }

        foreach (var (item, pickup, transform, entity) in
                 SystemAPI.Query<RefRO<ItemComponent>,
                                 RefRW<PickupComponent>,
                                 RefRO<LocalTransform>>()
                          .WithEntityAccess())
        {
            if (!pickup.ValueRO.IsInteractable) continue;

            if (item.ValueRO.Type == ItemType.Medkit)
            {
                bool nearPlayer = IsAnyPlayerNearby(transform.ValueRO.Position,playerPositions, 2.5f);
                _ = nearPlayer;
            }

        }

        playerPositions.Dispose();
    }

    private static bool IsAnyPlayerNearby(float3 itemPos,NativeList<float3> players,float radius)
    {
        float r2 = radius * radius;
        for (int i = 0; i < players.Length; i++)
        {
            if (math.distancesq(itemPos, players[i]) <= r2)
                return true;
        }
        return false;
    }
}

public struct PlayerTag : IComponentData { }