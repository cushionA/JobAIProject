using UnityEngine;
using Cysharp.Threading.Tasks;

public class AsyncTestBase : MonoBehaviour
{
    /// �e�X�g�Ŏg�p����X�e�[�^�X�B<br></br>
    /// ���f�Ԋu�̃f�[�^�������Ă���B<br></br>
    /// �C���X�y�N�^����ݒ�B
    /// </summary>
    [SerializeField]
    protected AsyncTestStatus status;

    /// <summary>
    /// �ړ��Ɏg�p���镨���R���|�[�l���g�B
    /// </summary>
    [SerializeField]
    Rigidbody2D rb;

    /// <summary>
    /// ���񔻒f�������𐔂���B<br></br>
    /// �񓯊��Ɠ����ŁA���҂��锻�f�񐔂Ƃ̊Ԃ̌덷���قȂ邩������B<br></br>
    /// �ŏ��̍s���̕�����1�����������l�ɁB
    /// </summary>
    [HideInInspector]
    public long judgeCount = -1;

    /// <summary>
    /// ���f�Ԋu��Ԃ��v���p�e�B�B
    /// </summary>
    public float JudgeInterval { get { return status.judgeInterval; } }

    /// <summary>
    /// �Ō�ɔ��f�������̎��ԁB<br></br>
    /// ����ƍ��̎��Ԃ̍��Ŏ��Ԍo�߂��͂���B
    /// </summary>
    float lastJudge = -10000;

    /// <summary>
    /// ���݂̈ړ������B<br></br>
    /// �P��-1�ŕ\���B
    /// </summary>
    private int moveDirection = 1;

    /// <summary>
    /// �����������B
    /// </summary>
    protected void Initialize()
    {

        //Debug.Log("�e�X�g�ҋ@");

        while ( true )
        {
            if ( GameManager.instance.isTest == true )
            {
                break;
            }
        }

        //Debug.Log("�e�X�g�J�n");

        MoveJudgeAct();
    }

    /// <summary>
    /// ���f�Ԋu�̑ҋ@�������������𔻒肷�郁�\�b�h�B<br></br>
    /// �񓯊��A�����ԂŌ������������߂ɍŏ��͓����������g�����B
    /// </summary>
    /// <returns></returns>
    protected bool IntervalEndJudge()
    {
        // �e�X�g�p�����ŁA���O��̔��肩�画�f�Ԋu�ȏ�̎��Ԃ��o�߂����ꍇ�ɐ^��Ԃ��B
        return GameManager.instance.isTest && ((GameManager.instance.NowTime - lastJudge) > status.judgeInterval);
    }

    /// <summary>
    /// �s���𔻒f���郁�\�b�h�B
    /// </summary>
    protected void MoveJudgeAct()
    {
        // 50%�̊m���ō��E�ړ��̕������ς��B
        moveDirection = (UnityEngine.Random.Range(0, 100) >= 50) ? 1 : -1;

        rb.linearVelocityX = moveDirection * status.xSpeed;

        //Debug.Log($"���l�F{moveDirection * status.xSpeed} ���x�F{rb.linearVelocityX}");

        lastJudge = GameManager.instance.NowTime;
        judgeCount++;
    }


}
