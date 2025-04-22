using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using static CombatManager;
using static JTestAIBase;

/// <summary>
/// 使用場面に基づいてデータを構造体に切り分けている。
/// メモリキャッシュを意識。
/// </summary>
[CreateAssetMenu(fileName = "JobAITestStatus", menuName = "Scriptable Objects/JobAITestStatus")]
public class JobAITestStatus : SerializedScriptableObject
{
    #region Enum定義

    /// <summary>
    /// 自分が行動を決定するための条件
    /// ○○の時、攻撃・回復・支援・逃走・護衛など
    /// 対象が自分、味方、敵のどれかという区分と否定（以上が以内になったり）フラグの組み合わせで表現
    /// 行動のバリエーションはモードチェンジ、攻撃、回復などの具体的行動
    /// 味方が死んだとき、は死亡状態で数秒キャラを残すことで、死亡を条件にしたフィルターにかかるようにするか。
    /// </summary>
    public enum ActJudgeCondition
    {
        指定のヘイト値の敵がいる時 = 1,
        //対象が一定数の時 = 2, // フィルターも活用することで、ここでかなりの数の単純な条件はやれる。一体以上条件でタイプフィルターで対象のタイプ絞ったり
        HPが一定割合の対象がいる時 = 2,
        MPが一定割合の対象がいる時 = 3,
        設定距離に対象がいる時 = 4, 　//距離系の処理は別のやり方で事前にキャッシュを行う。AIの設定の範囲だけセンサーで調べる方法をとる。判断時にやるようにする？
        特定の属性で攻撃する対象がいる時 = 5,
        特定の数の敵に狙われている時 = 6,// 陣営フィルタリングは有効
        条件なし = 0 // 何も当てはまらなかった時の補欠条件。
    }

    /// <summary>
    /// 行動判断をする前に
    /// 自分のMPやHPの割合などの、自分に関する前提条件を判断するための設定
    /// </summary>
    public enum SkipJudgeCondition
    {
        自分のHPが一定割合の時,
        自分のMPが一定割合の時,
        条件なし // 何も当てはまらなかった時の補欠条件。
    }

    /// <summary>
    /// MoveJudgeConditionの対象のタイプ
    /// </summary>
    public enum TargetType
    {
        自分 = 0,
        味方 = 1,
        敵 = 2
    }

    /// <summary>
    /// 判断の結果選択される行動のタイプ。
    /// 
    /// </summary>
    [Flags]
    public enum ActState
    {
        指定なし = 0,// ステートフィルター判断で使う。何も指定しない。
        追跡 = 1 << 0,
        逃走 = 1 << 1,
        攻撃 = 1 << 2,
        待機 = 1 << 3,// 攻撃後のクールタイム中など。この状態で動作する回避率を設定する？
        防御 = 1 << 4,// 動き出す距離を設定できるようにする？ その場で基本ガードだけど、相手がいくらか離れたら動き出す、的な
        支援 = 1 << 5,
        回復 = 1 << 6,
        集合 = 1 << 7,// 特定の味方の場所に行く。集合後に防御に移行するロジックを組めば護衛にならない？

        //遠慮 = 9// めちゃくちゃ狙われてる相手に対して、攻撃するかなーって迷ってる。
        // この状態はだんだんターゲットへのヘイト下がる。攻撃→遠慮→攻撃…を繰り返す。
        // ヘイトが関係ない条件でターゲットにした場合も攻撃頻度は落ちる。
        // 待機でいいじゃん
    }





    /// <summary>
    /// 敵に対するヘイト値の上昇、減少の条件。
    /// 条件に当てはまる敵のヘイト値が上昇したり減少したりする。
    /// あるいは味方の支援・回復・護衛対象を決める
    /// これも否定フラグとの組み合わせで使う
    /// </summary>
    public enum TargetSelectCondition
    {
        高度,
        HP割合,
        HP,
        敵に狙われてる数,//一番狙われてるか、狙われてないか
        合計攻撃力,
        合計防御力,
        斬撃攻撃力,//特定の属性の攻撃力が一番高い/低いヤツ
        刺突攻撃力,
        打撃攻撃力,
        炎攻撃力,
        雷攻撃力,
        光攻撃力,
        闇攻撃力,
        斬撃防御力,
        刺突防御力,
        打撃防御力,
        炎防御力,
        雷防御力,
        光防御力,
        闇防御力,
        // ここからは別判断。
        距離,// 未実装
        自分,
        プレイヤー,
        指定なし_ヘイト値, // 基本の条件。対象の中で最もヘイト高い相手を攻撃する。
        不要_状態変更// モードチェンジする。
    }


    /// <summary>
    /// キャラクターの属性。
    /// ここに当てはまる分全部ぶち込む。
    /// </summary>
    [Flags]
    public enum CharacterFeature
    {
        プレイヤー = 1 << 0,
        シスターさん = 1 << 1,
        NPC = 1 << 2,
        通常エネミー = 1 << 3,
        ボス = 1 << 4,
        兵士 = 1 << 5,//陸の雑兵
        飛行 = 1 << 6,//飛ぶやつ
        射手 = 1 << 7,//遠距離
        騎士 = 1 << 8,//盾持ち
        罠系 = 1 << 9,//待ち構えてるやつ
        強敵 = 1 << 10,// 強敵
        ザコ = 1 << 11,
        ヒーラー = 1 << 12,
        サポーター = 1 << 13,
        高速 = 1 << 14,
        指揮官 = 1 << 15,
        指定なし = 0//指定なし
    }


    /// <summary>
    /// キャラクターが所属する陣営
    /// </summary>
    public enum CharacterSide
    {
        プレイヤー = 0,// 味方
        魔物 = 1,// 一般的な敵
        その他 = 2,// それ以外
        指定なし = 3
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
        指定なし = 0
    }

    /// <summary>
    /// キャラのレベル
    /// このレベルが高いと他の敵に邪魔されない
    /// </summary>
    public enum CharacterRank
    {
        ザコ,//雑魚
        主力級,//基本はこれ
        指揮官,//強モブ
        ボス//ボスだけ
    }

    ///// <summary>
    ///// 行動を使用可能なモード
    ///// 五つまで
    ///// これもビット演算でやる？　複数選べるよ？
    ///// モード1か2なら…みたいに
    ///   モードはいらない。再現はできる。
    ///// </summary>
    //[Flags]
    //public enum Mode
    //{
    //    Mode1 = 1 << 0,
    //    Mode2 = 1 << 1,
    //    Mode3 = 1 << 2,
    //    Mode4 = 1 << 3,
    //    Mode5 = 1 << 4,
    //    AllMode = 0
    //}

    /// <summary>
    /// 特殊状態
    /// </summary>
    [Flags]
    public enum SpecialEffect
    {
        ヘイト増大 = 1 << 1,
        ヘイト減少 = 1 << 2,
        なし = 0,
    }

    #endregion Enum定義

    #region 構造体定義

    /// <summary>
    /// 各属性に対する攻撃力または防御力の値を保持する構造体
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ElementalStats
    {
        /// <summary>
        /// 斬撃属性の値
        /// </summary>
        [Header("斬撃属性")]
        public int slash;

        /// <summary>
        /// 刺突属性の値
        /// </summary>
        [Header("刺突属性")]
        public int pierce;

        /// <summary>
        /// 打撃属性の値
        /// </summary>
        [Header("打撃属性")]
        public int strike;

        /// <summary>
        /// 炎属性の値
        /// </summary>
        [Header("炎属性")]
        public int fire;

        /// <summary>
        /// 雷属性の値
        /// </summary>
        [Header("雷属性")]
        public int lightning;

        /// <summary>
        /// 光属性の値
        /// </summary>
        [Header("光属性")]
        public int light;

        /// <summary>
        /// 闇属性の値
        /// </summary>
        [Header("闇属性")]
        public int dark;

        /// <summary>
        /// 合計値を返す。
        /// </summary>
        /// <returns></returns>
        public int ReturnSum()
        {
            return slash + pierce + strike + fire + lightning + light + dark;
        }

    }

    /// <summary>
    /// 送信するデータ、不変の物
    /// 大半ビットでまとめれそう
    /// 空飛ぶ騎士の敵いるかもしれないしタイプは組み合わせ可能にする
    /// 初期化以降では、ステータスバフやデバフが切れた時に元に戻すくらいしかない
    /// Jobシステムで使用しないのでメモリレイアウトは最適化
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Auto)]
    public struct CharacterBaseData
    {

        /// <summary>
        /// 最大HP
        /// </summary>
        [Header("HP")]
        public int hp;

        /// <summary>
        /// 最大MP
        /// </summary>
        [Header("MP")]
        public int mp;

        /// <summary>
        /// 各属性の基礎攻撃力
        /// </summary>
        [Header("基礎属性攻撃力")]
        public ElementalStats baseAtk;

        /// <summary>
        /// 各属性の基礎防御力
        /// </summary>
        [Header("基礎属性防御力")]
        public ElementalStats baseDef;

        /// <summary>
        /// キャラの初期状態。
        /// </summary>
        [Header("最初にどんな行動をするのかの設定")]
        public ActState initialMove;

        /// <summary>
        /// デフォルトのキャラクターの所属
        /// </summary>
        public CharacterSide initialBelong;
    }

    /// <summary>
    /// 常に変わらないデータを格納する構造体。
    /// BaseDataとの違いは、初期化以降頻繁に参照する必要があるか。
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct SolidData
    {

        /// <summary>
        /// 外部表示用の攻撃力。
        /// </summary>
        [Header("表示用攻撃力")]
        public int displayAtk;

        /// <summary>
        /// 外部表示用の防御力。
        /// </summary>
        [Header("表示用防御力")]
        public int displayDef;

        /// <summary>
        /// 攻撃属性を示す列挙型
        /// ビット演算で見る
        /// NPCだけ入れる
        /// なに属性の攻撃をしてくるかというところ
        /// </summary>
        [Header("攻撃属性")]
        public Element attackElement;

        /// <summary>
        /// 弱点属性を示す列挙型
        /// ビット演算で見る
        /// NPCだけ入れる
        /// </summary>
        [Header("弱点属性")]
        public Element weakPoint;

        /// <summary>
        /// キャラの属性というか
        /// 特徴を示す。種類も包括
        /// </summary>
        [Header("キャラクター特徴")]
        public CharacterFeature feature;

        /// <summary>
        /// キャラの階級。<br/>
        /// これが上なほど味方の中で同じ敵をターゲットにしててもお控えしなくて済む、優先的に殴れる。<br/>
        /// あとランク低い味方に命令飛ばしたりできる。
        /// </summary>
        [Header("チーム内での階級")]
        public CharacterRank rank;

        /// <summary>
        /// この数値以上の敵から狙われている相手がターゲットになった場合、一旦次の判断までは待機になる
        /// その次の判断でやっぱり一番ヘイト高ければ狙う。(狙われまくってる相手へのヘイトは下がるので、普通はその次の判断でべつのやつが狙われる)
        /// 様子伺う、みたいなステート入れるか専用で
        /// 一定以上に狙われてる相手かつ、様子伺ってるキャラの場合だけヘイト下げるようにしよう。
        /// </summary>
        [Header("ターゲット上限")]
        [Tooltip("この数値以上の敵から狙われている相手が攻撃対象になった場合、一旦次の判断までは待機になる")]
        public int targetingLimit;
    }

    /// <summary>
    /// AIの設定。
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CharacterBrainStatus
    {
        /// <summary>
        /// AIの判断間隔
        /// </summary>
        [Header("判断間隔")]
        public float judgeInterval;

        /// <summary>
        /// 行動関連の設定データ
        /// </summary>
        [Header("行動設定")]
        public BehaviorData[] actCondition;

        /// <summary>
        /// 攻撃以外の行動条件データ.
        /// 最初の要素ほど優先度高いので重点。
        /// </summary>
        [Header("ヘイト条件データ")]
        public TargetJudgeData[] hateCondition;

    }

    /// <summary>
    /// 行動判断時に使用するデータ。
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct BehaviorData
    {

        /// <summary>
        /// 行動をスキップするための条件。
        /// </summary>
        [TabGroup(group: "AI挙動", tab: "スキップ条件")]
        public SkipJudgeData skipData;

        /// <summary>
        /// 行動の条件。
        /// 対象の陣営と特徴を指定できる。
        /// </summary>
        [TabGroup(group: "AI挙動", tab: "行動条件")]
        public ActJudgeData actCondition;

        /// <summary>
        /// 攻撃含むターゲット選択データ
        /// 要素は一つだが、その代わり複雑な条件で指定可能
        /// 特に指定ない場合のみヘイトで動く
        /// ここでヘイト以外の条件を指定した場合は、行動までセットで決める。
        /// </summary>
        [TabGroup(group: "AI挙動", tab: "対象選択条件")]
        public TargetJudgeData targetCondition;
    }

    /// <summary>
    /// 判断に使用するデータ。
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct SkipJudgeData
    {
        /// <summary>
        /// 行動判定をスキップする条件
        /// </summary>
        [Header("行動判定をスキップする条件")]
        public SkipJudgeCondition skipCondition;

        /// <summary>
        /// 判断に使用する数値。
        /// 条件によってはenumを変換した物だったりする。
        /// </summary>
        [Header("基準となる値")]
        public int judgeValue;

        /// <summary>
        /// 真の場合、条件が反転する
        /// 以上は以内になるなど
        /// </summary>
        [Header("基準反転フラグ")]
        [MarshalAs(UnmanagedType.U1)]
        public bool isInvert;

    }

    /// <summary>
    /// 判断に使用するデータ。
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ActJudgeData
    {
        /// <summary>
        /// 行動条件
        /// </summary>
        [Header("行動判定の条件")]
        public ActJudgeCondition judgeCondition;

        /// <summary>
        /// 判断に使用する数値。
        /// 条件によってはenumを変換した物だったりする。
        /// </summary>
        [Header("基準となる値")]
        public int judgeValue;

        /// <summary>
        /// 真の場合、条件が反転する
        /// 以上は以内になるなど
        /// </summary>
        [Header("基準反転フラグ")]
        [MarshalAs(UnmanagedType.U1)]
        public bool isInvert;

        /// <summary>
        /// これが指定なし、以外だとステート変更を行う。
        /// よって行動判断はスキップ
        /// </summary>
        [Header("変更先のモード（変更する場合）")]
        public ActState stateChange;

        /// <summary>
        /// 対象の陣営区分
        /// 複数指定あり
        /// </summary>
        [Header("チェック対象の条件")]
        public TargetFilter filter;
    }

    /// <summary>
    /// 行動判断後、行動のターゲットを選択する際に使用するデータ。
    /// ヘイトでもそれ以外でも構造体は同じ
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct TargetJudgeData
    {
        /// <summary>
        /// ターゲットの判断基準。
        /// </summary>
        [Header("ターゲット判断基準")]
        public TargetSelectCondition judgeCondition;

        /// <summary>
        /// 真の場合、条件が反転する
        /// 以上は以内になるなど
        /// </summary>
        [Header("基準反転フラグ")]
        [MarshalAs(UnmanagedType.U1)]
        public bool isInvert;

        /// <summary>
        /// 対象の陣営区分
        /// 複数指定あり
        /// </summary>
        [Header("チェック対象の条件")]
        public TargetFilter filter;

        /// <summary>
        /// 使用する行動の番号。
        /// 指定なし ( = -1)の場合は敵の条件から勝手に決める。(ヘイトで決めた場合は-1の指定なしになる)
        /// そうでない場合はここまで設定する。
        /// 
        /// あるいはヘイト上昇倍率になる。
        /// </summary>
        [Header("ヘイト倍率or使用する行動のNo")]
        [Tooltip("行動番号で-1を指定した場合、AIが対象の情報から行動を決める")]
        public float useAttackOrHateNum;
    }

    /// <summary>
    /// 行動条件や対象設定条件で検査対象をフィルターするための構造体
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct TargetFilter
    {
        /// <summary>
        /// 対象の陣営区分
        /// 複数指定あり
        /// </summary>
        [Header("対象の陣営")]
        [SerializeField]
        CharacterSide targetType;

        /// <summary>
        /// 対象の特徴
        /// 複数指定あり
        /// </summary>
        [Header("対象の特徴")]
        [SerializeField]
        CharacterFeature targetFeature;

        /// <summary>
        /// このフラグが真の時、全部当てはまってないとダメ。
        /// </summary>
        [Header("特徴の判断方法")]
        [SerializeField]
        [MarshalAs(UnmanagedType.U1)]
        bool isAndFeatureCheck;

        /// <summary>
        /// 対象の状態（バフ、デバフ）
        /// 複数指定あり
        /// </summary>
        [Header("対象が持つ特殊効果")]
        [SerializeField]
        SpecialEffect targetEffect;

        /// <summary>
        /// このフラグが真の時、全部当てはまってないとダメ。
        /// </summary>
        [Header("特殊効果の判断方法")]
        [SerializeField]
        [MarshalAs(UnmanagedType.U1)]
        bool isAndEffectCheck;

        /// <summary>
        /// 対象の状態（逃走、攻撃など）
        /// 複数指定あり
        /// </summary>
        [Header("対象の状態")]
        [SerializeField]
        ActState targetState;

        /// <summary>
        /// 対象のイベント状況（大ダメージを与えた、とか）でフィルタリング
        /// 複数指定あり
        /// </summary>
        [Header("対象のイベント")]
        [SerializeField]
        BrainEventFlagType targetEvent;

        /// <summary>
        /// このフラグが真の時、全部当てはまってないとダメ。
        /// </summary>
        [Header("イベントの判断方法")]
        [SerializeField]
        [MarshalAs(UnmanagedType.U1)]
        bool isAndEventCheck;

        /// <summary>
        /// 対象の弱点属性でフィルタリング
        /// 複数指定あり
        /// </summary>
        [Header("対象の弱点")]
        [SerializeField]
        Element targetWeakPoint;

        /// <summary>
        /// 対象が使う属性でフィルタリング
        /// 複数指定あり
        /// </summary>
        [Header("対象の使用属性")]
        [SerializeField]
        Element targetUseElement;

        /// <summary>
        /// 検査対象キャラクターの条件に当てはまるかをチェックする。
        /// </summary>
        /// <param name="belong"></param>
        /// <param name="feature"></param>
        /// <returns></returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public bool IsPassFilter(in CharacterData charaData)
        {
            // andかorで特徴条件判定
            // 当てはまらないなら帰る。
            if ( (isAndFeatureCheck ? ((targetFeature == 0) || (targetFeature & charaData.solidData.feature) == targetFeature) :
                                                  ((targetFeature == 0) || (targetFeature & charaData.solidData.feature) > 0)) == false )
            {
                return false;
            }

            // 特殊効果判断
            // 当てはまらないなら帰る。
            if ( (isAndEffectCheck ? ((targetEffect == 0) || (targetEffect & charaData.liveData.nowEffect) == targetEffect) :
                                      ((targetEffect == 0) || (targetEffect & charaData.liveData.nowEffect) > 0)) == false )
            {
                return false;
            }

            // イベント判断
            // 当てはまらないなら帰る。
            if ( (isAndEventCheck ? ((targetEvent == 0) || (targetEvent & charaData.liveData.brainEvent) == targetEvent) :
                                      ((targetEvent == 0) || (targetEvent & charaData.liveData.brainEvent) > 0)) == false )
            {
                return false;
            }

            // 残りの条件も判定。
            return ((targetType == 0 || ((targetType & charaData.liveData.belong) > 0)) && (targetState == 0 || ((targetState & charaData.liveData.actState) > 0))
                && (targetWeakPoint == 0 || ((targetWeakPoint & charaData.solidData.weakPoint) > 0)) && (targetUseElement == 0 || ((targetUseElement & charaData.solidData.attackElement) > 0)));
        }

    }

    ///// <summary>
    ///// モードを変える条件。
    ///// AIの振る舞い部分にあたる
    ///// これはまだ使わない
    ///// モード廃止に伴い封印
    ///// </summary>
    //[Serializable]
    //[StructLayout(LayoutKind.Sequential)]
    //public struct ModeBehavior
    //{
    //    /// <summary>
    //    /// アタックストップフラグが真の時だけ
    //    /// レベルとか関係なく発動
    //    /// </summary>
    //    [Header("攻撃抑制モードか")]
    //    public bool isAttackStop;

    //    /// <summary>
    //    /// 現在のモード
    //    /// 複数選択可能
    //    /// </summary>
    //    [Header("遷移元のモード")]
    //    public Mode nowMode;

    //    /// <summary>
    //    /// モードチェンジする体力割合
    //    /// 0なら無視
    //    /// </summary>
    //    [Header("モード変更する体力比。0で無視")]
    //    public int healthRatio;

    //    /// <summary>
    //    /// 前回のモードチェンジから何秒で変化するか
    //    /// 0なら無視
    //    /// </summary>
    //    [Header("モード変更時間")]
    //    public int changeTime;

    //    /// <summary>
    //    /// xからyの距離でこのモードに
    //    /// つまり直線距離Xメートルからyメートルの範囲ってことね
    //    /// 直線距離
    //    /// 00なら無視
    //    /// </summary>
    //    [Header("モード変更距離（00で無効）")]
    //    public Vector2 changeDistance;

    //    /// <summary>
    //    /// 変える先のモード
    //    /// Allならランダムに変わる
    //    /// 間合いとかの配列数からモードの数を割り出す
    //    /// </summary>
    //    [Header("変更先のモード")]
    //    public Mode changeMode;

    //    /// <summary>
    //    /// この条件のチェンジの優先度
    //    /// 0は基本モードにのみ使う
    //    /// いや基本モードはマイナス1でもいいな
    //    /// どこからでも戻れるように条件は軽く
    //    /// </summary>
    //    [Header("チェンジの優先度。5の時固定")]
    //    public int modeLevel;
    //}

    /// <summary>
    /// 攻撃のステータス。
    /// 倍率とか属性とかそのへん。
    /// これはステータスに持たせておく。
    /// 前回使用した時間、とかを記録するために、キャラクター側に別途リンクした管理情報が必要。
    /// Jobシステムで使用しない構造体はなるべくメモリレイアウトを最適化する。
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Auto)]
    public struct AttackData
    {
        /// <summary>
        /// 攻撃倍率。
        /// いわゆるモーション値
        /// </summary>
        [Header("攻撃倍率（モーション値）")]
        public float motionValue;
    }

    /// <summary>
    /// キャラの行動ステータス。
    /// 移動速度など。
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct MoveStatus
    {
        /// <summary>
        /// 通常の移動速度
        /// </summary>
        [Header("通常移動速度")]
        public int moveSpeed;

        /// <summary>
        /// 歩行速度。後ろ歩きも同じ
        /// </summary>
        [Header("歩行速度")]
        public int walkSpeed;

        /// <summary>
        /// ダッシュ速度
        /// </summary>
        [Header("ダッシュ速度")]
        public int dashSpeed;

        /// <summary>
        /// ジャンプの高さ。
        /// </summary>
        [Header("ジャンプの高さ")]
        public int jumpHeight;
    }

    #endregion 構造体定義

    /// <summary>
    /// キャラのベース、固定部分のデータ。
    /// これは直接仕様はせず、コピーして各キャラに渡してあげる。
    /// </summary>
    [Header("キャラクターの基本データ")]
    public CharacterBaseData baseData;

    /// <summary>
    /// 固定のデータ。
    /// </summary>
    [Header("固定データ")]
    public SolidData solidData;

    /// <summary>
    /// キャラのAIの設定。
    /// モードごとにモードEnumをint変換した数をインデックスにした配列になる。
    /// </summary>
    [Header("キャラAIの設定")]
    public ActStateBrainDictionary brainData;

    /// <summary>
    /// 移動速度などのデータ
    /// </summary>
    [Header("移動ステータス")]
    public MoveStatus moveStatus;

    /// <summary>
    /// 各攻撃の攻撃力などの設定。
    /// </summary>
    [Header("攻撃データ一覧")]
    public AttackData[] attackData;

    /// <summary>
    /// AIの移動判断間隔
    /// </summary>
    [Header("移動判断間隔")]
    public float moveJudgeInterval;

}
