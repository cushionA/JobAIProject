using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using static CombatManager;

[BurstCompile]
public class HateJudgeJob : IJobParallelFor
{

    public UnsafeList<BrainEventContainer> hateEvents;

    [WriteOnly]
    public NativeArray<int> teamHate;

    /// <summary>
    /// �G�������ʗp��Bit
    /// </summary>
    [Unity.Collections.ReadOnly]
    public int relationmap;

    public void Execute(int index)
    {
        for ( int i = 0; i < this.hateEvents.Length; i++ )
        {
            if ( true )
            {

            }
        }
    }
}
