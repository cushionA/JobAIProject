using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public class MTAsyncAI : AsyncTestBase
{
    /// <summary>
    /// �L�����Z���g�[�N���B
    /// </summary>
    private CancellationToken token;

    private ulong mtCounter = 0;

    private ulong frameCounter;

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

    private void Update()
    {
        this.frameCounter++;
        Debug.Log($"�}���`�X���b�h��{this.mtCounter}��A�t���[����{this.frameCounter}�t���[��");
        Debug.Log($"�}���`�X���b�h��Update�̖�{this.mtCounter / this.frameCounter}�{");
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
            // �}���`�X���b�h�ł��
            // ����ȍ~�̏������X���b�h�v�[���֐؂�ւ�
            await UniTask.SwitchToThreadPool();

            // �����Ə����Ă邩�킩��܂��񂪃}���`�X���b�h����
            while ( this.IntervalEndJudge() == false )
            {
                this.mtCounter++;
            }

            // �ړ����f�����ĂԂ̂Ń��C���X���b�h�ɖ߂�
            await UniTask.SwitchToMainThread();

            // �s�����f
            this.MoveJudgeAct();
        }
    }

}
