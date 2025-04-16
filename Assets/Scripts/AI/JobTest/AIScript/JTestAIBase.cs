using UnityEngine;
using Cysharp.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System.Collections.Generic;
using System;
using Unity.VisualScripting;
using static JobAITestStatus;
using System.Runtime.InteropServices;
using UnityEngine.Events;

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
/// �z����܂ލ\���̂̔z��͂���炵���B�������������NativeContainer�ł��肢
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

    #region enum��`

    /// <summary>
    /// ���f���ʂ��܂Ƃ߂Ċi�[����r�b�g���Z�p
    /// </summary>
    [Flags]
    public enum JudgeResult
    {
        �V�������f�������� = 1 << 1,// ���̎��͈ړ��������ς���
        �����]���������� = 1 << 2,
        �X�e�[�g�ύX������ = 1 << 3 // ��Ԃ��ς�������́A�s���w��ԍ������̂܂ܕύX��̃X�e�[�g�ɂȂ�
    }

    #endregion enum��`


    #region �\���̒�`

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
    /// �W���u����󂯎�����f�[�^�Ɋ�Â��ē��������B
    /// �ݒ�Ȃǂ̓X�e�[�^�X�ɕ���
    /// 
    /// ������ TempJob�̃������ō����WriteOnly��NativeArray�ŁAJob���s�̂��т�in�����\�b�h��ʂ��Ă����Ɍ��ʔ��f����B
    /// 
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct MovementInfo
    {

        // ����L�������ǂ��������A�݂����ȃf�[�^�܂œ���Ă�
        // �V�K���f��̓^�[�Q�b�g����ւ��Ƃ��A���f���ԓ���ւ��Ƃ�������ƃL�����f�[�^��������B

        /// <summary>
        /// �^�[�Q�b�g�̃n�b�V���R�[�h
        /// ����ő�����擾����B
        /// �����ւ̎x�����[�u�̃^�[�Q�b�g�����肤�邱�Ƃ͓��ɓ����B
        /// </summary>
        public int targetHash;

        /// <summary>
        /// ���݂̃^�[�Q�b�g�Ƃ̋����B�}�C�i�X������̂ŕ����ł�����B
        /// </summary>
        public int targetDirection;

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
        /// 
        /// �s�����w�肷��ԍ��B-1�A�̏ꍇ�����G��������s�����f
        /// �����łȂ���Β��ڎw��
        /// ����������NPC�̏ꍇ�A���@��g�ݑւ��ł���̂ōs���̎w��ԍ��͉ς��ˁB
        /// ����MP����Ȃ��Ƃ��ǂ����悤�BMP�͑S�L�����ŕۗL/�����悤�ɂ��āA����ōs���̐��������邩�B�ŁAMP�����邩�̔��f���d�l�ɑg�ݍ���
        /// 
        /// ���Ȃ݂ɃX�e�[�g�ύX���͕ύX��X�e�[�g�̔ԍ��ɂȂ�B
        /// �X�e�[�g�ύX�����炷�����f�ł���悤��lastJudgetime���ς��Ȃ��Ƃ�
        /// </summary>
        public int attackJudgeNum;

        /// <summary>
        /// ���f���ʂɂ��Ă̏����i�[����r�b�g
        /// </summary>
        public int result;

        /// <summary>
        /// �s����ԁB
        /// </summary>
        public ActState moveState;

    }

    /// <summary>
    /// Job�V�X�e���Ŏg�p����L�����N�^�[�f�[�^�\���́B
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CharacterData : IDisposable
    {
        /// <summary>
        /// �V�����L�����N�^�[�f�[�^���擾����B
        /// </summary>
        /// <param name="status"></param>
        /// <param name="gameObject"></param>
        public CharacterData(JobAITestStatus status, GameObject gameObject)
        {
            brainData = new NativeArray<CharacterBrainStatusForJob>(status.brainData.Length, Allocator.Persistent);

            for ( int i = 0; i < status.brainData.Length; i++ )
            {
                brainData[i].ConvertToJobFormat(status.brainData[i], Allocator.Persistent);
            }

            hashCode = gameObject.GetHashCode();
            liveData = new CharacterUpdateData(status.baseData, gameObject.transform.position);
            solidData = status.solidData;
            targetingCount = 0;
            // �ŏ��̓}�C�i�X��10000�����邱�Ƃł���������悤��
            lastJudgeTime = -10000;

            personalHate = new NativeHashMap<int, int>(7, Allocator.Persistent);
            shortRangeCharacter = new UnsafeList<int>(7, Allocator.Persistent);

            moveJudgeInterval = status.moveJudgeInterval;
            lastMoveJudgeTime = 0;// �ǂ����s�����f���ɐU���������
        }

        /// <summary>
        /// �Œ�̃f�[�^�B
        /// </summary>
        public SolidData solidData;

        /// <summary>
        /// �L������AI�̐ݒ�B(Job�o�[�W����)
        /// ���[�h���ƂɃ��[�hEnum��int�ϊ����������C���f�b�N�X�ɂ����z��ɂȂ�B
        /// </summary>
        public NativeArray<CharacterBrainStatusForJob> brainData;

        /// <summary>
        /// �X�V���ꂤ��f�[�^�B
        /// </summary>
        public CharacterUpdateData liveData;

        /// <summary>
        /// ������_���Ă�G�̐��B
        /// �{�X���w�����͖����ł悳����
        /// ���U�����Ă����U�����I������ʂ̃^�[�Q�b�g��_���B
        /// ���̃^�C�~���O�Ŋ��肱�߂������荞��
        /// �����܂Ńw�C�g�l�����炷�����ŁB��U�ҋ@�ɂȂ��āA�w�C�g���邾���Ȃ̂ŉ���ꂽ�牣��Ԃ���
        /// ������ԈȊO�Ȃ牓���ɂȂ邵�A�������łȂ���ԃw�C�g�����Ȃ�U�����āA���̎��͉����ɂȂ�
        /// </summary>
        public int targetingCount;

        /// <summary>
        /// �Ō�ɔ��f�������ԁB
        /// </summary>
        public float lastJudgeTime;

        /// <summary>
        /// �Ō�Ɉړ����f�������ԁB
        /// </summary>
        public float lastMoveJudgeTime;

        /// <summary>
        /// �L�����N�^�[�̃n�b�V���l��ۑ����Ă����B
        /// </summary>
        public int hashCode;

        /// <summary>
        /// �U�����Ă�������Ƃ��A���ړI�ȏ����ɓ��Ă͂܂�������̃w�C�g�����L�^����B
        /// </summary>
        public NativeHashMap<int, int> personalHate;

        /// <summary>
        /// �߂��ɂ���L�����N�^�[�̋L�^�B
        /// ����̓Z���T�[�Œf���I�Ɏ擾����Q�l�l�B
        /// �����7~10�̗\��
        /// </summary>
        public UnsafeList<int> shortRangeCharacter;

        /// <summary>
        /// AI�̈ړ����f�Ԋu
        /// </summary>
        [Header("�ړ����f�Ԋu")]
        public float moveJudgeInterval;

        /// <summary>
        /// NativeContainer���܂ރ����o�[��j���B
        /// CombatManager���ӔC�������Ĕj������B
        /// </summary>
        public void Dispose()
        {
            brainData.Dispose();
            personalHate.Dispose();
            shortRangeCharacter.Dispose();
        }
    }

    /// <summary>
    /// AI�̐ݒ�B�iJob�V�X�e���d�l�j
    /// �X�e�[�^�X����ڐA����B
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CharacterBrainStatusForJob : IDisposable
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
        public NativeArray<BehaviorData> actCondition;

        /// <summary>
        /// �U���ȊO�̍s�������f�[�^.
        /// �ŏ��̗v�f�قǗD��x�����̂ŏd�_�B
        /// </summary>
        [Header("�w�C�g�����f�[�^")]
        public NativeArray<TargetJudgeData> hateCondition;

        /// <summary>
        /// �w�C�g�����ɑΉ�����w�C�g�㏸�{��
        /// </summary>
        [Header("�w�C�g�㏸�{��")]
        public NativeArray<int> hateMultiplier;

        /// <summary>
        /// �U���܂ލs�������f�[�^.
        /// �v�f�͈�����A���̑��蕡�G�ȏ����Ŏw��\
        /// ���Ɏw��Ȃ��ꍇ�̂݃w�C�g�œ���
        /// �����Ńw�C�g�ȊO�̏������w�肵���ꍇ�́A�s���܂ŃZ�b�g�Ō��߂�B
        /// </summary>
        [Header("�s�������f�[�^")]
        public TargetJudgeData targetCondition;

        /// <summary>
        /// NativeArray���\�[�X���������
        /// </summary>
        public void Dispose()
        {
            if ( actCondition.IsCreated )
                actCondition.Dispose();
            if ( hateCondition.IsCreated )
                hateCondition.Dispose();
            if ( hateMultiplier.IsCreated )
                hateMultiplier.Dispose();
        }

        /// <summary>
        /// �I���W�i����CharacterBrainStatus����f�[�^�𖾎��I�ɈڐA
        /// </summary>
        /// <param name="source">�ڐA���̃L�����N�^�[�u���C���X�e�[�^�X</param>
        /// <param name="allocator">NativeArray�Ɏg�p����A���P�[�^</param>
        public void ConvertToJobFormat(in CharacterBrainStatus source, Allocator allocator)
        {

            // ��{�v���p�e�B���R�s�[
            judgeInterval = source.judgeInterval;
            targetCondition = source.targetCondition;

            // �z���V�����쐬
            actCondition = source.actCondition != null
                ? new NativeArray<BehaviorData>(source.actCondition, allocator)
                : new NativeArray<BehaviorData>(0, allocator);

            hateCondition = source.hateCondition != null
                ? new NativeArray<TargetJudgeData>(source.hateCondition, allocator)
                : new NativeArray<TargetJudgeData>(0, allocator);
        }

    }

    /// <summary>
    /// �X�V�����L�����N�^�[�̏��B
    /// ��Ԉُ�Ƃ��o�t������Ď��Ԍp���̏I���܂�Job�Ō��邩�B
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CharacterUpdateData
    {
        /// <summary>
        /// �ő�̗�
        /// </summary>
        public int maxHp;

        /// <summary>
        /// �̗�
        /// </summary>
        public int currentHp;

        /// <summary>
        /// �ő喂��
        /// </summary>
        public int maxMp;

        /// <summary>
        /// ����
        /// </summary>
        public int currentMp;

        /// <summary>
        /// HP�̊���
        /// </summary>
        public float hpRatio;

        /// <summary>
        /// MP�̊���
        /// </summary>
        public float mpRatio;

        /// <summary>
        /// �e�����̊�b�U����
        /// </summary>
        public ElementalStats atk;

        /// <summary>
        /// �e�����̊�b�h���
        /// </summary>
        public ElementalStats def;

        /// <summary>
        /// ���݈ʒu�B
        /// </summary>
        public Vector2 nowPosition;

        /// <summary>
        /// ���݂̃L�����N�^�[�̏���
        /// </summary>
        public CharacterSide belong;

        /// <summary>
        /// ���݂̍s���󋵁B
        /// ���f�Ԋu�o�߂�����X�V�H
        /// �U�����ꂽ�肵����X�V�H
        /// ���ƒ��Ԃ���̖��߂Ƃ��ł��X�V���Ă�������
        /// 
        /// �ړ��Ƃ�������AI�̓��삪�ς��B
        /// �����̏ꍇ�͓G�̋������Q�Ƃ��đ��肪���Ȃ��Ƃ���ɓ����悤�ƍl������
        /// </summary>
        public ActState actState;

        /// <summary>
        /// �L��������_���[�W��^�����A�Ȃǂ̃C�x���g���i�[����ꏊ�B
        /// </summary>
        public int brainEventBit;

        /// <summary>
        /// ������CharacterUpdateData��CharacterBaseData�̒l��K�p����
        /// </summary>
        /// <param name="baseData">�K�p���̃x�[�X�f�[�^</param>
        public CharacterUpdateData(in CharacterBaseData baseData, Vector2 initialPosition)
        {
            // �U���͂Ɩh��͂��X�V
            atk = baseData.baseAtk;
            def = baseData.baseDef;

            maxHp = baseData.hp;
            maxMp = baseData.mp;
            currentHp = baseData.hp;
            currentMp = baseData.mp;
            hpRatio = 1;
            mpRatio = 1;

            belong = baseData.initialBelong;

            nowPosition = initialPosition;

            actState = baseData.initialMove;
            brainEventBit = 0;
        }
    }

    #endregion

    #endregion


    /// �e�X�g�Ŏg�p����X�e�[�^�X�B<br></br>
    /// ���f�Ԋu�̃f�[�^�������Ă���B<br></br>
    /// �C���X�y�N�^����ݒ�B
    /// </summary>
    [SerializeField]
    protected JobAITestStatus status;

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
    /// �����������B
    /// </summary>
    protected void Initialize()
    {
        // �V�����L�����f�[�^�𑗂�A�R���o�b�g�}�l�[�W���[�ɑ���B
        // ����A����ς�ޗ������Č������ō���Ă��炨���B
        // NativeContainer�܂ލ\���̂��R�s�[����̂Ȃ񂩂��킢�B
        // ���������R�s�[���Ă��A�������ō�������̓��[�J���ϐ��ł����Ȃ�����Dispose()����̖��͂Ȃ��͂��B
        CombatManager.instance.CharacterAdd(status, gameObject);
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
