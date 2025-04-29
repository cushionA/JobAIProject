public class SyncAI : AsyncTestBase
{

    private void Start()
    {
        base.Initialize();
    }

    /// <summary>
    /// ループ内で判断を行う。
    /// </summary>
    private void Update()
    {
        // 判断をパスしたなら移動判断をする。
        if ( this.IntervalEndJudge() )
        {
            base.MoveJudgeAct();
        }
    }
}
