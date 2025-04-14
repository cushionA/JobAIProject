using System;
using System.Runtime.InteropServices;
using UnityEngine;
using static JTestAIBase;

/// <summary>
/// 使用場面に基づいてデータを構造体に切り分けている。
/// メモリキャッシュを意識。
/// </summary>
[CreateAssetMenu(fileName = "JobAITestStatus", menuName = "Scriptable Objects/JobAITestStatus")]
public class JobAITestStatus : ScriptableObject
{
    #region Enum定義

    /// <summary>
    /// 自分が行動を決定するための条件
    /// ○○の時、攻撃・回復・支援・逃走・護衛など
    /// 対象が自分、味方、敵のどれかという区分と否定（以上が以内になったり）フラグの組み合わせで表現
    /// 行動のバリエーションはモードチェンジ、攻撃、回復などの具体的行動
    /// </summary>
    public enum MoveJudgeCondition
    {
        対象が一定数の時,
        HPが一定割合の対象がいる時,
        一定距離に対象がいる時,
        任意のタイプの対象がいる時,
        対象が回復や支援を使用した時,// 回復魔法とかで回復した時に全体イベントを飛ばすか。
        対象が大ダメージを受けた時,
        対象が特定の特殊効果の影響を受けている時,//バフとかデバフ
        対象が死亡した時,
        対象が攻撃された時
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
    /// </summary>
    public enum MoveState
    {
        追跡,
        逃走,
        攻撃,
        待機,// 攻撃後のクールタイム中など。この状態で動作する回避率を設定する？
        防御,// 動き出す距離を設定できるようにする？ その場で基本ガードだけど、相手がいくらか離れたら動き出す、的な
        支援,
        回復,
        護衛,
        警戒
    }


    /// <summary>
    /// 敵に対するヘイト値の上昇、減少の条件。
    /// 条件に当てはまる敵のヘイト値が上昇したり減少したりする。
    /// あるいは味方の支援・回復・護衛対象を決める
    /// これも否定フラグとの組み合わせで使う
    /// </summary>
    public enum TargetSelectCondition
    {
        距離,
        高度,
        HP割合,
        HP,
        攻撃力,
        防御力,
        キャラタイプ,// 割合や数値を示すint値にタイプを入れる。
        弱点属性,// 割合や数値を示すint値にタイプを入れる。

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
    /// これでアクセスするマネージャーが決まる
    /// </summary>
    public enum CharacterSide
    {
        プレイヤー = 1 << 0,// 味方
        魔物 = 1 << 1,// 一般的な敵
        その他 = 1 << 2// それ以外
    }



    /// <summary>
    /// 使用する攻撃のタイプ
    /// 範囲攻撃とかも入れるか？
    /// </summary>
    public enum UseAttackType
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
        弱点属性 = 1 << 7,//敵や味方の弱点属性をサーチして代わりに使う
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
    /// 特殊状態
    /// </summary>
    public enum SpecialState
    {
        ヘイト増大 = 1 << 1,
        ヘイト減少 = 1 << 2,
        なし = 1 << 0,
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
    }

    /// <summary>
    /// 送信するデータ、不変の物
    /// 大半ビットでまとめれそう
    /// 空飛ぶ騎士の敵いるかもしれないしタイプは組み合わせ可能にする
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
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
        public MoveState initialMove;

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
        public CharacterFeature type;

        /// <summary>
        /// キャラの階級。<br/>
        /// これが上なほど味方の中で同じ敵をターゲットにしててもお控えしなくて済む、優先的に殴れる。<br/>
        /// あとランク低い味方に命令飛ばしたりできる。
        /// </summary>
        [Header("チーム内での階級")]
        public CharacterRank rank;
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
        /// AIの移動判断間隔
        /// </summary>
        [Header("移動判断間隔")]
        public float moveJudgeInterval;

        /// <summary>
        /// 行動条件データ
        /// </summary>
        [Header("行動条件データ")]
        public MoveJudgeData[] moveCondition;

        /// <summary>
        /// 攻撃以外の行動条件データ.
        /// 最初の要素ほど優先度高いので重点。
        /// </summary>
        [Header("ヘイト条件データ")]
        public TargetJudgeData[] hateCondition;

        /// <summary>
        /// ヘイト条件に対応するヘイト上昇倍率
        /// </summary>
        [Header("ヘイト上昇倍率")]
        public int[] hateMultiplier;

        /// <summary>
        /// 攻撃以外の行動条件データ.
        /// 最初の要素ほど優先度高いので重点。
        /// </summary>
        [Header("行動条件データ")]
        public TargetJudgeData[] targetCondition;
    }

    /// <summary>
    /// 行動判断時に使用するデータ。
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct MoveJudgeData
    {
        /// <summary>
        /// 行動条件
        /// </summary>
        [Header("行動条件")]
        public MoveJudgeCondition moveCondition;

        /// <summary>
        /// 判断条件の対象のタイプ
        /// </summary>
        [Header("判断対象タイプ")]
        public TargetType targetType;

        /// <summary>
        /// 真の場合、条件が反転する
        /// 以上は以内になるなど
        /// </summary>
        [Header("基準反転フラグ")]
        public bool isInvert;

        /// <summary>
        /// 判断条件の行動タイプ
        /// </summary>
        [Header("判断対象タイプ")]
        public MoveState moveType;
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
        public bool isInvert;
    }

    /// <summary>
    /// モードを変える条件。
    /// AIの振る舞い部分にあたる
    /// これはまだ使わない
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
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
    /// 攻撃のステータス。
    /// 倍率とか属性とかそのへん。
    /// これはステータスに持たせておく。
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
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
    /// </summary>
    [Header("キャラAIの設定")]
    public CharacterBrainStatus brainData;

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

}
