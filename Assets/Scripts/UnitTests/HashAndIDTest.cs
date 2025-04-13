using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using Unity.Collections;
using Unity.PerformanceTesting;
using System;

public class HashAndIDTest
{
    private GameObject[] gameObjects;
    private DicTestData[] components;

    // テスト設定
    private int objectCount = 50000;  // テスト用オブジェクト数
    private int warmupCount = 3;      // ウォームアップ回数
    private int measurementCount = 10; // テスト計測回数


    // 最適化防止用の変数
    private int dataSum;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // テストオブジェクトのロードと生成
        gameObjects = new GameObject[objectCount];
        components = new DicTestData[objectCount];

        // 複数のオブジェクトを並列でインスタンス化
        var tasks = new List<AsyncOperationHandle<GameObject>>(objectCount);

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            // Addressablesを使用してインスタンス化
            var task = Addressables.InstantiateAsync("MyDicTest");
            tasks.Add(task);
        }

        // すべてのオブジェクトが生成されるのを待つ
        foreach ( var task in tasks )
        {
            yield return task;
        }

        // 生成されたオブジェクトと必要なコンポーネントを取得
        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            gameObjects[i] = tasks[i].Result;
            gameObjects[i].name = $"TestObject_{i}";
            components[i] = gameObjects[i].GetComponent<DicTestData>();

            // GetComponentが非nullであることを確認
            Assert.IsNotNull(components[i], $"オブジェクト {i} の DicTestData が見つかりません");
        }
    }

    [TearDown]
    public void TearDown()
    {

        // 最後に出力することで過剰最適化を防ぐ
        UnityEngine.Debug.Log($"テスト結果確認用データ合計: {dataSum}");
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
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                localSum += gameObjects[i].GetInstanceID();
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_01_GetHashCode_Test()
    {

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                localSum += gameObjects[i].GetHashCode();
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

}