using System;
using UnityEngine;

public class DelegateAISamp : MonoBehaviour
{
    #region ��`
    /// <summary>
    /// �U������ۂ̏������f�̊�ƂȂ�ݒ�̗񋓌^�B
    /// </summary>
    enum JudgeConditions
    {
        ��苗�����ɂ��邩�Ŕ��� = 0,// ���ȓ��̋����ɂ���G���U��
        HP�ʂŔ��� = 1,// HP�����ȏォ
        MP�ʂŔ��� = 2,// MP�����ȏォ
        �U���͂Ŕ��� = 3 // �U���͂����ȏォ 
    }

    /// <summary>
    /// �ʒu��HP�ȂǁA�G�̃f�[�^���i�[����N���X�B<br></br>
    /// ����͒��g���Ȃ��_�~�[�B
    /// </summary>
    public class TargetData
    {

    }
    #endregion ��`

    #region �C���X�^���X�ϐ�
    /// <summary>
    /// ����AI���ǂ�ȏ����ōU�����s�����A�Ƃ����ݒ�B<br></br>
    /// �C���X�y�N�^��������z��B<br></br>
    /// ���Ƃ��΂��̕ϐ��̒l�� ��苗�����ɂ��邩�Ŕ��� �ł���΋��������ȓ��̂��̂��U������B
    /// </summary>
    [SerializeField]
    JudgeConditions useCondition;

    /// <summary>
    /// �f���Q�[�g�̔z��B<br></br>
    /// �����ɔ��f�������i�[���AJudgeConditions�񋓎q�̒l�ŃA�N�Z�X�ł���悤�ɂ���B
    /// </summary>
    Func<TargetData[], TargetData>[] judgeArray = new Func<TargetData[], TargetData>[4];

    #endregion �C���X�^���X�ϐ�

    #region ����

    /// <summary>
    /// ����������<br></br>
    /// �f���Q�[�g�z����쐬�B
    /// </summary>
    private void Start()
    {

        // �e�����ɑΉ����郍�[�J�����\�b�h���쐬���A�f���Q�[�g�z��Ɋi�[����B

        // ��������p�̃��[�J�����\�b�h�B
        // JudgeConditions.��苗�����ɂ��邩�Ŕ��� �ɑΉ��i�������e�̓_�~�[�j
        TargetData distanceJudge(TargetData[] tData)
        { return tData[0]; }

        // HP����p�̃��[�J�����\�b�h�B
        // JudgeConditions.HP�ʂŔ��� �ɑΉ��i�������e�̓_�~�[�j
        TargetData hpJudge(TargetData[] tData)
        { return tData[0]; }

        // MP����p�̃��[�J�����\�b�h�B
        // JudgeConditions.MP�ʂŔ��� �ɑΉ��i�������e�̓_�~�[�j
        TargetData mpJudge(TargetData[] tData)
        { return tData[0]; }

        // �U���͔���p�̃��[�J�����\�b�h�B
        // JudgeConditions.�U���͂Ŕ��� �ɑΉ��i�������e�̓_�~�[�j
        TargetData atkJudge(TargetData[] tData)
        { return tData[0]; }

        // �e������z��Ɋi�[�B
        judgeArray[(int)JudgeConditions.��苗�����ɂ��邩�Ŕ���] = distanceJudge;
        judgeArray[(int)JudgeConditions.HP�ʂŔ���] = hpJudge;
        judgeArray[(int)JudgeConditions.MP�ʂŔ���] = mpJudge;
        judgeArray[(int)JudgeConditions.�U���͂Ŕ���] = atkJudge;
    }

    /// <summary>
    /// ���C�����[�v
    /// </summary>
    void Update()
    {

        // �g�p����ݒ�Ŕz��̂ǂ̗v�f�ɃA�N�Z�X���邩�A�����肷��B
        TargetData target = judgeArray[(int)useCondition](new TargetData[10]);

        // ���f��ɍU���Ώۂ�����΍U������B
        if ( target != null )
        {
            Attack(target);
        }
    }

    /// <summary>
    /// �U�����鏈���B
    /// </summary>
    /// <param name="target">�U���^�[�Q�b�g</param>
    void Attack(TargetData target)
    {

    }
    #endregion ����
}
