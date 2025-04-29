using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

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
        this.gameObjects = new GameObject[this.objectCount];
        this.components1 = new DicTestData[this.objectCount];
        this.components2 = new MyDicTestComponent[this.objectCount];

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

            this.components2[i] = this.gameObjects[i].GetComponent<MyDicTestComponent>(); // 任意のMyDicTestComponentコンポーネント
            this.components2[i].IDSet();
            this.components1[i] = this.components2[i].data;

            // GetComponentが非nullであることを確認
            Assert.IsNotNull(this.components1[i], $"オブジェクト {i} の DicTestData が見つかりません");
            Assert.IsNotNull(this.components2[i], $"オブジェクト {i} の MyDicTestComponent が見つかりません");
        }

        // 辞書の初期化
        this.standardDictionary = new Dictionary<GameObject, DicTestData>(this.objectCount);
        this.customDictionary = new CharaDataDic<DicTestData>(this.objectCount);
        this.dualDictionary = new CharacterDataDictionary<DicTestData, MyDicTestComponent>(this.objectCount);

        // 初期値をクリア
        this.dataSum = 0;
    }

    [TearDown]
    public void TearDown()
    {
        // 生成したオブジェクトの破棄
        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            if ( this.gameObjects[i] != null )
            {
                _ = Addressables.ReleaseInstance(this.gameObjects[i]);
            }
        }

        this.dataSum = 0;

        this.PrepareSequentialTest();

        string[] matchingTest = new string[this.objectCount + 1];
        int unMatchCount = 0;

        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            this.dataSum += this.standardDictionary[this.gameObjects[i]].TestValue;
            this.customSum += this.customDictionary[this.gameObjects[i]].TestValue;
            this.charaSum += this.dualDictionary[this.gameObjects[i]].TestValue;

            // 一致しない場合
            if ( this.standardDictionary[this.gameObjects[i]].TestValue != this.customDictionary[this.gameObjects[i]].TestValue
                || this.dualDictionary[this.gameObjects[i]].TestValue != this.customDictionary[this.gameObjects[i]].TestValue )
            {
                unMatchCount++;
                matchingTest[i + 1] = $"結果：不一致 (Dictionary：{this.standardDictionary[this.gameObjects[i]].TestValue}) CharaDataDic：({this.customDictionary[this.gameObjects[i]].TestValue}) CharacterDataDic：({this.dualDictionary[this.gameObjects[i]].TestValue})";
            }
            else
            {
                matchingTest[i + 1] = $"結果：一致 ({this.standardDictionary[this.gameObjects[i]].TestValue})";
            }

        }

        matchingTest[0] = $"不一致:{unMatchCount} 件";

        string absolutePath = Path.Combine(
Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop\\qiita記事\\自作コレクション検証\\整合性テスト2結果.txt");

        // ファイルに新しい行を追加する
        using ( StreamWriter sw = new(absolutePath, true) )
        {
            // 結果サマリーを出力
            for ( int i = 0; i < matchingTest.Length; i++ )
            {
                sw.WriteLine(matchingTest[i]);
            }

            sw.WriteLine(string.Empty);
        }

        // 辞書のクリーンアップ
        this.standardDictionary.Clear();
        this.customDictionary.Dispose();
        this.dualDictionary.Dispose();

        // 最後に出力することで過剰最適化を防ぐ
        UnityEngine.Debug.Log($"テスト結果確認用データ合計: {this.dataSum}");
    }

    //
    // 要素追加テスト
    //

    [Test, Performance]
    public void Test_01_Add_StandardDictionary()
    {
        Measure.Method(() =>
        {
            this.standardDictionary.Clear();
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                this.standardDictionary.Add(this.gameObjects[i], this.components1[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_02_Add_CustomDictionary()
    {
        Measure.Method(() =>
        {
            this.customDictionary.Clear();
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                _ = this.customDictionary.Add(this.gameObjects[i], this.components1[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_03_Add_CharacterDataDictionary()
    {
        Measure.Method(() =>
        {
            this.dualDictionary.Clear();
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                _ = this.dualDictionary.Add(this.gameObjects[i], this.components1[i], this.components2[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    //
    // 順次アクセステスト
    //

    [Test, Performance]
    public void Test_04_SequentialAccess_StandardDictionary()
    {
        // テスト前の準備
        this.PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            // ゲームオブジェクトから値を取得して、localSumに加算していく。
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                DicTestData comp = this.standardDictionary[this.gameObjects[i]];
                localSum += comp.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_05_SequentialAccess_CustomDictionary()
    {
        // テスト前の準備
        this.PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                DicTestData comp = this.customDictionary[this.gameObjects[i]];
                localSum += comp.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_06_SequentialAccess_CharacterDataDictionary()
    {
        // テスト前の準備
        this.PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                // T1型データのみアクセス
                DicTestData comp = this.dualDictionary[this.gameObjects[i]];
                localSum += comp.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_07_SequentialAccess_CharacterDataDictionary_T2()
    {
        // テスト前の準備
        this.PrepareSequentialTest();

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                _ = this.dualDictionary.TryGetValue(this.gameObjects[i], out MyDicTestComponent comp2, out int index);
                localSum += comp2.data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    //
    // ランダムアクセステスト
    //

    [Test, Performance]
    public void Test_10_RandomAccess_StandardDictionary()
    {
        List<int> randomIndices = this.PrepareRandomTest();

        // 事前準備：辞書に要素を追加
        this.standardDictionary.Clear();

        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.components1[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                DicTestData comp = this.standardDictionary[this.gameObjects[idx]];
                localSum += comp.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_11_RandomAccess_CustomDictionary()
    {
        List<int> randomIndices = this.PrepareRandomTest();

        // 事前準備：辞書に要素を追加
        this.customDictionary.Clear();

        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            _ = this.customDictionary.Add(this.gameObjects[i], this.components1[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                DicTestData comp = this.customDictionary[this.gameObjects[idx]];
                localSum += comp.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_12_RandomAccess_CharacterDataDictionary()
    {
        List<int> randomIndices = this.PrepareRandomTest();

        // 事前準備：辞書に要素を追加
        this.dualDictionary.Clear();

        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            _ = this.dualDictionary.Add(this.gameObjects[i], this.components1[i], this.components2[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                _ = this.dualDictionary.TryGetValue(this.gameObjects[idx], out DicTestData comp1, out int index);
                localSum += comp1.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_13_RandomAccess_CharacterDataDictionary_T2()
    {
        List<int> randomIndices = this.PrepareRandomTest();

        // 事前準備：辞書に要素を追加
        this.dualDictionary.Clear();

        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            _ = this.dualDictionary.Add(this.gameObjects[i], this.components1[i], this.components2[i]);
        }

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                int idx = randomIndices[i];
                _ = this.dualDictionary.TryGetValue(this.gameObjects[idx], out MyDicTestComponent comp2, out int index);
                localSum += comp2.data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    //
    // ヘルパーメソッド
    //

    private void PrepareSequentialTest()
    {
        // 事前準備：辞書に要素を追加
        this.standardDictionary.Clear();
        this.customDictionary.Clear();
        this.dualDictionary.Clear();

        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.components1[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.components1[i]);
            _ = this.dualDictionary.Add(this.gameObjects[i], this.components1[i], this.components2[i]);
        }
    }

    private List<int> PrepareRandomTest()
    {
        // シャッフルしたインデックスを作成
        List<int> randomIndices = new(this.objectCount);
        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            randomIndices.Add(i);
        }

        // Fisher-Yates シャッフル
        System.Random random = new(42); // 再現性のために固定シード
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