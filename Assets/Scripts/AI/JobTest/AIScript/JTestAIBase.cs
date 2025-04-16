using UnityEngine;
using Cysharp.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System.Collections.Generic;
using System;
using Unity.VisualScripting;
using static JobAITestStatus;
using System.Runtime.InteropServices;
using UnityEngine.Events;

/// <summary>
/// NativeContainerのリストを使うぞ
/// 基本方針は以下
/// 
/// 
/// 書き換え可能データと不可データ、いくつものNativeContainerを統一の番号で管理する。
/// いくつものコンテナをラップしたクラスで管理し、外部からはキャラ番号でデータにアクセスしたり書き換えたりする。
/// 書き換えは内部で行う。可能な限り struct.member= のように
/// 
/// 高速化するならその分おもちゃを作る。意味のない複雑化、高速化は無駄
/// 
/// 配列を含む構造体の配列はありらしい。しかしいずれもNativeContainerでお願い
/// 
/// 書き換え不可能データ
/// キャラデータ、判断基準データ
/// 
/// 書き換え可能データ
/// キャラ状態データ、行動データ、
/// </summary>
public class JTestAIBase : MonoBehaviour
{
    #region 定義

    #region enum定義

    /// <summary>
    /// 判断結果をまとめて格納するビット演算用
    /// </summary>
    [Flags]
    public enum JudgeResult
    {
        新しく判断をしたか = 1 << 1,// この時は移動方向も変える
        方向転換をしたか = 1 << 2,
        ステート変更したか = 1 << 3 // 状態が変わった時は、行動指定番号がそのまま変更先のステートになる
    }

    #endregion enum定義


    #region 構造体定義

    /// <summary>
    /// 判断に使用するデータの構造体。
    /// 現在の行動状態、移動方向、判断基準、HPほかステータス、全て収まっている。
    /// これに従って動く
    /// あるいはデータを分けるか？
    /// 
    /// 攻撃や魔法のエフェクトなどのデータは別のデータ型に入れて、選択した攻撃の番号などで参照するか？
    /// なんなら全部のデータ配列でもいい？　HP、などの名前の列挙子の値でアクセスすればいい。
    ///
    /// 判断条件と、条件をパスした時の行動（逃走モードなどの状態変化や攻撃などの具体的行動）
    /// 状態は外部から変えられるようにしよ。被弾イベントで逃走とか
    /// ヘイト値の管理と味方への命令、敵への威嚇などの送信の実装が悩みどころ。
    /// 全部値型でやらないとな
    /// ヘイト値はどう使うか。○○の時攻撃、ヘイト値が一番高いやつに攻撃、攻撃する相手によって（位置とか弱点とかで）攻撃を決定、って流れにする？
    /// これだと攻撃条件、ヘイト値の設定による値（＝攻撃する相手の傾向）判断、相手に合わせた攻撃、の三つの段階になってるな
    /// 逃走と戦闘や警戒でだいぶ判断変わりそう
    /// シスターさんにはどう設定するか
    /// 
    /// ジョブから受け取ったデータに基づいて動くだけ。
    /// 設定などはステータスに分離
    /// 
    /// こいつは TempJobのメモリで作ったWriteOnlyのNativeArrayで、Job実行のたびにinつきメソッドを通じてここに結果反映する。
    /// 
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct MovementInfo
    {

        // これキャラがどう動くか、みたいなデータまで入れてる
        // 新規判断後はターゲット入れ替えとか、判断時間入れ替えとかちょっとキャラデータをいじる。

        /// <summary>
        /// ターゲットのハッシュコード
        /// これで相手を取得する。
        /// 味方への支援ムーブのターゲットもありうることは頭に入れる。
        /// </summary>
        public int targetHash;

        /// <summary>
        /// 現在のターゲットとの距離。マイナスもあるので方向でもある。
        /// </summary>
        public int targetDirection;

        /// <summary>
        /// 攻撃の判断で通過した判断条件が何番かというデータ。判断にかからんければ、というか攻撃しないときは-1入れとく<br/>
        /// これは攻撃状態の時使用する。<br/>
        /// 基本的に判断条件はキャラステータスから引っ張ってくるはずなので、〇番の条件に紐づく攻撃手段は各キャラクターが各自でおこなう。
        /// 敵キャラなどの場合、基本は番号で攻撃を直接指定することが多い。<br/>
        /// どの判断条件をクリアしたか（距離が1~3とか）で攻撃が対応する物が設定されている。<br/>
        /// で、決定した攻撃の番号を渡すことでキャラが攻撃する。<br/>
        /// しかしシスターさんの場合は装備する魔法が変わるため、条件で攻撃を指定する。<br/>
        /// その場合は条件を受け取って、クラスの方でシスターさんが使う魔法を決める。<br/>
        /// というより魔法と普通の攻撃を統合して、攻撃を選ぶ感じにした方がAIの汎用性が出そう
        /// 
        /// 行動を指定する番号。-1、の場合だけ敵条件から行動判断
        /// そうでなければ直接指定
        /// しかし味方NPCの場合、魔法を組み替えできるので行動の指定番号は可変だね。
        /// あとMP足りないときどうしよう。MPは全キャラで保有/消費するようにして、それで行動の制限かけるか。で、MPがあるかの判断を仕様に組み込む
        /// 
        /// ちなみにステート変更時は変更先ステートの番号になる。
        /// ステート変更したらすぐ判断できるようにlastJudgetimeも変えないとな
        /// </summary>
        public int attackJudgeNum;

        /// <summary>
        /// 判断結果についての情報を格納するビット
        /// </summary>
        public int result;

        /// <summary>
        /// 行動状態。
        /// </summary>
        public ActState moveState;

    }

    /// <summary>
    /// Jobシステムで使用するキャラクターデータ構造体。
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CharacterData : IDisposable
    {
        /// <summary>
        /// 新しいキャラクターデータを取得する。
        /// </summary>
        /// <param name="status"></param>
        /// <param name="gameObject"></param>
        public CharacterData(JobAITestStatus status, GameObject gameObject)
        {
            brainData = new NativeArray<CharacterBrainStatusForJob>(status.brainData.Length, Allocator.Persistent);

            for ( int i = 0; i < status.brainData.Length; i++ )
            {
                brainData[i].ConvertToJobFormat(status.brainData[i], Allocator.Persistent);
            }

            hashCode = gameObject.GetHashCode();
            liveData = new CharacterUpdateData(status.baseData, gameObject.transform.position);
            solidData = status.solidData;
            targetingCount = 0;
            // 最初はマイナスで10000を入れることですぐ動けるように
            lastJudgeTime = -10000;

            personalHate = new NativeHashMap<int, int>(7, Allocator.Persistent);
            shortRangeCharacter = new UnsafeList<int>(7, Allocator.Persistent);

            moveJudgeInterval = status.moveJudgeInterval;
            lastMoveJudgeTime = 0;// どうせ行動判断時に振り向くから
        }

        /// <summary>
        /// 固定のデータ。
        /// </summary>
        public SolidData solidData;

        /// <summary>
        /// キャラのAIの設定。(Jobバージョン)
        /// モードごとにモードEnumをint変換した数をインデックスにした配列になる。
        /// </summary>
        public NativeArray<CharacterBrainStatusForJob> brainData;

        /// <summary>
        /// 更新されうるデータ。
        /// </summary>
        public CharacterUpdateData liveData;

        /// <summary>
        /// 自分を狙ってる敵の数。
        /// ボスか指揮官は無視でよさそう
        /// 今攻撃してるやつも攻撃を終えたら別のターゲットを狙う。
        /// このタイミングで割りこめるやつが割り込む
        /// あくまでヘイト値を減らす感じで。一旦待機になって、ヘイト減るだけなので殴られたら殴り返すよ
        /// 遠慮状態以外なら遠慮になるし、遠慮中でなお一番ヘイト高いなら攻撃して、その次は遠慮になる
        /// </summary>
        public int targetingCount;

        /// <summary>
        /// 最後に判断した時間。
        /// </summary>
        public float lastJudgeTime;

        /// <summary>
        /// 最後に移動判断した時間。
        /// </summary>
        public float lastMoveJudgeTime;

        /// <summary>
        /// キャラクターのハッシュ値を保存しておく。
        /// </summary>
        public int hashCode;

        /// <summary>
        /// 攻撃してきた相手とか、直接的な条件に当てはまった相手のヘイトだけ記録する。
        /// </summary>
        public NativeHashMap<int, int> personalHate;

        /// <summary>
        /// 近くにいるキャラクターの記録。
        /// これはセンサーで断続的に取得する参考値。
        /// 上限は7~10の予定
        /// </summary>
        public UnsafeList<int> shortRangeCharacter;

        /// <summary>
        /// AIの移動判断間隔
        /// </summary>
        [Header("移動判断間隔")]
        public float moveJudgeInterval;

        /// <summary>
        /// NativeContainerを含むメンバーを破棄。
        /// CombatManagerが責任を持って破棄する。
        /// </summary>
        public void Dispose()
        {
            brainData.Dispose();
            personalHate.Dispose();
            shortRangeCharacter.Dispose();
        }
    }

    /// <summary>
    /// AIの設定。（Jobシステム仕様）
    /// ステータスから移植する。
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CharacterBrainStatusForJob : IDisposable
    {
        /// <summary>
        /// AIの判断間隔
        /// </summary>
        [Header("判断間隔")]
        public float judgeInterval;



        /// <summary>
        /// 行動条件データ
        /// </summary>
        [Header("行動条件データ")]
        public NativeArray<BehaviorData> actCondition;

        /// <summary>
        /// 攻撃以外の行動条件データ.
        /// 最初の要素ほど優先度高いので重点。
        /// </summary>
        [Header("ヘイト条件データ")]
        public NativeArray<TargetJudgeData> hateCondition;

        /// <summary>
        /// ヘイト条件に対応するヘイト上昇倍率
        /// </summary>
        [Header("ヘイト上昇倍率")]
        public NativeArray<int> hateMultiplier;

        /// <summary>
        /// 攻撃含む行動条件データ.
        /// 要素は一つだが、その代わり複雑な条件で指定可能
        /// 特に指定ない場合のみヘイトで動く
        /// ここでヘイト以外の条件を指定した場合は、行動までセットで決める。
        /// </summary>
        [Header("行動条件データ")]
        public TargetJudgeData targetCondition;

        /// <summary>
        /// NativeArrayリソースを解放する
        /// </summary>
        public void Dispose()
        {
            if ( actCondition.IsCreated )
                actCondition.Dispose();
            if ( hateCondition.IsCreated )
                hateCondition.Dispose();
            if ( hateMultiplier.IsCreated )
                hateMultiplier.Dispose();
        }

        /// <summary>
        /// オリジナルのCharacterBrainStatusからデータを明示的に移植
        /// </summary>
        /// <param name="source">移植元のキャラクターブレインステータス</param>
        /// <param name="allocator">NativeArrayに使用するアロケータ</param>
        public void ConvertToJobFormat(in CharacterBrainStatus source, Allocator allocator)
        {

            // 基本プロパティをコピー
            judgeInterval = source.judgeInterval;
            targetCondition = source.targetCondition;

            // 配列を新しく作成
            actCondition = source.actCondition != null
                ? new NativeArray<BehaviorData>(source.actCondition, allocator)
                : new NativeArray<BehaviorData>(0, allocator);

            hateCondition = source.hateCondition != null
                ? new NativeArray<TargetJudgeData>(source.hateCondition, allocator)
                : new NativeArray<TargetJudgeData>(0, allocator);
        }

    }

    /// <summary>
    /// 更新されるキャラクターの情報。
    /// 状態異常とかバフも入れて時間継続の終了までJobで見るか。
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CharacterUpdateData
    {
        /// <summary>
        /// 最大体力
        /// </summary>
        public int maxHp;

        /// <summary>
        /// 体力
        /// </summary>
        public int currentHp;

        /// <summary>
        /// 最大魔力
        /// </summary>
        public int maxMp;

        /// <summary>
        /// 魔力
        /// </summary>
        public int currentMp;

        /// <summary>
        /// HPの割合
        /// </summary>
        public float hpRatio;

        /// <summary>
        /// MPの割合
        /// </summary>
        public float mpRatio;

        /// <summary>
        /// 各属性の基礎攻撃力
        /// </summary>
        public ElementalStats atk;

        /// <summary>
        /// 各属性の基礎防御力
        /// </summary>
        public ElementalStats def;

        /// <summary>
        /// 現在位置。
        /// </summary>
        public Vector2 nowPosition;

        /// <summary>
        /// 現在のキャラクターの所属
        /// </summary>
        public CharacterSide belong;

        /// <summary>
        /// 現在の行動状況。
        /// 判断間隔経過したら更新？
        /// 攻撃されたりしたら更新？
        /// あと仲間からの命令とかでも更新していいかも
        /// 
        /// 移動とか逃走でAIの動作が変わる。
        /// 逃走の場合は敵の距離を参照して相手が少ないところに逃げようと考えたり
        /// </summary>
        public ActState actState;

        /// <summary>
        /// キャラが大ダメージを与えた、などのイベントを格納する場所。
        /// </summary>
        public int brainEventBit;

        /// <summary>
        /// 既存のCharacterUpdateDataにCharacterBaseDataの値を適用する
        /// </summary>
        /// <param name="baseData">適用元のベースデータ</param>
        public CharacterUpdateData(in CharacterBaseData baseData, Vector2 initialPosition)
        {
            // 攻撃力と防御力を更新
            atk = baseData.baseAtk;
            def = baseData.baseDef;

            maxHp = baseData.hp;
            maxMp = baseData.mp;
            currentHp = baseData.hp;
            currentMp = baseData.mp;
            hpRatio = 1;
            mpRatio = 1;

            belong = baseData.initialBelong;

            nowPosition = initialPosition;

            actState = baseData.initialMove;
            brainEventBit = 0;
        }
    }

    #endregion

    #endregion


    /// テストで使用するステータス。<br></br>
    /// 判断間隔のデータが入っている。<br></br>
    /// インスペクタから設定。
    /// </summary>
    [SerializeField]
    protected JobAITestStatus status;

    /// <summary>
    /// 移動に使用する物理コンポーネント。
    /// </summary>
    [SerializeField]
    Rigidbody2D rb;

    /// <summary>
    /// 何回判断したかを数える。<br></br>
    /// 非同期と同期で、期待する判断回数との間の誤差が異なるかを見る。<br></br>
    /// 最初の行動の分だけ1引いた初期値に。
    /// </summary>
    [HideInInspector]
    public long judgeCount = -1;


    /// <summary>
    /// 初期化処理。
    /// </summary>
    protected void Initialize()
    {
        // 新しいキャラデータを送り、コンバットマネージャーに送る。
        // いや、やっぱり材料送って向こうで作ってもらおう。
        // NativeContainer含む構造体をコピーするのなんかこわい。
        // ただもしコピーしても、こっちで作った分はローカル変数でしかないからDispose()周りの問題はないはず。
        CombatManager.instance.CharacterAdd(status, gameObject);
    }



    /// <summary>
    /// 行動を判断するメソッド。
    /// </summary>
    protected void MoveJudgeAct()
    {
        // 50%の確率で左右移動の方向が変わる。
        // moveDirection = (UnityEngine.Random.Range(0, 100) >= 50) ? 1 : -1;

        //  rb.linearVelocityX = moveDirection * status.xSpeed;

        //Debug.Log($"数値：{moveDirection * status.xSpeed} 速度：{rb.linearVelocityX}");

        //lastJudge = GameManager.instance.NowTime;
        judgeCount++;
    }

    /// <summary>
    /// ターゲットを決めて攻撃する。
    /// </summary>
    protected void Attackct()
    {

    }

}
