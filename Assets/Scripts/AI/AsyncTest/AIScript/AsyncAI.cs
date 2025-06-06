using Cysharp.Threading.Tasks;
using System.Threading;

public class AsyncAI : AsyncTestBase
{
    /// <summary>
    /// キャンセルトークン。
    /// </summary>
    private CancellationToken token;

    /// <summary>
    /// 初期化して非同期判断ループ開始。
    /// </summary>
    private void Start()
    {
        // 初期化
        base.Initialize();

        // 待機せずに非同期ループ開始。
        this.AsyncJudge().Forget();
    }

    /// <summary>
    /// 非同期の判断メソッド。<br></br>
    /// 指定秒数ごとに行動判断をするループを回し続ける。
    /// </summary>
    /// <returns></returns>
    private async UniTaskVoid AsyncJudge()
    {
        // トークン取得
        this.token = GameManager.instance.cToken.Token;

        // キャンセルされるまで続ける。
        // トークンをこんな使い方しているのは、トークンを渡すとcancellationToken.Cancel()が例外を投げるから。
        // ＝ 例外処理をしたくないから。
        // もっといい方法があれば教えてください。
        while ( this.token.IsCancellationRequested == false )
        {
            // 判断間隔条件が真になるまで待つ。
            // 公平を期するために同じ判断処理を使う。
            await UniTask.WaitUntil(() => this.IntervalEndJudge());

            // 行動判断
            this.MoveJudgeAct();
        }
    }

}
