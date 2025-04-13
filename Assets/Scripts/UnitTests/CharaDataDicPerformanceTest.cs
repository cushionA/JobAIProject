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

public class CharaDataDicPerformanceTest
{
    private GameObject[] gameObjects;
    private DicTestData[] components;

    // テスト設定
    private int objectCount = 50000;  // テスト用オブジェクト数
    private int warmupCount = 3;      // ウォームアップ回数
    private int measurementCount = 10; // テスト計測回数

    private Dictionary<GameObject, DicTestData> standardDictionary;
    private CharaDataDic<DicTestData> customDictionary;

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

        // 辞書の初期化
        standardDictionary = new Dictionary<GameObject, DicTestData>(objectCount);
        customDictionary = new CharaDataDic<DicTestData>(objectCount);

        // 初期値をクリア
        dataSum = 0;
    }

    [TearDown]
    public void TearDown()
    {
        // 生成したオブジェクトの破棄
        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            if ( gameObjects[i] != null )
            {
                Addressables.ReleaseInstance(gameObjects[i]);
            }
        }

        // 辞書のクリーンアップ
        standardDictionary.Clear();
        customDictionary.Dispose();

        // 最後に出力することで過剰最適化を防ぐ
        UnityEngine.Debug.Log($"テスト結果確認用データ合計: {dataSum}");
    }

    //
    // 要素追加テスト
    //

    [Test, Performance]
    public void Test_01_Add_StandardDictionary()
    {
        Measure.Method(() =>
        {
            standardDictionary.Clear();
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                standardDictionary.Add(gameObjects[i], components[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_02_Add_CustomDictionary()
    {
        Measure.Method(() =>
        {
            customDictionary.Clear();
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                customDictionary.Add(gameObjects[i], components[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    //
    // 順次アクセステスト
    //

    //[Test, Performance]
    //public void Test_03_SequentialAccess_GetComponent()
    //{
    //    // テスト前の準備
    //    PrepareSequentialTest();

    //    Measure.Method(() =>
    //    {
    //        int localSum = 0;
    //        for ( int i = 0; i < gameObjects.Length; i++ )
    //        {
    //            DicTestData comp = gameObjects[i].GetComponent<DicTestData>();
    //            localSum += comp.TestValue;
    //        }
    //        dataSum += localSum;
    //    })
    //    .WarmupCount(warmupCount)
    //    .MeasurementCount(measurementCount)
    //    .Run();
    //}

    [Test, Performance]
    public void Test_04_SequentialAccess_StandardDictionary()
    {
        // テスト前の準備
        PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                DicTestData comp = standardDictionary[gameObjects[i]];
                localSum += comp.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_05_SequentialAccess_CustomDictionary()
    {
        // テスト前の準備
        PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                DicTestData comp = customDictionary[gameObjects[i]];
                localSum += comp.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_06_SequentialAccess_CustomDictionary_DirectIndex()
    {
        // テスト前の準備
        standardDictionary.Clear();
        customDictionary.Clear();

        List<int> valueIndices = new List<int>(objectCount);

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            standardDictionary.Add(gameObjects[i], components[i]);
            int index = customDictionary.Add(gameObjects[i], components[i]);
            valueIndices.Add(index);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < valueIndices.Count; i++ )
            {
                int index = valueIndices[i];
                ref DicTestData comp = ref customDictionary.GetDataByIndex(index);
                localSum += comp.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    //
    // ランダムアクセステスト
    //

    //[Test, Performance]
    //public void Test_07_RandomAccess_GetComponent()
    //{
    //    List<int> randomIndices = PrepareRandomTest();

    //    Measure.Method(() =>
    //    {
    //        int localSum = 0;
    //        for ( int i = 0; i < gameObjects.Length; i++ )
    //        {
    //            int idx = randomIndices[i];
    //            DicTestData comp = gameObjects[idx].GetComponent<DicTestData>();
    //            localSum += comp.TestValue;
    //        }
    //        dataSum += localSum;
    //    })
    //    .WarmupCount(warmupCount)
    //    .MeasurementCount(measurementCount)
    //    .Run();
    //}

    [Test, Performance]
    public void Test_08_RandomAccess_StandardDictionary()
    {
        List<int> randomIndices = PrepareRandomTest();

        // 事前準備：辞書に要素を追加
        standardDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            standardDictionary.Add(gameObjects[i], components[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                DicTestData comp = standardDictionary[gameObjects[idx]];
                localSum += comp.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_09_RandomAccess_CustomDictionary()
    {
        List<int> randomIndices = PrepareRandomTest();

        // 事前準備：辞書に要素を追加
        customDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            customDictionary.Add(gameObjects[i], components[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                DicTestData comp = customDictionary[gameObjects[idx]];
                localSum += comp.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    ////
    //// メモリ使用量テスト
    ////

    //[Test, Performance]
    //public void Test_10_MemoryUsage()
    //{
    //    // メモリ使用量の測定には、GCとスタックトレースを使用
    //    // 注意：これは完全に正確ではありませんが、傾向を把握するのに役立ちます

    //    // 標準Dictionaryのメモリ使用量
    //    standardDictionary.Clear();
    //    System.GC.Collect();
    //    System.GC.WaitForPendingFinalizers();
    //    System.GC.Collect();
    //    long beforeStandard = System.GC.GetTotalMemory(true);

    //    for ( int i = 0; i < gameObjects.Length; i++ )
    //    {
    //        standardDictionary.Add(gameObjects[i], components[i]);
    //    }

    //    long afterStandard = System.GC.GetTotalMemory(true);
    //    long standardMemory = afterStandard - beforeStandard;

    //    // カスタムDictionaryのメモリ使用量
    //    customDictionary.Dispose();
    //    customDictionary = new CharaDataDic<DicTestData>(objectCount);
    //    System.GC.Collect();
    //    System.GC.WaitForPendingFinalizers();
    //    System.GC.Collect();
    //    long beforeCustom = System.GC.GetTotalMemory(true);

    //    for ( int i = 0; i < gameObjects.Length; i++ )
    //    {
    //        customDictionary.Add(gameObjects[i], components[i]);
    //    }

    //    long afterCustom = System.GC.GetTotalMemory(true);
    //    long customMemory = afterCustom - beforeCustom;

    //    // 結果をUnityTestのContextに記録
    //    UnityEngine.Debug.Log($"オブジェクト数: {objectCount}");
    //    UnityEngine.Debug.Log($"標準Dictionary メモリ使用量: {standardMemory} バイト");
    //    UnityEngine.Debug.Log($"カスタムDictionary メモリ使用量: {customMemory} バイト");
    //    UnityEngine.Debug.Log($"メモリ使用量差分: {standardMemory - customMemory} バイト");
    //    UnityEngine.Debug.Log($"メモリ削減率: {(1.0f - (float)customMemory / standardMemory) * 100}%");

    //    // 結果をPerformanceTest.Reportに記録
    //    Measure.Custom("標準Dictionary(バイト)", standardMemory);
    //    Measure.Custom("カスタムDictionary(バイト)", customMemory);
    //    Measure.Custom("差分(バイト)", standardMemory - customMemory);
    //}

    //
    // ヘルパーメソッド
    //

    private void PrepareSequentialTest()
    {
        // 事前準備：辞書に要素を追加
        standardDictionary.Clear();
        customDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            standardDictionary.Add(gameObjects[i], components[i]);
            customDictionary.Add(gameObjects[i], components[i]);
        }
    }

    private List<int> PrepareRandomTest()
    {

        // シャッフルしたインデックスを作成
        List<int> randomIndices = new List<int>(objectCount);
        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            randomIndices.Add(i);
        }

        // Fisher-Yates シャッフル
        System.Random random = new System.Random(42); // 再現性のために固定シード
        for ( int i = randomIndices.Count - 1; i > 0; i-- )
        {
            int j = random.Next(0, i + 1);
            int temp = randomIndices[i];
            randomIndices[i] = randomIndices[j];
            randomIndices[j] = temp;
        }

        return randomIndices;
    }
}