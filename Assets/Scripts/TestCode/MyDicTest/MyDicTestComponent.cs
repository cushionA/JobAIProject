using System;
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
        data.TestValue = UnityEngine.Random.Range(0, 100);
    }


}
// DicTestData�N���X���Ȃ��ꍇ�̃_�~�[��`
// ���ۂ̊��ł͂��łɒ�`����Ă�����̂��g�p
[System.Serializable]
public struct DicTestData
{
    public int TestValue;
}
