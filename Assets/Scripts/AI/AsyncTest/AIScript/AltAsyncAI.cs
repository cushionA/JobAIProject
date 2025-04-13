using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

public class AltAsyncAI : AsyncTestBase
{
    /// <summary>
    /// キャンセルトークン。
    /// </summary>
    CancellationToken token;

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
            // 忖度なしで一番いい？ 処理をつかう
            await UniTask.WaitForSeconds(status.judgeInterval);

            // 行動判断
            MoveJudgeAct();
        }
    }

}
