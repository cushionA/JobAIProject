using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.PerformanceTesting;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using System;
using static JTestAIBase;
using static CombatManager;
using static JobAITestStatus;
using static AiFunctionLibrary;
using Unity.Burst;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

/// <summary>
/// AITestJobのパフォーマンステスト
/// </summary>
public class AIPerformanceTests
{
    // テスト用のデータ
    private UnsafeList<CharacterData> _characterData;
    private UnsafeList<MovementInfo> _judgeResultJob;
    private UnsafeList<MovementInfo> _judgeResultNonJob;
    private NativeArray<NativeHashMap<int, int>> _teamHate;
    private NativeArray<int> _relationMap;
    //private NativeArray<FunctionPointer<SkipJudgeDelegate>> _skipFunctions;
    //private NativeArray<FunctionPointer<TargetJudgeDelegate>> _targetFunctions;

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

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // テストデータの初期化
        await InitializeTestData();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        // メモリリソースの解放
        DisposeTestData();
    }

    /// <summary>
    /// テストデータの初期化
    /// </summary>
    private async UniTask InitializeTestData()
    {
        // UnsafeListの初期化
        _characterData = new UnsafeList<CharacterData>(_characterCount, Allocator.Persistent);
        _characterData.Resize(_characterCount, NativeArrayOptions.ClearMemory);

        _judgeResultJob = new UnsafeList<MovementInfo>(_characterCount, Allocator.Persistent);
        _judgeResultJob.Resize(_characterCount, NativeArrayOptions.ClearMemory);

        _judgeResultNonJob = new UnsafeList<MovementInfo>(_characterCount, Allocator.Persistent);
        _judgeResultNonJob.Resize(_characterCount, NativeArrayOptions.ClearMemory);

        // チームごとのヘイトマップを初期化
        _teamHate = new NativeArray<NativeHashMap<int, int>>(Enum.GetValues(typeof(CharacterSide)).Length, Allocator.Persistent);
        for ( int i = 0; i < _teamHate.Length; i++ )
        {
            _teamHate[i] = new NativeHashMap<int, int>(_characterCount, Allocator.Persistent);
        }

        // 陣営関係マップを初期化
        _relationMap = new NativeArray<int>(Enum.GetValues(typeof(CharacterSide)).Length, Allocator.Persistent);
        for ( int i = 0; i < _relationMap.Length; i++ )
        {
            // プレイヤーは敵に敵対、敵はプレイヤーに敵対、他は中立など
            switch ( (CharacterSide)i )
            {
                case CharacterSide.プレイヤー:
                    _relationMap[i] = 1 << (int)CharacterSide.魔物;  // プレイヤーは敵に敵対
                    break;
                case CharacterSide.魔物:
                    _relationMap[i] = 1 << (int)CharacterSide.プレイヤー;  // 敵はプレイヤーに敵対
                    break;
                case CharacterSide.その他:
                default:
                    _relationMap[i] = 0;  // 中立は誰にも敵対しない
                    break;
            }
        }

        // キャラクターデータの初期化
        await InitializeCharacterData();

        // AIインスタンスの初期化
        InitializeAIInstances();
    }

    /// <summary>
    /// AIインスタンスの初期化
    /// </summary>
    private void InitializeAIInstances()
    {
        // AITestJobの初期化
        _aiTestJob = new AITestJob
        {
            teamHate = _teamHate,
            characterData = _characterData,
            nowTime = _nowTime,
            judgeResult = _judgeResultJob,
            relationMap = _relationMap,
            skipFunctions = AiFunctionLibrary.skipFunctions,
            targetFunctions = AiFunctionLibrary.targetFunctions
        };

        // NonJobAIの初期化
        _nonJobAI = new NonJobAI
        {
            teamHate = _teamHate,
            characterData = _characterData,
            nowTime = _nowTime,
            judgeResult = _judgeResultNonJob,
            relationMap = _relationMap,
            skipFunctions = AiFunctionLibrary.skipFunctions,
            targetFunctions = AiFunctionLibrary.targetFunctions
        };
    }

    /// <summary>
    /// キャラクターデータの初期化
    /// </summary>
    private async UniTask InitializeCharacterData()
    {
        // 複数のオブジェクトを並列でインスタンス化
        var tasks = new List<Task<GameObject>>();
        var instantiatedObjects = new List<GameObject>();

        for ( int i = 0; i < _characterCount; i++ )
        {
            // Addressablesを使用してインスタンス化タスクを作成し、リストに追加
            var task = Addressables.InstantiateAsync(types[i % 3]).Task
                .ContinueWith(t =>
                {
                    // 完了したらGameObjectを取得
                    return t.Result.gameObject;
                });

            tasks.Add(task);
        }

        // すべてのインスタンス化タスクが完了するのを待機
        var results = await Task.WhenAll(tasks);

        // インスタンス化されたオブジェクトを保存
        instantiatedObjects.AddRange(results);

        // キャラデータ作成
        for ( int i = 0; i < instantiatedObjects.Count; i++ )
        {
            _characterData.Add(instantiatedObjects[i].GetComponent<JTestAIBase>().MakeTestData());

            // ヘイトマップも初期化
            int teamNum = (int)_characterData[i].liveData.belong;
            _teamHate[teamNum].Add(_characterData[i].hashCode, 10);
        }

        // キャラクターデータのステータスをランダム化
        // 個々のCharacterDataを処理する形に変更
        for ( int i = 0; i < _characterData.Length; i++ )
        {
            CharacterData data = _characterData[i];
            CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
            _characterData[i] = data;
        }
    }

    /// <summary>
    /// テストデータのメモリ解放
    /// </summary>
    private void DisposeTestData()
    {
        // UnsafeListの解放
        if ( _characterData.IsCreated )
        {
            // 各キャラクターデータ内のネイティブコンテナを解放
            for ( int i = 0; i < _characterData.Length; i++ )
            {
                CharacterData data = _characterData[i];
                data.Dispose();
            }

            _characterData.Dispose();
        }

        if ( _judgeResultJob.IsCreated )
            _judgeResultJob.Dispose();

        if ( _judgeResultNonJob.IsCreated )
            _judgeResultNonJob.Dispose();

        // チームヘイトマップの解放
        if ( _teamHate.IsCreated )
        {
            for ( int i = 0; i < _teamHate.Length; i++ )
            {
                if ( _teamHate[i].IsCreated )
                    _teamHate[i].Dispose();
            }

            _teamHate.Dispose();
        }

        // その他のNativeArrayの解放
        if ( _relationMap.IsCreated )
            _relationMap.Dispose();
    }

    /// <summary>
    /// 各テスト実行前のステータスランダム化
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        // キャラクターデータのステータスをランダム化
        // 個々のCharacterDataを処理する形に変更
        for ( int i = 0; i < _characterData.Length; i++ )
        {
            CharacterData data = _characterData[i];
            CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
            _characterData[i] = data;
        }

        // AIインスタンスの時間を更新
        _aiTestJob.nowTime = _nowTime;
        _nonJobAI.nowTime = _nowTime;
    }

    /// <summary>
    /// 非JobSystemのAI処理パフォーマンステスト
    /// </summary>
    [Test, Performance]
    public void NonJobAI_Performance_Test()
    {
        Measure.Method(() =>
        {
            // 非JobSystemのAI処理実行
            _nonJobAI.ExecuteAIDecision();
        })
        .WarmupCount(3)       // ウォームアップ回数
        .MeasurementCount(10) // 計測回数
        .IterationsPerMeasurement(1) // 1回の計測あたりの実行回数
        .GC()                 // GCの計測も行う
        .Run();
    }

    /// <summary>
    /// JobSystemのAI処理パフォーマンステスト
    /// </summary>
    [Test, Performance]
    public void JobSystemAI_Performance_Test()
    {
        Measure.Method(() =>
        {
            // JobSystemのAI処理実行
            JobHandle handle = _aiTestJob.Schedule(_characterCount, 64);
            handle.Complete();
        })
        .WarmupCount(3)       // ウォームアップ回数
        .MeasurementCount(10) // 計測回数
        .IterationsPerMeasurement(1) // 1回の計測あたりの実行回数
        .GC()                 // GCの計測も行う
        .Run();
    }

    /// <summary>
    /// 異なるキャラクター数でのパフォーマンス比較テスト
    /// </summary>
    [UnityTest, Performance]
    public IEnumerator Compare_Different_Character_Counts()
    {
        // テスト用のキャラクター数の配列
        int[] characterCounts = { 100, 500, 1000, 5000, 10000 };

        foreach ( int count in characterCounts )
        {
            // テストケース名を設定
            using ( Measure.Scope($"Character Count: {count}") )
            {
                // キャラクター数の更新と再初期化
                UniTask recreateTask = RecreateTestData(count);

                // UniTaskの完了を待機
                while ( !recreateTask.Status.IsCompleted() )
                {
                    yield return null;
                }

                // 非JobSystemテスト
                using ( Measure.Scope("NonJobAI") )
                {
                    _nonJobAI.ExecuteAIDecision();
                }

                // フレームスキップ
                yield return null;

                // JobSystemテスト
                using ( Measure.Scope("JobSystemAI") )
                {
                    JobHandle handle = _aiTestJob.Schedule(count, 64);
                    handle.Complete();
                }
            }

            // 次のテストの前にフレームをスキップ
            yield return null;
        }
    }

    /// <summary>
    /// テストデータの再作成（キャラクター数変更時）
    /// </summary>
    private async UniTask RecreateTestData(int newCharacterCount)
    {
        // 現在のデータを解放
        DisposeTestData();

        // キャラクター数を更新
        _characterCount = newCharacterCount;

        // 新しいデータを初期化して完了を待機
        await InitializeTestData();
    }

    /// <summary>
    /// 結果の検証テスト（両実装が同じ結果を出すか確認）
    /// </summary>
    [Test]
    public void Verify_Results_Are_Same()
    {
        // データをランダム化
        // 個々のCharacterDataを処理する形に変更
        for ( int i = 0; i < _characterData.Length; i++ )
        {
            CharacterData data = _characterData[i];
            CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
            _characterData[i] = data;
        }

        // 両方のAI処理を実行
        _nonJobAI.ExecuteAIDecision();

        JobHandle handle = _aiTestJob.Schedule(_characterCount, 64);
        handle.Complete();

        // 結果を比較
        bool resultsMatch = true;
        string mismatchInfo = "";

        for ( int i = 0; i < _characterCount; i++ )
        {
            MovementInfo nonJobResult = _judgeResultNonJob[i];
            MovementInfo jobResult = _judgeResultJob[i];

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
}