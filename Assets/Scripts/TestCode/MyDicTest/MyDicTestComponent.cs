using UnityEngine;

/// <summary>
/// ����R���N�V�����e�X�g�p�̃R���|�[�l���g�B<br></br>
/// GetComponet�ɑ������鏈���ŕ�������擾���邾���B
/// </summary>
public class MyDicTestComponent : MonoBehaviour
{

    public DicTestData data;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void IDSet()
    {
        this.data.TestValue = UnityEngine.Random.Range(0, 100);
    }

}

/// <summary>
/// ����R���N�V�������e�X�g���邽�߂̃f�[�^
/// </summary>
[System.Serializable]
public struct DicTestData : ILogicalDelate
{
    public int TestValue;

    public bool isLogicalDelate;

    public bool IsLogicalDelate()
    {
        return this.isLogicalDelate;
    }

    public void LogicalDelete()
    {
        this.isLogicalDelate = true;
    }
}
