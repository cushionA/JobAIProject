using Cysharp.Threading.Tasks;
using System.Threading;

public class AltAsyncAI : AsyncTestBase
{
    /// <summary>
    /// �L�����Z���g�[�N���B
    /// </summary>
    private CancellationToken token;

    /// <summary>
    /// ���������Ĕ񓯊����f���[�v�J�n�B
    /// </summary>
    private void Start()
    {
        // ������
        base.Initialize();

        // �ҋ@�����ɔ񓯊����[�v�J�n�B
        this.AsyncJudge().Forget();
    }

    /// <summary>
    /// �񓯊��̔��f���\�b�h�B<br></br>
    /// �w��b�����Ƃɍs�����f�����郋�[�v���񂵑�����B
    /// </summary>
    /// <returns></returns>
    private async UniTaskVoid AsyncJudge()
    {
        // �g�[�N���擾
        this.token = GameManager.instance.cToken.Token;

        // �L�����Z�������܂ő�����B
        // �g�[�N��������Ȏg�������Ă���̂́AcancellationToken.Cancel()����O�𓊂��邩��B�����Ƃ������@����΋����Ă��������B
        while ( this.token.IsCancellationRequested == false )
        {
            // �u�x�Ȃ��ň�Ԃ����H ����������
            await UniTask.WaitForSeconds(this.status.judgeInterval);

            // �s�����f
            this.MoveJudgeAct();
        }
    }

}
