using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

/// <summary>
/// CharaDataDicと標準Dictionary、NativeHashMap、UnsafeHashMapの正確性とパフォーマンスを徹底的に比較するテスト
/// </summary>
[Serializable]
public class CharacterDicEnhancedTest
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
    private NativeHashMap<int, DicTestData> nativeHashMap;
    private NativeParallelHashMap<int, DicTestData> nativeParallelHashMap;
    private UnsafeHashMap<int, DicTestData> unsafeHashMap;
    private UnsafeParallelHashMap<int, DicTestData> unsafeParallelHashMap;

    // 計測結果の一時保存用
    private string[] matchingTest;
    private int unMatchCount;
    private double stdAddTime;
    private double customAddTime;
    private double nativeAddTime;
    private double unsafeAddTime;
    private double stdSequentialTime;
    private double customSequentialTime;
    private double nativeSequentialTime;
    private double unsafeSequentialTime;
    private double stdRandomTime;
    private double customRandomTime;
    private double nativeRandomTime;
    private double unsafeRandomTime;
    private double stdDeleteTime;
    private double customDeleteTime;
    private double nativeDeleteTime;
    private double unsafeDeleteTime;

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

        // Native/Unsafeコレクションの初期化
        this.nativeHashMap = new NativeHashMap<int, DicTestData>(this.objectCount, Allocator.Persistent);
        this.nativeParallelHashMap = new NativeParallelHashMap<int, DicTestData>(this.objectCount, Allocator.Persistent);
        this.unsafeHashMap = new UnsafeHashMap<int, DicTestData>(this.objectCount, Allocator.Persistent);
        this.unsafeParallelHashMap = new UnsafeParallelHashMap<int, DicTestData>(this.objectCount, Allocator.Persistent);

        // 初期値をクリア
        this.dataSum = 0;
        this.errorCount = 0;
        this.stdAddTime = 0;
        this.customAddTime = 0;
        this.nativeAddTime = 0;
        this.unsafeAddTime = 0;
        this.stdSequentialTime = 0;
        this.customSequentialTime = 0;
        this.nativeSequentialTime = 0;
        this.unsafeSequentialTime = 0;
        this.stdRandomTime = 0;
        this.customRandomTime = 0;
        this.nativeRandomTime = 0;
        this.unsafeRandomTime = 0;
        this.stdDeleteTime = 0;
        this.customDeleteTime = 0;
        this.nativeDeleteTime = 0;
        this.unsafeDeleteTime = 0;
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

        // Native/Unsafeコレクションの破棄
        if ( this.nativeHashMap.IsCreated )
        {
            this.nativeHashMap.Dispose();
        }

        if ( this.nativeParallelHashMap.IsCreated )
        {
            this.nativeParallelHashMap.Dispose();
        }

        if ( this.unsafeHashMap.IsCreated )
        {
            this.unsafeHashMap.Dispose();
        }

        if ( this.unsafeParallelHashMap.IsCreated )
        {
            this.unsafeParallelHashMap.Dispose();
        }

        // 辞書のクリーンアップ
        this.standardDictionary.Clear();
        this.customDictionary.Dispose();

        // 結果サマリーを出力
        UnityEngine.Debug.Log($"テスト結果確認用データ合計: {this.dataSum}");
        UnityEngine.Debug.Log($"エラー数: {this.errorCount}");
        UnityEngine.Debug.Log("=== パフォーマンス比較 ===");
        UnityEngine.Debug.Log($"要素追加: 標準Dictionary {this.stdAddTime:F3}ms vs CharaDataDic {this.customAddTime:F3}ms (比率: {this.customAddTime / this.stdAddTime:P})");
        UnityEngine.Debug.Log($"要素追加: 標準Dictionary {this.stdAddTime:F3}ms vs NativeHashMap {this.nativeAddTime:F3}ms (比率: {this.nativeAddTime / this.stdAddTime:P})");
        UnityEngine.Debug.Log($"要素追加: 標準Dictionary {this.stdAddTime:F3}ms vs UnsafeHashMap {this.unsafeAddTime:F3}ms (比率: {this.unsafeAddTime / this.stdAddTime:P})");
        UnityEngine.Debug.Log($"順次アクセス: 標準Dictionary {this.stdSequentialTime:F3}ms vs CharaDataDic {this.customSequentialTime:F3}ms (比率: {this.customSequentialTime / this.stdSequentialTime:P})");
        UnityEngine.Debug.Log($"順次アクセス: 標準Dictionary {this.stdSequentialTime:F3}ms vs NativeHashMap {this.nativeSequentialTime:F3}ms (比率: {this.nativeSequentialTime / this.stdSequentialTime:P})");
        UnityEngine.Debug.Log($"順次アクセス: 標準Dictionary {this.stdSequentialTime:F3}ms vs UnsafeHashMap {this.unsafeSequentialTime:F3}ms (比率: {this.unsafeSequentialTime / this.stdSequentialTime:P})");
        UnityEngine.Debug.Log($"ランダムアクセス: 標準Dictionary {this.stdRandomTime:F3}ms vs CharaDataDic {this.customRandomTime:F3}ms (比率: {this.customRandomTime / this.stdRandomTime:P})");
        UnityEngine.Debug.Log($"ランダムアクセス: 標準Dictionary {this.stdRandomTime:F3}ms vs NativeHashMap {this.nativeRandomTime:F3}ms (比率: {this.nativeRandomTime / this.stdRandomTime:P})");
        UnityEngine.Debug.Log($"ランダムアクセス: 標準Dictionary {this.stdRandomTime:F3}ms vs UnsafeHashMap {this.unsafeRandomTime:F3}ms (比率: {this.unsafeRandomTime / this.stdRandomTime:P})");
        UnityEngine.Debug.Log($"要素削除: 標準Dictionary {this.stdDeleteTime:F3}ms vs CharaDataDic {this.customDeleteTime:F3}ms (比率: {this.customDeleteTime / this.stdDeleteTime:P})");
        UnityEngine.Debug.Log($"要素削除: 標準Dictionary {this.stdDeleteTime:F3}ms vs NativeHashMap {this.nativeDeleteTime:F3}ms (比率: {this.nativeDeleteTime / this.stdDeleteTime:P})");
        UnityEngine.Debug.Log($"要素削除: 標準Dictionary {this.stdDeleteTime:F3}ms vs UnsafeHashMap {this.unsafeDeleteTime:F3}ms (比率: {this.unsafeDeleteTime / this.stdDeleteTime:P})");

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
            sw.WriteLine($"要素追加: 標準Dictionary {this.stdAddTime:F3}ms vs NativeHashMap {this.nativeAddTime:F3}ms (比率: {this.nativeAddTime / this.stdAddTime:P})");
            sw.WriteLine($"要素追加: 標準Dictionary {this.stdAddTime:F3}ms vs UnsafeHashMap {this.unsafeAddTime:F3}ms (比率: {this.unsafeAddTime / this.stdAddTime:P})");
            sw.WriteLine($"順次アクセス: 標準Dictionary {this.stdSequentialTime:F3}ms vs CharaDataDic {this.customSequentialTime:F3}ms (比率: {this.customSequentialTime / this.stdSequentialTime:P})");
            sw.WriteLine($"順次アクセス: 標準Dictionary {this.stdSequentialTime:F3}ms vs NativeHashMap {this.nativeSequentialTime:F3}ms (比率: {this.nativeSequentialTime / this.stdSequentialTime:P})");
            sw.WriteLine($"順次アクセス: 標準Dictionary {this.stdSequentialTime:F3}ms vs UnsafeHashMap {this.unsafeSequentialTime:F3}ms (比率: {this.unsafeSequentialTime / this.stdSequentialTime:P})");
            sw.WriteLine($"ランダムアクセス: 標準Dictionary {this.stdRandomTime:F3}ms vs CharaDataDic {this.customRandomTime:F3}ms (比率: {this.customRandomTime / this.stdRandomTime:P})");
            sw.WriteLine($"ランダムアクセス: 標準Dictionary {this.stdRandomTime:F3}ms vs NativeHashMap {this.nativeRandomTime:F3}ms (比率: {this.nativeRandomTime / this.stdRandomTime:P})");
            sw.WriteLine($"ランダムアクセス: 標準Dictionary {this.stdRandomTime:F3}ms vs UnsafeHashMap {this.unsafeRandomTime:F3}ms (比率: {this.unsafeRandomTime / this.stdRandomTime:P})");
            sw.WriteLine($"要素削除: 標準Dictionary {this.stdDeleteTime:F3}ms vs CharaDataDic {this.customDeleteTime:F3}ms (比率: {this.customDeleteTime / this.stdDeleteTime:P})");
            sw.WriteLine($"要素削除: 標準Dictionary {this.stdDeleteTime:F3}ms vs NativeHashMap {this.nativeDeleteTime:F3}ms (比率: {this.nativeDeleteTime / this.stdDeleteTime:P})");
            sw.WriteLine($"要素削除: 標準Dictionary {this.stdDeleteTime:F3}ms vs UnsafeHashMap {this.unsafeDeleteTime:F3}ms (比率: {this.unsafeDeleteTime / this.stdDeleteTime:P})");
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
        this.nativeHashMap.Clear();
        this.unsafeHashMap.Clear();

        // 要素を追加
        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
        }

        // 要素照会テスト用の配列
        this.matchingTest = new string[this.gameObjects.Length + 1];

        // すべての要素に正しくアクセスできるか確認
        for ( int i = 0; i < this.objectCount; i++ )
        {
            DicTestData stdData = this.standardDictionary[this.gameObjects[i]];
            DicTestData customData = this.customDictionary[this.gameObjects[i]];
            DicTestData nativeData = this.nativeHashMap[this.gameObjectHashes[i]];
            DicTestData unsafeData = this.unsafeHashMap[this.gameObjectHashes[i]];

            // 同じデータが取得できることを確認
            Assert.AreEqual(stdData.TestValue, customData.TestValue,
                $"Index {i}: 標準Dictionary ({stdData.TestValue}) と CharaDataDic ({customData.TestValue}) で異なる値が返されました");
            Assert.AreEqual(stdData.TestValue, nativeData.TestValue,
                $"Index {i}: 標準Dictionary ({stdData.TestValue}) と NativeHashMap ({nativeData.TestValue}) で異なる値が返されました");
            Assert.AreEqual(stdData.TestValue, unsafeData.TestValue,
                $"Index {i}: 標準Dictionary ({stdData.TestValue}) と UnsafeHashMap ({unsafeData.TestValue}) で異なる値が返されました");

            // 一致しない場合
            if ( stdData.TestValue != customData.TestValue ||
                 stdData.TestValue != nativeData.TestValue ||
                 stdData.TestValue != unsafeData.TestValue )
            {
                this.unMatchCount++;
                this.matchingTest[i + 1] = $"結果：不一致 (Dictionary：{stdData.TestValue}) CharaDataDic：({customData.TestValue}) NativeHashMap：({nativeData.TestValue}) UnsafeHashMap：({unsafeData.TestValue})";
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

            bool custResult = this.customDictionary.TryGetValueByHash(hash, out _, out _);

            bool nativeResult = this.nativeHashMap.TryGetValue(hash, out _);

            bool unsafeResult = this.unsafeHashMap.TryGetValue(hash, out _);

            Assert.IsTrue(custResult, $"Index {i}: CharaDataDicでハッシュコード {hash} での検索に失敗しました");
            Assert.IsTrue(nativeResult, $"Index {i}: NativeHashMapでハッシュコード {hash} での検索に失敗しました");
            Assert.IsTrue(unsafeResult, $"Index {i}: UnsafeHashMapでハッシュコード {hash} での検索に失敗しました");
        }
    }

    /// <summary>
    /// 正確性テスト - 削除
    /// </summary>
    [Test]
    public void CorrectnessTest_Remove()
    {
        // 辞書を初期化して要素を追加
        this.standardDictionary.Clear();
        this.customDictionary.Clear();
        this.nativeHashMap.Clear();
        this.unsafeHashMap.Clear();

        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
        }

        // 選択されたオブジェクトを削除
        foreach ( int idx in this.selectedForDeletion )
        {
            bool stdResult = this.standardDictionary.Remove(this.gameObjects[idx]);
            bool customResult = this.customDictionary.Remove(this.gameObjects[idx]);
            bool nativeResult = this.nativeHashMap.Remove(this.gameObjectHashes[idx]);
            bool unsafeResult = this.unsafeHashMap.Remove(this.gameObjectHashes[idx]);

            Assert.IsTrue(stdResult, $"標準Dictionaryから要素 {idx} の削除に失敗しました");
            Assert.IsTrue(customResult, $"CharaDataDicから要素 {idx} の削除に失敗しました");
            Assert.IsTrue(nativeResult, $"NativeHashMapから要素 {idx} の削除に失敗しました");
            Assert.IsTrue(unsafeResult, $"UnsafeHashMapから要素 {idx} の削除に失敗しました");
        }

        // 削除されたか確認
        foreach ( int idx in this.selectedForDeletion )
        {
            bool stdContains = this.standardDictionary.ContainsKey(this.gameObjects[idx]);
            bool customContains = this.customDictionary.TryGetValue(this.gameObjects[idx], out _, out _);
            bool nativeContains = this.nativeHashMap.ContainsKey(this.gameObjectHashes[idx]);
            bool unsafeContains = this.unsafeHashMap.ContainsKey(this.gameObjectHashes[idx]);

            Assert.IsFalse(stdContains, $"標準Dictionaryに削除したはずの要素 {idx} が含まれています");
            Assert.IsFalse(customContains, $"CharaDataDicに削除したはずの要素 {idx} が含まれています");
            Assert.IsFalse(nativeContains, $"NativeHashMapに削除したはずの要素 {idx} が含まれています");
            Assert.IsFalse(unsafeContains, $"UnsafeHashMapに削除したはずの要素 {idx} が含まれています");
        }

        // 他の要素はまだ存在するか確認
        for ( int i = 0; i < this.objectCount; i++ )
        {
            if ( !this.selectedForDeletion.Contains(i) )
            {
                bool stdContains = this.standardDictionary.ContainsKey(this.gameObjects[i]);
                bool customContains = this.customDictionary.TryGetValue(this.gameObjects[i], out _, out _);
                bool nativeContains = this.nativeHashMap.ContainsKey(this.gameObjectHashes[i]);
                bool unsafeContains = this.unsafeHashMap.ContainsKey(this.gameObjectHashes[i]);

                Assert.IsTrue(stdContains, $"標準Dictionaryから誤って要素 {i} が削除されています");
                Assert.IsTrue(customContains, $"CharaDataDicから誤って要素 {i} が削除されています");
                Assert.IsTrue(nativeContains, $"NativeHashMapから誤って要素 {i} が削除されています");
                Assert.IsTrue(unsafeContains, $"UnsafeHashMapから誤って要素 {i} が削除されています");
            }
        }
    }

    /// <summary>
    /// 正確性テスト - 耐久性（大量追加と削除を繰り返す）
    /// </summary>
    [Test]
    public void CorrectnessTest_Durability()
    {
        // 5回繰り返し追加と削除を行う
        for ( int run = 0; run < 5; run++ )
        {
            // 辞書を初期化
            this.standardDictionary.Clear();
            this.customDictionary.Clear();
            this.nativeHashMap.Clear();
            this.unsafeHashMap.Clear();

            // 全要素を追加
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
                _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
                this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
                this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }

            // 半分の要素を削除
            for ( int i = 0; i < this.objectCount; i += 2 )
            {
                _ = this.standardDictionary.Remove(this.gameObjects[i]);
                _ = this.customDictionary.Remove(this.gameObjects[i]);
                _ = this.nativeHashMap.Remove(this.gameObjectHashes[i]);
                _ = this.unsafeHashMap.Remove(this.gameObjectHashes[i]);
            }

            // 削除した要素を再追加
            for ( int i = 0; i < this.objectCount; i += 2 )
            {
                this.standardDictionary[this.gameObjects[i]] = this.testData[i];
                _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
                this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
                this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }

            // 全要素にアクセスして一致を確認
            for ( int i = 0; i < this.objectCount; i++ )
            {
                DicTestData stdData = this.standardDictionary[this.gameObjects[i]];
                DicTestData customData = this.customDictionary[this.gameObjects[i]];
                DicTestData nativeData = this.nativeHashMap[this.gameObjectHashes[i]];
                DicTestData unsafeData = this.unsafeHashMap[this.gameObjectHashes[i]];

                if ( stdData.TestValue != customData.TestValue ||
                     stdData.TestValue != nativeData.TestValue ||
                     stdData.TestValue != unsafeData.TestValue )
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

        // NativeHashMap追加テスト
        Measure.Method(() =>
        {
            this.nativeHashMap.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_Add", SampleUnit.Millisecond))
        .Run();

        // NativeHashMapの結果を保存
        this.nativeAddTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "NativeHashMap_Add")?.Median ?? 0;

        // UnsafeHashMap追加テスト
        Measure.Method(() =>
        {
            this.unsafeHashMap.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Add", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMapの結果を保存
        this.unsafeAddTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "UnsafeHashMap_Add")?.Median ?? 0;
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

        // NativeHashMap連続アクセステスト
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = run % this.objectCount;
                DicTestData data = this.nativeHashMap[this.gameObjectHashes[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_Sequential", SampleUnit.Millisecond))
        .Run();

        // NativeHashMapの結果を保存
        this.nativeSequentialTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "NativeHashMap_Sequential")?.Median ?? 0;

        // UnsafeHashMap連続アクセステスト
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = run % this.objectCount;
                DicTestData data = this.unsafeHashMap[this.gameObjectHashes[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Sequential", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMapの結果を保存
        this.unsafeSequentialTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "UnsafeHashMap_Sequential")?.Median ?? 0;
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

        // NativeHashMapランダムアクセステスト
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = randomIndices[run];
                DicTestData data = this.nativeHashMap[this.gameObjectHashes[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_Random", SampleUnit.Millisecond))
        .Run();

        // NativeHashMapの結果を保存
        this.nativeRandomTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "NativeHashMap_Random")?.Median ?? 0;

        // UnsafeHashMapランダムアクセステスト
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int run = 0; run < this.accessTestRuns; run++ )
            {
                int idx = randomIndices[run];
                DicTestData data = this.unsafeHashMap[this.gameObjectHashes[idx]];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Random", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMapの結果を保存
        this.unsafeRandomTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "UnsafeHashMap_Random")?.Median ?? 0;
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

        // NativeHashMap削除テスト
        this.PrepareForAccessTest();
        Measure.Method(() =>
        {
            foreach ( int idx in this.selectedForDeletion )
            {
                _ = this.nativeHashMap.Remove(this.gameObjectHashes[idx]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_Remove", SampleUnit.Millisecond))
        .Run();

        // NativeHashMapの結果を保存
        this.nativeDeleteTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "NativeHashMap_Remove")?.Median ?? 0;

        // UnsafeHashMap削除テスト
        this.PrepareForAccessTest();
        Measure.Method(() =>
        {
            foreach ( int idx in this.selectedForDeletion )
            {
                _ = this.unsafeHashMap.Remove(this.gameObjectHashes[idx]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Remove", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMapの結果を保存
        this.unsafeDeleteTime = PerformanceTest.Active.SampleGroups
            .FirstOrDefault(sg => sg.Name == "UnsafeHashMap_Remove")?.Median ?? 0;
    }

    /// <summary>
    /// パフォーマンステスト - ハッシュ直接アクセス
    /// </summary>
  // [Test, Performance]
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

        // CharaDataDicのハッシュコードを使った直接アクセス
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
        .SampleGroup(new SampleGroup("CharaDataDic_HashCodeAccess", SampleUnit.Millisecond))
        .Run();

        // NativeHashMapのキーとしてのハッシュコードアクセス
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < this.accessTestRuns; i++ )
            {
                int idx = i % this.objectCount;
                int hash = hashes[idx];
                DicTestData data = this.nativeHashMap[hash];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NativeHashMap_HashCodeAccess", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMapのキーとしてのハッシュコードアクセス
        Measure.Method(() =>
        {
            long localSum = 0;
            for ( int i = 0; i < this.accessTestRuns; i++ )
            {
                int idx = i % this.objectCount;
                int hash = hashes[idx];
                DicTestData data = this.unsafeHashMap[hash];
                localSum += data.TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_HashCodeAccess", SampleUnit.Millisecond))
        .Run();
    }

    /// <summary>
    /// パフォーマンステスト - インデックス直接アクセス
    /// </summary>
   // [Test, Performance]
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
    /// NativeHashMapとUnsafeHashMapの並列処理性能比較
    /// </summary>
//    [Test, Performance]
    public void PerformanceTest_ParallelAccess()
    {
        // 標準のHashMapでの処理
        Measure.Method(() =>
        {
            this.nativeHashMap.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NativeHashmap", SampleUnit.Millisecond))
        .Run();

        // 並列HashMapでの処理
        Measure.Method(() =>
        {
            this.nativeParallelHashMap.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.nativeParallelHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("NativeParallelHashMap", SampleUnit.Millisecond))
        .Run();

        // UnsafeHashMapでの処理
        Measure.Method(() =>
        {
            this.unsafeHashMap.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("UnsafeHashMap_Standard", SampleUnit.Millisecond))
        .Run();

        // Unsafe並列HashMapでの処理
        Measure.Method(() =>
        {
            this.unsafeParallelHashMap.Clear();
            for ( int i = 0; i < this.objectCount; i++ )
            {
                this.unsafeParallelHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            }
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .SampleGroup(new SampleGroup("UnsafeParallelHashMap", SampleUnit.Millisecond))
        .Run();
    }

    /// <summary>
    /// ストレステスト - 連続した操作の組み合わせ
    /// </summary>
    [Test]
    public void StressTest_MixedOperations()
    {
        // 辞書を初期化
        this.standardDictionary.Clear();
        this.customDictionary.Clear();
        this.nativeHashMap.Clear();
        this.unsafeHashMap.Clear();

        // 1. 全要素を追加
        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
        }

        // 2. ランダムなアクセス
        System.Random rnd = new(123);
        for ( int i = 0; i < this.accessTestRuns; i++ )
        {
            int idx = rnd.Next(this.objectCount);
            bool stdExists = this.standardDictionary.TryGetValue(this.gameObjects[idx], out DicTestData stdData);

            bool customExists = this.customDictionary.TryGetValue(this.gameObjects[idx], out DicTestData customData, out _);
            bool nativeExists = this.nativeHashMap.TryGetValue(this.gameObjectHashes[idx], out DicTestData nativeData);
            bool unsafeExists = this.unsafeHashMap.TryGetValue(this.gameObjectHashes[idx], out DicTestData unsafeData);

            // 全てのディクショナリで結果が一致するか確認
            Assert.AreEqual(stdExists, customExists,
                $"存在チェック不一致: 標準Dictionary({stdExists}) vs CharaDataDic({customExists})");
            Assert.AreEqual(stdExists, nativeExists,
                $"存在チェック不一致: 標準Dictionary({stdExists}) vs NativeHashMap({nativeExists})");
            Assert.AreEqual(stdExists, unsafeExists,
                $"存在チェック不一致: 標準Dictionary({stdExists}) vs UnsafeHashMap({unsafeExists})");

            if ( stdExists && customExists && nativeExists && unsafeExists )
            {
                Assert.AreEqual(stdData.TestValue, customData.TestValue,
                    $"データ値不一致: 標準Dictionary({stdData.TestValue}) vs CharaDataDic({customData.TestValue})");
                Assert.AreEqual(stdData.TestValue, nativeData.TestValue,
                    $"データ値不一致: 標準Dictionary({stdData.TestValue}) vs NativeHashMap({nativeData.TestValue})");
                Assert.AreEqual(stdData.TestValue, unsafeData.TestValue,
                    $"データ値不一致: 標準Dictionary({stdData.TestValue}) vs UnsafeHashMap({unsafeData.TestValue})");
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
            _ = this.nativeHashMap.Remove(this.gameObjectHashes[idx]);
            _ = this.unsafeHashMap.Remove(this.gameObjectHashes[idx]);
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
            this.nativeHashMap.Add(this.gameObjectHashes[idx], this.testData[idx]);
            this.unsafeHashMap.Add(this.gameObjectHashes[idx], this.testData[idx]);
        }

        // 5. 整合性確認 - 標準Dictionaryの各キーが他のコレクションにも存在するか
        foreach ( var kvp in this.standardDictionary )
        {
            bool customExists = this.customDictionary.TryGetValue(kvp.Key, out DicTestData customData, out _);
            bool nativeExists = this.nativeHashMap.ContainsKey(kvp.Key.GetHashCode());
            bool unsafeExists = this.unsafeHashMap.ContainsKey(kvp.Key.GetHashCode());

            Assert.IsTrue(customExists, "CharaDataDicに標準Dictionaryのキーが見つかりません");
            Assert.IsTrue(nativeExists, "NativeHashMapに標準Dictionaryのキーが見つかりません");
            Assert.IsTrue(unsafeExists, "UnsafeHashMapに標準Dictionaryのキーが見つかりません");

            if ( customExists && nativeExists && unsafeExists )
            {
                DicTestData nativeData = this.nativeHashMap[kvp.Key.GetHashCode()];
                DicTestData unsafeData = this.unsafeHashMap[kvp.Key.GetHashCode()];

                Assert.AreEqual(kvp.Value.TestValue, customData.TestValue, "CharaDataDicの値が一致しません");
                Assert.AreEqual(kvp.Value.TestValue, nativeData.TestValue, "NativeHashMapの値が一致しません");
                Assert.AreEqual(kvp.Value.TestValue, unsafeData.TestValue, "UnsafeHashMapの値が一致しません");
            }
        }
    }

    // テスト用のヘルパーメソッド
    private void PrepareForAccessTest()
    {
        this.standardDictionary.Clear();
        this.customDictionary.Clear();
        this.nativeHashMap.Clear();
        this.unsafeHashMap.Clear();

        for ( int i = 0; i < this.objectCount; i++ )
        {
            this.standardDictionary.Add(this.gameObjects[i], this.testData[i]);
            _ = this.customDictionary.Add(this.gameObjects[i], this.testData[i]);
            this.nativeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
            this.unsafeHashMap.Add(this.gameObjectHashes[i], this.testData[i]);
        }
    }
}