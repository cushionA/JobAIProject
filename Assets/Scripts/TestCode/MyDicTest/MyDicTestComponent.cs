using UnityEngine;

/// <summary>
/// ����R���N�V�����e�X�g�p�̃R���|�[�l���g�B<br></br>
/// GetComponet�ɑ������鏈���ŕ�������擾���邾���B
/// </summary>
public class MyDicTestComponent : MonoBehaviour
{

    public DicTestData data;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // �K���ȕ�����𐶐��B
        data.TestValue = $"{this.gameObject.name} {Time.deltaTime}".GetHashCode();
    }

    // Update is called once per frame
    void Update()
    {

    }
}

public struct DicTestData
{
    // �O�����炱�����擾���Ă��B
    public int TestValue;
}
