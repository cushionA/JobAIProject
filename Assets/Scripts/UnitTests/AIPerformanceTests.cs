using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.PerformanceTesting;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using static JTestAIBase;
using static CombatManager;
using static JobAITestStatus;
using static AiFunctionLibrary;
using Unity.Burst;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using System.Text;
using UnityEngine.ResourceManagement.ResourceLocations;

/// <summary>
/// AITestJobのパフォーマンステスト
/// </summary>
public class AIPerformanceTests
{
    // テスト用のデータ
    private UnsafeList<CharacterData> _characterData;
    private UnsafeList<MovementInfo> _judgeResultJob;
    private UnsafeList<MovementInfo> _judgeResultNonJob;
    private List<MovementInfo> _judgeResultStandard; // StandardAI用の結果リスト
    private NativeHashMap<int2, int> _teamHate;
    private NativeArray<int> _relationMap;

    // 初期化状態を追跡するフラグ
    private bool _dataInitialized = false;
    private bool _charactersInitialized = false;
    private bool _aiInstancesInitialized = false;

    int jobBatchCount = 50;

    /// <summary>
    /// 
    /// </summary>
    private JTestAIBase[] characters;

    /// <summary>
    /// 生成オブジェクトの配列。
    /// </summary>
    private string[] types = new string[] { "Assets/Prefab/JobAI/TypeA.prefab", "Assets/Prefab/JobAI/TypeB.prefab", "Assets/Prefab/JobAI/TypeC.prefab" };

    // テスト用のパラメータ
    private int _characterCount = 18;
    private float _nowTime = 100.0f;

    // AIテスト用のインスタンス
    private AITestJob _aiTestJob;
    private NonJobAI _nonJobAI;
    private StandardAI _standardAI; // 標準コレクションを使用するAI

    [UnitySetUp]
    public IEnumerator OneTimeSetUp()
    {
        Debug.Log("開始: OneTimeSetUp");

        // テストデータの初期化
        try
        {
            InitializeTestData();
            Debug.Log($"テストデータの初期化完了: teamHate.IsCreated={_teamHate.IsCreated}, Length={_teamHate.Count}");
        }
        catch ( Exception ex )
        {
            Debug.LogError($"テストデータ初期化中のエラー: {ex.Message}\n{ex.StackTrace}");
            yield break;
        }

        // キャラクターデータの初期化 - IEnumeratorなので yield returnする
        yield return InitializeCharacterData();

        if ( !_charactersInitialized )
        {
            Debug.LogError("キャラクターデータの初期化に失敗しました");
            yield break;
        }

        Debug.Log($"キャラクターデータの初期化完了: characterData.Length={_characterData.Length}");

        // teamHateの中身を確認
        for ( int i = 0; i < _teamHate.Count; i++ )
        {
            Debug.Log($"teamHate[{i}].Count={_teamHate.Count}, IsCreated={_teamHate.IsCreated}");
        }

        // AIインスタンスの初期化
        try
        {
            InitializeAIInstances();
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
        DisposeTestData();
        Debug.Log("完了: OneTimeTearDown");
    }

    /// <summary>
    /// テストデータの初期化
    /// </summary>
    private void InitializeTestData()
    {
        Debug.Log($"開始: InitializeTestData (CharacterCount={_characterCount})");
        try
        {
            // UnsafeListの初期化
            _characterData = new UnsafeList<CharacterData>(_characterCount, Allocator.Persistent);
            _characterData.Resize(_characterCount, NativeArrayOptions.ClearMemory);
            Debug.Log($"_characterData初期化完了: Length={_characterData.Length}, IsCreated={_characterData.IsCreated}");

            _judgeResultJob = new UnsafeList<MovementInfo>(_characterCount, Allocator.Persistent);
            _judgeResultJob.Resize(_characterCount, NativeArrayOptions.ClearMemory);

            _judgeResultNonJob = new UnsafeList<MovementInfo>(_characterCount, Allocator.Persistent);
            _judgeResultNonJob.Resize(_characterCount, NativeArrayOptions.ClearMemory);

            // StandardAI用の結果リストを初期化
            _judgeResultStandard = new List<MovementInfo>(_characterCount);
            for ( int i = 0; i < _characterCount; i++ )
            {
                _judgeResultStandard.Add(new MovementInfo());
            }

            // チームごとのヘイトマップを初期化
            _teamHate = new NativeHashMap<int2, int>(3, Allocator.Persistent);
            Debug.Log($"_teamHate配列初期化完了: Length={_teamHate.Count}, IsCreated={_teamHate.IsCreated}");

            // 陣営関係マップを初期化
            _relationMap = new NativeArray<int>(3, Allocator.Persistent);

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

            _dataInitialized = true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeTestDataでのエラー: {ex.Message}\n{ex.StackTrace}");
            _dataInitialized = false;
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
            if ( !_teamHate.IsCreated )
            {
                Debug.LogError("teamHateが初期化されていません");
                return;
            }

            if ( !_characterData.IsCreated )
            {
                Debug.LogError("characterDataが初期化されていません");
                return;
            }

            if ( !_relationMap.IsCreated )
            {
                Debug.LogError("relationMapが初期化されていません");
                return;
            }

            // チームヘイトの各要素を確認
            for ( int i = 0; i < _teamHate.Count; i++ )
            {
                if ( !_teamHate.IsCreated )
                {
                    Debug.LogError($"teamHate[{i}]が初期化されていません");
                    return;
                }
                Debug.Log($"teamHate[{i}].Count={_teamHate.Count}, IsCreated={_teamHate.IsCreated}");
            }

            // AITestJobの初期化
            _aiTestJob = new AITestJob
            {
                teamHate = _teamHate,
                characterData = _characterData,
                nowTime = _nowTime,
                judgeResult = _judgeResultJob,
                relationMap = _relationMap,
            };

            // NonJobAIの初期化
            _nonJobAI = new NonJobAI
            {
                teamHate = _teamHate,
                characterData = _characterData,
                nowTime = _nowTime,
                judgeResult = _judgeResultNonJob,
                relationMap = _relationMap
            };

            // StandardAIの初期化（NativeContainerからデータをコピー）
            _standardAI = new StandardAI(_teamHate, _characterData, _nowTime, _relationMap);
            _standardAI.judgeResult = _judgeResultStandard;

            _aiInstancesInitialized = true;
        }
        catch ( Exception ex )
        {
            Debug.LogError($"InitializeAIInstancesでのエラー: {ex.Message}\n{ex.StackTrace}");
            _aiInstancesInitialized = false;
        }
        Debug.Log($"完了: InitializeAIInstances (成功={_aiInstancesInitialized})");
    }

    /// <summary>
    /// キャラクターデータの初期化
    /// </summary>
    private IEnumerator InitializeCharacterData()
    {
        Debug.Log($"開始: InitializeCharacterData (CharacterCount={_characterCount})");

        // _characterDataが初期化されているか確認
        if ( !_characterData.IsCreated )
        {
            Debug.LogError("_characterDataが初期化されていません");
            _charactersInitialized = false;
            yield break;
        }

        // _teamHateが初期化されているか確認
        if ( !_teamHate.IsCreated )
        {
            Debug.LogError("_teamHateが初期化されていません");
            _charactersInitialized = false;
            yield break;
        }

        // プレハブの存在確認
        bool allPrefabsValid = true;
        for ( int i = 0; i < types.Length; i++ )
        {
            AsyncOperationHandle<IList<IResourceLocation>> checkOp = default;
            checkOp = Addressables.LoadResourceLocationsAsync(types[i]);
            yield return checkOp;

            if ( checkOp.Result.Count == 0 )
            {
                Debug.LogError($"プレハブが見つかりません: {types[i]}");
                allPrefabsValid = false;
            }
            else
            {
                Debug.Log($"プレハブを確認: {types[i]}");
            }
        }

        if ( !allPrefabsValid )
        {
            Debug.LogError("一部のプレハブが見つかりませんでした");
            _charactersInitialized = false;
            yield break;
        }

        // 複数のオブジェクトを並列でインスタンス化
        var tasks = new List<AsyncOperationHandle<GameObject>>(_characterCount);

        for ( int i = 0; i < _characterCount; i++ )
        {
            // Addressablesを使用してインスタンス化
            var task = Addressables.InstantiateAsync(types[i % 3]);
            tasks.Add(task);

            // 100個ごとにフレームスキップ（パフォーマンス対策）
            if ( i % 100 == 0 && i > 0 )
            {
                yield return null;
            }
        }

        Debug.Log($"インスタンス化リクエスト完了: {tasks.Count}個 {_characterCount}");

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
            _charactersInitialized = false;
            yield break;
        }

        Debug.Log("全オブジェクトのインスタンス化完了");

        // チェックポイント：_teamHateの状態を確認
        for ( int i = 0; i < _teamHate.Count; i++ )
        {
            Debug.Log($"キャラ生成後の_teamHate[{i}]: IsCreated={_teamHate.IsCreated}, Count={_teamHate.Count}");
        }

        // 生成されたオブジェクトと必要なコンポーネントを取得
        int successCount = 0;

        for ( int i = 0; i < tasks.Count; i++ )
        {
            GameObject obj = null;
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

            CharacterData data = default;
            try
            {
                data = aiComponent.MakeTestData();
                _characterData[i] = data;
            }
            catch ( Exception ex )
            {
                Debug.LogError($"データ生成時にエラー: {ex.Message}");
                continue;
            }

            successCount++;

            // ヘイトマップも初期化
            int teamNum = (int)data.liveData.belong;

            if ( !_teamHate.IsCreated )
            {
                Debug.LogError($"_teamHate[{teamNum}]が無効です");
                continue;
            }

            int2 hateKey = new int2(teamNum, data.hashCode);

            try
            {
                if ( _teamHate.ContainsKey(hateKey) )
                {
                    Debug.LogWarning($"重複するhashCode: {data.hashCode} (チーム: {teamNum})");
                    continue;
                }

                _teamHate.Add(hateKey, 10);
            }
            catch ( Exception ex )
            {
                Debug.LogError($"ヘイトマップ更新時にエラー: {ex.Message}");
                continue;
            }

            // 100個ごとにログ
            if ( i % 100 == 0 )
            {
                Debug.Log($"キャラクター初期化進捗: {i}/{tasks.Count}, teamHate[{teamNum}].Count={_teamHate.Count}");
            }
        }

        Debug.Log($"キャラクターデータ初期化完了: 成功数={successCount}/{tasks.Count}");

        // 各teamHateの最終状態を確認
        for ( int i = 0; i < _teamHate.Count; i++ )
        {
            Debug.Log($"最終_teamHate[{i}]: IsCreated={_teamHate.IsCreated}, Count={_teamHate.Count}");
        }

        // キャラクターデータのステータスをランダム化
        Debug.Log("キャラクターデータのランダム化を開始");

        for ( int i = 0; i < _characterData.Length; i++ )
        {
            CharacterData data = _characterData[i];

            try
            {
                CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
                _characterData[i] = data;
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

        _charactersInitialized = successCount > 0;
        Debug.Log($"完了: InitializeCharacterData (成功={_charactersInitialized})");
    }

    public static void DebugPrintCharacterData(CharacterData data)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("===== CharacterData詳細情報 =====");
        sb.AppendLine($"ハッシュコード: {data.hashCode}");

        // 基本情報
        sb.AppendLine($"最終判断時間: {data.lastJudgeTime}");
        sb.AppendLine($"最終移動判断時間: {data.lastMoveJudgeTime}");
        sb.AppendLine($"移動判断間隔: {data.moveJudgeInterval}");
        sb.AppendLine($"ターゲット数: {data.targetingCount}");

        // ライブデータ
        sb.AppendLine("【LiveData情報】");
        sb.AppendLine($"  現在位置: {data.liveData.nowPosition}");
        sb.AppendLine($"  現在HP: {data.liveData.currentHp}/{data.liveData.maxHp}");
        sb.AppendLine($"  所属: {data.liveData.belong}");
        sb.AppendLine($"  状態: {data.liveData.actState}");
        // 他のliveDataフィールドも必要に応じて追加

        // BrainData情報
        sb.AppendLine("【BrainData情報】");
        if ( data.brainData.IsCreated )
        {
            sb.AppendLine($"  登録数: {data.brainData.Count}");
            var keys = data.brainData.GetKeyArray(Allocator.Temp);
            try
            {
                foreach ( var key in keys )
                {
                    if ( data.brainData.TryGetValue(key, out var brainStatus) )
                    {
                        sb.AppendLine($"  モード[{key}]:");
                        sb.AppendLine($"    判断間隔: {brainStatus.judgeInterval}");
                        // 他のbrainStatusフィールドも必要に応じて追加
                    }
                }
                keys.Dispose();
            }
            catch ( Exception ex )
            {
                sb.AppendLine($"  BrainDataアクセス中にエラー: {ex.Message}");
                if ( keys.IsCreated )
                    keys.Dispose();
            }
        }
        else
        {
            sb.AppendLine("  BrainDataは作成されていません");
        }

        // 個人ヘイト情報
        sb.AppendLine("【PersonalHate情報】");
        if ( data.personalHate.IsCreated )
        {
            sb.AppendLine($"  登録数: {data.personalHate.Count}");
            var hateKeys = data.personalHate.GetKeyArray(Allocator.Temp);
            try
            {
                foreach ( var target in hateKeys )
                {
                    if ( data.personalHate.TryGetValue(target, out var hateValue) )
                    {
                        sb.AppendLine($"  対象[{target}]: ヘイト値={hateValue}");
                    }
                }
                hateKeys.Dispose();
            }
            catch ( Exception ex )
            {
                sb.AppendLine($"  PersonalHateアクセス中にエラー: {ex.Message}");
                if ( hateKeys.IsCreated )
                    hateKeys.Dispose();
            }
        }
        else
        {
            sb.AppendLine("  PersonalHateは作成されていません");
        }

        // 近距離キャラクター情報
        sb.AppendLine("【ShortRangeCharacter情報】");
        if ( data.shortRangeCharacter.IsCreated )
        {
            sb.AppendLine($"  登録数: {data.shortRangeCharacter.Length}");
            try
            {
                for ( int i = 0; i < data.shortRangeCharacter.Length; i++ )
                {
                    sb.AppendLine($"  近距離キャラ[{i}]: Hash={data.shortRangeCharacter[i]}");
                }
            }
            catch ( Exception ex )
            {
                sb.AppendLine($"  ShortRangeCharacterアクセス中にエラー: {ex.Message}");
            }
        }
        else
        {
            sb.AppendLine("  ShortRangeCharacterは作成されていません");
        }

        sb.AppendLine("============================");

        // 長いログを分割して出力（Unity consoleの文字数制限回避）
        const int maxLogLength = 1000;
        for ( int i = 0; i < sb.Length; i += maxLogLength )
        {
            int length = Math.Min(maxLogLength, sb.Length - i);
            Debug.Log(sb.ToString(i, length));
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

        // StandardAI用の結果リストは管理されたオブジェクトなのでGCが処理する
        _judgeResultStandard = null;

        // チームヘイトマップの解放
        if ( _teamHate.IsCreated )
        {
            _teamHate.Dispose();
        }

        // その他のNativeArrayの解放
        if ( _relationMap.IsCreated )
            _relationMap.Dispose();
    }

    /// <summary>
    /// 非JobSystemのAI処理パフォーマンステスト
    /// </summary>
    [Test, Performance]
    public void NonJobAI_Performance_Test()
    {
        Debug.Log($"テストデータの初期化完了: teamHate.IsCreated={_teamHate.IsCreated}, Length={_teamHate.Count}");

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
    /// StandardAIのパフォーマンステスト
    /// </summary>
    [Test, Performance]
    public void StandardAI_Performance_Test()
    {
        Debug.Log($"StandardAIテスト開始: characterData.Count={_standardAI.characterData.Count}");

        Measure.Method(() =>
        {
            // StandardAIの処理実行
            _standardAI.ExecuteAIDecision();
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
        Debug.Log($"テストデータの初期化完了: teamHate.IsCreated={_teamHate.IsCreated}, Length={_teamHate.Count}");

        Measure.Method(() =>
        {
            // JobSystemのAI処理実行
            JobHandle handle = _aiTestJob.Schedule(_characterCount, jobBatchCount);
            handle.Complete();
        })
        .WarmupCount(3)       // ウォームアップ回数
        .MeasurementCount(10) // 計測回数
        .IterationsPerMeasurement(1) // 1回の計測あたりの実行回数
        .GC()                 // GCの計測も行う
        .Run();
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
        InitializeTestData();

        Debug.Log($"テストデータの初期化完了: teamHate.IsCreated={_teamHate.IsCreated}, Length={_teamHate.Count}");

        // キャラクターデータの初期化
        await InitializeCharacterData();

        // AIインスタンスの初期化
        InitializeAIInstances();
    }

    /// <summary>
    /// 結果の検証テスト（全実装が同じ結果を出すか確認）
    /// </summary>
    [Test]
    public void Verify_Results_Are_Same()
    {
        // データをランダム化
        for ( int i = 0; i < _characterData.Length; i++ )
        {
            CharacterData data = _characterData[i];
            CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
            _characterData[i] = data;
        }

        // AIインスタンスの時間を更新
        _aiTestJob.nowTime = _nowTime;
        _nonJobAI.nowTime = _nowTime;
        _standardAI.nowTime = _nowTime;

        // 各AIの処理を実行
        _nonJobAI.ExecuteAIDecision();
        _standardAI.ExecuteAIDecision();

        JobHandle handle = _aiTestJob.Schedule(_characterCount, jobBatchCount);
        handle.Complete();

        // 結果を比較（JobとNonJob）
        bool jobNonJobMatch = true;
        string jobNonJobMismatchInfo = "";

        for ( int i = 0; i < _characterCount; i++ )
        {
            MovementInfo nonJobResult = _judgeResultNonJob[i];
            MovementInfo jobResult = _judgeResultJob[i];

            // 結果が一致するか確認
            if ( nonJobResult.result != jobResult.result ||
                nonJobResult.actNum != jobResult.actNum ||
                nonJobResult.targetHash != jobResult.targetHash )
            {
                jobNonJobMatch = false;
                jobNonJobMismatchInfo = $"JobとNonJobの不一致(index={i}): NonJob({nonJobResult.result}, {nonJobResult.actNum}, {nonJobResult.targetHash}) " +
                                      $"vs Job({jobResult.result}, {jobResult.actNum}, {jobResult.targetHash})";
                break;
            }
        }

        // 結果を比較（StandardとNonJob）
        bool standardNonJobMatch = true;
        string standardNonJobMismatchInfo = "";

        for ( int i = 0; i < _characterCount; i++ )
        {
            MovementInfo nonJobResult = _judgeResultNonJob[i];
            MovementInfo standardResult = _judgeResultStandard[i];

            // 結果が一致するか確認
            if ( nonJobResult.result != standardResult.result ||
                nonJobResult.actNum != standardResult.actNum ||
                nonJobResult.targetHash != standardResult.targetHash )
            {
                standardNonJobMatch = false;
                standardNonJobMismatchInfo = $"StandardとNonJobの不一致(index={i}): NonJob({nonJobResult.result}, {nonJobResult.actNum}, {nonJobResult.targetHash}) " +
                                           $"vs Standard({standardResult.result}, {standardResult.actNum}, {standardResult.targetHash})";
                break;
            }
        }

        // 検証
        Assert.IsTrue(jobNonJobMatch, jobNonJobMismatchInfo);
        Assert.IsTrue(standardNonJobMatch, standardNonJobMismatchInfo);
    }

    /// <summary>
    /// 3種類のAI実装を比較検証するテスト
    /// </summary>
//    [Test, Performance]
    public void Compare_Three_AI_Implementations()
    {
        // データをランダム化
        for ( int i = 0; i < _characterData.Length; i++ )
        {
            CharacterData data = _characterData[i];
            CharacterDataRandomizer.RandomizeCharacterData(ref data, _characterData);
            _characterData[i] = data;
        }

        // 時間を更新（全AIに同じ時間を設定）
        float testTime = 200.0f; // テスト用の時間
        _aiTestJob.nowTime = testTime;
        _nonJobAI.nowTime = testTime;
        _standardAI.nowTime = testTime;

        // 各AIの処理を実行し、パフォーマンスを測定
        using ( Measure.Scope("JobSystemAI実行時間") )
        {
            JobHandle handle = _aiTestJob.Schedule(_characterCount, jobBatchCount);
            handle.Complete();
        }

        using ( Measure.Scope("NonJobAI実行時間") )
        {
            _nonJobAI.ExecuteAIDecision();
        }

        using ( Measure.Scope("StandardAI実行時間") )
        {
            _standardAI.ExecuteAIDecision();
        }

        // 結果検証用のログを出力
        int matchCount = 0;
        int mismatchCount = 0;

        for ( int i = 0; i < Math.Min(5, _characterCount); i++ )
        {
            // 各AIの結果を取得
            MovementInfo jobResult = _judgeResultJob[i];
            MovementInfo nonJobResult = _judgeResultNonJob[i];
            MovementInfo standardResult = _judgeResultStandard[i];

            // 結果が一致するか確認
            bool allMatch = jobResult.result == nonJobResult.result &&
                           jobResult.result == standardResult.result &&
                           jobResult.actNum == nonJobResult.actNum &&
                           jobResult.actNum == standardResult.actNum &&
                           jobResult.targetHash == nonJobResult.targetHash &&
                           jobResult.targetHash == standardResult.targetHash;

            if ( allMatch )
            {
                matchCount++;
            }
            else
            {
                mismatchCount++;
                Debug.LogWarning($"結果不一致(index={i}):\n" +
                                $"Job: {jobResult.result}, {jobResult.actNum}, {jobResult.targetHash}\n" +
                                $"NonJob: {nonJobResult.result}, {nonJobResult.actNum}, {nonJobResult.targetHash}\n" +
                                $"Standard: {standardResult.result}, {standardResult.actNum}, {standardResult.targetHash}");
            }
        }

        Debug.Log($"サンプル結果比較: 一致={matchCount}, 不一致={mismatchCount}");

        // 検証（すべての実装で結果が一致することを確認）
        Assert.AreEqual(0, mismatchCount, "異なる実装間で結果が一致しません");
    }

    /// <summary>
    /// 異なるキャラクター数でのパフォーマンス比較テスト
    /// </summary>
    //[UnityTest, Performance]
    public IEnumerator Compare_Different_Character_Counts()
    {
        // テスト用のキャラクター数の配列
        int[] characterCounts = { 10, 50, 100 };

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

                // StandardAIテスト
                using ( Measure.Scope("StandardAI") )
                {
                    _standardAI.ExecuteAIDecision();
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
}