using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public class MTAsyncAI : AsyncTestBase
{
    /// <summary>
    /// キャンセルトークン。
    /// </summary>
    private CancellationToken token;

    private ulong mtCounter = 0;

    private ulong frameCounter;

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

    private void Update()
    {
        this.frameCounter++;
        Debug.Log($"マルチスレッドは{this.mtCounter}回、フレームは{this.frameCounter}フレーム");
        Debug.Log($"マルチスレッドはUpdateの約{this.mtCounter / this.frameCounter}倍");
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
        // トークンをこんな使い方しているのは、cancellationToken.Cancel()が例外を投げるから。もっといい方法あれば教えてください。
        while ( this.token.IsCancellationRequested == false )
        {
            // マルチスレッドでやる
            // これ以降の処理をスレッドプールへ切り替え
            await UniTask.SwitchToThreadPool();

            // ちゃんと書けてるかわかりませんがマルチスレッド処理
            while ( this.IntervalEndJudge() == false )
            {
                this.mtCounter++;
            }

            // 移動判断処理呼ぶのでメインスレッドに戻す
            await UniTask.SwitchToMainThread();

            // 行動判断
            this.MoveJudgeAct();
        }
    }

}
