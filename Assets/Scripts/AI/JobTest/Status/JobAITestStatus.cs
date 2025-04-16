using System;
using System.Runtime.CompilerServices;
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
    public enum ActJudgeCondition
    {
        �w��̃w�C�g�l�̓G�����鎞,
        �Ώۂ���萔�̎�,
        HP����芄���̑Ώۂ����鎞,
        �ݒ苗���ɑΏۂ����鎞, �@//�����n�̏����͕ʂ̂����Ŏ��O�ɃL���b�V�����s���BAI�̐ݒ�͈̔͂����Z���T�[�Œ��ׂ���@���Ƃ�
        �C�ӂ̃^�C�v�̑Ώۂ����鎞,
        �Ώۂ��񕜂�x�����g�p������,// �񕜖��@�Ƃ��ŉ񕜂������ɑS�̃C�x���g���΂����B
        �Ώۂ���_���[�W���󂯂���,
        �Ώۂ�����̓�����ʂ̉e�����󂯂Ă��鎞,//�o�t�Ƃ��f�o�t
        �Ώۂ����S������,
        �Ώۂ��U�����ꂽ��,
        ����̑����ōU������Ώۂ����鎞,
        ����̐��̓G�ɑ_���Ă��鎞,// �w�c�t�B���^�����O�͗L��
        �����Ȃ� // �������Ă͂܂�Ȃ��������̕⌇�����B
    }

    /// <summary>
    /// �s�����f������O��
    /// ������MP��HP�̊����Ȃǂ́A�����Ɋւ���O������𔻒f���邽�߂̐ݒ�
    /// </summary>
    public enum SkipJudgeCondition
    {
        ������HP����芄���̎�,
        ������MP����芄���̎�,
        �����Ȃ� // �������Ă͂܂�Ȃ��������̕⌇�����B
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
    /// 
    /// </summary>
    public enum ActState
    {
        �w��Ȃ� = 0,// �X�e�[�g�ύX���f�Ŏg���B����ȊO�������X�e�[�^�X�ɂ���ƃX�e�[�g�ύX����B
        �ǐ� = 1,
        ���� = 2,
        �U�� = 3,
        �ҋ@ = 4,// �U����̃N�[���^�C�����ȂǁB���̏�Ԃœ��삷���𗦂�ݒ肷��H
        �h�� = 5,// �����o��������ݒ�ł���悤�ɂ���H ���̏�Ŋ�{�K�[�h�����ǁA���肪�����炩���ꂽ�瓮���o���A�I��
        �x�� = 6,
        �� = 7,
        ��q = 8,
        //���� = 9// �߂��Ⴍ����_���Ă鑊��ɑ΂��āA�U�����邩�ȁ[���Ė����Ă�B
        // ���̏�Ԃ͂��񂾂�^�[�Q�b�g�ւ̃w�C�g������B�U�����������U���c���J��Ԃ��B
        // �w�C�g���֌W�Ȃ������Ń^�[�Q�b�g�ɂ����ꍇ���U���p�x�͗�����B
        // �ҋ@�ł��������

    }



    /// <summary>
    /// �G�ɑ΂���w�C�g�l�̏㏸�A�����̏����B
    /// �����ɓ��Ă͂܂�G�̃w�C�g�l���㏸�����茸�������肷��B
    /// ���邢�͖����̎x���E�񕜁E��q�Ώۂ����߂�
    /// ������ے�t���O�Ƃ̑g�ݍ��킹�Ŏg��
    /// </summary>
    public enum TargetSelectCondition
    {
        ����,// �����̋�����OK
        ���x,
        HP����,
        HP,
        �U����,
        �h���,
        �L�����^�C�v,// �����␔�l������int�l�Ƀ^�C�v������B
        ��_����,// �����␔�l������int�l�Ƀ^�C�v������B
        �G�ɑ_���Ă鐔,//��ԑ_���Ă邩�A�_���ĂȂ���
        �����U����,//����̑����̍U���͂���ԍ���/�Ⴂ���c
        �����h���,//����̑����̍U���͂���ԍ���/�Ⴂ���c
        �������,//����̃o�t��f�o�t�����邩/�Ȃ���
        �w��Ȃ�_�w�C�g�l // ��{�̏����B�Ώۂ̒��ōł��w�C�g����������U������B
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
    /// </summary>
    public enum CharacterSide
    {
        �v���C���[ = 1,// ����
        ���� = 2,// ��ʓI�ȓG
        ���̑� = 3,// ����ȊO
        �w��Ȃ� = 0
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

    ///// <summary>
    ///// �s�����g�p�\�ȃ��[�h
    ///// �܂܂�
    ///// ������r�b�g���Z�ł��H�@�����I�ׂ��H
    ///// ���[�h1��2�Ȃ�c�݂�����
    ///   ���[�h�͂���Ȃ��B�Č��͂ł���B
    ///// </summary>
    //[Flags]
    //public enum Mode
    //{
    //    Mode1 = 1 << 0,
    //    Mode2 = 1 << 1,
    //    Mode3 = 1 << 2,
    //    Mode4 = 1 << 3,
    //    Mode5 = 1 << 4,
    //    AllMode = 0
    //}

    /// <summary>
    /// ������
    /// </summary>
    [Flags]
    public enum SpecialState
    {
        �w�C�g���� = 1 << 1,
        �w�C�g���� = 1 << 2,
        �Ȃ� = 0,
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
    /// �������ȍ~�ł́A�X�e�[�^�X�o�t��f�o�t���؂ꂽ���Ɍ��ɖ߂����炢�����Ȃ�
    /// Job�V�X�e���Ŏg�p���Ȃ��̂Ń��������C�A�E�g�͍œK��
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Auto)]
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
        [Header("�ŏ��ɂǂ�ȍs��������̂��̐ݒ�")]
        public ActState initialMove;

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
        /// �s�������f�[�^
        /// </summary>
        [Header("�s�������f�[�^")]
        public BehaviorData[] actCondition;

        /// <summary>
        /// �U���ȊO�̍s�������f�[�^.
        /// �ŏ��̗v�f�قǗD��x�����̂ŏd�_�B
        /// </summary>
        [Header("�w�C�g�����f�[�^")]
        public TargetJudgeData[] hateCondition;

        /// <summary>
        /// ���̐��l�ȏ�̓G����_���Ă��鑊�肪�^�[�Q�b�g�ɂȂ����ꍇ�A��U���̔��f�܂ł͑ҋ@�ɂȂ�
        /// ���̎��̔��f�ł���ς��ԃw�C�g������Α_���B(�_���܂����Ă鑊��ւ̃w�C�g�͉�����̂ŁA���ʂ͂��̎��̔��f�łׂ̂���_����)
        /// �l�q�f���A�݂����ȃX�e�[�g����邩��p��
        /// ���ȏ�ɑ_���Ă鑊�肩�A�l�q�f���Ă�L�����̏ꍇ�����w�C�g������悤�ɂ��悤�B
        /// </summary>
        public int targetingLimit;

        /// <summary>
        /// �U���܂ލs�������f�[�^.
        /// �v�f�͈�����A���̑��蕡�G�ȏ����Ŏw��\
        /// ���Ɏw��Ȃ��ꍇ�̂݃w�C�g�œ���
        /// �����Ńw�C�g�ȊO�̏������w�肵���ꍇ�́A�s���܂ŃZ�b�g�Ō��߂�B
        /// </summary>
        [Header("�s�������f�[�^")]
        public TargetJudgeData targetCondition;
    }

    /// <summary>
    /// �s�����f���Ɏg�p����f�[�^�B
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct BehaviorData
    {

        /// <summary>
        /// �s�����X�L�b�v���邽�߂̏����B
        /// </summary>
        public SkipJudgeData skipData;

        /// <summary>
        /// �s���̏����B
        /// �Ώۂ̐w�c�Ɠ������w��ł���B
        /// </summary>
        public ActJudgeData condition;

        /// <summary>
        /// �s���̑O������B
        /// �����̏����ɂ��Ďw��ł���B
        /// HP���Z�p�[�Z���g�Ƃ�
        /// �����ݒ肵�Ă����Εs�v�ȏ����ɂ��Ă͔��f���Ȃ��Ă悭�Ȃ邵�ȁB
        /// </summary>
        public ActJudgeData selfCondition;

    }

    /// <summary>
    /// ���f�Ɏg�p����f�[�^�B
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct SkipJudgeData
    {
        /// <summary>
        /// �s��������X�L�b�v�������
        /// </summary>
        [Header("�s��������X�L�b�v�������")]
        public SkipJudgeCondition skipCondition;

        /// <summary>
        /// ���f�Ɏg�p���鐔�l�B
        /// �����ɂ���Ă�enum��ϊ��������������肷��B
        /// </summary>
        public int judgeValue;

        /// <summary>
        /// �^�̏ꍇ�A���������]����
        /// �ȏ�͈ȓ��ɂȂ�Ȃ�
        /// </summary>
        [Header("����]�t���O")]
        public bool isInvert;

    }

    /// <summary>
    /// ���f�Ɏg�p����f�[�^�B
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ActJudgeData
    {
        /// <summary>
        /// �s������
        /// </summary>
        [Header("�s������̏���")]
        public ActJudgeCondition judgeCondition;

        /// <summary>
        /// ���f�Ɏg�p���鐔�l�B
        /// �����ɂ���Ă�enum��ϊ��������������肷��B
        /// </summary>
        public int judgeValue;

        /// <summary>
        /// �^�̏ꍇ�A���������]����
        /// �ȏ�͈ȓ��ɂȂ�Ȃ�
        /// </summary>
        [Header("����]�t���O")]
        public bool isInvert;

        /// <summary>
        /// ���ꂪ�w��Ȃ��A�ȊO���ƃX�e�[�g�ύX���s���B
        /// ����čs�����f�̓X�L�b�v
        /// </summary>
        public ActState stateChange;

        /// <summary>
        /// �Ώۂ̐w�c�敪
        /// �����w�肠��
        /// </summary>
        [Header("�`�F�b�N�Ώۂ̏���")]
        public TargetFilter filter;
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

        /// <summary>
        /// �Ώۂ̐w�c�敪
        /// �����w�肠��
        /// </summary>
        [Header("�`�F�b�N�Ώۂ̏���")]
        public TargetFilter filter;

        /// <summary>
        /// �g�p����s���̔ԍ��B
        /// �w��Ȃ� ( = -1)�̏ꍇ�͓G�̏������珟��Ɍ��߂�B
        /// �����łȂ��ꍇ�͂����܂Őݒ肷��B
        /// 
        /// ���邢�̓w�C�g�㏸�{���ɂȂ�B
        /// </summary>
        public float useAttackOrHateNum;
    }

    /// <summary>
    /// �s��������Ώېݒ�����Ō����Ώۂ��t�B���^�[���邽�߂̍\����
    /// </summary>
    public struct TargetFilter
    {
        /// <summary>
        /// �Ώۂ̐w�c�敪
        /// �����w�肠��
        /// </summary>
        [Header("�Ώۂ̐w�c")]
        [SerializeField]
        CharacterSide targetType;

        /// <summary>
        /// �Ώۂ̓���
        /// �����w�肠��
        /// </summary>
        [Header("�Ώۂ̓���")]
        [SerializeField]
        CharacterFeature targetFeature;

        /// <summary>
        /// ���̃t���O���^�̎��A�S�����Ă͂܂��ĂȂ��ƃ_���B
        /// </summary>
        [Header("�����̔��f���@")]
        [SerializeField]
        bool isAndFeatureCheck;

        /// <summary>
        /// �����ΏۃL�����N�^�[�̏����ɓ��Ă͂܂邩���`�F�b�N����B
        /// </summary>
        /// <param name="belong"></param>
        /// <param name="feature"></param>
        /// <returns></returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public bool IsPassFilter(CharacterSide belong, CharacterFeature feature)
        {
            // and��or�œ�����������
            bool isFeature = isAndFeatureCheck ? ((targetFeature == 0) || (targetFeature & feature) == targetFeature) :
                                                  ((targetFeature == 0) || (targetFeature & feature) > 0);

            return ((targetType == 0) || (targetType & belong) > 0) && isFeature;
        }

    }

    ///// <summary>
    ///// ���[�h��ς�������B
    ///// AI�̐U�镑�������ɂ�����
    ///// ����͂܂��g��Ȃ�
    ///// ���[�h�p�~�ɔ�������
    ///// </summary>
    //[Serializable]
    //[StructLayout(LayoutKind.Sequential)]
    //public struct ModeBehavior
    //{
    //    /// <summary>
    //    /// �A�^�b�N�X�g�b�v�t���O���^�̎�����
    //    /// ���x���Ƃ��֌W�Ȃ�����
    //    /// </summary>
    //    [Header("�U���}�����[�h��")]
    //    public bool isAttackStop;

    //    /// <summary>
    //    /// ���݂̃��[�h
    //    /// �����I���\
    //    /// </summary>
    //    [Header("�J�ڌ��̃��[�h")]
    //    public Mode nowMode;

    //    /// <summary>
    //    /// ���[�h�`�F���W����̗͊���
    //    /// 0�Ȃ疳��
    //    /// </summary>
    //    [Header("���[�h�ύX����̗͔�B0�Ŗ���")]
    //    public int healthRatio;

    //    /// <summary>
    //    /// �O��̃��[�h�`�F���W���牽�b�ŕω����邩
    //    /// 0�Ȃ疳��
    //    /// </summary>
    //    [Header("���[�h�ύX����")]
    //    public int changeTime;

    //    /// <summary>
    //    /// x����y�̋����ł��̃��[�h��
    //    /// �܂蒼������X���[�g������y���[�g���͈̔͂��Ă��Ƃ�
    //    /// ��������
    //    /// 00�Ȃ疳��
    //    /// </summary>
    //    [Header("���[�h�ύX�����i00�Ŗ����j")]
    //    public Vector2 changeDistance;

    //    /// <summary>
    //    /// �ς����̃��[�h
    //    /// All�Ȃ烉���_���ɕς��
    //    /// �ԍ����Ƃ��̔z�񐔂��烂�[�h�̐�������o��
    //    /// </summary>
    //    [Header("�ύX��̃��[�h")]
    //    public Mode changeMode;

    //    /// <summary>
    //    /// ���̏����̃`�F���W�̗D��x
    //    /// 0�͊�{���[�h�ɂ̂ݎg��
    //    /// �����{���[�h�̓}�C�i�X1�ł�������
    //    /// �ǂ�����ł��߂��悤�ɏ����͌y��
    //    /// </summary>
    //    [Header("�`�F���W�̗D��x�B5�̎��Œ�")]
    //    public int modeLevel;
    //}

    /// <summary>
    /// �U���̃X�e�[�^�X�B
    /// �{���Ƃ������Ƃ����̂ւ�B
    /// ����̓X�e�[�^�X�Ɏ������Ă����B
    /// �O��g�p�������ԁA�Ƃ����L�^���邽�߂ɁA�L�����N�^�[���ɕʓr�����N�����Ǘ���񂪕K�v�B
    /// Job�V�X�e���Ŏg�p���Ȃ��\���̂͂Ȃ�ׂ����������C�A�E�g���œK������B
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Auto)]
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
    /// ���[�h���ƂɃ��[�hEnum��int�ϊ����������C���f�b�N�X�ɂ����z��ɂȂ�B
    /// </summary>
    [Header("�L����AI�̐ݒ�")]
    public CharacterBrainStatus[] brainData;

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

    /// <summary>
    /// AI�̈ړ������ω����f�Ԋu
    /// </summary>
    [Header("�ړ����f�Ԋu")]
    public float moveJudgeInterval;
}
