using UnityEngine;

[CreateAssetMenu(fileName = "AsyncTestStatus", menuName = "Scriptable Objects/AsyncTestStatus")]
public class AsyncTestStatus : ScriptableObject
{
    /// <summary>
    /// ���f���Ȃ����Ԋu�i�b�j
    /// </summary>
    public float judgeInterval;

    /// <summary>
    /// �ړ��X�s�[�h�B
    /// </summary>
    public float xSpeed;
}
