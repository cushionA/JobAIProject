using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.TestTools;
using static CombatManager;
using static JobAITestStatus;
using static JTestAIBase;

/// <summary>
/// AITestJobのパフォーマンステスト
/// </summary>
public class AIPerformanceTestsDebug
{
    // テスト用のデータ
    private UnsafeList<CharacterData> _characterData;
    private UnsafeList<MovementInfo> _judgeResultJob;
    private UnsafeList<MovementInfo> _judgeResultNonJob;
    private NativeHashMap<int2, int> _teamHate;
    private NativeArray<int> _relationMap;
    //private NativeArray<FunctionPointer<SkipJudgeDelegate>> _skipFunctions;
    //private NativeArray<FunctionPointer<TargetJudgeDelegate>> _targetFunctions;

    // 初期化状態を追跡するフラグ
    private bool _dataInitialized = false;
    private bool _charactersInitialized = false;
    private bool _aiInstancesInitialized = false;

    /// <summary>
    /// 
    /// </summary>
    private JTestAIBase[] characters;

    /// <summary>
    /// 生成オブジェクトの配列。
    /// </summary>
    private string[] types = new string[] { "Assets/Prefab/JobAI/TypeA.prefab", "Assets/Prefab/JobAI/TypeB.prefab", "Assets/Prefab/JobAI/TypeC.prefab" };

    // テスト用のパラメータ
    private int _characterCount = 1000;
    private float _nowTime = 10.0f;

    // AIテスト用のインスタンス
    private AITestJob _aiTestJob;
    private NonJobAI _nonJobAI;

    [UnitySetUp]
    public IEnumerator OneTimeSetUp()
    {
        Debug.Log("開始: OneTimeSetUp");

        // テストデータの初期化
        try
        {
            this.InitializeTestData();
            Debug.Log($"テストデータの初期化完了: teamHate.IsCreated={this._teamHate.IsCreated}, Length={this._teamHate.Count}");
        }
        catch ( Exception ex )
        {
            Debug.LogError($"テストデータ初期化中のエラー: {ex.Message}\n{ex.StackTrace}");
            yield break;
        }

        // キャラクターデータの初期化 - IEnumeratorなので yield returnする
        yield return this.InitializeCharacterData();

        if ( !this._charactersInitialized )
        {
            Debug.LogError("キャラクターデータの初期化に失敗しました");
            yield break;
        }

        Debug.Log($"キャラクターデータの初期化完了: characterData.Length={this._characterData.Length}");

        // teamHateの中身を確認
        for ( int i = 0; i < this._teamHate.Count; i++ )
        {
            Debug.Log($"teamHate[{i}].Count={this._teamHate.Count}, IsCreated={this._teamHate.IsCreated}");
        }

        // AIインスタンスの初期化
        try
        {
            this.InitializeAIInstances();
            Debug.Log("AIインスタンスの初期化完了");
        }
        catch ( Exception ex )
        {
            Debug.LogError($"AIインスタンス初期化中のエラー: {ex.Message}\n{ex.StackTrace}");
            yield break;
        }

        Debug.Log("完了: OneTimeSetUp");
    }

    [TearDown]
    public void OneTimeTearDown()
    {
        Debug.Log("開始: OneTimeTearDown");
        // メモリリソースの解放
        this.DisposeTestData();
        Debug.Log("完了: OneTimeTearDown");
    }

    /// <summary>
    /// テストデータの初期化
    /// </summary>
    private void InitializeTestData()
    {
        Debug.Log($"開始: InitializeTestData (CharacterCount={this._characterCount})");
        try
        {
            // UnsafeListの初期化
            this._characterData = new UnsafeList<CharacterData>(this._characterCount, Allocator.Persistent);
            this._characterData.Resize(this._characterCount, NativeArrayOptions.ClearMemory);
            Debug.Log($"_characterData初期化完了: Length={this._characterData.Length}, IsCreated={this._characterData.IsCreated}");

            this._judgeResultJob = new UnsafeList<MovementInfo>(this._characterCount, Allocator.Persistent);
            this._judgeResultJob.Resize(this._characterCount, NativeArrayOptions.ClearMemory);

            this._judgeResultNonJob = new UnsafeList<MovementInfo>(this._characterCount, Allocator.Persistent);
            this._judgeResultNonJob.Resize(this._characterCount, NativeArrayOptions.ClearMemory);

            // チームごとのヘイトマップを初期化
            this._teamHate = new NativeHashMap<int2, int>(3, Allocator.Persistent);
            Debug.Log($"_teamHate配列初期化完了: Length={this._teamHate.Count}, IsCreated={this._teamHate.IsCreated}");

            // 陣営関係マップを初期化
            this._relationMap = new NativeArray<int>(3, Allocator.Persistent);

            for ( int i = 0; i < this._relationMap.Length; i++ )
            {
                // プレイヤーは敵に敵対、敵はプレイヤーに敵対、他は中立など
                switch ( (CharacterSide)i )
                {
                    case CharacterSide.プレイヤー:
                        this._relationMap[i] = 1 << (int)CharacterSide.魔物;  // プレイヤーは敵に敵対
                        break;
                    case CharacterSide.魔物:
                        this._relationMap[i] = 1 << (int)CharacterSide.プレイヤー;  // 敵はプレイヤーに敵対
                        break;
                    case CharacterSide.その他:
                    default:
                        this._relationMap[i] = 0;  // 中立は誰にも敵対しない
                        break;
                }
            }

            this._dataInitialized = true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeTestDataでのエラー: {ex.Message}\n{ex.StackTrace}");
            this._dataInitialized = false;
        }

        Debug.Log("完了: InitializeTestData");
    }

    /// <summary>
    /// AIインスタンスの初期化
    /// </summary>
    private void InitializeAIInstances()
    {
        Debug.Log("開始: InitializeAIInstances");
        try
        {
            // 各コンテナの状態確認
            if ( !this._teamHate.IsCreated )
            {
                Debug.LogError("teamHateが初期化されていません");
                return;
            }

            if ( !this._characterData.IsCreated )
            {
                Debug.LogError("characterDataが初期化されていません");
                return;
            }

            if ( !this._relationMap.IsCreated )
            {
                Debug.LogError("relationMapが初期化されていません");
                return;
            }

            // チームヘイトの各要素を確認
            for ( int i = 0; i < this._teamHate.Count; i++ )
            {
                if ( !this._teamHate.IsCreated )
                {
                    Debug.LogError($"teamHate[{i}]が初期化されていません");
                    return;
                }

                Debug.Log($"teamHate[{i}].Count={this._teamHate.Count}, IsCreated={this._teamHate.IsCreated}");
            }

            // AITestJobの初期化
            this._aiTestJob = new AITestJob
            {
                teamHate = this._teamHate,
                characterData = this._characterData,
                nowTime = this._nowTime,
                judgeResult = this._judgeResultJob,
                relationMap = this._relationMap
            };

            // NonJobAIの初期化
            this._nonJobAI = new NonJobAI
            {
                teamHate = this._teamHate,
                characterData = this._characterData,
                nowTime = this._nowTime,
                judgeResult = this._judgeResultNonJob,
                relationMap = this._relationMap
            };

            this._aiInstancesInitialized = true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeAIInstancesでのエラー: {ex.Message}\n{ex.StackTrace}");
            this._aiInstancesInitialized = false;
        }

        Debug.Log($"完了: InitializeAIInstances (成功={this._aiInstancesInitialized})");
    }

    /// <summary>
    /// キャラクターデータの初期化
    /// </summary>
    private IEnumerator InitializeCharacterData()
    {
        Debug.Log($"開始: InitializeCharacterData (CharacterCount={this._characterCount})");

        // _characterDataが初期化されているか確認
        if ( !this._characterData.IsCreated )
        {
            Debug.LogError("_characterDataが初期化されていません");
            this._charactersInitialized = false;
            yield break;
        }

        // _teamHateが初期化されているか確認
        if ( !this._teamHate.IsCreated )
        {
            Debug.LogError("_teamHateが初期化されていません");
            this._charactersInitialized = false;
            yield break;
        }

        // プレハブの存在確認
        bool allPrefabsValid = true;
        for ( int i = 0; i < this.types.Length; i++ )
        {
            AsyncOperationHandle<IList<IResourceLocation>> checkOp = Addressables.LoadResourceLocationsAsync(this.types[i]);
            yield return checkOp;

            if ( checkOp.Result.Count == 0 )
            {
                Debug.LogError($"プレハブが見つかりません: {this.types[i]}");
                allPrefabsValid = false;
            }
            else
            {
                Debug.Log($"プレハブを確認: {this.types[i]}");
            }
        }

        if ( !allPrefabsValid )
        {
            Debug.LogError("一部のプレハブが見つかりませんでした");
            this._charactersInitialized = false;
            yield break;
        }

        // 複数のオブジェクトを並列でインスタンス化
        var tasks = new List<AsyncOperationHandle<GameObject>>(this._characterCount);

        for ( int i = 0; i < this._characterCount; i++ )
        {
            // Addressablesを使用してインスタンス化
            var task = Addressables.InstantiateAsync(this.types[i % 3]);
            tasks.Add(task);

            // 100個ごとにフレームスキップ（パフォーマンス対策）
            if ( i % 100 == 0 && i > 0 )
            {
                yield return null;
            }
        }

        Debug.Log($"インスタンス化リクエスト完了: {tasks.Count}個");

        // すべてのオブジェクトが生成されるのを待つ
        bool allTasksCompleted = true;
        foreach ( var task in tasks )
        {
            yield return task;
            if ( task.Status != AsyncOperationStatus.Succeeded )
            {
                allTasksCompleted = false;
                Debug.LogError("オブジェクトのインスタンス化に失敗しました");
            }
        }

        if ( !allTasksCompleted )
        {
            Debug.LogError("一部のオブジェクトのインスタンス化に失敗しました");
            this._charactersInitialized = false;
            yield break;
        }

        Debug.Log("全オブジェクトのインスタンス化完了");

        // チェックポイント：_teamHateの状態を確認
        for ( int i = 0; i < this._teamHate.Count; i++ )
        {
            Debug.Log($"キャラ生成後の_teamHate[{i}]: IsCreated={this._teamHate.IsCreated}, Count={this._teamHate.Count}");
        }

        // 生成されたオブジェクトと必要なコンポーネントを取得
        int successCount = 0;

        for ( int i = 0; i < tasks.Count; i++ )
        {
            GameObject obj;
            try
            {
                obj = tasks[i].Result;
            }
            catch ( Exception ex )
            {
                Debug.LogError($"オブジェクト取得時にエラー: {ex.Message}");
                continue;
            }

            if ( obj == null )
            {
                Debug.LogError($"インデックス{i}のオブジェクトがnullです");
                continue;
            }

            var aiComponent = obj.GetComponent<JTestAIBase>();
            if ( aiComponent == null )
            {
                Debug.LogError($"JTestAIBaseコンポーネントが見つかりません: {obj.name}");
                continue;
            }

            CharacterData data;
            try
            {
                (JobAITestStatus, GameObject) mat = aiComponent.MakeTestData();
                data = new CharacterData(mat.Item1, mat.Item2);
                this._characterData[i] = data;
            }
            catch ( Exception ex )
            {
                Debug.LogError($"データ生成時にエラー: {ex.Message}");
                continue;
            }

            successCount++;

            // ヘイトマップも初期化
            int teamNum = (int)data.liveData.belong;

            if ( !this._teamHate.IsCreated )
            {
                Debug.LogError($"_teamHate[{teamNum}]が無効です");
                continue;
            }

            int2 hateKey = new(teamNum, data.hashCode);

            try
            {
                if ( this._teamHate.ContainsKey(hateKey) )
                {
                    Debug.LogWarning($"重複するhashCode: {data.hashCode} (チーム: {teamNum})");
                    continue;
                }

                this._teamHate.Add(hateKey, 10);
            }
            catch ( Exception ex )
            {
                Debug.LogError($"ヘイトマップ更新時にエラー: {ex.Message}");
                continue;
            }

            // 100個ごとにログ
            if ( i % 100 == 0 )
            {
                Debug.Log($"キャラクター初期化進捗: {i}/{tasks.Count}, teamHate[{teamNum}].Count={this._teamHate.Count}");
            }
        }

        Debug.Log($"キャラクターデータ初期化完了: 成功数={successCount}/{tasks.Count}");

        // 各teamHateの最終状態を確認
        for ( int i = 0; i < this._teamHate.Count; i++ )
        {
            Debug.Log($"最終_teamHate[{i}]: IsCreated={this._teamHate.IsCreated}, Count={this._teamHate.Count}");
        }

        // キャラクターデータのステータスをランダム化
        Debug.Log("キャラクターデータのランダム化を開始");

        for ( int i = 0; i < this._characterData.Length; i++ )
        {
            CharacterData data = this._characterData[i];

            try
            {
                CharacterDataRandomizer.RandomizeCharacterData(ref data, this._characterData);
                this._characterData[i] = data;
            }
            catch ( Exception ex )
            {
                Debug.LogError($"データランダム化時にエラー (index={i}): {ex.Message}");
            }

            // 100個ごとにフレームスキップ
            if ( i % 100 == 0 && i > 0 )
            {
                yield return null;
            }
        }

        Debug.Log("キャラクターデータのランダム化完了");

        this._charactersInitialized = successCount > 0;
        Debug.Log($"完了: InitializeCharacterData (成功={this._charactersInitialized})");
    }

    /// <summary>
    /// テストデータのメモリ解放
    /// </summary>
    private void DisposeTestData()
    {
        Debug.Log("開始: DisposeTestData");
        try
        {
            // UnsafeListの解放
            if ( this._characterData.IsCreated )
            {
                Debug.Log($"_characterDataの解放開始: Length={this._characterData.Length}");
                // 各キャラクターデータ内のネイティブコンテナを解放
                for ( int i = 0; i < this._characterData.Length; i++ )
                {
                    CharacterData data = this._characterData[i];
                    data.Dispose();
                }

                this._characterData.Dispose();
                Debug.Log("_characterDataの解放完了");
            }

            if ( this._judgeResultJob.IsCreated )
            {
                this._judgeResultJob.Dispose();
                Debug.Log("_judgeResultJobの解放完了");
            }

            if ( this._judgeResultNonJob.IsCreated )
            {
                this._judgeResultNonJob.Dispose();
                Debug.Log("_judgeResultNonJobの解放完了");
            }

            // チームヘイトマップの解放
            if ( this._teamHate.IsCreated )
            {
                Debug.Log($"_teamHateの解放開始: Length={this._teamHate.Count}");

                this._teamHate.Dispose();
                Debug.Log("_teamHateの解放完了");
            }

            // その他のNativeArrayの解放
            if ( this._relationMap.IsCreated )
            {
                this._relationMap.Dispose();
                Debug.Log("_relationMapの解放完了");
            }

            this._dataInitialized = false;
            this._charactersInitialized = false;
            this._aiInstancesInitialized = false;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"DisposeTestDataでのエラー: {ex.Message}\n{ex.StackTrace}");
        }

        Debug.Log("完了: DisposeTestData");
    }

    /// <summary>
    /// 各テスト実行前のステータスランダム化
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        Debug.Log("開始: SetUp");

        // 初期化チェック
        if ( !this._dataInitialized || !this._charactersInitialized || !this._aiInstancesInitialized )
        {
            Debug.LogError($"初期化が完了していません: Data={this._dataInitialized}, Characters={this._charactersInitialized}, AI={this._aiInstancesInitialized}");
        }

        // _teamHateの状態確認
        if ( this._teamHate.IsCreated )
        {
            for ( int i = 0; i < this._teamHate.Count; i++ )
            {
                Debug.Log($"SetUpでの_teamHate[{i}]: IsCreated={this._teamHate.IsCreated}, Count={this._teamHate.Count}");
            }
        }
        else
        {
            Debug.LogError("SetUpで_teamHateが初期化されていません");
        }

        try
        {
            // キャラクターデータのステータスをランダム化
            // 個々のCharacterDataを処理する形に変更
            for ( int i = 0; i < this._characterData.Length; i++ )
            {
                CharacterData data = this._characterData[i];
                CharacterDataRandomizer.RandomizeCharacterData(ref data, this._characterData);
                this._characterData[i] = data;
            }

            // AIインスタンスの時間を更新
            this._aiTestJob.nowTime = this._nowTime;
            this._nonJobAI.nowTime = this._nowTime;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"SetUpでのエラー: {ex.Message}\n{ex.StackTrace}");
        }

        Debug.Log("完了: SetUp");
    }

    /// <summary>
    /// JobSystemのAI処理パフォーマンステスト
    /// </summary>
    [Test, Performance]
    public void JobSystemAI_Performance_Test()
    {
        Debug.Log("開始: JobSystemAI_Performance_Test");

        // _teamHateの状態を詳細にログ
        if ( this._teamHate.IsCreated )
        {
            Debug.Log($"テスト内_teamHate: Length={this._teamHate.Count}, IsCreated={this._teamHate.IsCreated}");
            for ( int i = 0; i < this._teamHate.Count; i++ )
            {
                Debug.Log($"テスト内_teamHate[{i}]: Count={this._teamHate.Count}, IsCreated={this._teamHate.IsCreated}");

                // キーのサンプルをログ（最大5個）
                var enumerator = this._teamHate.GetEnumerator();
                int count = 0;
                while ( enumerator.MoveNext() && count < 5 )
                {
                    Debug.Log($"  サンプルキー: {enumerator.Current.Key}, 値: {enumerator.Current.Value}");
                    count++;
                }

            }
        }
        else
        {
            Debug.LogError("テスト内で_teamHateが無効です");
        }

        // AITestJobを再初期化してみる
        try
        {
            // AITestJobの初期化
            this._aiTestJob = new AITestJob
            {
                teamHate = this._teamHate,
                characterData = this._characterData,
                nowTime = this._nowTime,
                judgeResult = this._judgeResultJob,
                relationMap = this._relationMap
            };

            Debug.Log("AITestJobを再初期化しました");

            // ネイティブコンテナが有効か最終確認
            Debug.Log($"実行前の最終チェック: teamHate.IsCreated={this._teamHate.IsCreated}, チーム数={this._teamHate.Count}");
            if ( this._teamHate.IsCreated )
            {
                for ( int i = 0; i < this._teamHate.Count; i++ )
                {
                    Debug.Log($"_teamHate[{i}].IsCreated={this._teamHate.IsCreated}, Count={this._teamHate.Count}");
                }
            }

            Measure.Method(() =>
            {
                // JobSystemのAI処理実行
                JobHandle handle = this._aiTestJob.Schedule(this._characterCount, 64);
                handle.Complete();
            })
            .WarmupCount(3)       // ウォームアップ回数
            .MeasurementCount(10) // 計測回数
            .IterationsPerMeasurement(1) // 1回の計測あたりの実行回数
            .GC()                 // GCの計測も行う
            .Run();
        }
        catch ( Exception ex )
        {
            Debug.LogError($"JobSystemAI_Performance_Testでのエラー: {ex.ToString()}");
        }

        Debug.Log("完了: JobSystemAI_Performance_Test");
    }

    /// <summary>
    /// 結果の検証テスト（両実装が同じ結果を出すか確認）
    /// </summary>
    [Test]
    public void Verify_Results_Are_Same()
    {
        Debug.Log("開始: Verify_Results_Are_Same");

        // 初期化状態のチェック
        if ( !this._dataInitialized || !this._charactersInitialized || !this._aiInstancesInitialized )
        {
            Debug.LogError($"初期化が完了していません: Data={this._dataInitialized}, Characters={this._charactersInitialized}, AI={this._aiInstancesInitialized}");
            Assert.Fail("初期化が完了していないため、テストを実行できません");
            return;
        }

        // _teamHateの状態確認
        if ( this._teamHate.IsCreated )
        {
            for ( int i = 0; i < this._teamHate.Count; i++ )
            {
                Debug.Log($"検証テスト内_teamHate[{i}]: IsCreated={this._teamHate.IsCreated}, Count={this._teamHate.Count}");
            }
        }
        else
        {
            Debug.LogError("検証テスト内で_teamHateが初期化されていません");
            Assert.Fail("_teamHateが初期化されていないため、テストを実行できません");
            return;
        }

        try
        {
            // AIJobの再初期化
            this._aiTestJob = new AITestJob
            {
                teamHate = this._teamHate,
                characterData = this._characterData,
                nowTime = this._nowTime,
                judgeResult = this._judgeResultJob,
                relationMap = this._relationMap
            };

            // データをランダム化
            for ( int i = 0; i < this._characterData.Length; i++ )
            {
                CharacterData data = this._characterData[i];
                CharacterDataRandomizer.RandomizeCharacterData(ref data, this._characterData);
                this._characterData[i] = data;
            }

            // 両方のAI処理を実行
            this._nonJobAI.ExecuteAIDecision();
            Debug.Log("NonJobAI実行完了");

            // ジョブ実行前の最終チェック

            Debug.Log($"ジョブ実行前_teamHate: IsCreated={this._teamHate.IsCreated}, Count={this._teamHate.Count}");

            JobHandle handle = this._aiTestJob.Schedule(this._characterCount, 64);
            handle.Complete();
            Debug.Log("JobSystemAI実行完了");

            // 結果を比較
            bool resultsMatch = true;
            string mismatchInfo = "";

            for ( int i = 0; i < this._characterCount; i++ )
            {
                MovementInfo nonJobResult = this._judgeResultNonJob[i];
                MovementInfo jobResult = this._judgeResultJob[i];

                // 結果が一致するか確認
                if ( nonJobResult.result != jobResult.result ||
                    nonJobResult.actNum != jobResult.actNum ||
                    nonJobResult.targetHash != jobResult.targetHash )
                {
                    resultsMatch = false;
                    mismatchInfo = $"Mismatch at index {i}: NonJob({nonJobResult.result}, {nonJobResult.actNum}, {nonJobResult.targetHash}) " +
                                   $"vs Job({jobResult.result}, {jobResult.actNum}, {jobResult.targetHash})";
                    break;
                }
            }

            // 検証
            Assert.IsTrue(resultsMatch, mismatchInfo);
        }
        catch ( Exception ex )
        {
            Debug.LogError($"検証テスト実行中のエラー: {ex.ToString()}");
            Assert.Fail($"テスト実行中にエラーが発生: {ex.Message}");
        }

        Debug.Log("完了: Verify_Results_Are_Same");
    }
}