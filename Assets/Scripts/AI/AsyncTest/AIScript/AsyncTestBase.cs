using UnityEngine;

public class AsyncTestBase : MonoBehaviour
{
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
    private Rigidbody2D rb;

    /// <summary>
    /// 何回判断したかを数える。<br></br>
    /// 非同期と同期で、期待する判断回数との間の誤差が異なるかを見る。<br></br>
    /// 最初の行動の分だけ1引いた初期値に。
    /// </summary>
    [HideInInspector]
    public long judgeCount = -1;

    /// <summary>
    /// 判断間隔を返すプロパティ。
    /// </summary>
    public float JudgeInterval => this.status.judgeInterval;

    /// <summary>
    /// 最後に判断した時の時間。<br></br>
    /// これと今の時間の差で時間経過をはかる。
    /// </summary>
    private float lastJudge = -10000;

    /// <summary>
    /// 現在の移動方向。<br></br>
    /// １か-1で表現。
    /// </summary>
    private int moveDirection = 1;

    /// <summary>
    /// 初期化処理。
    /// </summary>
    protected void Initialize()
    {

        //Debug.Log("テスト待機");

        while ( true )
        {
            if ( GameManager.instance.isTest == true )
            {
                break;
            }
        }

        //Debug.Log("テスト開始");

        this.MoveJudgeAct();
    }

    /// <summary>
    /// 判断間隔の待機が完了したかを判定するメソッド。<br></br>
    /// 非同期、同期間で公平を期すために最初は同じ処理を使おう。
    /// </summary>
    /// <returns></returns>
    protected bool IntervalEndJudge()
    {
        // テスト継続中で、かつ前回の判定から判断間隔以上の時間が経過した場合に真を返す。
        return GameManager.instance.isTest && ((GameManager.instance.NowTime - this.lastJudge) > this.status.judgeInterval);
    }

    /// <summary>
    /// 行動を判断するメソッド。
    /// </summary>
    protected void MoveJudgeAct()
    {
        // 50%の確率で左右移動の方向が変わる。
        this.moveDirection = (UnityEngine.Random.Range(0, 100) >= 50) ? 1 : -1;

        this.rb.linearVelocityX = this.moveDirection * this.status.xSpeed;

        //Debug.Log($"数値：{moveDirection * status.xSpeed} 速度：{rb.linearVelocityX}");

        this.lastJudge = GameManager.instance.NowTime;
        this.judgeCount++;
    }

}
