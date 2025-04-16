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

/// <summary>
/// AIが判断を行うJob
/// 流れとしてはヘイト判断（ここで一番憎いヤツは出しておく）→行動判断→対象設定（攻撃/防御の場合ヘイト、それ以外の場合は任意条件を優先順に判断）
/// ヘイト処理はチームヘイトが一番高いやつを陣営ごとに出しておいて、個人ヘイト足したらそれを超えるか、で見ていこうか
/// </summary>
public class AITestJob : IJobParallelFor
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
        if ( nowTime - characterData[index].lastJudgeTime < characterData[index].brainData[nowMode].judgeInterval )
        {
            resultData.result = 0;

            // 移動方向判断だけはする。
            if ( nowTime - characterData[index].lastMoveJudgeTime < characterData[index].moveJudgeInterval )
            {

            }

            // 結果を設定。
            judgeResult[index] = resultData;

            return;
        }

        // characterData[index].brainData[nowMode].judgeInterval みたいな値は何回も使うなら一時変数に保存していい。


        int maxHate = -1; // 一番ヘイト高いやつを記録。フィルタリングしつつね

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
        // ヘイト減少の仕組み考えないとな。30パーセントずつ減らす？　あーーー

        // 行動条件の中で前提を満たしたものを取得するビット
        // なお、実際の判断時により優先的な条件が満たされた場合は上位ビットはまとめて消す。
        int enableCondition = 0;

        // 前提となる自分についてのスキップ条件を確認。
        for ( int i = 0; i < characterData[index].brainData[nowMode].actCondition.Length; i++ )
        {
            if ( characterData[index].brainData[nowMode].actCondition[i].skipData.skipCondition == JobAITestStatus.SkipJudgeCondition.条件なし )
            {
                enableCondition &= 1 << i;
            }

            SkipJudgeData skipData = characterData[index].brainData[nowMode].actCondition[i].skipData;

            if ( skipData.skipCondition == SkipJudgeCondition.自分のHPが一定割合の時 )
            {

            }
            else if ( skipData.skipCondition == SkipJudgeCondition.自分のMPが一定割合の時 )
            {

            }

        }

        // 条件を満たした行動の中で最も優先的なもの。
        int selectmove;

        for ( int i = 0; i < characterData.Length; i++ )
        {

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

        // 結果を設定。
        judgeResult[index] = resultData;
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
