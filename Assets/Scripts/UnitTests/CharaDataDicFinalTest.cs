using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

/// <summary>
/// CharaDataDicと標準Dictionaryの正確性とパフォーマンスを徹底的に比較するテスト
/// </summary>
[Serializable]
public class CharaDataDicFinalTest
{
    [SerializeField]
    public AssetReference testObj;

    // テスト設定
    private int objectCount = 50000;       // テスト用オブジェクト数
    private int warmupCount = 3;           // ウォームアップ回数
    private int measurementCount = 10;     // テスト計測回数
    private int deleteRatio = 30;          // 削除テストで削除する比率（％）
    private int accessTestRuns = 10000;    // アクセステストの繰り返し回数

    // テスト用オブジェクト
    private GameObject[] gameObjects;
    private DicTestData[] testData;
    private int[] gameObjectHashes;         // ハッシュコード保存用
    private HashSet<int> selectedForDeletion; // 削除用に選択されたインデックス

    // テスト対象のコレクション
    private Dictionary<GameObject, DicTestData> standardDictionary;
    private CharaDataDic<DicTestData> customDictionary;

    // 計測結果の一時保存用
    private string[] matchingTest;
    private int unMatchCount;
    private double stdAddTime;
    private double customAddTime;
    private double stdSequentialTime;
    private double customSequentialTime;
    private double stdRandomTime;
    private double customRandomTime;
    private double stdDeleteTime;
    private double customDeleteTime;

    // 最適化防止用の変数
    private long dataSum;
    private int errorCount;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // テストオブジェクトの生成
        this.gameObjects = new GameObject[this.objectCount];
        this.testData = new DicTestData[this.objectCount];
        this.gameObjectHashes = new int[this.objectCount];
        this.selectedForDeletion = new HashSet<int>();

        // 複数のオブジェクトを並列でインスタンス化
        var tasks = new List<AsyncOperationHandle<GameObject>>(this.objectCount);

        for ( int i = 0; i < this.objectCount; i++ )
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
        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.gameObjects[i] = tasks[i].Result;
            this.gameObjects[i].name = $"TestObject_{i}";

            var component = this.gameObjects[i].GetComponent<MyDicTestComponent>();
            component.IDSet();
            this.testData[i] = component.data;

            // ハッシュコードを保存
            this.gameObjectHashes[i] = this.gameObjects[i].GetHashCode();

            // GetComponentが非nullであることを確認
            Assert.IsNotNull(this.testData[i], $"オブジェクト {i} の DicTestData が見つかりません");
        }

        // 削除用のオブジェクトをランダムに選択
        int deleteCount = this.objectCount * this.deleteRatio / 100;
        this.selectedForDeletion.Clear();
        System.Random rnd = new(42); // 再現性のために固定シード
        while ( this.selectedForDeletion.Count < deleteCount )
        {
            _ = this.selectedForDeletion.Add(rnd.Next(0, this.objectCount));
        }

        // 辞書の初期化
        this.standardDictionary = new Dictionary<GameObject, DicTestData>(this.objectCount);
        this.customDictionary = new CharaDataDic<DicTestData>(this.objectCount);

        // 初期値をクリア
        this.dataSum = 0;
        this.errorCount = 0;
        this.stdAddTime = 0;
        this.customAddTime = 0;
        this.stdSequentialTime = 0;
        this.customSequentialTime = 0;
        this.stdRandomTime = 0;
        this.customRandomTime = 0;
        this.stdDeleteTime = 0;
        this.customDeleteTime = 0;
    }

    [TearDown]
    public void TearDown()
    {
        // 生成したオブジェクトの破棄
        for ( int i = 0; i < this.objectCount; i++ )
        {
            if ( this.gameObjects[i] != null )
            {
                _ = Addressables.ReleaseInstance(this.gameObjects[i]);
            }
        }

        // 辞書のクリーンアップ
        this.standardDictionary.Clear();
        this.customDictionary.Dispose();

        // 結果サマリーを出力
        UnityEngine.Debug.Log($"テスト結果確認用データ合計: {this.dataSum}");
        UnityEngine.Debug.Log($"エラー数: {this.errorCount}");
        UnityEngine.Debug.Log("=== パフォーマンス比較 ===");
        UnityEngine.Debug.Log($"要素追加: 標準Dictionary {this.stdAddTime:F3}ms vs CharaDataDic {this.customAddTime:F3}ms (比率: {this.customAddTime / this.stdAddTime:P})");
        UnityEngine.Debug.Log($"順次アクセス: 標準Dictionary {this.stdSequentialTime:F3}ms vs CharaDataDic {this.customSequentialTime:F3}ms (比率: {this.customSequentialTime / this.stdSequentialTime:P})");
        UnityEngine.Debug.Log($"ランダムアクセス: 標準Dictionary {this.stdRandomTime:F3}ms vs CharaDataDic {this.customRandomTime:F3}ms (比率: {this.customRandomTime / this.stdRandomTime:P})");
        UnityEngine.Debug.Log($"要素削除: 標準Dictionary {this.stdDeleteTime:F3}ms vs CharaDataDic {this.customDeleteTime:F3}ms (比率: {this.customDeleteTime / this.stdDeleteTime:P})");

        string absolutePath = Path.Combine(
Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop\\qiita記事\\自作コレクション検証\\性能テスト結果.txt");

        // ファイルに新しい行を追加する
        using ( StreamWriter sw = new(absolutePath, true) )
        {
            // 結果サマリーを出力
            sw.WriteLine($"テスト結果確認用データ合計: {this.dataSum}");
            sw.WriteLine($"エラー数: {this.errorCount}");
            sw.WriteLine("=== パフォーマンス比較 ===");
            sw.WriteLine($"要素追加: 標準Dictionary {this.stdAddTime:F3}ms vs CharaDataDic {this.customAddTime:F3}ms (比率: {this.customAddTime / this.stdAddTime:P})");
            sw.WriteLine($"順次アクセス: 標準Dictionary {this.stdSequentialTime:F3}ms vs CharaDataDic {this.customSequentialTime:F3}ms (比率: {this.customSequentialTime / this.stdSequentialTime:P})");
            sw.WriteLine($"ランダムアクセス: 標準Dictionary {this.stdRandomTime:F3}ms vs CharaDataDic {this.customRandomTime:F3}ms (比率: {this.customRandomTime / this.stdRandomTime:P})");
            sw.WriteLine($"要素削除: 標準Dictionary {this.stdDeleteTime:F3}ms vs CharaDataDic {this.customDeleteTime:F3}ms (比率: {this.customDeleteTime / this.stdDeleteTime:P})");
            sw.WriteLine(string.Empty);
        }

        absolutePath = Path.Combine(
Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop\\qiita記事\\自作コレクション検証\\整合性テスト結果.txt");

        // ファイルに新しい行を追加する
        using ( StreamWriter sw = new(absolutePath, true) )
        {
            // 結果サマリーを出力
            for ( int i = 0; i < this.matchingTest.Length; i++ )
            {
                sw.WriteLine(this.matchingTest[i]);
            }

            sw.WriteLine(string.Empty);
        }

    }

    /// <summary>
    /// 正確性テスト - 追加&アクセス
    /// </summary>
    [Test]
    public void CorrectnessTest_AddAndAccess()
    {
        // 両方の辞書を初期化
        this.standardDictionary.Clear();
        this.customDictionary.Clear();

        // 要素を追加
        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
        }

        // 要素照会テスト用の配列
        this.matchingTest = new string[this.gameObjects.Length + 1];

        // すべての要素に正しくアクセスできるか確認
        for ( int i = 0; i < this.objectCount; i++ )
        {
            DicTestData stdData = this.standardDictionary[this.gameObjects[i]];
            DicTestData customData = this.customDictionary[this.gameObjects[i]];

            // 同じデータが取得できることを確認
            Assert.AreEqual(stdData.TestValue, customData.TestValue,
                $"Index {i}: 標準Dictionary ({stdData.TestValue}) と CharaDataDic ({customData.TestValue}) で異なる値が返されました");

            // 一致しない場合
            if ( stdData.TestValue != customData.TestValue )
            {
                this.unMatchCount++;
                this.matchingTest[i + 1] = $"結果：不一致 (Dictionary：{stdData.TestValue}) CharaDataDic：({customData.TestValue})";
            }
            else
            {
                this.matchingTest[i + 1] = $"結果：一致 ({stdData.TestValue})";
            }

        }

        this.matchingTest[0] = $"不一致:{this.unMatchCount} 件";

        // ハッシュコードによるアクセスも確認
        for ( int i = 0; i < this.objectCount; i++ )
        {
            int hash = this.gameObjectHashes[i];

            bool result = this.customDictionary.TryGetValueByHash(hash, out _, out _);

            Assert.IsTrue(result, $"Index {i}: ハッシュコード {hash} での検索に失敗しました");
        }
    }

    /// <summary>
    /// 正確性テスト - 削除
    /// </summary>
    [Test]
    public void CorrectnessTest_Remove()
    {
        // 両方の辞書を初期化して要素を追加
        this.standardDictionary.Clear();
        this.customDictionary.Clear();

        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
        }

        // 選択されたオブジェクトを削除
        foreach ( int idx in this.selectedForDeletion )
        {
            bool stdResult = this.standardDictionary.Remove(this.gameObjects[idx]);
            bool customResult = this.customDictionary.Remove(this.gameObjects[idx]);

            Assert.IsTrue(stdResult, $"標準Dictionaryから要素 {idx} の削除に失敗しました");
            Assert.IsTrue(customResult, $"CharaDataDicから要素 {idx} の削除に失敗しました");
        }

        // 削除されたか確認
        foreach ( int idx in this.selectedForDeletion )
        {
            bool stdContains = this.standardDictionary.ContainsKey(this.gameObjects[idx]);
            bool customContains = this.customDictionary.TryGetValue(this.gameObjects[idx], out _, out _);

            Assert.IsFalse(stdContains, $"標準Dictionaryに削除したはずの要素 {idx} が含まれています");
            Assert.IsFalse(customContains, $"CharaDataDicに削除したはずの要素 {idx} が含まれています");
        }

        // 他の要素はまだ存在するか確認
        for ( int i = 0; i < this.objectCount; i++ )
        {
            if ( !this.selectedForDeletion.Contains(i) )
            {
                bool stdContains = this.standardDictionary.ContainsKey(this.gameObjects[i]);
                bool customContains = this.customDictionary.TryGetValue(this.gameObjects[i], out _, out _);

                Assert.IsTrue(stdContains, $"標準Dictionaryから誤って要素 {i} が削除されています");
                Assert.IsTrue(customContains, $"CharaDataDicから誤って要素 {i} が削除されています");
            }
        }
    }

    /// <summary>
    /// 正確性テスト - 耐久性（大量追加と削除を繰り返す）
    /// </summary>
    [Test]
    public void CorrectnessTest_Durability()
    {
        // 10回繰り返し追加と削除を行う
        for ( int run = 0; run < 5; run++ )
        {
            // 両方の辞書を初期化
            this.standardDictionary.Clear();
            this.customDictionary.Clear();

            // 全要素を追加
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
                _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            }

            // 半分の要素を削除
            for ( int i = 0; i < this.objectCount; i += 2 )
            {
                _ = this.standardDictionary.Remove(this.gameObjects[i]);
                _ = this.customDictionary.Remove(this.gameObjects[i]);
            }

            // 削除した要素を再追加
            for ( int i = 0; i < this.objectCount; i += 2 )
            {
                this.standardDictionary[this.gameObjects[i]] = this.testData[i];
                _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            }

            // 全要素にアクセスして一致を確認
            for ( int i = 0; i < this.objectCount; i++ )
            {
                DicTestData stdData = this.standardDictionary[this.gameObjects[i]];
                DicTestData customData = this.customDictionary[this.gameObjects[i]];

                if ( stdData.TestValue != customData.TestValue )
                {
                    this.errorCount++;
                }
            }
        }

        Assert.AreEqual(0, this.errorCount, "繰り返しの追加削除テスト中にエラーが発生しました");
    }

    /// <summary>
    /// パフォーマンステスト - 要素追加
    /// </summary>
    [Test, Performance]
    public void PerformanceTest_Add()
    {
        // 標準Dictionary追加テスト
        Measure.Method(() =>
        {
            this.standardDictionary.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("StandardDictionary_Add", SampleUnit.Millisecond))
        .Run();

        // 標準Dictionaryの結果を保存
        this.stdAddTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "StandardDictionary_Add")?.Median ?? 0;

        // CharaDataDic追加テスト
        Measure.Method(() =>
        {
            this.customDictionary.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("CharaDataDic_Add", SampleUnit.Millisecond))
        .Run();

        // CharaDataDicの結果を保存
        this.customAddTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "CharaDataDic_Add")?.Median ?? 0;
    }

    /// <summary>
    /// パフォーマンステスト - 連続アクセス
    /// </summary>
    [Test, Performance]
    public void PerformanceTest_SequentialAccess()
    {
        // 事前準備：辞書に要素を追加
        this.PrepareForAccessTest();

        // 標準Dictionary連続アクセステスト
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = run % this.objectCount;
                DicTestData data = this.standardDictionary[this.gameObjects[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("StandardDictionary_Sequential", SampleUnit.Millisecond))
        .Run();

        // 標準Dictionaryの結果を保存
        this.stdSequentialTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "StandardDictionary_Sequential")?.Median ?? 0;

        // CharaDataDic連続アクセステスト
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = run % this.objectCount;
                DicTestData data = this.customDictionary[this.gameObjects[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("CharaDataDic_Sequential", SampleUnit.Millisecond))
        .Run();

        // CharaDataDicの結果を保存
        this.customSequentialTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "CharaDataDic_Sequential")?.Median ?? 0;
    }

    /// <summary>
    /// パフォーマンステスト - ランダムアクセス
    /// </summary>
    [Test, Performance]
    public void PerformanceTest_RandomAccess()
    {
        // 事前準備：辞書に要素を追加
        this.PrepareForAccessTest();

        // ランダムインデックスを生成
        System.Random rnd = new(42);
        int[] randomIndices = new int[this.accessTestRuns];
        for ( int i = 0; i < this.accessTestRuns; i++ )
        {
            randomIndices[i] = rnd.Next(0, this.objectCount);
        }

        // 標準Dictionaryランダムアクセステスト
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = randomIndices[run];
                DicTestData data = this.standardDictionary[this.gameObjects[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("StandardDictionary_Random", SampleUnit.Millisecond))
        .Run();

        // 標準Dictionaryの結果を保存
        this.stdRandomTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "StandardDictionary_Random")?.Median ?? 0;

        // CharaDataDicランダムアクセステスト
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = randomIndices[run];
                DicTestData data = this.customDictionary[this.gameObjects[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("CharaDataDic_Random", SampleUnit.Millisecond))
        .Run();

        // CharaDataDicの結果を保存
        this.customRandomTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "CharaDataDic_Random")?.Median ?? 0;
    }

    /// <summary>
    /// パフォーマンステスト - 要素削除
    /// </summary>
    [Test, Performance]
    public void PerformanceTest_Remove()
    {
        // 標準Dictionary削除テスト
        this.PrepareForAccessTest();
        Measure.Method(() =>
        {
            foreach ( int idx in this.selectedForDeletion )
            {
                _ = this.standardDictionary.Remove(this.gameObjects[idx]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("StandardDictionary_Remove", SampleUnit.Millisecond))
        .Run();

        // 標準Dictionaryの結果を保存
        this.stdDeleteTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "StandardDictionary_Remove")?.Median ?? 0;

        // CharaDataDic削除テスト
        this.PrepareForAccessTest();
        Measure.Method(() =>
        {
            foreach ( int idx in this.selectedForDeletion )
            {
                _ = this.customDictionary.Remove(this.gameObjects[idx]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("CharaDataDic_Remove", SampleUnit.Millisecond))
        .Run();

        // CharaDataDicの結果を保存
        this.customDeleteTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "CharaDataDic_Remove")?.Median ?? 0;
    }

    /// <summary>
    /// パフォーマンステスト - ハッシュ直接アクセス
    /// </summary>
    [Test, Performance]
    public void PerformanceTest_HashCodeAccess()
    {
        // 事前準備：辞書に要素を追加
        this.PrepareForAccessTest();

        // ゲームオブジェクトのハッシュコードを収集
        int[] hashes = new int[this.objectCount];
        for ( int i = 0; i < this.objectCount; i++ )
        {
            hashes[i] = this.gameObjects[i].GetHashCode();
        }

        // 通常の方法でアクセス（参照基準）
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < this.accessTestRuns; i++ )
            {
                int idx = i % this.objectCount;
                DicTestData data = this.customDictionary[this.gameObjects[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("GameObjectAccess", SampleUnit.Millisecond))
        .Run();

        // ハッシュコードを使った直接アクセス
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < this.accessTestRuns; i++ )
            {
                int idx = i % this.objectCount;
                int hash = hashes[idx];
                _ = this.customDictionary.TryGetValueByHash(hash, out DicTestData data, out _);
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("HashCodeAccess", SampleUnit.Millisecond))
        .Run();
    }

    /// <summary>
    /// パフォーマンステスト - インデックス直接アクセス
    /// </summary>
    [Test, Performance]
    public void PerformanceTest_DirectIndexAccess()
    {
        // 事前準備：辞書に要素を追加
        this.PrepareForAccessTest();

        // インデックスを収集
        List<int> indices = new(this.objectCount);
        for ( int i = 0; i < this.objectCount; i++ )
        {
            int index = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            indices.Add(index);
        }

        // 通常の方法でアクセス（参照基準）
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < this.accessTestRuns; i++ )
            {
                int idx = i % this.objectCount;
                DicTestData data = this.customDictionary[this.gameObjects[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NormalAccess", SampleUnit.Millisecond))
        .Run();

        // インデックスでの直接アクセス
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < this.accessTestRuns; i++ )
            {
                int idx = i % this.objectCount;
                int valueIndex = indices[idx];
                ref DicTestData data = ref this.customDictionary.GetDataByIndex(valueIndex);
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("DirectIndexAccess", SampleUnit.Millisecond))
        .Run();
    }

    /// <summary>
    /// ストレステスト - 連続した操作の組み合わせ
    /// </summary>
    [Test]
    public void StressTest_MixedOperations()
    {
        // 両方の辞書を初期化
        this.standardDictionary.Clear();
        this.customDictionary.Clear();

        // 1. 全要素を追加
        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
        }

        // 2. ランダムなアクセス
        System.Random rnd = new(123);
        for ( int i = 0; i < this.accessTestRuns; i++ )
        {
            int idx = rnd.Next(this.objectCount);
            bool stdExists = this.standardDictionary.TryGetValue(this.gameObjects[idx], out DicTestData stdData);
            bool customExists = this.customDictionary.TryGetValue(this.gameObjects[idx], out DicTestData customData, out int index);

            // 両方のディクショナリで結果が一致するか確認
            Assert.AreEqual(stdExists, customExists,
                $"存在チェック不一致: 標準Dictionary({stdExists}) vs CharaDataDic({customExists})");

            if ( stdExists && customExists )
            {
                Assert.AreEqual(stdData.TestValue, customData.TestValue,
                    $"データ値不一致: 標準Dictionary({stdData.TestValue}) vs CharaDataDic({customData.TestValue})");
            }
        }

        // 3. ランダムに要素を削除
        int deleteCount = this.objectCount / 3;
        HashSet<int> deleteIndices = new();
        while ( deleteIndices.Count < deleteCount )
        {
            _ = deleteIndices.Add(rnd.Next(this.objectCount));
        }

        foreach ( int idx in deleteIndices )
        {
            _ = this.standardDictionary.Remove(this.gameObjects[idx]);
            _ = this.customDictionary.Remove(this.gameObjects[idx]);
        }

        // 4. ランダムに要素を追加
        int addCount = deleteCount / 2;
        for ( int i = 0; i < addCount; i++ )
        {
            int idx = rnd.Next(this.objectCount);
            if ( this.standardDictionary.ContainsKey(this.gameObjects[idx]) )
            {
                continue;
            }

            this.standardDictionary[this.gameObjects[idx]] = this.testData[idx];
            _ = this.customDictionary.Add(this.gameObjects[idx], this.testData[idx]);
        }

        // 5. 整合性確認 - 標準Dictionaryの各キーがCustomDicにも存在するか
        foreach ( var kvp in this.standardDictionary )
        {
            bool exists = this.customDictionary.TryGetValue(kvp.Key, out DicTestData data, out _);
            Assert.IsTrue(exists, "CharaDataDicに標準Dictionaryのキーが見つかりません");
            Assert.AreEqual(kvp.Value.TestValue, data.TestValue, "値が一致しません");
        }

        // 6. 整合性確認 - CustomDicの各キーが標準Dictionaryにも存在するか
        this.customDictionary.ForEach((index, data) =>
        {
            // このキーを持つゲームオブジェクトを特定する必要があります
            // 実装が難しいのでスキップ（逆方向の確認はできていない）
        });
    }

    // テスト用のヘルパーメソッド
    private void PrepareForAccessTest()
    {
        this.standardDictionary.Clear();
        this.customDictionary.Clear();

        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
        }
    }
}

