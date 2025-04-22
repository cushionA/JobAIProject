using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using static JTestAIBase;
using static CombatManager;
using static JobAITestStatus;
using static AiFunctionLibrary;
using System.Runtime.CompilerServices;
using Unity.Burst;

/// <summary>
/// AITestJobの非JobSystem版実装
/// </summary>
public struct NonJobAI
{
    /// <summary>
    /// 読み取り専用のチームごとの全体ヘイト
    /// </summary>
    public NativeArray<NativeHashMap<int, int>> teamHate;

    /// <summary>
    /// CharacterDataDicの変換後
    /// </summary>
    public UnsafeList<CharacterData> characterData;

    /// <summary>
    /// 現在時間
    /// </summary>
    public float nowTime;

    /// <summary>
    /// 行動決定データ。
    /// ターゲット変更の反映とかも全部こっちでやる。
    /// </summary>
    public UnsafeList<MovementInfo> judgeResult;

    /// <summary>
    /// プレイヤー、敵、その他、それぞれが敵対している陣営をビットで表現。
    /// キャラデータのチーム設定と一緒に使う
    /// </summary>
    public NativeArray<int> relationMap;

    /// <summary>
    /// 関数ポインタの配列から処理する
    /// </summary>
    public NativeArray<FunctionPointer<SkipJudgeDelegate>> skipFunctions;

    /// <summary>
    /// 関数ポインタの配列から処理する
    /// </summary>
    public NativeArray<FunctionPointer<TargetJudgeDelegate>> targetFunctions;

    /// <summary>
    /// AIの判断を実行する
    /// </summary>
    public void ExecuteAIDecision()
    {
        // 各キャラクターについてAI判断を実行
        for ( int index = 0; index < characterData.Length; index++ )
        {
            // 結果の構造体を作成
            MovementInfo resultData = new MovementInfo();

            // 現在の行動のステートを数値に変換
            int nowMode = (int)characterData[index].liveData.actState;

            // 判断時間が経過したかを確認
            // 経過してないなら処理しない
            if ( nowTime - characterData[index].lastJudgeTime < characterData[index].brainData[nowMode].judgeInterval )
            {
                resultData.result = JudgeResult.何もなし;
                judgeResult[index] = resultData;
                continue;
            }

            // 行動条件の中で前提を満たしたものを取得するビット
            int enableCondition = 0;

            // 前提となる自分についてのスキップ条件を確認
            // 最後の条件は補欠条件なので無視
            for ( int i = 0; i < characterData[index].brainData[nowMode].actCondition.Length - 1; i++ )
            {
                if ( characterData[index].brainData[nowMode].actCondition[i].skipData.skipCondition == JobAITestStatus.SkipJudgeCondition.条件なし )
                {
                    enableCondition &= 1 << i;
                }

                SkipJudgeData skipData = characterData[index].brainData[nowMode].actCondition[i].skipData;

                // スキップ条件を解釈して判断
                if ( skipFunctions[(int)skipData.skipCondition].Invoke(skipData, characterData[index]) )
                {
                    enableCondition &= 1 << i;
                }
            }

            // 条件を満たした行動の中で最も優先的なもの
            // 初期値は最後の条件、つまり条件なしの補欠条件
            int selectMove = characterData[index].brainData[nowMode].actCondition.Length - 1;

            // キャラデータを確認する
            for ( int i = 0; i < characterData.Length; i++ )
            {
                // 自分はスキップ
                if ( index == i )
                {
                    continue;
                }

                // 行動判断
                if ( enableCondition != 0 )
                {
                    for ( int j = 0; j < characterData[index].brainData[nowMode].actCondition.Length - 1; j++ )
                    {
                        // ある条件満たしたらbreakして、以降はそれ以下の条件もう見ない
                        if ( CheckActCondition(
                            characterData[index].brainData[nowMode].actCondition[j],
                            characterData[index],
                            characterData[i],
                            teamHate) )
                        {
                            selectMove = i;

                            // enableConditionのbitも消す
                            // i桁目までのビットをすべて1にするマスクを作成
                            int mask = (1 << j) - 1;

                            // マスクと元の値の論理積を取ることで上位ビットをクリア
                            enableCondition = enableCondition & mask;
                            break;
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
            TargetJudgeData targetJudgeData = characterData[index].brainData[nowMode].actCondition[selectMove].targetCondition;
            int nowValue = targetJudgeData.isInvert ? int.MaxValue : int.MinValue;
            int newTargetHash = 0;

            // 状態変更の場合ここで戻る
            if ( targetJudgeData.judgeCondition == TargetSelectCondition.不要_状態変更 )
            {
                // 指定状態に移行
                resultData.result = JudgeResult.新しく判断をした;
                resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;

                // 判断結果を設定
                judgeResult[index] = resultData;
                continue;
            }

            // それ以外であればターゲットを判断
            if ( targetJudgeData.judgeCondition == TargetSelectCondition.距離 )
            {
                // 自分の位置をキャッシュ
                float myPositionX = characterData[index].liveData.nowPosition.x;
                float myPositionY = characterData[index].liveData.nowPosition.y;

                // ターゲット選定ループ
                for ( int i = 0; i < characterData.Length; i++ )
                {
                    // 自分自身か、フィルターをパスできなければ戻る
                    if ( i == index || !targetJudgeData.filter.IsPassFilter(characterData[i]) )
                    {
                        continue;
                    }

                    // マンハッタン距離で遠近判断
                    float distance = Math.Abs(myPositionX - characterData[i].liveData.nowPosition.x) +
                                    Math.Abs(myPositionY - characterData[i].liveData.nowPosition.y);

                    // 一番高いやつを求める
                    if ( !targetJudgeData.isInvert )
                    {
                        if ( distance > nowValue )
                        {
                            nowValue = (int)distance;
                            newTargetHash = characterData[i].hashCode;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        if ( distance < nowValue )
                        {
                            nowValue = (int)distance;
                            newTargetHash = characterData[i].hashCode;
                        }
                    }
                }
            }
            else if ( targetJudgeData.judgeCondition == TargetSelectCondition.自分 )
            {
                newTargetHash = characterData[index].hashCode;
            }
            else if ( targetJudgeData.judgeCondition == TargetSelectCondition.プレイヤー )
            {
                // プレイヤーのハッシュはシングルトンから取得する想定
                // newTargetHash = characterData[i].hashCode;
            }
            else if ( targetJudgeData.judgeCondition == TargetSelectCondition.指定なし_ヘイト値 )
            {
                // ターゲット選定ループ
                for ( int i = 0; i < characterData.Length; i++ )
                {
                    // 自分自身か、フィルターをパスできなければ戻る
                    if ( i == index || !targetJudgeData.filter.IsPassFilter(characterData[i]) )
                    {
                        continue;
                    }

                    // ヘイト値を確認
                    int targetHash = characterData[i].hashCode;
                    int targetHate = 0;

                    if ( characterData[index].personalHate.ContainsKey(targetHash) )
                    {
                        targetHate += (int)characterData[index].personalHate[targetHash];
                    }

                    if ( teamHate[(int)characterData[index].liveData.belong].ContainsKey(targetHash) )
                    {
                        targetHate += teamHate[(int)characterData[index].liveData.belong][targetHash];
                    }

                    // 一番高いやつを求める
                    if ( !targetJudgeData.isInvert )
                    {
                        if ( targetHate > nowValue )
                        {
                            nowValue = targetHate;
                            newTargetHash = characterData[i].hashCode;
                        }
                    }
                    // 一番低いやつを求める
                    else
                    {
                        if ( targetHate < nowValue )
                        {
                            nowValue = targetHate;
                            newTargetHash = characterData[i].hashCode;
                        }
                    }
                }
            }
            // 通常のターゲット選定
            else
            {
                // ターゲット選定ループ
                for ( int i = 0; i < characterData.Length; i++ )
                {
                    // ハッシュを取得
                    if ( targetFunctions[(int)targetJudgeData.judgeCondition].Invoke(targetJudgeData, characterData[i], ref nowValue) )
                    {
                        newTargetHash = characterData[i].hashCode;
                    }
                }
            }

            // ここでターゲット見つかってなければ待機に移行
            if ( newTargetHash == 0 )
            {
                // 待機に移行
                resultData.result = JudgeResult.新しく判断をした;
                resultData.actNum = (int)ActState.待機;
            }

            // 判断結果を設定
            judgeResult[index] = resultData;
        }
    }

    /// <summary>
    /// 行動条件をチェックする
    /// </summary>
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool CheckActCondition(
        in BehaviorData condition,
        in CharacterData myData,
        in CharacterData targetData,
        NativeArray<NativeHashMap<int, int>> teamHate)
    {
        bool result = true;

        // フィルター通過しないなら戻る
        if ( !condition.actCondition.filter.IsPassFilter(targetData) )
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

                if ( teamHate[(int)myData.liveData.belong].ContainsKey(targetHash) )
                {
                    targetHate += teamHate[(int)myData.liveData.belong][targetHash];
                }

                // 通常は以上、逆の場合は以下
                if ( !condition.actCondition.isInvert )
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
                if ( !condition.actCondition.isInvert )
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
                if ( !condition.actCondition.isInvert )
                {
                    result = targetData.liveData.mpRatio >= condition.actCondition.judgeValue;
                }
                else
                {
                    result = targetData.liveData.mpRatio <= condition.actCondition.judgeValue;
                }
                return result;

            case ActJudgeCondition.設定距離に対象がいる時:
                // 二乗の距離で判定する
                int judgeDist = condition.actCondition.judgeValue * condition.actCondition.judgeValue;

                // 今の距離の二乗
                int distance = (int)(Mathf.Pow(targetData.liveData.nowPosition.x - myData.liveData.nowPosition.x, 2) +
                               Mathf.Pow(targetData.liveData.nowPosition.y - myData.liveData.nowPosition.y, 2));

                // 通常は以上、逆の場合は以下
                if ( !condition.actCondition.isInvert )
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
                if ( !condition.actCondition.isInvert )
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
                if ( !condition.actCondition.isInvert )
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
}