using Cysharp.Threading.Tasks;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static JobAITestStatus;
using static JTestAIBase;

/// <summary>
/// 次のフレームで全体に適用するイベントリスト、みたいなのを持って、そこにキャラクターがデータを渡せるようにするか
/// Dispose必須
/// </summary>
public class CombatManager : MonoBehaviour, IDisposable
{

    #region 定義

    /// <summary>
    /// AIのイベントのタイプ。
    /// </summary>
    public enum AIEventType
    {
        大ダメージを与えた,
        回復や支援を使用した,
        キャラが死亡した,
        指揮官が倒された,
        攻撃対象指定,// 指揮官による命令
    }

    /// <summary>
    /// AIのイベントの送信先。
    /// ここに渡せばJobシステムで処理してくれる。
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct AIEventContainer
    {

        /// <summary>
        /// イベントのタイプ
        /// </summary>
        public AIEventType type;

        /// <summary>
        /// 敵を倒したり、攻撃命令の指定になったりする、ヘイト編集先のハッシュ
        /// イベントの種類によって
        /// </summary>
        public int targetHash;

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
    public CharacterDataDictionary<CharacterData, JTestAIBase>[] charaDataDictionary = new CharacterDataDictionary<CharacterData, JTestAIBase>[3];
    /// <summary>
    /// ↓こんな感じで
    /// </summary>
    NativeArray<UnsafeList<CharacterData>> team = new NativeArray<UnsafeList<CharacterData>>(3, Allocator.Persistent);

    /// <summary>
    /// プレイヤー、敵、その他、それぞれが敵対している陣営をビットで表現。
    /// </summary>
    public static readonly int[] relationMap = new int[3];

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
    /// </summary>
    public UnsafeList<AIEventContainer> eventContainer = new UnsafeList<AIEventContainer>(7, Allocator.Persistent);

    // 

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
        charaDataDictionary[teamNum].AddByHash(hashCode, new CharacterData(status, addObject));

        for ( int i = 0; i < teamHate.Length; i++ )
        {
            if ( teamNum == i )
            {
                continue;
            }

            // 敵対チェック
            if ( HostileCheck(i, teamNum) )
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
    public void CharacterRemove(int hashCode, CharacterSide team)
    {
        int teamNum = (int)team;

        // 削除の前に値を処理する。
        charaDataDictionary[teamNum][hashCode].Dispose();

        // キャラデータを削除し、敵対する陣営のヘイトリストからも消す。
        charaDataDictionary[teamNum].RemoveByHash(hashCode);

        for ( int i = 0; i < teamHate.Length; i++ )
        {
            // 含むかをチェック
            if ( teamHate[i].ContainsKey(hashCode) )
            {
                teamHate[i].Remove(hashCode);
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
    private bool HostileCheck(int team1, int team2)
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

        Destroy(instance);
    }
}
