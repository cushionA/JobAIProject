using System;
using System.Collections.Generic;
using UnityEngine;
using static JTestAIBase;
using static CombatManager;
using static JobAITestStatus;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.InteropServices;

/// <summary>
/// AITestJobの非JobSystem版実装（標準コレクション使用）
/// </summary>
public struct StandardAI
{
    /// <summary>
    /// 読み取り専用のチームごとの全体ヘイト
    /// </summary>
    public Dictionary<int2, int> teamHate;

    /// <summary>
    /// CharacterDataDicの変換後
    /// </summary>
    public List<CharacterData> characterData;

    /// <summary>
    /// 現在時間
    /// </summary>
    public float nowTime;

    /// <summary>
    /// 行動決定データ。
    /// ターゲット変更の反映とかも全部こっちでやる。
    /// </summary>
    public List<MovementInfo> judgeResult;

    /// <summary>
    /// プレイヤー、敵、その他、それぞれが敵対している陣営をビットで表現。
    /// キャラデータのチーム設定と一緒に使う
    /// </summary>
    public int[] relationMap;


    /// <summary>
    /// JobAIのデータからStandardAIを初期化するコンストラクタ
    /// </summary>
    /// <param name="jobTeamHate">JobAIのチームヘイトデータ</param>
    /// <param name="jobCharacterData">JobAIのキャラクターデータ</param>
    /// <param name="time">現在時間</param>
    /// <param name="jobRelationMap">JobAIの関係マップ</param>
    public StandardAI(NativeHashMap<int2, int> jobTeamHate, UnsafeList<CharacterData> jobCharacterData, float time, NativeArray<int> jobRelationMap)
    {
        // 現在時間をコピー
        nowTime = time;

        // チームヘイトデータのコピー
        teamHate = new Dictionary<int2, int>();
        foreach ( var key in jobTeamHate.GetKeyArray(Allocator.Temp) )
        {
            int2 newKey = new int2(key.x, key.y);
            teamHate[newKey] = jobTeamHate[key];
        }

        // キャラクターデータのコピー
        characterData = new List<CharacterData>(jobCharacterData.Length);
        for ( int i = 0; i < jobCharacterData.Length; i++ )
        {
            characterData.Add(jobCharacterData[i]);
        }

        // 判定結果用のリストを作成
        judgeResult = new List<MovementInfo>(characterData.Count);
        for ( int i = 0; i < characterData.Count; i++ )
        {
            judgeResult.Add(new MovementInfo());
        }

        // 関係マップのコピー
        relationMap = new int[jobRelationMap.Length];
        for ( int i = 0; i < jobRelationMap.Length; i++ )
        {
            relationMap[i] = jobRelationMap[i];
        }
    }


    /// <summary>
    /// 判定結果をJobAIの結果コンテナにコピーするメソッド
    /// </summary>
    /// <param name="jobResult">JobAIの結果コンテナ</param>
    public void CopyResultToJobContainer(ref UnsafeList<MovementInfo> jobResult)
    {
        // Spanを使用してリストにアクセス
        Span<MovementInfo> resultsSpan = judgeResult.AsSpan();

        for ( int i = 0; i < resultsSpan.Length && i < jobResult.Length; i++ )
        {
            jobResult[i] = resultsSpan[i];
        }
    }

    /// <summary>
    /// AIの判断を実行する
    /// </summary>
    public void ExecuteAIDecision()
    {
        // Spanを使用してリストにアクセス
        Span<CharacterData> charaSpan = CollectionMarshal.AsSpan(characterData);
        Span<MovementInfo> resultSpan = CollectionMarshal.AsSpan(judgeResult);

        // 各キャラクターについてAI判断を実行
        for ( int index = 0; index < charaSpan.Length; index++ )
        {
            // 結果の構造体を作成
            MovementInfo resultData = new MovementInfo();

            // 現在の行動のステートを数値に変換
            int nowMode = (int)charaSpan[index].liveData.actState;

            // 判断時間が経過したかを確認
            // 経過してないなら処理しない
            if ( nowTime - charaSpan[index].lastJudgeTime < charaSpan[index].brainData[nowMode].judgeInterval )
            {
                resultData.result = JudgeResult.何もなし;
                resultSpan[index] = resultData;
                continue;
            }

            // 行動条件の中で前提を満たしたものを取得するビット
            int enableCondition = 0;

            // 前提となる自分についてのスキップ条件を確認
            // 最後の条件は補欠条件なので無視
            for ( int i = 0; i < charaSpan[index].brainData[nowMode].actCondition.Length - 1; i++ )
            {
                SkipJudgeData skipData = charaSpan[index].brainData[nowMode].actCondition[i].skipData;

                // スキップ条件を解釈して判断
                if ( skipData.skipCondition == SkipJudgeCondition.条件なし || JudgeSkipByCondition(skipData, charaSpan[index]) == 1 )
                {
                    enableCondition |= 1 << i; // |= が正しい（ビットを立てる）
                }
            }

            // 条件を満たした行動の中で最も優先的なもの
            // 初期値は最後の条件、つまり条件なしの補欠条件
            int selectMove = charaSpan[index].brainData[nowMode].actCondition.Length - 1;

            // キャラデータを確認する
            for ( int i = 0; i < charaSpan.Length; i++ )
            {
                // 自分はスキップ
                if ( index == i )
                {
                    continue;
                }

                // 行動判断
                if ( enableCondition != 0 )
                {
                    for ( int j = 0; j < charaSpan[index].brainData[nowMode].actCondition.Length - 1; j++ )
                    {
                        // ビットが立っているかチェック
                        if ( (enableCondition & (1 << j)) != 0 )
                        {
                            // ある条件満たしたらbreakして、以降はそれ以下の条件もう見ない
                            if ( CheckActCondition(
                                charaSpan[index].brainData[nowMode].actCondition[j],
                                charaSpan[index],
                                charaSpan[i],
                                teamHate) )
                            {
                                selectMove = j; // 条件のインデックス

                                // enableConditionのbitも消す
                                // j桁目までのビットをすべて1にするマスクを作成
                                int mask = (1 << j) - 1;

                                // マスクと元の値の論理積を取ることで上位ビットをクリア
                                enableCondition = enableCondition & mask;
                                break;
                            }
                        }
                    }
                }
                // 条件満たしたらループ終わり。
                else
                {
                    break;
                }
            }

            // 最も条件に近いターゲットを確認する
            // 比較用初期値はInvertによって変動
            TargetJudgeData targetJudgeData = charaSpan[index].brainData[nowMode].actCondition[selectMove].targetCondition;
            int nowValue = targetJudgeData.isInvert == BitableBool.TRUE ? int.MaxValue : int.MinValue;
            int newTargetHash = 0;

            // 状態変更の場合ここで戻る
            if ( targetJudgeData.judgeCondition == TargetSelectCondition.不要_状態変更 )
            {
                // 指定状態に移行
                resultData.result = JudgeResult.新しく判断をした;
                resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;

                // 判断結果を設定
                resultSpan[index] = resultData;
                continue;
            }
            else
            {
                // ターゲット選定ループ

                // ハッシュを取得
                int tIndex = JudgeTargetByCondition(targetJudgeData, charaSpan, index, teamHate);
                if ( tIndex >= 0 )
                {
                    newTargetHash = characterData[tIndex].hashCode;
                }
            }

            // ここでターゲット見つかってなければ待機に移行
            if ( newTargetHash == 0 )
            {
                // 待機に移行
                resultData.result = JudgeResult.新しく判断をした;
                resultData.actNum = (int)ActState.待機;
            }
            else
            {
                // ターゲットを設定
                resultData.result = JudgeResult.新しく判断をした;
                resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;
                resultData.targetHash = newTargetHash;
            }

            // 判断結果を設定
            resultSpan[index] = resultData;
        }
    }

    #region スキップ条件判断

    /// <summary>
    /// SkipJudgeConditionに基づいて判定を行うメソッド
    /// </summary>
    /// <param name="skipData">スキップ判定用データ</param>
    /// <param name="charaData">キャラクターデータ</param>
    /// <returns>条件に合致する場合は1、それ以外は0</returns>
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int JudgeSkipByCondition(in SkipJudgeData skipData, in CharacterData charaData)
    {
        SkipJudgeCondition condition = skipData.skipCondition;
        switch ( condition )
        {
            case SkipJudgeCondition.自分のHPが一定割合の時:
                // 各条件を個別に int で評価
                int equalConditionHP = skipData.judgeValue == charaData.liveData.hpRatio ? 1 : 0;
                int lessConditionHP = skipData.judgeValue < charaData.liveData.hpRatio ? 1 : 0;
                int invertConditionHP = skipData.isInvert == BitableBool.TRUE ? 1 : 0;
                // 明示的に条件を組み合わせる
                int condition1HP = equalConditionHP;
                int condition2HP = (lessConditionHP != 0) == (invertConditionHP != 0) ? 1 : 0;
                if ( condition1HP != 0 || condition2HP != 0 )
                {
                    return 1;
                }
                return 0;

            case SkipJudgeCondition.自分のMPが一定割合の時:
                // 各条件を個別に int で評価
                int equalConditionMP = skipData.judgeValue == charaData.liveData.mpRatio ? 1 : 0;
                int lessConditionMP = skipData.judgeValue < charaData.liveData.mpRatio ? 1 : 0;
                int invertConditionMP = skipData.isInvert == BitableBool.TRUE ? 1 : 0;
                // 明示的に条件を組み合わせる
                int condition1MP = equalConditionMP;
                int condition2MP = (lessConditionMP != 0) == (invertConditionMP != 0) ? 1 : 0;
                if ( condition1MP != 0 || condition2MP != 0 )
                {
                    return 1;
                }
                return 0;

            default:
                // デフォルトケース（未定義の条件の場合）
                Debug.LogWarning($"未定義のスキップ条件: {condition}");
                return 0;
        }
    }

    #endregion スキップ条件判断

    /// <summary>
    /// 行動判断の処理を隔離したメソッド
    /// </summary>
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool CheckActCondition(in BehaviorData condition, in CharacterData myData, in CharacterData targetData, Dictionary<int2, int> tHate)
    {
        bool result = true;

        // フィルター通過しないなら戻る。
        if ( condition.actCondition.filter.IsPassFilter(targetData) == 0 )
        {
            return false;
        }

        switch ( condition.actCondition.judgeCondition )
        {
            case ActJudgeCondition.指定のヘイト値の敵がいる時:

                int targetHash = targetData.hashCode;
                int targetHate = 0;

                if ( myData.personalHate.ContainsKey(targetHash) )
                {
                    targetHate += myData.personalHate[targetHash];
                }

                // チームのヘイトはint2で確認する。
                int2 hateKey = new int2((int)myData.liveData.belong, targetHash);

                if ( tHate.ContainsKey(hateKey) )
                {
                    targetHate += tHate[hateKey];
                }

                // 通常は以上、逆の場合は以下
                if ( condition.actCondition.isInvert == BitableBool.FALSE )
                {
                    result = targetHate >= condition.actCondition.judgeValue;
                }
                else
                {
                    result = targetHate <= condition.actCondition.judgeValue;
                }

                return result;

            case ActJudgeCondition.HPが一定割合の対象がいる時:

                // 通常は以上、逆の場合は以下
                if ( condition.actCondition.isInvert == BitableBool.FALSE )
                {
                    result = targetData.liveData.hpRatio >= condition.actCondition.judgeValue;
                }
                else
                {
                    result = targetData.liveData.hpRatio <= condition.actCondition.judgeValue;
                }

                return result;

            case ActJudgeCondition.MPが一定割合の対象がいる時:

                // 通常は以上、逆の場合は以下
                if ( condition.actCondition.isInvert == BitableBool.FALSE )
                {
                    result = targetData.liveData.mpRatio >= condition.actCondition.judgeValue;
                }
                else
                {
                    result = targetData.liveData.mpRatio <= condition.actCondition.judgeValue;
                }
                return result;

            case ActJudgeCondition.設定距離に対象がいる時:

                // 二乗の距離で判定する。
                int judgeDist = condition.actCondition.judgeValue * condition.actCondition.judgeValue;

                // 今の距離の二乗。
                int distance = (int)(Mathf.Pow(targetData.liveData.nowPosition.x - myData.liveData.nowPosition.x, 2) +
                               Mathf.Pow(targetData.liveData.nowPosition.y - myData.liveData.nowPosition.y, 2));

                // 通常は以上、逆の場合は以下
                if ( condition.actCondition.isInvert == BitableBool.FALSE )
                {
                    result = distance >= judgeDist;
                }
                else
                {
                    result = distance <= judgeDist;
                }
                return result;

            case ActJudgeCondition.特定の属性で攻撃する対象がいる時:

                // 通常はいる時、逆の場合はいないとき
                if ( condition.actCondition.isInvert == BitableBool.FALSE )
                {
                    result = ((int)targetData.solidData.attackElement & condition.actCondition.judgeValue) > 0;
                }
                else
                {
                    result = ((int)targetData.solidData.attackElement & condition.actCondition.judgeValue) == 0;
                }
                return result;

            case ActJudgeCondition.特定の数の敵に狙われている時:
                // 通常は以上、逆の場合は以下
                if ( condition.actCondition.isInvert == BitableBool.FALSE )
                {
                    result = targetData.targetingCount >= condition.actCondition.judgeValue;
                }
                else
                {
                    result = targetData.targetingCount <= condition.actCondition.judgeValue;
                }
                return result;

            default: // 条件なし (0) または未定義の値
                return result;
        }
    }

    #region　ターゲット判断処理

    /// <summary>
    /// TargetConditionに基づいて判定を行うメソッド
    /// </summary>
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int JudgeTargetByCondition(in TargetJudgeData judgeData, ReadOnlySpan<CharacterData> cData, int myIndex, Dictionary<int2, int> tHate)
    {
        int score = judgeData.isInvert == BitableBool.TRUE ? int.MaxValue : int.MinValue;
        int index = -1;

        TargetSelectCondition condition = judgeData.judgeCondition;
        int isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

        switch ( condition )
        {
            case TargetSelectCondition.高度:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    int height = (int)cData[i].liveData.nowPosition.y;


                    // 一番高いやつを求める (isInvert == 1)
                    if ( isInvert == 0 )
                    {
                        int isGreater = height > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = height;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める (isInvert == 0)
                    else
                    {
                        int isLess = height < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = height;
                            index = i;
                        }
                    }
                }

                return index;

            case TargetSelectCondition.HP割合:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.hpRatio > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.hpRatio;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.hpRatio < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.hpRatio;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.HP:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.currentHp > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.currentHp;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.currentHp < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.currentHp;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.敵に狙われてる数:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].targetingCount > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].targetingCount;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].targetingCount < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].targetingCount;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.合計攻撃力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.dispAtk > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.dispAtk;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.dispAtk < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.dispAtk;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.合計防御力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.dispDef > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.dispDef;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.dispDef < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.dispDef;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.斬撃攻撃力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.atk.slash > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.atk.slash;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.atk.slash < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.atk.slash;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.刺突攻撃力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.atk.pierce > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.atk.pierce;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.atk.pierce < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.atk.pierce;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.打撃攻撃力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.atk.strike > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.atk.strike;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.atk.strike < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.atk.strike;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.炎攻撃力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.atk.fire > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.atk.fire;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.atk.fire < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.atk.fire;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.雷攻撃力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.atk.lightning > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.atk.lightning;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.atk.lightning < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.atk.lightning;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.光攻撃力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.atk.light > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.atk.light;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.atk.light < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.atk.light;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.闇攻撃力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.atk.dark > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.atk.dark;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.atk.dark < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.atk.dark;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.斬撃防御力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.def.slash > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.def.slash;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.def.slash < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.def.slash;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.刺突防御力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.def.pierce > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.def.pierce;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.def.pierce < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.def.pierce;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.打撃防御力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.def.strike > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.def.strike;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.def.strike < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.def.strike;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.炎防御力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.def.fire > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.def.fire;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.def.fire < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.def.fire;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.雷防御力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.def.lightning > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.def.lightning;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.def.lightning < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.def.lightning;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.光防御力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.def.light > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.def.light;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.def.light < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.def.light;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.闇防御力:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // 一番高いやつを求める
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.def.dark > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.def.dark;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        int isLess = cData[i].liveData.def.dark < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.def.dark;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.距離:
                // 自分の位置をキャッシュ
                float myPositionX = cData[myIndex].liveData.nowPosition.x;
                float myPositionY = cData[myIndex].liveData.nowPosition.y;
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // 自分自身か、フィルターをパスできなければ戻る。
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }
                    // マンハッタン距離で遠近判断
                    float distance = Math.Abs(myPositionX - cData[i].liveData.nowPosition.x) +
                                    Math.Abs(myPositionY - cData[i].liveData.nowPosition.y);

                    // 一番高いやつを求める。
                    if ( isInvert == 0 )
                    {
                        if ( distance > score )
                        {
                            score = (int)distance;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める。
                    else
                    {
                        if ( distance < score )
                        {
                            score = (int)distance;
                            index = i;
                        }
                    }
                }

                return index;

            case TargetSelectCondition.自分:
                return myIndex;

            case TargetSelectCondition.プレイヤー:
                // 何かしらのシングルトンにプレイヤーのHashは持たせとこ
                // newTargetHash = characterData[i].hashCode;
                return -1;

            case TargetSelectCondition.指定なし_ヘイト値:
                // ターゲット選定ループ
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // 自分自身か、フィルターをパスできなければ戻る。
                    if ( i == index || judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }
                    // ヘイト値を確認
                    int targetHash = cData[i].hashCode;
                    int targetHate = 0;
                    if ( cData[index].personalHate.ContainsKey(targetHash) )
                    {
                        targetHate += (int)cData[index].personalHate[targetHash];
                    }
                    // チームのヘイトはint2で確認する。
                    int2 hateKey = new int2((int)cData[index].liveData.belong, targetHash);
                    if ( tHate.ContainsKey(hateKey) )
                    {
                        targetHate += tHate[hateKey];
                    }

                    // 一番高いやつを求める。
                    if ( judgeData.isInvert == BitableBool.FALSE )
                    {
                        if ( targetHate > score )
                        {
                            score = targetHate;
                            index = i;
                        }
                    }
                    // 一番低いやつを求める。
                    else
                    {
                        if ( targetHate < score )
                        {
                            score = targetHate;
                            index = i;
                        }
                    }
                }
                break;

            default:
                // デフォルトケース（未定義の条件の場合）
                Debug.LogWarning($"未定義のターゲット選択条件: {condition}");
                return -1;
        }

        return -1;
    }

    #endregion ターゲット判断処理
}