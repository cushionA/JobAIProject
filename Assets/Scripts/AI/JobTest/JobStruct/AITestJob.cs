using Unity.Jobs;
using UnityEngine;
using Unity.Collections;

public class AITestJob : IJobParallelFor
{
    /// <summary>
    /// 読み取り専用のチームごとの全体ヘイト
    /// </summary>
    [ReadOnly]
    public NativeArray<NativeHashMap<int, int>> teamHate;



    public void Execute(int index)
    {
        throw new System.NotImplementedException();
    }
}
