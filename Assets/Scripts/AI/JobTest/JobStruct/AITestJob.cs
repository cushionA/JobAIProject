using Unity.Jobs;
using UnityEngine;
using Unity.Collections;

public class AITestJob : IJobParallelFor
{
    /// <summary>
    /// �ǂݎ���p�̃`�[�����Ƃ̑S�̃w�C�g
    /// </summary>
    [ReadOnly]
    public NativeArray<NativeHashMap<int, int>> teamHate;



    public void Execute(int index)
    {
        throw new System.NotImplementedException();
    }
}
