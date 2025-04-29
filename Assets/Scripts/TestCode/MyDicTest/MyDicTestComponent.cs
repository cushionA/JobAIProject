using UnityEngine;

/// <summary>
/// 自作コレクションテスト用のコンポーネント。<br></br>
/// GetComponetに相当する処理で文字列を取得するだけ。
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
/// 自作コレクションをテストするためのデータ
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
