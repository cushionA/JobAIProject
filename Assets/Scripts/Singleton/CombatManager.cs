using Cysharp.Threading.Tasks;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using static JobAITestStatus;
using static JTestAIBase;

/// <summary>
/// 次のフレームで全体に適用するイベントリスト、みたいなのを持って、そこにキャラクターがデータを渡せるようにするか
/// Dispose必須。AI関連のDispose()はここでやる責任がある。
/// </summary>
public class CombatManager : MonoBehaviour, IDisposable
{

    #region 定義

    /// <summary>
    /// AIのイベントのタイプ。
    /// チームのヘイトをいじるか、イベントフラグを立てるかのどっちか
    /// 各キャラがチームの数と同じだけイベントフラグ入れ物を作って、イベント発生時にビット演算で反映する。
    /// 倒した系のフラグは撃破時のチームヘイト上昇で対応する。
    /// </summary>
    [Flags]
    public enum BrainEventFlagType
    {
        None = 0,  // フラグなしの状態を表す基本値
        大ダメージを与えた = 1 << 0,   // 相手に大きなダメージを与えた
        大ダメージを受けた = 1 << 1,   // 相手から大きなダメージを受けた
        回復を使用 = 1 << 2,         // 回復アビリティを使用した
        支援を使用 = 1 << 3,         // 支援アビリティを使用した
        //誰かを倒した = 1 << 4,        // 敵または味方を倒した
        //指揮官を倒した = 1 << 5,      // 指揮官を倒した
        攻撃対象指定 = 1 << 5,        // 指揮官による攻撃対象の指定
        威圧 = 1 << 6,//威圧状態だと敵が怖がる？ これはバッドステータスでもいいとは思う
    }

    /// <summary>
    /// AIのイベントの送信先。
    /// これをシングルトンに渡せばJobシステムで処理してくれる。
    /// 時間経過したらそのキャラからフラグを消すための設定。消すための記録がこれ。
    /// イベント追加時に対象キャラにフラグを設定し、抹消時に消す。
    /// 時間経過の他、キャラ死亡時もここに問い合わせないとな。
    /// ハッシュが一致するイベントを全探索して削除。
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct BrainEventContainer
    {

        /// <summary>
        /// イベントのタイプ
        /// </summary>
        public BrainEventFlagType eventType;

        /// <summary>
        /// イベントを呼んだ人のハッシュ。
        /// 
        /// </summary>
        public int targetHash;

        /// <summary>
        /// イベント開始時間
        /// </summary>
        public float startTime;

        /// <summary>
        /// イベントがどれくらいの間保持されるか、という時間。
        /// </summary>
        public float eventHoldTime;


        /// <summary>
        /// AIのイベントのコンストラクタ。
        /// startTimeは現在時を入れる。
        /// </summary>
        /// <param name="brainEvent"></param>
        /// <param name="hashCode"></param>
        /// <param name="holdTime"></param>
        public BrainEventContainer(BrainEventFlagType brainEvent, int hashCode, float holdTime)
        {
            eventType = brainEvent;
            targetHash = hashCode;
            startTime = GameManager.instance.NowTime;
            eventHoldTime = holdTime;
        }

    }

    #endregion 定義

    /// <summary>
    /// シングルトン。
    /// </summary>
    public static CombatManager instance;

    /// <summary>
    /// キャラデータを保持するコレクション。
    /// 陣営ごとに分ける
    /// Jobシステムに渡す時は上手くCharacterDataのNativeArrayにしてやろう。
    /// </summary>
    public CharacterDataDictionary<CharacterData, JTestAIBase> charaDataDictionary = new CharacterDataDictionary<CharacterData, JTestAIBase>(7);

    /// <summary>
    /// プレイヤー、敵、その他、それぞれが敵対している陣営をビットで表現。
    /// キャラデータのチーム設定と一緒に使う
    /// </summary>
    public static NativeArray<int> relationMap = new Unity.Collections.NativeArray<int>(3, Allocator.Persistent);

    /// <summary>
    /// 陣営ごとに設定されたヘイト値。
    /// ハッシュキーにはゲームオブジェクトのハッシュ値を渡す。
    /// 配列の要素ごとにジョブを実行
    /// いやたぶんジョブシステムじゃなくてハッシュで個別に対象オブジェクトにヘイト設定した方が速い
    /// イベントの数だけハッシュマップをループして、
    /// </summary>
    public NativeArray<NativeHashMap<int, int>> teamHate = new NativeArray<NativeHashMap<int, int>>(3, Allocator.Persistent);

    /// <summary>
    /// AIのイベントを受け付ける入れ物。
    /// 時間管理のために使う。
    /// Jobシステムで一括で時間見るか、普通にループするか（イベントはそんなに数がなさそうだし普通が速いかも）
    /// </summary>
    public UnsafeList<BrainEventContainer> eventContainer = new UnsafeList<BrainEventContainer>(7, Allocator.Persistent);

    /// <summary>
    /// 行動決定データ。
    /// Jobの書き込み先で、ターゲット変更の反映とかも全部これをもとに受け取ったキャラクターがやる。
    /// </summary>
    public UnsafeList<MovementInfo> judgeResult = new UnsafeList<MovementInfo>(7, Allocator.Persistent);

    /// <summary>
    /// 前回判断を実行した際の時間を記録する。
    /// これを使用して判断結果を受けたオブジェクトたちが前回判定時間を再度設定する。
    /// </summary>
    public float lastJudgeTime;

    /// <summary>
    /// 起動時にシングルトンのインスタンス作成。
    /// </summary>
    private void Awake()
    {
        if ( instance == null )
        {
            instance = this; // this を代入
            DontDestroyOnLoad(this); // シーン遷移時に破棄されないようにする
        }
        else
        {
            Destroy(this);
        }
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // HashMapの初期化
        for ( int i = 0; i < teamHate.Length; i++ )
        {
            teamHate[i] = new NativeHashMap<int, int>(7, Allocator.Persistent);
        }
    }

    /// <summary>
    /// ここで毎フレームジョブを発行する。
    /// </summary>
    void Update()
    {
        // 毎フレームジョブ実行
        BrainJobAct();
    }

    /// <summary>
    /// 新規キャラクターを追加する。
    /// </summary>
    /// <param name="data"></param>
    /// <param name="hashCode"></param>
    /// <param name="team"></param>
    public void CharacterAdd(JobAITestStatus status, GameObject addObject)
    {
        // 初期所属を追加
        int teamNum = (int)status.baseData.initialBelong;
        int hashCode = addObject.GetHashCode();

        // キャラデータを追加し、敵対する陣営のヘイトリストにも入れる。
        charaDataDictionary.AddByHash(hashCode, new CharacterData(status, addObject));

        for ( int i = 0; i < teamHate.Length; i++ )
        {
            if ( teamNum == i )
            {
                continue;
            }

            // 敵対チェック
            if ( CheckTeamHostility(i, teamNum) )
            {
                // ひとまずヘイトの初期値は10とする。
                teamHate[i].Add(hashCode, 10);
            }
        }
    }

    /// <summary>
    /// 退場キャラクターを削除する。
    /// Dispose()してるから、陣営変更の寝返り処理とかで使いまわさないようにね
    /// </summary>
    /// <param name="hashCode"></param>
    /// <param name="team"></param>
    public void CharacterDead(int hashCode, CharacterSide team)
    {
        int teamNum = (int)team;

        // 削除の前に値を処理する。
        charaDataDictionary[hashCode].Dispose();

        // キャラデータを削除し、敵対する陣営のヘイトリストからも消す。
        charaDataDictionary.RemoveByHash(hashCode);

        for ( int i = 0; i < teamHate.Length; i++ )
        {
            // 含むかをチェック
            if ( teamHate[i].ContainsKey(hashCode) )
            {
                teamHate[i].Remove(hashCode);
            }
        }

        // 消えるやつに紐づいたイベントを削除。
        // 安全にループ内で削除するために後ろから前へとループする。
        for ( int i = eventContainer.Length - 1; i > 0; i-- )
        {
            // 消えるやつにハッシュが一致するなら。
            if ( eventContainer[i].targetHash == hashCode )
            {
                eventContainer.RemoveAtSwapBack(i);
            }
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

    /// <summary>
    /// NativeContainerを削除する。
    /// </summary>
    public void Dispose()
    {
        for ( int i = 0; i < 3; i++ )
        {
            charaDataDictionary[i].Dispose();
            teamHate[i].Dispose();

        }
        eventContainer.Dispose();
        teamHate.Dispose();
        relationMap.Dispose();

        // デリゲート配列も責任をもって破棄
        AiFunctionLibrary.targetFunctions.Dispose();
        AiFunctionLibrary.skipFunctions.Dispose();

        Destroy(instance);
    }

    /// <summary>
    /// ジョブを実行する。
    /// </summary>
    private void BrainJobAct()
    {
        AITestJob brainJob = new AITestJob
        {
            // データの引き渡し。
            relationMap = relationMap,
            characterData = this.charaDataDictionary.GetInternalList1ForJob(),
            teamHate = this.teamHate,
            targetFunctions = AiFunctionLibrary.targetFunctions,
            skipFunctions = AiFunctionLibrary.skipFunctions,
            nowTime = GameManager.instance.NowTime,
            judgeResult = this.judgeResult
        };

        JobHandle handle = brainJob.Schedule(brainJob.characterData.Length, 64);

        // ジョブの完了を待機
        handle.Complete();

    }

}
