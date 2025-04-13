using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

public class AltAsyncAI : AsyncTestBase
{
    /// <summary>
    /// �L�����Z���g�[�N���B
    /// </summary>
    CancellationToken token;

    /// <summary>
    /// ���������Ĕ񓯊����f���[�v�J�n�B
    /// </summary>
    void Start()
    {
        // ������
        base.Initialize();

        // �ҋ@�����ɔ񓯊����[�v�J�n�B
        AsyncJudge().Forget();
    }

    /// <summary>
    /// �񓯊��̔��f���\�b�h�B<br></br>
    /// �w��b�����Ƃɍs�����f�����郋�[�v���񂵑�����B
    /// </summary>
    /// <returns></returns>
    async UniTaskVoid AsyncJudge()
    {
        // �g�[�N���擾
        token = GameManager.instance.cToken.Token;

        // �L�����Z�������܂ő�����B
        // �g�[�N��������Ȏg�������Ă���̂́AcancellationToken.Cancel()����O�𓊂��邩��B�����Ƃ������@����΋����Ă��������B
        while ( token.IsCancellationRequested == false )
        {
            // �u�x�Ȃ��ň�Ԃ����H ����������
            await UniTask.WaitForSeconds(status.judgeInterval);

            // �s�����f
            MoveJudgeAct();
        }
    }

}
