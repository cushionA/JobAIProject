using System.ComponentModel;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using static CombatManager;

[BurstCompile]
public class HateJudgeJob : IJobParallelFor
{

    public UnsafeList<AIEventContainer> hateEvents;

    [WriteOnly]
    public NativeArray<int> teamHate;

    /// <summary>
    /// “G–¡•û”»•Ê—p‚ÌBit
    /// </summary>
    [Unity.Collections.ReadOnly]
    public int relationmap;

    public void Execute(int index)
    {
        for ( int i = 0; i < hateEvents.Length; i++ )
        {
            if ( true )
            {

            }
        }
    }
}
