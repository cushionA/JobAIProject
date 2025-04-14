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
using System.ComponentModel;
using System.IO;

public class CharacterDataDictionaryPerformanceTest
{
    private GameObject[] gameObjects;
    private DicTestData[] components1;
    private MyDicTestComponent[] components2; // T2のマネージド型コンポーネント

    // テスト設定
    private int objectCount = 50000;  // テスト用オブジェクト数
    private int warmupCount = 3;      // ウォームアップ回数
    private int measurementCount = 10; // テスト計測回数

    private Dictionary<GameObject, DicTestData> standardDictionary;
    private CharaDataDic<DicTestData> customDictionary;
    private CharacterDataDictionary<DicTestData, MyDicTestComponent> dualDictionary;

    // 最適化防止用の変数
    private long dataSum;
    private long customSum;
    private long charaSum;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // テストオブジェクトのロードと生成
        gameObjects = new GameObject[objectCount];
        components1 = new DicTestData[objectCount];
        components2 = new MyDicTestComponent[objectCount];

        // 複数のオブジェクトを並列でインスタンス化
        var tasks = new List<AsyncOperationHandle<GameObject>>(objectCount);

        for ( int i = 0; i < gameObjects.Length; i++ )
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
        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            gameObjects[i] = tasks[i].Result;
            gameObjects[i].name = $"TestObject_{i}";

            components2[i] = gameObjects[i].GetComponent<MyDicTestComponent>(); // 任意のMyDicTestComponentコンポーネント
            components2[i].IDSet();
            components1[i] = components2[i].data;

            // GetComponentが非nullであることを確認
            Assert.IsNotNull(components1[i], $"オブジェクト {i} の DicTestData が見つかりません");
            Assert.IsNotNull(components2[i], $"オブジェクト {i} の MyDicTestComponent が見つかりません");
        }

        // 辞書の初期化
        standardDictionary = new Dictionary<GameObject, DicTestData>(objectCount);
        customDictionary = new CharaDataDic<DicTestData>(objectCount);
        dualDictionary = new CharacterDataDictionary<DicTestData, MyDicTestComponent>(objectCount);

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

        dataSum = 0;

        PrepareSequentialTest();

        string[] matchingTest = new string[objectCount + 1];
        int unMatchCount = 0;

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            dataSum += standardDictionary[gameObjects[i]].TestValue;
            customSum += customDictionary[gameObjects[i]].TestValue;
            charaSum += dualDictionary[gameObjects[i]].TestValue;

            // 一致しない場合
            if ( standardDictionary[gameObjects[i]].TestValue != customDictionary[gameObjects[i]].TestValue
                || dualDictionary[gameObjects[i]].TestValue != customDictionary[gameObjects[i]].TestValue )
            {
                unMatchCount++;
                matchingTest[i + 1] = $"結果：不一致 (Dictionary：{standardDictionary[gameObjects[i]].TestValue}) CharaDataDic：({customDictionary[gameObjects[i]].TestValue}) CharacterDataDic：({dualDictionary[gameObjects[i]].TestValue})";
            }
            else
            {
                matchingTest[i + 1] = $"結果：一致 ({standardDictionary[gameObjects[i]].TestValue})";
            }

        }

        matchingTest[0] = $"不一致:{unMatchCount} 件";

        string absolutePath = Path.Combine(
Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop\\qiita記事\\自作コレクション検証\\整合性テスト2結果.txt");

        // ファイルに新しい行を追加する
        using ( StreamWriter sw = new StreamWriter(absolutePath, true) )
        {
            // 結果サマリーを出力
            for ( int i = 0; i < matchingTest.Length; i++ )
            {
                sw.WriteLine(matchingTest[i]);
            }
            sw.WriteLine(string.Empty);
        }

        // 辞書のクリーンアップ
        standardDictionary.Clear();
        customDictionary.Dispose();
        dualDictionary.Dispose();



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
                standardDictionary.Add(gameObjects[i], components1[i]);
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
                customDictionary.Add(gameObjects[i], components1[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_03_Add_CharacterDataDictionary()
    {
        Measure.Method(() =>
        {
            dualDictionary.Clear();
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                dualDictionary.Add(gameObjects[i], components1[i], components2[i]);
            }
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    //
    // 順次アクセステスト
    //

    [Test, Performance]
    public void Test_04_SequentialAccess_StandardDictionary()
    {
        // テスト前の準備
        PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            // ゲームオブジェクトから値を取得して、localSumに加算していく。
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
    public void Test_06_SequentialAccess_CharacterDataDictionary()
    {
        // テスト前の準備
        PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                // T1型データのみアクセス
                DicTestData comp = dualDictionary[gameObjects[i]];
                localSum += comp.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_07_SequentialAccess_CharacterDataDictionary_T2()
    {
        // テスト前の準備
        PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                dualDictionary.TryGetValue(gameObjects[i], out MyDicTestComponent comp2, out int index);
                localSum += comp2.data.TestValue;
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

    [Test, Performance]
    public void Test_10_RandomAccess_StandardDictionary()
    {
        List<int> randomIndices = PrepareRandomTest();

        // 事前準備：辞書に要素を追加
        standardDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            standardDictionary.Add(gameObjects[i], components1[i]);
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
    public void Test_11_RandomAccess_CustomDictionary()
    {
        List<int> randomIndices = PrepareRandomTest();

        // 事前準備：辞書に要素を追加
        customDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            customDictionary.Add(gameObjects[i], components1[i]);
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

    [Test, Performance]
    public void Test_12_RandomAccess_CharacterDataDictionary()
    {
        List<int> randomIndices = PrepareRandomTest();

        // 事前準備：辞書に要素を追加
        dualDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            dualDictionary.Add(gameObjects[i], components1[i], components2[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                dualDictionary.TryGetValue(gameObjects[idx], out DicTestData comp1, out int index);
                localSum += comp1.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_13_RandomAccess_CharacterDataDictionary_T2()
    {
        List<int> randomIndices = PrepareRandomTest();

        // 事前準備：辞書に要素を追加
        dualDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            dualDictionary.Add(gameObjects[i], components1[i], components2[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                dualDictionary.TryGetValue(gameObjects[idx], out MyDicTestComponent comp2, out int index);
                localSum += comp2.data.TestValue;
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }


    //
    // ヘルパーメソッド
    //

    private void PrepareSequentialTest()
    {
        // 事前準備：辞書に要素を追加
        standardDictionary.Clear();
        customDictionary.Clear();
        dualDictionary.Clear();

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            standardDictionary.Add(gameObjects[i], components1[i]);
            customDictionary.Add(gameObjects[i], components1[i]);
            dualDictionary.Add(gameObjects[i], components1[i], components2[i]);
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