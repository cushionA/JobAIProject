using UnityEngine;
using Cysharp.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System.Collections.Generic;
using System;
using Unity.VisualScripting;

/// <summary>
/// NativeContainer�̃��X�g���g����
/// ��{���j�͈ȉ�
/// 
/// 
/// ���������\�f�[�^�ƕs�f�[�^�A��������NativeContainer�𓝈�̔ԍ��ŊǗ�����B
/// �������̃R���e�i�����b�v�����N���X�ŊǗ����A�O������̓L�����ԍ��Ńf�[�^�ɃA�N�Z�X�����菑���������肷��B
/// ���������͓����ōs���B�\�Ȍ��� struct.member= �̂悤��
/// 
/// ����������Ȃ炻�̕�������������B�Ӗ��̂Ȃ����G���A�������͖���
/// 
/// �z����܂ލ\���̂̔z��͂���炵���B�������������NativeArray�ł��肢
/// 
/// ���������s�\�f�[�^
/// �L�����f�[�^�A���f��f�[�^
/// 
/// ���������\�f�[�^
/// �L������ԃf�[�^�A�s���f�[�^�A
/// </summary>
public class JTestAIBase : MonoBehaviour
{
    #region ��`


    public enum MoveState
    {
        �ǐ�,
        ����,
        �U��,
        �ҋ@,// �U����̃N�[���^�C�����ȂǁB���̏�Ԃœ��삷���𗦂�ݒ肷��H
        �K�[�h,// �����o��������ݒ�ł���悤�ɂ���H ���̏�Ŋ�{�K�[�h�����ǁA���肪�����炩���ꂽ�瓮���o���A�I��
        �x��,
        ��,
        ��q,
        �x��,
        ���S
    }


    /// <summary>
    /// ���f�Ɏg�p����f�[�^�̍\���́B
    /// ���݂̍s����ԁA�ړ������A���f��AHP�ق��X�e�[�^�X�A�S�Ď��܂��Ă���B
    /// ����ɏ]���ē���
    /// ���邢�̓f�[�^�𕪂��邩�H
    /// 
    /// �U���▂�@�̃G�t�F�N�g�Ȃǂ̃f�[�^�͕ʂ̃f�[�^�^�ɓ���āA�I�������U���̔ԍ��ȂǂŎQ�Ƃ��邩�H
    /// �Ȃ�Ȃ�S���̃f�[�^�z��ł������H�@HP�A�Ȃǂ̖��O�̗񋓎q�̒l�ŃA�N�Z�X����΂����B
    ///
    /// ���f�����ƁA�������p�X�������̍s���i�������[�h�Ȃǂ̏�ԕω���U���Ȃǂ̋�̓I�s���j
    /// ��Ԃ͊O������ς�����悤�ɂ���B��e�C�x���g�œ����Ƃ�
    /// �w�C�g�l�̊Ǘ��Ɩ����ւ̖��߁A�G�ւ̈Њd�Ȃǂ̑��M�̎������Y�݂ǂ���B
    /// �S���l�^�ł��Ȃ��Ƃ�
    /// �w�C�g�l�͂ǂ��g�����B�����̎��U���A�w�C�g�l����ԍ�����ɍU���A�U�����鑊��ɂ���āi�ʒu�Ƃ���_�Ƃ��Łj�U��������A���ė���ɂ���H
    /// ���ꂾ�ƍU�������A�w�C�g�l�̐ݒ�ɂ��l�i���U�����鑊��̌X���j���f�A����ɍ��킹���U���A�̎O�̒i�K�ɂȂ��Ă��
    /// �����Ɛ퓬��x���ł����Ԕ��f�ς�肻��
    /// �V�X�^�[����ɂ͂ǂ��ݒ肷�邩
    /// 
    /// 
    /// 
    /// </summary>
    public struct MoveData
    {

        // ����L�������ǂ��������A�݂����ȃf�[�^�܂œ����Ă��

        /// <summary>
        /// �L�����f�[�^�z��̒��ł̃^�[�Q�b�g�̈ʒu�B
        /// ����ő�����擾����B
        /// �����ւ̎x�����[�u�����肤�邱�Ƃ͓��ɓ����B
        /// </summary>
        public int targetNum;

        /// <summary>
        /// ���݂̈ړ������B
        /// </summary>
        public int moveDirection;

        /// <summary>
        /// ���݂̍s���󋵁B
        /// ���f�Ԋu�o�߂�����X�V�H
        /// �U�����ꂽ�肵����X�V�H
        /// ���ƒ��Ԃ���̖��߂Ƃ��ł��X�V���Ă�������
        /// 
        /// �ړ��Ƃ�������AI�̓��삪�ς��B
        /// �����̏ꍇ�͓G�̋������Q�Ƃ��đ��肪���Ȃ��Ƃ���ɓ����悤�ƍl������
        /// </summary>
        public MoveState state;

        /// <summary>
        /// �U���̔��f�Œʉ߂������f���������Ԃ��Ƃ����f�[�^�B���f�ɂ�����񂯂�΁A�Ƃ������U�����Ȃ��Ƃ���-1����Ƃ�<br/>
        /// ����͍U����Ԃ̎��g�p����B<br/>
        /// ��{�I�ɔ��f�����̓L�����X�e�[�^�X������������Ă���͂��Ȃ̂ŁA�Z�Ԃ̏����ɕR�Â��U����i�͊e�L�����N�^�[���e���ł����Ȃ��B
        /// �G�L�����Ȃǂ̏ꍇ�A��{�͔ԍ��ōU���𒼐ڎw�肷�邱�Ƃ������B<br/>
        /// �ǂ̔��f�������N���A�������i������1~3�Ƃ��j�ōU�����Ή����镨���ݒ肳��Ă���B<br/>
        /// �ŁA���肵���U���̔ԍ���n�����ƂŃL�������U������B<br/>
        /// �������V�X�^�[����̏ꍇ�͑������閂�@���ς�邽�߁A�����ōU�����w�肷��B<br/>
        /// ���̏ꍇ�͏������󂯎���āA�N���X�̕��ŃV�X�^�[���񂪎g�����@�����߂�B<br/>
        /// �Ƃ�����薂�@�ƕ��ʂ̍U���𓝍����āA�U����I�Ԋ����ɂ�������AI�̔ėp�����o����
        /// </summary>
        public int attackJudgeNum;

    }



    /// <summary>
    /// �̗͂̊����Ƃ��ω�����L�����f�[�^
    /// ��Ԉُ�̓R���f�B�V�����f�[�^���猩��
    /// </summary>
    public struct ConditionData
    {


        /// <summary>
        /// �̗͂̊���
        /// </summary>
        public float hpRatio;

        /// <summary>
        /// �̗͂̐��l
        /// </summary>
        public float hpNum;

        /// <summary>
        /// MP�̊���
        /// </summary>
        public float mpRatio;

    }


    #region ��`


    /// <summary>
    /// ���M����f�[�^�A�s�ς̕�
    /// �唼�r�b�g�ł܂Ƃ߂ꂻ��
    /// ���ԋR�m�̓G���邩������Ȃ����^�C�v�͑g�ݍ��킹�\�ɂ���H
    /// </summary>
    public struct CharacterData
    {

        /// <summary>
        /// �G�̎��
        /// �R�m�Ƃ��ˎ�Ƃ�����������
        /// </summary>
        public KindOfCharacter kind;

        /// <summary>
        /// �L�����̑����Ƃ�����
        /// �^�C�v������
        /// </summary>
        public CharacterFeature type;



        /// <summary>
        /// �����H
        /// ����Ȃ�r�b�g�ł܂Ƃ߂��
        /// </summary>
        [Header("���G���ǂ���")]
        public bool isStrong;

        /// <summary>
        /// �\���U����
        /// </summary>
        public float displayAtk;

        /// <summary>
        /// �\���h���
        /// </summary>
        public float displayDef;

        /// <summary>
        /// �U�������������񋓌^
        /// �r�b�g���Z�Ō���
        /// NPC���������
        /// �Ȃɑ����̍U�������Ă��邩�Ƃ����Ƃ���
        /// </summary>
        public Element attackElement;

        /// <summary>
        /// ��_�����������񋓌^
        /// �r�b�g���Z�Ō���
        /// NPC���������
        /// </summary>
        public Element WeakPoint;

        /// <summary>
        /// �L�����̊K���B<br/>
        /// ���ꂪ��Ȃقǖ����̒��œ����G���^�[�Q�b�g�ɂ��ĂĂ����T�����Ȃ��čςށA�D��I�ɉ����B<br/>
        /// ���ƃ����N�Ⴂ�����ɖ��ߔ�΂�����ł���B
        /// </summary>
        [Header("�`�[�����ł̊K��")]
        public CharacterRank rank;

    }

    /// <summary>
    /// �L�����N�^�[�̃^�C�v�B<br/>
    /// �������Ă͂܂�ꍇ������B
    /// </summary>
    [Flags]
    public enum KindOfCharacter
    {
        Soldier,//���̎G��
        Fly,//��Ԃ��
        Shooter,//������
        Knight,//������
        Trap,//�҂��\���Ă���
        none//�w��Ȃ�
    }

    /// <summary>
    /// �L�����N�^�[�̑����B
    /// �����ɓ��Ă͂܂镪�S���Ԃ����ށB
    /// </summary>
    [Flags]
    public enum CharacterFeature
    {
        Player = 1 << 0,
        Sister = 1 << 1,
        NPC = 1 << 2,
        Enemy = 1 << 3,
        Boss = 1 << 4,
        Soldier = 1 << 5,//���̎G��
        Fly = 1 << 6,//��Ԃ��
        Shooter = 1 << 7,//������
        Knight = 1 << 8,//������
        Trap = 1 << 9,//�҂��\���Ă���
        none = 0//�w��Ȃ�
    }


    /// <summary>
    /// �L�����N�^�[����������w�c
    /// ����ŃA�N�Z�X����}�l�[�W���[�����܂�
    /// </summary>
    public enum CharacterSide
    {
        Player,
        Enemy,
        Other//����͎g��Ȃ��B����d�l�Ƃ���Other���m�͎������v���C���[�A�G���G�l�~�[�Ƃ݂Ȃ��悤��
             //�g��Ȃ������܂��߂Ƃ��Ďc���B���ƃv���C���[��Other��G�Ƃ݂Ȃ����G�̓v���C���[�Ƃ݂Ȃ�
    }

    public enum AttackType
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
        �ő��� = 1 << 7,
        �ғő��� = 1 << 8,
        �������� = 1 << 9,
        ��_���� = 1 << 13,//�G�̎�_�������T�[�`���đ���Ɏg��
        �w��Ȃ� = 0
    }

    /// <summary>
    /// �L�����̃��x��
    /// ���̃��x���������Ƒ��̓G�Ɏז�����Ȃ�
    /// </summary>
    public enum CharacterRank
    {
        weak,//�G��
        normal,//��{�͂���
        strong,//�����u
        absolute//�{�X����
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
    /// ���[�h��ς������
    /// </summary>
    [Serializable]
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
    /// �h��͂̂܂Ƃߍ\����
    /// </summary>
    public struct DefStatus
    {
        /// <summary>
        /// �a���h��
        /// �h��͂��ꂼ��̑����ɑΉ����Ă���̂ő��v
        /// �S�Ă̖h��͂̑��v�݂����Ȃ̂͂���Ȃ�
        /// </summary>
        [Header("�a���h��́B�̗͂ŏオ��")]
        public float slashDef;

        [Header("�h�˖h��B�ؗ͂ŏオ��")]
        public float pierDef;

        [Header("�Ō��h��A�Z�ʂŏオ��")]
        public float strDef;

        [Header("�_���h��A�؂ƌ����ŏオ��")]
        public float holyDef;

        [Header("�Ŗh��B�����ŏオ��")]
        public float darkDef;

        [Header("���h��B�����Ɛ����ŏオ��")]
        public float fireDef;

        [Header("���h��B�����Ǝ��v�ŏオ��")]
        public float thunderDef;



    }


    /// <summary>
    /// �A�C�e�����ʔ{�������t���O�Ȃǂ̃C�x���g���Ǘ����邽�߂̃N���X
    /// 
    /// ����S���A�r���e�B�ɖ₢���킹��΂������������
    /// 
    ///  �A�N�V�����ω�,

    ///  �A�C�e�����ʕϓ�,
    ///  ����o�t, //�����ŁA�o�t���ʎ��ԉ����A�����ȂǁB�J�E���^�[�U���{���Ƃ�������ɂ��邩
    ///  ����f�o�t,//��~�Ƃ��B��~�̓X�^���̏I�����A�j���̏I���ł͂Ȃ���Ԉُ�̏I���܂łƂ���B���@�֎~���H
    ///  ��Ԉُ�~��,//�łƂ����������̂����܂��Ă����ߒ��@����͏�ԊǗ��A�r���e�B�Ŏ�������H�@��Ԉُ�ϐ�
    ///  �o���A,  �G�t�F�N�g���Ǘ�����H�@����̓R���g���[���A�r���e�B�ŊǗ����邩
    ///  �G���`�����g,�@ �G���`���̓X�e�[�^�X�iNPC�j�═��i�v���C���[�j�ɃG���`������ۑ�������悤�ɂ���H�@�����ɕۑ������邩�B����
    ///  
    ///  ���������������g���K�[�C�x���g������������������
    ///  �U���q�b�g�C�x���g,  ok
    ///  �W���X�g�K�[�h�C�x���g,�@�@ok
    ///  �m���C�x���g,   ok�@�@�̗͌n�̗͕͑ϓ����ɌĂԂ�
    ///  �̗̓}�b�N�X���C�x���g,    ok
    ///  ������C�x���g,   ok
    ///  �G���j���C�x���g,    ok
    ///  ��e���C�x���g,�@�@ok
    /// </summary>
    public struct SpecialConditionContainer
    {

        ///// <summary>
        ///// �g���K�[�C�x���g
        ///// ���ꂼ��̃g���K�[�C�x���g�^�C�v��
        ///// �t���O���r�b�g���Z�ŕۑ����Ă���
        ///// </summary>
        //Dictionary<ConditionAndEffectControllAbility.EventType, TriggerEventSelect> triggerEvents;

        ///// <summary>
        ///// ���ʂȃo�t
        ///// </summary>
        //SpecialBuffSelect specialBuff;


        ///// <summary>
        ///// ���ʂȃf�o�t
        ///// �����ɋL�^���Ă����H
        ///// ����K�v�Ȃ��ˁH
        ///// �A�r���e�B�ɕ����΂����킯��
        ///// </summary>
        //SpecialDebuffSelect specialDebuff;


    }



    #endregion




    #endregion


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
    /// job�V�X�e���ɓn�����߂�NativeContainer
    /// </summary>
    NativeList<int> characterData = new NativeList<int>();

    /// <summary>
    /// ���f�Ԋu��Ԃ��v���p�e�B�B
    /// </summary>
    public float JudgeInterval { get { return status.judgeInterval; } }


    /// <summary>
    /// �����������B
    /// </summary>
    protected void Initialize()
    {

        // characterData.add

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
        return GameManager.instance.isTest && ((GameManager.instance.NowTime) > status.judgeInterval);
    }

    /// <summary>
    /// �s���𔻒f���郁�\�b�h�B
    /// </summary>
    protected void MoveJudgeAct()
    {
        // 50%�̊m���ō��E�ړ��̕������ς��B
        // moveDirection = (UnityEngine.Random.Range(0, 100) >= 50) ? 1 : -1;

        //  rb.linearVelocityX = moveDirection * status.xSpeed;

        //Debug.Log($"���l�F{moveDirection * status.xSpeed} ���x�F{rb.linearVelocityX}");

        //lastJudge = GameManager.instance.NowTime;
        judgeCount++;
    }

    /// <summary>
    /// �^�[�Q�b�g�����߂čU������B
    /// </summary>
    protected void Attackct()
    {

    }

}
