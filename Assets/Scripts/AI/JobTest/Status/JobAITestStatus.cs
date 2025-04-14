using System;
using System.Runtime.InteropServices;
using UnityEngine;
using static JTestAIBase;

/// <summary>
/// �g�p��ʂɊ�Â��ăf�[�^���\���̂ɐ؂蕪���Ă���B
/// �������L���b�V�����ӎ��B
/// </summary>
[CreateAssetMenu(fileName = "JobAITestStatus", menuName = "Scriptable Objects/JobAITestStatus")]
public class JobAITestStatus : ScriptableObject
{
    #region Enum��`

    /// <summary>
    /// �������s�������肷�邽�߂̏���
    /// �����̎��A�U���E�񕜁E�x���E�����E��q�Ȃ�
    /// �Ώۂ������A�����A�G�̂ǂꂩ�Ƃ����敪�Ɣے�i�ȏオ�ȓ��ɂȂ�����j�t���O�̑g�ݍ��킹�ŕ\��
    /// �s���̃o���G�[�V�����̓��[�h�`�F���W�A�U���A�񕜂Ȃǂ̋�̓I�s��
    /// </summary>
    public enum MoveJudgeCondition
    {
        �Ώۂ���萔�̎�,
        HP����芄���̑Ώۂ����鎞,
        ��苗���ɑΏۂ����鎞,
        �C�ӂ̃^�C�v�̑Ώۂ����鎞,
        �Ώۂ��񕜂�x�����g�p������,// �񕜖��@�Ƃ��ŉ񕜂������ɑS�̃C�x���g���΂����B
        �Ώۂ���_���[�W���󂯂���,
        �Ώۂ�����̓�����ʂ̉e�����󂯂Ă��鎞,//�o�t�Ƃ��f�o�t
        �Ώۂ����S������,
        �Ώۂ��U�����ꂽ��
    }

    /// <summary>
    /// MoveJudgeCondition�̑Ώۂ̃^�C�v
    /// </summary>
    public enum TargetType
    {
        ���� = 0,
        ���� = 1,
        �G = 2
    }

    /// <summary>
    /// ���f�̌��ʑI�������s���̃^�C�v�B
    /// </summary>
    public enum MoveState
    {
        �ǐ�,
        ����,
        �U��,
        �ҋ@,// �U����̃N�[���^�C�����ȂǁB���̏�Ԃœ��삷���𗦂�ݒ肷��H
        �h��,// �����o��������ݒ�ł���悤�ɂ���H ���̏�Ŋ�{�K�[�h�����ǁA���肪�����炩���ꂽ�瓮���o���A�I��
        �x��,
        ��,
        ��q,
        �x��
    }


    /// <summary>
    /// �G�ɑ΂���w�C�g�l�̏㏸�A�����̏����B
    /// �����ɓ��Ă͂܂�G�̃w�C�g�l���㏸�����茸�������肷��B
    /// ���邢�͖����̎x���E�񕜁E��q�Ώۂ����߂�
    /// ������ے�t���O�Ƃ̑g�ݍ��킹�Ŏg��
    /// </summary>
    public enum TargetSelectCondition
    {
        ����,
        ���x,
        HP����,
        HP,
        �U����,
        �h���,
        �L�����^�C�v,// �����␔�l������int�l�Ƀ^�C�v������B
        ��_����,// �����␔�l������int�l�Ƀ^�C�v������B

    }


    /// <summary>
    /// �L�����N�^�[�̑����B
    /// �����ɓ��Ă͂܂镪�S���Ԃ����ށB
    /// </summary>
    [Flags]
    public enum CharacterFeature
    {
        �v���C���[ = 1 << 0,
        �V�X�^�[���� = 1 << 1,
        NPC = 1 << 2,
        �ʏ�G�l�~�[ = 1 << 3,
        �{�X = 1 << 4,
        ���m = 1 << 5,//���̎G��
        ��s = 1 << 6,//��Ԃ��
        �ˎ� = 1 << 7,//������
        �R�m = 1 << 8,//������
        㩌n = 1 << 9,//�҂��\���Ă���
        ���G = 1 << 10,// ���G
        �U�R = 1 << 11,
        �q�[���[ = 1 << 12,
        �T�|�[�^�[ = 1 << 13,
        ���� = 1 << 14,
        �w���� = 1 << 15,
        �w��Ȃ� = 0//�w��Ȃ�
    }


    /// <summary>
    /// �L�����N�^�[����������w�c
    /// ����ŃA�N�Z�X����}�l�[�W���[�����܂�
    /// </summary>
    public enum CharacterSide
    {
        �v���C���[ = 1 << 0,// ����
        ���� = 1 << 1,// ��ʓI�ȓG
        ���̑� = 1 << 2// ����ȊO
    }



    /// <summary>
    /// �g�p����U���̃^�C�v
    /// �͈͍U���Ƃ�������邩�H
    /// </summary>
    public enum UseAttackType
    {
        Slash,//�a��
        Stab,//�h��
        Strike//�Ō�
    }


    /// <summary>
    /// �����̗񋓌^
    /// ��Ԉُ�͏�����
    /// 
    /// 
    /// </summary>
    [Flags]
    public enum Element
    {
        �a������ = 1 << 0,
        �h�ˑ��� = 1 << 1,
        �Ō����� = 1 << 2,
        ������ = 1 << 3,
        �ő��� = 1 << 4,
        ������ = 1 << 5,
        ������ = 1 << 6,
        ��_���� = 1 << 7,//�G�□���̎�_�������T�[�`���đ���Ɏg��
        �w��Ȃ� = 0
    }

    /// <summary>
    /// �L�����̃��x��
    /// ���̃��x���������Ƒ��̓G�Ɏז�����Ȃ�
    /// </summary>
    public enum CharacterRank
    {
        �U�R,//�G��
        ��͋�,//��{�͂���
        �w����,//�����u
        �{�X//�{�X����
    }

    /// <summary>
    /// �s�����g�p�\�ȃ��[�h
    /// �܂܂�
    /// ������r�b�g���Z�ł��H�@�����I�ׂ��H
    /// ���[�h1��2�Ȃ�c�݂�����
    /// </summary>
    [Flags]
    public enum Mode
    {
        Mode1 = 1 << 0,
        Mode2 = 1 << 1,
        Mode3 = 1 << 2,
        Mode4 = 1 << 3,
        Mode5 = 1 << 4,
        AllMode = 1 << 5
    }

    /// <summary>
    /// ������
    /// </summary>
    public enum SpecialState
    {
        �w�C�g���� = 1 << 1,
        �w�C�g���� = 1 << 2,
        �Ȃ� = 1 << 0,
    }

    #endregion Enum��`

    #region �\���̒�`

    /// <summary>
    /// �e�����ɑ΂���U���͂܂��͖h��͂̒l��ێ�����\����
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ElementalStats
    {
        /// <summary>
        /// �a�������̒l
        /// </summary>
        [Header("�a������")]
        public int slash;

        /// <summary>
        /// �h�ˑ����̒l
        /// </summary>
        [Header("�h�ˑ���")]
        public int pierce;

        /// <summary>
        /// �Ō������̒l
        /// </summary>
        [Header("�Ō�����")]
        public int strike;

        /// <summary>
        /// �������̒l
        /// </summary>
        [Header("������")]
        public int fire;

        /// <summary>
        /// �������̒l
        /// </summary>
        [Header("������")]
        public int lightning;

        /// <summary>
        /// �������̒l
        /// </summary>
        [Header("������")]
        public int light;

        /// <summary>
        /// �ő����̒l
        /// </summary>
        [Header("�ő���")]
        public int dark;
    }

    /// <summary>
    /// ���M����f�[�^�A�s�ς̕�
    /// �唼�r�b�g�ł܂Ƃ߂ꂻ��
    /// ���ԋR�m�̓G���邩������Ȃ����^�C�v�͑g�ݍ��킹�\�ɂ���
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CharacterBaseData
    {

        /// <summary>
        /// �ő�HP
        /// </summary>
        [Header("HP")]
        public int hp;

        /// <summary>
        /// �ő�MP
        /// </summary>
        [Header("MP")]
        public int mp;

        /// <summary>
        /// �e�����̊�b�U����
        /// </summary>
        [Header("��b�����U����")]
        public ElementalStats baseAtk;

        /// <summary>
        /// �e�����̊�b�h���
        /// </summary>
        [Header("��b�����h���")]
        public ElementalStats baseDef;

        /// <summary>
        /// �L�����̏�����ԁB
        /// </summary>
        public MoveState initialMove;

        /// <summary>
        /// �f�t�H���g�̃L�����N�^�[�̏���
        /// </summary>
        public CharacterSide initialBelong;
    }

    /// <summary>
    /// ��ɕς��Ȃ��f�[�^���i�[����\���́B
    /// BaseData�Ƃ̈Ⴂ�́A�������ȍ~�p�ɂɎQ�Ƃ���K�v�����邩�B
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct SolidData
    {

        /// <summary>
        /// �O���\���p�̍U���́B
        /// </summary>
        [Header("�\���p�U����")]
        public int displayAtk;

        /// <summary>
        /// �O���\���p�̖h��́B
        /// </summary>
        [Header("�\���p�h���")]
        public int displayDef;

        /// <summary>
        /// �U�������������񋓌^
        /// �r�b�g���Z�Ō���
        /// NPC���������
        /// �Ȃɑ����̍U�������Ă��邩�Ƃ����Ƃ���
        /// </summary>
        [Header("�U������")]
        public Element attackElement;

        /// <summary>
        /// ��_�����������񋓌^
        /// �r�b�g���Z�Ō���
        /// NPC���������
        /// </summary>
        [Header("��_����")]
        public Element weakPoint;

        /// <summary>
        /// �L�����̑����Ƃ�����
        /// �����������B��ނ��
        /// </summary>
        [Header("�L�����N�^�[����")]
        public CharacterFeature type;

        /// <summary>
        /// �L�����̊K���B<br/>
        /// ���ꂪ��Ȃقǖ����̒��œ����G���^�[�Q�b�g�ɂ��ĂĂ����T�����Ȃ��čςށA�D��I�ɉ����B<br/>
        /// ���ƃ����N�Ⴂ�����ɖ��ߔ�΂�����ł���B
        /// </summary>
        [Header("�`�[�����ł̊K��")]
        public CharacterRank rank;
    }

    /// <summary>
    /// AI�̐ݒ�B
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CharacterBrainStatus
    {
        /// <summary>
        /// AI�̔��f�Ԋu
        /// </summary>
        [Header("���f�Ԋu")]
        public float judgeInterval;

        /// <summary>
        /// AI�̈ړ����f�Ԋu
        /// </summary>
        [Header("�ړ����f�Ԋu")]
        public float moveJudgeInterval;

        /// <summary>
        /// �s�������f�[�^
        /// </summary>
        [Header("�s�������f�[�^")]
        public MoveJudgeData[] moveCondition;

        /// <summary>
        /// �U���ȊO�̍s�������f�[�^.
        /// �ŏ��̗v�f�قǗD��x�����̂ŏd�_�B
        /// </summary>
        [Header("�w�C�g�����f�[�^")]
        public TargetJudgeData[] hateCondition;

        /// <summary>
        /// �w�C�g�����ɑΉ�����w�C�g�㏸�{��
        /// </summary>
        [Header("�w�C�g�㏸�{��")]
        public int[] hateMultiplier;

        /// <summary>
        /// �U���ȊO�̍s�������f�[�^.
        /// �ŏ��̗v�f�قǗD��x�����̂ŏd�_�B
        /// </summary>
        [Header("�s�������f�[�^")]
        public TargetJudgeData[] targetCondition;
    }

    /// <summary>
    /// �s�����f���Ɏg�p����f�[�^�B
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct MoveJudgeData
    {
        /// <summary>
        /// �s������
        /// </summary>
        [Header("�s������")]
        public MoveJudgeCondition moveCondition;

        /// <summary>
        /// ���f�����̑Ώۂ̃^�C�v
        /// </summary>
        [Header("���f�Ώۃ^�C�v")]
        public TargetType targetType;

        /// <summary>
        /// �^�̏ꍇ�A���������]����
        /// �ȏ�͈ȓ��ɂȂ�Ȃ�
        /// </summary>
        [Header("����]�t���O")]
        public bool isInvert;

        /// <summary>
        /// ���f�����̍s���^�C�v
        /// </summary>
        [Header("���f�Ώۃ^�C�v")]
        public MoveState moveType;
    }

    /// <summary>
    /// �s�����f��A�s���̃^�[�Q�b�g��I������ۂɎg�p����f�[�^�B
    /// �w�C�g�ł�����ȊO�ł��\���͓̂���
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct TargetJudgeData
    {
        /// <summary>
        /// �^�[�Q�b�g�̔��f��B
        /// </summary>
        [Header("�^�[�Q�b�g���f�")]
        public TargetSelectCondition judgeCondition;

        /// <summary>
        /// �^�̏ꍇ�A���������]����
        /// �ȏ�͈ȓ��ɂȂ�Ȃ�
        /// </summary>
        [Header("����]�t���O")]
        public bool isInvert;
    }

    /// <summary>
    /// ���[�h��ς�������B
    /// AI�̐U�镑�������ɂ�����
    /// ����͂܂��g��Ȃ�
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ModeBehavior
    {
        /// <summary>
        /// �A�^�b�N�X�g�b�v�t���O���^�̎�����
        /// ���x���Ƃ��֌W�Ȃ�����
        /// </summary>
        [Header("�U���}�����[�h��")]
        public bool isAttackStop;

        /// <summary>
        /// ���݂̃��[�h
        /// �����I���\
        /// </summary>
        [Header("�J�ڌ��̃��[�h")]
        public Mode nowMode;

        /// <summary>
        /// ���[�h�`�F���W����̗͊���
        /// 0�Ȃ疳��
        /// </summary>
        [Header("���[�h�ύX����̗͔�B0�Ŗ���")]
        public int healthRatio;

        /// <summary>
        /// �O��̃��[�h�`�F���W���牽�b�ŕω����邩
        /// 0�Ȃ疳��
        /// </summary>
        [Header("���[�h�ύX����")]
        public int changeTime;

        /// <summary>
        /// x����y�̋����ł��̃��[�h��
        /// �܂蒼������X���[�g������y���[�g���͈̔͂��Ă��Ƃ�
        /// ��������
        /// 00�Ȃ疳��
        /// </summary>
        [Header("���[�h�ύX�����i00�Ŗ����j")]
        public Vector2 changeDistance;

        /// <summary>
        /// �ς����̃��[�h
        /// All�Ȃ烉���_���ɕς��
        /// �ԍ����Ƃ��̔z�񐔂��烂�[�h�̐�������o��
        /// </summary>
        [Header("�ύX��̃��[�h")]
        public Mode changeMode;

        /// <summary>
        /// ���̏����̃`�F���W�̗D��x
        /// 0�͊�{���[�h�ɂ̂ݎg��
        /// �����{���[�h�̓}�C�i�X1�ł�������
        /// �ǂ�����ł��߂��悤�ɏ����͌y��
        /// </summary>
        [Header("�`�F���W�̗D��x�B5�̎��Œ�")]
        public int modeLevel;
    }

    /// <summary>
    /// �U���̃X�e�[�^�X�B
    /// �{���Ƃ������Ƃ����̂ւ�B
    /// ����̓X�e�[�^�X�Ɏ������Ă����B
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct AttackData
    {
        /// <summary>
        /// �U���{���B
        /// �����郂�[�V�����l
        /// </summary>
        [Header("�U���{���i���[�V�����l�j")]
        public float motionValue;
    }

    /// <summary>
    /// �L�����̍s���X�e�[�^�X�B
    /// �ړ����x�ȂǁB
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct MoveStatus
    {
        /// <summary>
        /// �ʏ�̈ړ����x
        /// </summary>
        [Header("�ʏ�ړ����x")]
        public int moveSpeed;

        /// <summary>
        /// ���s���x�B������������
        /// </summary>
        [Header("���s���x")]
        public int walkSpeed;

        /// <summary>
        /// �_�b�V�����x
        /// </summary>
        [Header("�_�b�V�����x")]
        public int dashSpeed;

        /// <summary>
        /// �W�����v�̍����B
        /// </summary>
        [Header("�W�����v�̍���")]
        public int jumpHeight;
    }

    #endregion �\���̒�`

    /// <summary>
    /// �L�����̃x�[�X�A�Œ蕔���̃f�[�^�B
    /// ����͒��ڎd�l�͂����A�R�s�[���Ċe�L�����ɓn���Ă�����B
    /// </summary>
    [Header("�L�����N�^�[�̊�{�f�[�^")]
    public CharacterBaseData baseData;

    /// <summary>
    /// �Œ�̃f�[�^�B
    /// </summary>
    [Header("�Œ�f�[�^")]
    public SolidData solidData;

    /// <summary>
    /// �L������AI�̐ݒ�B
    /// </summary>
    [Header("�L����AI�̐ݒ�")]
    public CharacterBrainStatus brainData;

    /// <summary>
    /// �ړ����x�Ȃǂ̃f�[�^
    /// </summary>
    [Header("�ړ��X�e�[�^�X")]
    public MoveStatus moveStatus;

    /// <summary>
    /// �e�U���̍U���͂Ȃǂ̐ݒ�B
    /// </summary>
    [Header("�U���f�[�^�ꗗ")]
    public AttackData[] attackData;

}
