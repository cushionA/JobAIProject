public class SyncAI : AsyncTestBase
{

    private void Start()
    {
        base.Initialize();
    }

    /// <summary>
    /// ���[�v���Ŕ��f���s���B
    /// </summary>
    private void Update()
    {
        // ���f���p�X�����Ȃ�ړ����f������B
        if ( this.IntervalEndJudge() )
        {
            base.MoveJudgeAct();
        }
    }
}
