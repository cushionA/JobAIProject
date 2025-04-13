using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using UnityEngine.Profiling;
using Unity.VisualScripting;

public class MTAsyncAI : AsyncTestBase
{
    /// <summary>
    /// キャンセルトークン。
    /// </summary>
    CancellationToken token;

    ulong mtCounter = 0;

    ulong frameCounter;

    /// <summary>
    /// 初期化して非同期判断ループ開始。
    /// </summary>
    void Start()
    {
        // 初期化
        base.Initialize();

        // 待機せずに非同期ループ開始。
        AsyncJudge().Forget();
    }

    private void Update()
    {
        frameCounter++;
        Debug.Log($"マルチスレッドは{mtCounter}回、フレームは{frameCounter}フレーム");
        Debug.Log($"マルチスレッドはUpdateの約{mtCounter / frameCounter}倍");
    }

    /// <summary>
    /// 非同期の判断メソッド。<br></br>
    /// 指定秒数ごとに行動判断をするループを回し続ける。
    /// </summary>
    /// <returns></returns>
    async UniTaskVoid AsyncJudge()
    {
        // トークン取得
        token = GameManager.instance.cToken.Token;

        // キャンセルされるまで続ける。
        // トークンをこんな使い方しているのは、cancellationToken.Cancel()が例外を投げるから。もっといい方法あれば教えてください。
        while ( token.IsCancellationRequested == false )
        {
            // マルチスレッドでやる
            // これ以降の処理をスレッドプールへ切り替え
            await UniTask.SwitchToThreadPool();

            // ちゃんと書けてるかわかりませんがマルチスレッド処理
            while ( IntervalEndJudge() == false )
            {
                mtCounter++;
            }

            // 移動判断処理呼ぶのでメインスレッドに戻す
            await UniTask.SwitchToMainThread();


            // 行動判断
            MoveJudgeAct();
        }
    }

}
