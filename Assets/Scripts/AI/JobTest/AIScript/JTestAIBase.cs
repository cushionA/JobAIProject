using UnityEngine;
using Cysharp.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System.Collections.Generic;
using System;
using Unity.VisualScripting;

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
/// 配列を含む構造体の配列はありらしい。しかしいずれもNativeArrayでお願い
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


    public enum MoveState
    {
        追跡,
        逃走,
        攻撃,
        待機,// 攻撃後のクールタイム中など。この状態で動作する回避率を設定する？
        ガード,// 動き出す距離を設定できるようにする？ その場で基本ガードだけど、相手がいくらか離れたら動き出す、的な
        支援,
        回復,
        護衛,
        警戒,
        死亡
    }


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
    /// 
    /// 
    /// </summary>
    public struct MoveData
    {

        // これキャラがどう動くか、みたいなデータまで入ってるね

        /// <summary>
        /// キャラデータ配列の中でのターゲットの位置。
        /// これで相手を取得する。
        /// 味方への支援ムーブもありうることは頭に入れる。
        /// </summary>
        public int targetNum;

        /// <summary>
        /// 現在の移動方向。
        /// </summary>
        public int moveDirection;

        /// <summary>
        /// 現在の行動状況。
        /// 判断間隔経過したら更新？
        /// 攻撃されたりしたら更新？
        /// あと仲間からの命令とかでも更新していいかも
        /// 
        /// 移動とか逃走でAIの動作が変わる。
        /// 逃走の場合は敵の距離を参照して相手が少ないところに逃げようと考えたり
        /// </summary>
        public MoveState state;

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
        /// </summary>
        public int attackJudgeNum;

    }



    /// <summary>
    /// 体力の割合とか変化するキャラデータ
    /// 状態異常はコンディションデータから見ろ
    /// </summary>
    public struct ConditionData
    {


        /// <summary>
        /// 体力の割合
        /// </summary>
        public float hpRatio;

        /// <summary>
        /// 体力の数値
        /// </summary>
        public float hpNum;

        /// <summary>
        /// MPの割合
        /// </summary>
        public float mpRatio;

    }


    #region 定義


    /// <summary>
    /// 送信するデータ、不変の物
    /// 大半ビットでまとめれそう
    /// 空飛ぶ騎士の敵いるかもしれないしタイプは組み合わせ可能にする？
    /// </summary>
    public struct CharacterData
    {

        /// <summary>
        /// 敵の種類
        /// 騎士とか射手とかそういうの
        /// </summary>
        public KindOfCharacter kind;

        /// <summary>
        /// キャラの属性というか
        /// タイプを示す
        /// </summary>
        public CharacterFeature type;



        /// <summary>
        /// 強い？
        /// こんなんビットでまとめろや
        /// </summary>
        [Header("強敵かどうか")]
        public bool isStrong;

        /// <summary>
        /// 表示攻撃力
        /// </summary>
        public float displayAtk;

        /// <summary>
        /// 表示防御力
        /// </summary>
        public float displayDef;

        /// <summary>
        /// 攻撃属性を示す列挙型
        /// ビット演算で見る
        /// NPCだけ入れる
        /// なに属性の攻撃をしてくるかというところ
        /// </summary>
        public Element attackElement;

        /// <summary>
        /// 弱点属性を示す列挙型
        /// ビット演算で見る
        /// NPCだけ入れる
        /// </summary>
        public Element WeakPoint;

        /// <summary>
        /// キャラの階級。<br/>
        /// これが上なほど味方の中で同じ敵をターゲットにしててもお控えしなくて済む、優先的に殴れる。<br/>
        /// あとランク低い味方に命令飛ばしたりできる。
        /// </summary>
        [Header("チーム内での階級")]
        public CharacterRank rank;

    }

    /// <summary>
    /// キャラクターのタイプ。<br/>
    /// 複数当てはまる場合もある。
    /// </summary>
    [Flags]
    public enum KindOfCharacter
    {
        Soldier,//陸の雑兵
        Fly,//飛ぶやつ
        Shooter,//遠距離
        Knight,//盾持ち
        Trap,//待ち構えてるやつ
        none//指定なし
    }

    /// <summary>
    /// キャラクターの属性。
    /// ここに当てはまる分全部ぶち込む。
    /// </summary>
    [Flags]
    public enum CharacterFeature
    {
        Player = 1 << 0,
        Sister = 1 << 1,
        NPC = 1 << 2,
        Enemy = 1 << 3,
        Boss = 1 << 4,
        Soldier = 1 << 5,//陸の雑兵
        Fly = 1 << 6,//飛ぶやつ
        Shooter = 1 << 7,//遠距離
        Knight = 1 << 8,//盾持ち
        Trap = 1 << 9,//待ち構えてるやつ
        none = 0//指定なし
    }


    /// <summary>
    /// キャラクターが所属する陣営
    /// これでアクセスするマネージャーが決まる
    /// </summary>
    public enum CharacterSide
    {
        Player,
        Enemy,
        Other//これは使わない。統一仕様としてOther同士は自分をプレイヤー、敵をエネミーとみなすように
             //使わないがいましめとして残す。あとプレイヤーはOtherを敵とみなすし敵はプレイヤーとみなす
    }

    public enum AttackType
    {
        Slash,//斬撃
        Stab,//刺突
        Strike//打撃
    }


    /// <summary>
    /// 属性の列挙型
    /// 状態異常は除くか
    /// 
    /// 
    /// </summary>
    [Flags]
    public enum Element
    {
        斬撃属性 = 1 << 0,
        刺突属性 = 1 << 1,
        打撃属性 = 1 << 2,
        聖属性 = 1 << 3,
        闇属性 = 1 << 4,
        炎属性 = 1 << 5,
        雷属性 = 1 << 6,
        毒属性 = 1 << 7,
        猛毒属性 = 1 << 8,
        凍結属性 = 1 << 9,
        弱点属性 = 1 << 13,//敵の弱点属性をサーチして代わりに使う
        指定なし = 0
    }

    /// <summary>
    /// キャラのレベル
    /// このレベルが高いと他の敵に邪魔されない
    /// </summary>
    public enum CharacterRank
    {
        weak,//雑魚
        normal,//基本はこれ
        strong,//強モブ
        absolute//ボスだけ
    }

    /// <summary>
    /// 行動を使用可能なモード
    /// 五つまで
    /// これもビット演算でやる？　複数選べるよ？
    /// モード1か2なら…みたいに
    /// </summary>
    [Flags]
    public enum Mode
    {
        Mode1 = 1 << 0,
        Mode2 = 1 << 1,
        Mode3 = 1 << 2,
        Mode4 = 1 << 3,
        Mode5 = 1 << 4,
        AllMode = 1 << 5
    }
    /// <summary>
    /// モードを変える条件
    /// </summary>
    [Serializable]
    public struct ModeBehavior
    {

        /// <summary>
        /// アタックストップフラグが真の時だけ
        /// レベルとか関係なく発動
        /// </summary>
        [Header("攻撃抑制モードか")]
        public bool isAttackStop;

        /// <summary>
        /// 現在のモード
        /// 複数選択可能
        /// </summary>
        [Header("遷移元のモード")]
        public Mode nowMode;

        /// <summary>
        /// モードチェンジする体力割合
        /// 0なら無視
        /// </summary>
        [Header("モード変更する体力比。0で無視")]
        public int healthRatio;

        /// <summary>
        /// 前回のモードチェンジから何秒で変化するか
        /// 0なら無視
        /// </summary>
        [Header("モード変更時間")]
        public int changeTime;

        /// <summary>
        /// xからyの距離でこのモードに
        /// つまり直線距離Xメートルからyメートルの範囲ってことね
		/// 直線距離
        /// 00なら無視
        /// </summary>
        [Header("モード変更距離（00で無効）")]
        public Vector2 changeDistance;

        /// <summary>
        /// 変える先のモード
        /// Allならランダムに変わる
        /// 間合いとかの配列数からモードの数を割り出す
        /// </summary>
        [Header("変更先のモード")]
        public Mode changeMode;

        /// <summary>
        /// この条件のチェンジの優先度
        /// 0は基本モードにのみ使う
        /// いや基本モードはマイナス1でもいいな
        /// どこからでも戻れるように条件は軽く
        /// </summary>
        [Header("チェンジの優先度。5の時固定")]
        public int modeLevel;

    }




    /// <summary>
    /// 防御力のまとめ構造体
    /// </summary>
    public struct DefStatus
    {
        /// <summary>
        /// 斬撃防御
        /// 防御はそれぞれの属性に対応しているので大丈夫
        /// 全ての防御力の総計みたいなのはいらない
        /// </summary>
        [Header("斬撃防御力。体力で上がる")]
        public float slashDef;

        [Header("刺突防御。筋力で上がる")]
        public float pierDef;

        [Header("打撃防御、技量で上がる")]
        public float strDef;

        [Header("神聖防御、筋と賢さで上がる")]
        public float holyDef;

        [Header("闇防御。賢さで上がる")]
        public float darkDef;

        [Header("炎防御。賢さと生命で上がる")]
        public float fireDef;

        [Header("雷防御。賢さと持久で上がる")]
        public float thunderDef;



    }


    /// <summary>
    /// アイテム効果倍率や特殊フラグなどのイベントを管理するためのクラス
    /// 
    /// これ全部アビリティに問い合わせればいいだけじゃん
    /// 
    ///  アクション変化,

    ///  アイテム効果変動,
    ///  特殊バフ, //音消滅、バフ効果時間延長、復活など。カウンター攻撃倍率とかもこれにするか
    ///  特殊デバフ,//停止とか。停止はスタンの終わりをアニメの終わりではなく状態異常の終わりまでとする。魔法禁止も？
    ///  状態異常蓄積,//毒とかそういうのがたまっていく過程　これは状態管理アビリティで持たせる？　状態異常耐性
    ///  バリア,  エフェクトを管理する？　これはコントロールアビリティで管理するか
    ///  エンチャント,　 エンチャはステータス（NPC）や武器（プレイヤー）にエンチャ情報を保存させるようにする？　装備に保存させるか。盾も
    ///  
    ///  ↓↓↓↓↓↓↓トリガーイベント↓↓↓↓↓↓↓↓↓
    ///  攻撃ヒットイベント,  ok
    ///  ジャストガードイベント,　　ok
    ///  瀕死イベント,   ok　　体力系は体力変動時に呼ぶか
    ///  体力マックス時イベント,    ok
    ///  回避時イベント,   ok
    ///  敵撃破時イベント,    ok
    ///  被弾時イベント,　　ok
    /// </summary>
    public struct SpecialConditionContainer
    {

        ///// <summary>
        ///// トリガーイベント
        ///// それぞれのトリガーイベントタイプに
        ///// フラグをビット演算で保存していく
        ///// </summary>
        //Dictionary<ConditionAndEffectControllAbility.EventType, TriggerEventSelect> triggerEvents;

        ///// <summary>
        ///// 特別なバフ
        ///// </summary>
        //SpecialBuffSelect specialBuff;


        ///// <summary>
        ///// 特別なデバフ
        ///// ここに記録していく？
        ///// いや必要なくね？
        ///// アビリティに聞けばいいわけで
        ///// </summary>
        //SpecialDebuffSelect specialDebuff;


    }



    #endregion




    #endregion


    /// テストで使用するステータス。<br></br>
    /// 判断間隔のデータが入っている。<br></br>
    /// インスペクタから設定。
    /// </summary>
    [SerializeField]
    protected AsyncTestStatus status;

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
    /// jobシステムに渡すためのNativeContainer
    /// </summary>
    NativeList<int> characterData = new NativeList<int>();

    /// <summary>
    /// 判断間隔を返すプロパティ。
    /// </summary>
    public float JudgeInterval { get { return status.judgeInterval; } }


    /// <summary>
    /// 初期化処理。
    /// </summary>
    protected void Initialize()
    {

        // characterData.add

        //Debug.Log("テスト待機");

        while ( true )
        {
            if ( GameManager.instance.isTest == true )
            {
                break;
            }
        }

        //Debug.Log("テスト開始");

        MoveJudgeAct();
    }

    /// <summary>
    /// 判断間隔の待機が完了したかを判定するメソッド。<br></br>
    /// 非同期、同期間で公平を期すために最初は同じ処理を使おう。
    /// </summary>
    /// <returns></returns>
    protected bool IntervalEndJudge()
    {
        // テスト継続中で、かつ前回の判定から判断間隔以上の時間が経過した場合に真を返す。
        return GameManager.instance.isTest && ((GameManager.instance.NowTime) > status.judgeInterval);
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
