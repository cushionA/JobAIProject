using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

public class HashAndIDTest
{
    private GameObject[] gameObjects;
    private DicTestData[] components;

    // テスト設定
    private int objectCount = 5000;  // テスト用オブジェクト数
    private int warmupCount = 3;      // ウォームアップ回数
    private int measurementCount = 10; // テスト計測回数

    // 最適化防止用の変数
    private int dataSum;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // テストオブジェクトのロードと生成
        this.gameObjects = new GameObject[this.objectCount];
        this.components = new DicTestData[this.objectCount];

        // 複数のオブジェクトを並列でインスタンス化
        var tasks = new List<AsyncOperationHandle<GameObject>>(this.objectCount);

        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            // Addressablesを使用してインスタンス化
            var task = Addressables.InstantiateAsync("Assets/Prefab/TestPrefab/MyDicTest.prefab");
            tasks.Add(task);
        }

        // すべてのオブジェクトが生成されるのを待つ
        foreach ( var task in tasks )
        {
            yield return task;
        }

        // 生成されたオブジェクトと必要なコンポーネントを取得
        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            this.gameObjects[i] = tasks[i].Result;
            this.gameObjects[i].name = $"TestObject_{i}";
            this.components[i] = this.gameObjects[i].GetComponent<DicTestData>();

            // GetComponentが非nullであることを確認
            Assert.IsNotNull(this.components[i], $"オブジェクト {i} の DicTestData が見つかりません");
        }
    }

    [TearDown]
    public void TearDown()
    {

        // 最後に出力することで過剰最適化を防ぐ
        UnityEngine.Debug.Log($"テスト結果確認用データ合計: {this.dataSum}");
    }

    //
    // 順次アクセステスト
    //

    [Test, Performance]
    public void Test_01_GetInstansID_Test()
    {
        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                localSum += this.gameObjects[i].GetInstanceID();
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_02_GetHashCode_Test()
    {

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                localSum += this.gameObjects[i].GetHashCode();
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_03_GetComponent_Test()
    {

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                localSum += this.gameObjects[i].GetComponent<DicTestData>().TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

}