using UnityEngine;
using Cysharp.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System.Collections.Generic;
using System;
using Unity.VisualScripting;
using static JobAITestStatus;
using System.Runtime.InteropServices;

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
    public struct MoveData
    {

        // ����L�������ǂ��������A�݂����ȃf�[�^�܂œ����Ă��

        /// <summary>
        /// �^�[�Q�b�g�̃X�N���v�g�B
        /// ����ő�����擾����B
        /// �����ւ̎x�����[�u�̃^�[�Q�b�g�����肤�邱�Ƃ͓��ɓ����B
        /// </summary>
        public JTestAIBase target;

        /// <summary>
        /// ���݂̈ړ����x�B�}�C�i�X������̂ŕ����ł�����B
        /// </summary>
        public int moveSpeed;

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



    #region ��`

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
            baseData = status.baseData;
            brainData = new CharacterBrainStatusForJob(status.brainData, Allocator.Persistent);
            hashCode = gameObject.GetHashCode();
            liveData = new CharacterUpdateData(baseData, gameObject.transform.position);
            solidData = status.solidData;
        }

        /// <summary>
        /// �Œ�f�[�^�B
        /// </summary>
        CharacterBaseData baseData;

        /// <summary>
        /// �Œ�̃f�[�^�B
        /// </summary>
        public SolidData solidData;

        /// <summary>
        /// �L������AI�̐ݒ�B(Job�o�[�W����)
        /// </summary>
        public CharacterBrainStatusForJob brainData;

        /// <summary>
        /// �X�V���ꂤ��f�[�^�B
        /// </summary>
        public CharacterUpdateData liveData;

        /// <summary>
        /// �L�����N�^�[�̃n�b�V���l��ۑ����Ă����B
        /// </summary>
        public int hashCode;

        /// <summary>
        /// NativeContainer���܂ރ����o�[��j���B
        /// </summary>
        public void Dispose()
        {
            brainData.Dispose();
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
        /// AI�̈ړ����f�Ԋu
        /// </summary>
        [Header("�ړ����f�Ԋu")]
        public float moveJudgeInterval;

        /// <summary>
        /// �s�������f�[�^
        /// </summary>
        [Header("�s�������f�[�^")]
        public NativeArray<MoveJudgeData> moveCondition;

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
        /// �U���ȊO�̍s�������f�[�^.
        /// �ŏ��̗v�f�قǗD��x�����̂ŏd�_�B
        /// </summary>
        [Header("�s�������f�[�^")]
        public NativeArray<TargetJudgeData> targetCondition;

        /// <summary>
        /// NativeArray���\�[�X���������
        /// </summary>
        public void Dispose()
        {
            if ( moveCondition.IsCreated )
                moveCondition.Dispose();
            if ( hateCondition.IsCreated )
                hateCondition.Dispose();
            if ( hateMultiplier.IsCreated )
                hateMultiplier.Dispose();
            if ( targetCondition.IsCreated )
                targetCondition.Dispose();
        }

        /// <summary>
        /// �I���W�i����CharacterBrainStatus����f�[�^�𖾎��I�ɈڐA
        /// </summary>
        /// <param name="source">�ڐA���̃L�����N�^�[�u���C���X�e�[�^�X</param>
        /// <param name="allocator">NativeArray�Ɏg�p����A���P�[�^</param>
        public CharacterBrainStatusForJob(in CharacterBrainStatus source, Allocator allocator)
        {

            // ��{�v���p�e�B���R�s�[
            judgeInterval = source.judgeInterval;
            moveJudgeInterval = source.moveJudgeInterval;

            // �z���V�����쐬
            moveCondition = source.moveCondition != null
                ? new NativeArray<MoveJudgeData>(source.moveCondition, allocator)
                : new NativeArray<MoveJudgeData>(0, allocator);

            hateCondition = source.hateCondition != null
                ? new NativeArray<TargetJudgeData>(source.hateCondition, allocator)
                : new NativeArray<TargetJudgeData>(0, allocator);

            hateMultiplier = source.hateMultiplier != null
                ? new NativeArray<int>(source.hateMultiplier, allocator)
                : new NativeArray<int>(0, allocator);

            targetCondition = source.targetCondition != null
                ? new NativeArray<TargetJudgeData>(source.targetCondition, allocator)
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
        public MoveState state;

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

            state = baseData.initialMove;
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
