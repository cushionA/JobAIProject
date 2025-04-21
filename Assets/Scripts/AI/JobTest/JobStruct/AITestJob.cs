using Unity.Jobs;
using UnityEngine;
using Unity.Collections;
using static JTestAIBase;
using Unity.Collections.LowLevel.Unsafe;
using static CombatManager;
using System.ComponentModel;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using System.Runtime.CompilerServices;
using static JobAITestStatus;
using static AiFunctionLibrary;
using Unity.Burst;
using System.Runtime.InteropServices.WindowsRuntime;
using System;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using Unity.Plastic.Newtonsoft.Json.Linq;

/// <summary>
/// AIが判断を行うJob
/// 流れとしてはヘイト判断（ここで一番憎いヤツは出しておく）→行動判断→対象設定（攻撃/防御の場合ヘイト、それ以外の場合は任意条件を優先順に判断）
/// ヘイト処理はチームヘイトが一番高いやつを陣営ごとに出しておいて、個人ヘイト足したらそれを超えるか、で見ていこうか
/// </summary>
[BurstCompile]
public struct AITestJob : IJobParallelFor
{
    /// <summary>
    /// 読み取り専用のチームごとの全体ヘイト
    /// </summary>
    [ReadOnly]
    public NativeArray<NativeHashMap<int, int>> teamHate;

    /// <summary>
    /// CharacterDataDicの変換後
    /// </summary>
    [Unity.Collections.ReadOnly]
    public UnsafeList<CharacterData> characterData;

    /// <summary>
    /// 現在時間
    /// </summary>
    public float nowTime;

    /// <summary>
    /// 行動決定データ。
    /// ターゲット変更の反映とかも全部こっちでやる。
    /// </summary>
    [WriteOnly]
    public UnsafeList<MovementInfo> judgeResult;

    /// <summary>
    /// プレイヤー、敵、その他、それぞれが敵対している陣営をビットで表現。
    /// キャラデータのチーム設定と一緒に使う
    /// </summary>
    [ReadOnly]
    public NativeArray<int> relationMap;

    /// <summary>
    /// 関数ポインタの配列から処理する
    /// </summary>
    [ReadOnly]
    public NativeArray<FunctionPointer<SkipJudgeDelegate>> skipFunctions;

    [ReadOnly]
    public NativeArray<FunctionPointer<TargetJudgeDelegate>> targetFunctions;


    /// <summary>
    /// characterDataとjudgeResultのインデックスをベースに処理する。
    /// </summary>
    /// <param name="index"></param>
    public void Execute(int index)
    {
        // 結果の構造体を作成。
        MovementInfo resultData = new MovementInfo();

        // 現在の行動のステートを数値に変換
        int nowMode = (int)characterData[index].liveData.actState;

        // 判断時間が経過したかを確認。
        // 経過してないなら処理しない。
        // あるいはターゲット消えた場合も判定したい。チームヘイトに含まれてなければ。それだと味方がヘイトの時どうするの。
        // キャラ死亡時に全キャラに対しターゲットしてるかどうかを確認するようにしよう。で、ターゲットだったら前回判断時間をマイナスにする。
        if ( nowTime - characterData[index].lastJudgeTime < characterData[index].brainData[nowMode].judgeInterval )
        {
            resultData.result = JudgeResult.何もなし;

            // 移動方向判断だけはする。
            //　正確には距離判定。
            // ハッシュ値持ってんだからジョブから出た後でやろう。
            // Resultを解釈して

            // 結果を設定。
            judgeResult[index] = resultData;

            return;
        }

        // characterData[index].brainData[nowMode].judgeInterval みたいな値は何回も使うなら一時変数に保存していい。


        // まず判断時間の経過を確認
        // 次に線形探索で行動間隔の確認を行いつつ、敵にはヘイト判断も行う。
        // 全ての行動条件を確認しつつ、どの条件が確定したかを配列に入れる
        // ちなみに0、つまり一番優先度が高い設定が当てはまった場合は有無を言わさずループ中断。
        // 逆に何も当てはまらなかった場合は補欠条件が実行。
        // ちなみに逃走、とか支援、のモードをあんまり生かせてないよな。
        // モードごとに条件設定できるようにするか。
        // で、条件がいらないモードについては行動判断を省く

        // 確認対象の条件はビット値で保存。
        // そしてビットの設定がある条件のみ確認する。
        // 条件満たしたらビットは消す。
        // さらに、1とか2番目とかのより優先度が高い条件が付いたらそれ以下の条件は全部消す。
        // で、現段階で一番優先度が高い満たした条件を保存しておく
        // その状態で最後まで走査してヘイト値設定も完了する。
        // ちなみにヘイト値設定は自分がヘイト持ってる相手のヘイトを足した値を確認するだけだろ
        // ヘイト減少の仕組み考えないとな。30パーセントずつ減らす？　あーーーーーーーー

        // 行動条件の中で前提を満たしたものを取得するビット
        // なお、実際の判断時により優先的な条件が満たされた場合は上位ビットはまとめて消す。
        int enableCondition = 0;

        // 前提となる自分についてのスキップ条件を確認。
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

        // 条件を満たした行動の中で最も優先的なもの。
        // 初期値は最後の条件、つまり条件なしの補欠条件
        int selectMove = characterData[index].brainData[nowMode].actCondition.Length - 1;

        // ヘイト条件確認用の一時バッファ
        NativeArray<Vector2Int> hateIndex = new NativeArray<Vector2Int>(characterData[index].brainData[nowMode].hateCondition.Length, Allocator.Temp);
        NativeArray<TargetJudgeData> hateCondition = characterData[index].brainData[nowMode].hateCondition;

        // ヘイト確認バッファの初期化
        for ( int i = 0; i < hateIndex.Length; i++ )
        {
            if ( hateCondition[i].isInvert )
            {
                hateIndex[i].Set(int.MaxValue, -1);
            }
            else
            {
                hateIndex[i].Set(int.MinValue, -1);
            }
        }

        // キャラデータを確認する。
        for ( int i = 0; i < characterData.Length; i++ )
        {
            // 自分はスキップ
            if ( index == i )
            {
                continue;
            }

            // まずヘイト判断。
            // 各ヘイト条件について、条件更新を記録する。
            for ( int j = 0; j < hateCondition.Length; j++ )
            {
                int value = hateIndex[j].x;
                if ( targetFunctions[(int)hateCondition[j].judgeCondition].Invoke(hateCondition[j], characterData[i], ref value) )
                {
                    hateIndex[j].Set(value, i);
                }
            }

            // 次に行動判断。
            // ここはスイッチ文使おう。連続するInt値ならコンパイラがジャンプテーブル作ってくれるので
            if ( enableCondition != 0 )
            {
                for ( int j = 0; j < characterData[index].brainData[nowMode].actCondition.Length - 1; j++ )
                {
                    // ある条件満たしたらbreakして、以降はそれ以下の条件もう見ない。
                    if ( CheckActCondition(characterData[index].brainData[nowMode].actCondition[j], characterData[index], characterData[i]) )
                    {
                        selectMove = i;

                        // enableConditionのbitも消す。
                        // i桁目までのビットをすべて1にするマスクを作成
                        // (1 << (i + 1)) - 1 は 0から i-1桁目までのビットがすべて1
                        int mask = (1 << i) - 1;

                        // マスクと元の値の論理積を取ることで上位ビットをクリア
                        enableCondition = enableCondition & mask;
                        break;
                    }
                }
            }

        }

        // その後、二回目のループで条件に当てはまるキャラを探す。
        // 二回目で済むかな？　判断条件の数だけ探さないとダメじゃない？
        // 準備用のジョブで一番攻撃力が高い/低い、とかのキャラを陣営ごとに探しとくべきじゃない？
        // それは明確にやるべき。
        // いや、でも対象を所属や特徴でフィルタリングするならやっぱりダメかも
        // 大人しく条件ごとに線形するか。
        // 救いとなるのは、

        // 距離に関しては別処理を実装すると決めた。
        // kd木や空間分割データ構造とかあるみたいだけど、更新負荷的にいまいち実用的じゃない気がする。
        // 最適な敵数の範囲が異なるから
        // それよりは近距離の物理センサーで数秒に一回検査した方がいい。Nonalloc系のサーチでバッファに stack allocも使おう
        // 敵百体以上増やすならトリガーはまずいかも


        // 最も条件に近いターゲットを確認する。
        // 比較用初期値はInvertによって変動。
        TargetJudgeData targetJudgeData = characterData[index].brainData[nowMode].actCondition[selectMove].targetCondition;
        int nowValue = targetJudgeData.isInvert ? int.MaxValue : int.MinValue;
        int newTargetHash = 0;

        // 状態変更の場合ここで戻る。
        if ( targetJudgeData.judgeCondition == TargetSelectCondition.不要_状態変更 )
        {
            // 指定状態に移行
            resultData.result = JudgeResult.新しく判断をした;
            resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;

            // 判断結果を設定。
            judgeResult[index] = resultData;
            return;
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
                // 自分自身か、フィルターをパスできなければ戻る。
                if ( i == index || !targetJudgeData.filter.IsPassFilter(characterData[i]) )
                {
                    continue;
                }

                // マンハッタン距離で遠近判断
                float distance = Math.Abs(myPositionX - characterData[i].liveData.nowPosition.x) + Math.Abs(myPositionY - characterData[i].liveData.nowPosition.y);

                // 一番高いやつを求める。
                if ( !targetJudgeData.isInvert )
                {
                    if ( distance > nowValue )
                    {
                        nowValue = (int)distance;
                        newTargetHash = characterData[i].hashCode;
                    }
                }
                // 一番低いやつを求める。
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
        // 何かしらのシングルトンにプレイヤーのHashは持たせとこ
        else if ( targetJudgeData.judgeCondition == TargetSelectCondition.プレイヤー )
        {
            // newTargetHash = characterData[i].hashCode;
        }
        else if ( targetJudgeData.judgeCondition == TargetSelectCondition.指定なし_ヘイト値 )
        {
            // ターゲット選定ループ
            for ( int i = 0; i < characterData.Length; i++ )
            {
                // 自分自身か、フィルターをパスできなければ戻る。
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

                // 一番高いやつを求める。
                if ( !targetJudgeData.isInvert )
                {
                    if ( targetHate > nowValue )
                    {
                        nowValue = targetHate;
                        newTargetHash = characterData[i].hashCode;
                    }
                }
                // 一番低いやつを求める。
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
                // ほんとはここでターゲットカウントで狙われ過ぎのやつ弾いた方がいい
                if ( targetFunctions[(int)targetJudgeData.judgeCondition].Invoke(targetJudgeData, characterData[i], ref nowValue) )
                {
                    newTargetHash = characterData[i].hashCode;
                }
            }
        }

        // ここでターゲット見つかってなければ待機に移行。
        if ( newTargetHash == 0 )
        {
            // 待機に移行
            resultData.result = JudgeResult.新しく判断をした;
            resultData.actNum = (int)ActState.待機;
        }

        // 判断結果を設定。
        judgeResult[index] = resultData;

        // テスト仕様記録
        // 要素数は10 ～ 1000で
        // ステータスはいくつかベースとなるテンプレのCharacterData作って、その数値をいじるコード書いてやる。
        // で、Jobシステムをまんまベタ移植した普通のクラスを作成して、速度を比較
        // 最後は二つのテストにより作成されたpublic UnsafeList<MovementInfo> judgeResult　の同一性をかくにんして、精度のチェックまで終わり

    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="conditions"></param>
    /// <param name="charaData"></param>
    /// <param name="nowHate"></param>
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public bool CheckActCondition(in BehaviorData condition, in CharacterData myData, in CharacterData targetData)
    {
        bool result = true;

        // フィルター通過しないなら戻る。
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

            // 集計は廃止
            //case ActJudgeCondition.対象が一定数の時:
            //    return HasRequiredNumberOfTargets(context);

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

                // 二乗の距離で判定する。
                int judgeDist = condition.actCondition.judgeValue * condition.actCondition.judgeValue;

                // 今の距離の二乗。
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


    /// <summary>
    /// 二つのチームが敵対しているかをチェックするメソッド。
    /// </summary>
    /// <param name="team1"></param>
    /// <param name="team2"></param>
    /// <returns></returns>
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    private bool CheckTeamHostility(int team1, int team2)
    {
        return (relationMap[team1] & 1 << team2) > 0;
    }
}
