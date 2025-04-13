using System;
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
        data.TestValue = UnityEngine.Random.Range(0, 100);
    }


}
// DicTestDataクラスがない場合のダミー定義
// 実際の環境ではすでに定義されているものを使用
[System.Serializable]
public struct DicTestData
{
    public int TestValue;
}
