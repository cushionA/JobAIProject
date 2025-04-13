using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using UnityEngine.Profiling;
using Unity.VisualScripting;

public class MTAsyncAI : AsyncTestBase
{
    /// <summary>
    /// �L�����Z���g�[�N���B
    /// </summary>
    CancellationToken token;

    ulong mtCounter = 0;

    ulong frameCounter;

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

    private void Update()
    {
        frameCounter++;
        Debug.Log($"�}���`�X���b�h��{mtCounter}��A�t���[����{frameCounter}�t���[��");
        Debug.Log($"�}���`�X���b�h��Update�̖�{mtCounter / frameCounter}�{");
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
            // �}���`�X���b�h�ł��
            // ����ȍ~�̏������X���b�h�v�[���֐؂�ւ�
            await UniTask.SwitchToThreadPool();

            // �����Ə����Ă邩�킩��܂��񂪃}���`�X���b�h����
            while ( IntervalEndJudge() == false )
            {
                mtCounter++;
            }

            // �ړ����f�����ĂԂ̂Ń��C���X���b�h�ɖ߂�
            await UniTask.SwitchToMainThread();


            // �s�����f
            MoveJudgeAct();
        }
    }

}
