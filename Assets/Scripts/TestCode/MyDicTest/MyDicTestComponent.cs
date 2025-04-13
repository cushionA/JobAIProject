using UnityEngine;

/// <summary>
/// 自作コレクションテスト用のコンポーネント。<br></br>
/// GetComponetに相当する処理で文字列を取得するだけ。
/// </summary>
public class MyDicTestComponent : MonoBehaviour
{

    public DicTestData data;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // 適当な文字列を生成。
        data.TestValue = $"{this.gameObject.name} {Time.deltaTime}".GetHashCode();
    }

    // Update is called once per frame
    void Update()
    {

    }
}

public struct DicTestData
{
    // 外部からこいつを取得してやる。
    public int TestValue;
}
