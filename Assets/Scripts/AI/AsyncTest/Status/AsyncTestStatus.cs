using UnityEngine;

[CreateAssetMenu(fileName = "AsyncTestStatus", menuName = "Scriptable Objects/AsyncTestStatus")]
public class AsyncTestStatus : ScriptableObject
{
    /// <summary>
    /// 判断しなおす間隔（秒）
    /// </summary>
    public float judgeInterval;

    /// <summary>
    /// 移動スピード。
    /// </summary>
    public float xSpeed;
}
