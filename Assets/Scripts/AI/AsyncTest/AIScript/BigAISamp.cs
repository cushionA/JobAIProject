using UnityEngine;

public class BigAISamp : MonoBehaviour
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

    #endregion �C���X�^���X�ϐ�

    #region ����

    /// <summary>
    /// ���C�����[�v
    /// </summary>
    void Update()
    {

        // �G�̒�����^�[�Q�b�g������B
        // �����ƂȂ�G�̌��z��̓V���O���g�����玝���Ă���z��B(�����͂������Ă���j
        // TargetData target = Judge(GManager.instance.EnemyArray);

        //// ���f��ɍU���Ώۂ�����΍U������B
        //if ( target != null )
        //{
        //    Attack();
        //}
    }

    /// <summary>
    /// �U�������ɓ��Ă͂܂����^�[�Q�b�g�̃f�[�^��Ԃ������B
    /// </summary>
    /// <param name="tData">�^�[�Q�b�g���̃��X�g</param>
    /// <returns>����̔��f�̌��ʁA�U���ΏۂɂȂ����^�[�Q�b�g�B�Ώۂ����Ȃ���� null</returns>
    TargetData Judge(TargetData[] tData)
    {
        //for ( int i = 0; i < tData.Length; i++ )
        //{
        //    if ( useCondition == JudgeConditions.��苗�����ɂ��邩�Ŕ��� )
        //    {
        //        // �����̔���������āA���������̋����Ȃ炻�����^�[�Q�b�g�ɁB
        //        return tData[i];
        //    }
        //    else if ( useCondition == JudgeConditions.HP�ʂŔ��� )
        //    {
        //        // HP�̔���������āA�������ȏ�Ȃ炻�����^�[�Q�b�g�ɁB
        //        return tData[i];
        //    }
        //    else if ( useCondition == JudgeConditions.MP�ʂŔ��� )
        //    {
        //        // MP�̔���������āA�������ȏ�Ȃ炻�����^�[�Q�b�g�ɁB
        //        return tData[i];
        //    }
        //    else
        //    {
        //        // �U���͂̔���������āA�������ȏ�Ȃ炻�����^�[�Q�b�g�ɁB
        //        return tData[i];
        //    }
        //}
        // ���Ȃ���� null�B
        return null;
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
