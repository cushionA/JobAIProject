using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static JobAITestStatus;
using static JTestAIBase;

/// <summary>
/// ���̃t���[���őS�̂ɓK�p����C�x���g���X�g�A�݂����Ȃ̂������āA�����ɃL�����N�^�[���f�[�^��n����悤�ɂ���
/// Dispose�K�{�BAI�֘A��Dispose()�͂����ł��ӔC������B
/// </summary>
public class CombatManager : MonoBehaviour, IDisposable
{

    #region ��`

    /// <summary>
    /// AI�̃C�x���g�̃^�C�v�B<br/>
    /// �e�C�x���g�̓`�[���̃w�C�g�������邩�A�C�x���g�t���O�𗧂Ă邩�̓���B<br/>
    /// �s���ɉ����ăt���O�𗧂āA���̃L�����̏����ɂ���ĉ��߂��ς��B
    /// </summary>
    [Flags]
    public enum BrainEventFlagType
    {
        None = 0,  // �t���O�Ȃ��̏�Ԃ�\����{�l
        ��_���[�W��^���� = 1 << 0,   // ����ɑ傫�ȃ_���[�W��^����
        ��_���[�W���󂯂� = 1 << 1,   // ���肩��傫�ȃ_���[�W���󂯂�
        �񕜂��g�p = 1 << 2,         // �񕜃A�r���e�B���g�p����
        �x�����g�p = 1 << 3,         // �x���A�r���e�B���g�p����
        //�N����|���� = 1 << 4,        // �G�܂��͖�����|����
        //�w������|���� = 1 << 5,      // �w������|����
        �U���Ώێw�� = 1 << 4,        // �w�����ɂ��U���Ώۂ̎w��
        �Ј� = 1 << 5,//�Ј���Ԃ��ƓG���|����H ����̓o�b�h�X�e�[�^�X�ł������Ƃ͎v��
    }

    /// <summary>
    /// AI�̃L�����C�x���g�̑��M��B
    /// ������V���O���g���ɒu���ăC�x���g��o��ɂ���B
    /// ���Ԍo�߂����炻�̃L��������t���O���������߂̐ݒ�B���Ă��t���O���������߂Ƀf�[�^��ێ�����B
    /// �C�x���g�ǉ����ɑΏۃL�����Ƀt���O��ݒ肵�A�����𖞂������痧�Ă��t���O�������B
    /// ���Ԍo�߂̑��A�L�������S���������ɖ₢���킹�Ȃ��ƁB
    /// �ΏۃL�����̃n�b�V�������S�L�����n�b�V���ƈ�v����C�x���g����`�T�����č폜�Ƃ��B
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct BrainEventContainer
    {

        /// <summary>
        /// �C�x���g�̃^�C�v
        /// </summary>
        public BrainEventFlagType eventType;

        /// <summary>
        /// �C�x���g���Ă񂾐l�̃n�b�V���B
        /// 
        /// </summary>
        public int targetHash;

        /// <summary>
        /// �C�x���g�J�n����
        /// </summary>
        public float startTime;

        /// <summary>
        /// �C�x���g���ǂꂭ�炢�̊ԕێ�����邩�A�Ƃ������ԁB
        /// </summary>
        public float eventHoldTime;

        /// <summary>
        /// AI�̃C�x���g�̃R���X�g���N�^�B
        /// startTime�͌��ݎ�������B
        /// </summary>
        /// <param name="brainEvent"></param>
        /// <param name="hashCode"></param>
        /// <param name="holdTime"></param>
        public BrainEventContainer(BrainEventFlagType brainEvent, int hashCode, float holdTime)
        {
            this.eventType = brainEvent;
            this.targetHash = hashCode;
            this.startTime = GameManager.instance.NowTime;
            this.eventHoldTime = holdTime;
        }

    }

    #region �L�����N�^�[�f�[�^�֘A�̍\���̒�`

    /// <summary>
    /// Job�V�X�e���Ŏg�p����L�����N�^�[�f�[�^�\���́B
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CharacterData : IDisposable, ILogicalDelate
    {
        /// <summary>
        /// �V�����L�����N�^�[�f�[�^���擾����B
        /// </summary>
        /// <param name="status"></param>
        /// <param name="gameObject"></param>
        public CharacterData(JobAITestStatus status, GameObject gameObject)
        {
            this.brainData = new NativeHashMap<int, CharacterBrainStatusForJob>(status.brainData.Count, Allocator.Persistent);

            foreach ( var item in status.brainData )
            {
                CharacterBrainStatusForJob newData = new(item.Value, Allocator.Persistent);
                this.brainData.Add((int)item.Key, newData);
            }

            this.hashCode = gameObject.GetHashCode();
            this.liveData = new CharacterUpdateData(status.baseData, gameObject.transform.position);
            this.solidData = status.solidData;
            this.targetingCount = 0;
            // �ŏ��̓}�C�i�X��10000�����邱�Ƃł���������悤��
            this.lastJudgeTime = -10000;

            this.personalHate = new NativeHashMap<int, int>(7, Allocator.Persistent);
            this.shortRangeCharacter = new UnsafeList<int>(7, Allocator.Persistent);

            this.moveJudgeInterval = status.moveJudgeInterval;
            this.lastMoveJudgeTime = 0;// �ǂ����s�����f���ɐU���������

            // �ŏ��͘_���폜�t���O�Ȃ��B
            this.isLogicalDelate = BitableBool.FALSE;
        }

        /// <summary>
        /// �Œ�̃f�[�^�B
        /// </summary>
        public SolidData solidData;

        /// <summary>
        /// �L������AI�̐ݒ�B(Job�o�[�W����)
        /// ���[�h���ƂɃ��[�hEnum��int�ϊ����������C���f�b�N�X�ɂ����z��ɂȂ�B
        /// </summary>
        public NativeHashMap<int, CharacterBrainStatusForJob> brainData;

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
        /// �v�f�������7~10�̗\��
        /// </summary>
        public UnsafeList<int> shortRangeCharacter;

        /// <summary>
        /// AI�̈ړ����f�Ԋu
        /// </summary>
        [Header("�ړ����f�Ԋu")]
        public float moveJudgeInterval;

        /// <summary>
        /// �_���폜�t���O�B
        /// </summary>
        /// 
        private BitableBool isLogicalDelate;

        /// <summary>
        /// NativeContainer���܂ރ����o�[��j���B
        /// CombatManager���ӔC�������Ĕj������B
        /// </summary>
        public void Dispose()
        {
            this.brainData.Dispose();
            this.personalHate.Dispose();
            this.shortRangeCharacter.Dispose();
        }

        /// <summary>
        /// �_���폜�t���O�̊m�F�B
        /// </summary>
        /// <returns>�^�ł���Θ_���폜�ς�</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLogicalDelate()
        {
            return this.isLogicalDelate == BitableBool.TRUE;
        }

        /// <summary>
        /// �_���폜�����s����B
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogicalDelete()
        {
            this.isLogicalDelate = BitableBool.TRUE;
        }
    }

    /// <summary>
    /// AI�̐ݒ�B�iJob�V�X�e���d�l�j
    /// �X�e�[�^�X��CharacterBrainStatus����ڐA����B
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
        /// NativeArray���\�[�X���������
        /// </summary>
        public void Dispose()
        {
            if ( this.actCondition.IsCreated )
            {
                this.actCondition.Dispose();
            }

            if ( this.hateCondition.IsCreated )
            {
                this.hateCondition.Dispose();
            }
        }

        /// <summary>
        /// �I���W�i����CharacterBrainStatus����f�[�^�𖾎��I�ɈڐA
        /// </summary>
        /// <param name="source">�ڐA���̃L�����N�^�[�u���C���X�e�[�^�X</param>
        /// <param name="allocator">NativeArray�Ɏg�p����A���P�[�^</param>
        public CharacterBrainStatusForJob(in CharacterBrainStatus source, Allocator allocator)
        {

            // ��{�v���p�e�B���R�s�[
            this.judgeInterval = source.judgeInterval;

            // �z���V�����쐬
            this.actCondition = source.actCondition != null
                ? new NativeArray<BehaviorData>(source.actCondition, allocator)
                : new NativeArray<BehaviorData>(0, allocator);

            this.hateCondition = source.hateCondition != null
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
        public int hpRatio;

        /// <summary>
        /// MP�̊���
        /// </summary>
        public int mpRatio;

        /// <summary>
        /// �e�����̊�b�U����
        /// </summary>
        public ElementalStats atk;

        /// <summary>
        /// �S�U���͂̉��Z�B
        /// </summary>
        public int dispAtk;

        /// <summary>
        /// �e�����̊�b�h���
        /// </summary>
        public ElementalStats def;

        /// <summary>
        /// �S�h��͂̉��Z�B
        /// </summary>
        public int dispDef;

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
        /// �o�t��f�o�t�Ȃǂ̌��݂̌���
        /// </summary>
        public SpecialEffect nowEffect;

        /// <summary>
        /// AI�����҂̍s����F�����邽�߂̃C�x���g�t���O�B
        /// �񋓌^AIEventFlagType�@�̃r�b�g���Z�Ɏg���B
        /// CombatManager���t���O�Ǘ��͂��Ă����
        /// </summary>
        public BrainEventFlagType brainEvent;

        /// <summary>
        /// ������CharacterUpdateData��CharacterBaseData�̒l��K�p����
        /// </summary>
        /// <param name="baseData">�K�p���̃x�[�X�f�[�^</param>
        public CharacterUpdateData(in CharacterBaseData baseData, Vector2 initialPosition)
        {
            // �U���͂Ɩh��͂��X�V
            this.atk = baseData.baseAtk;
            this.def = baseData.baseDef;

            this.maxHp = baseData.hp;
            this.maxMp = baseData.mp;
            this.currentHp = baseData.hp;
            this.currentMp = baseData.mp;
            this.hpRatio = 1;
            this.mpRatio = 1;

            this.belong = baseData.initialBelong;

            this.nowPosition = initialPosition;

            this.actState = baseData.initialMove;
            this.brainEventBit = 0;

            this.dispAtk = this.atk.ReturnSum();
            this.dispDef = this.def.ReturnSum();

            this.nowEffect = SpecialEffect.�Ȃ�;
            this.brainEvent = BrainEventFlagType.None;
        }
    }

    #endregion �L�����N�^�[�f�[�^�֘A�̍\���̒�`

    #endregion ��`

    /// <summary>
    /// �V���O���g���̃C���X�^���X�B
    /// </summary>
    public static CombatManager instance;

    /// <summary>
    /// �L�����f�[�^��ێ�����R���N�V�����B<br/>
    /// Job�V�X�e���ɓn������CharacterData��UnsafeList�ɂ���B<br/>
    /// ���W������IJobParallelForTransform�Ŏ擾����H�@������������LocalScale�i���L�����̌����j�܂Ŏ���Ă����Ƃ��������B<>br/>
    /// �����Ȃ�ĕ����]���������Ƀf�[�^����������΂�����������
    /// </summary>
    public CharacterDataDictionary<CharacterData, JTestAIBase> charaDataDictionary = new(7);

    /// <summary>
    /// �v���C���[�A�G�A���̑��A���ꂼ�ꂪ�G�΂��Ă���w�c���r�b�g�ŕ\���B<br/>
    /// �L�����f�[�^�̃`�[���ݒ�ƈꏏ�Ɏg��<br/>
    /// </summary>
    public static NativeArray<int> relationMap = new(3, Allocator.Persistent);

    /// <summary>
    /// �w�c���Ƃɐݒ肳�ꂽ�w�C�g�l�B<br/>
    /// �n�b�V���L�[�ɂ̓Q�[���I�u�W�F�N�g�̃n�b�V���l�ƃ`�[���̏���n��<br/>
    /// (�`�[���l,�n�b�V���l)�Ƃ����`��<br/>
    /// </summary>
    public NativeHashMap<int2, int> teamHate = new(7, Allocator.Persistent);

    /// <summary>
    /// AI�̃C�x���g���󂯕t������ꕨ�B
    /// ���ԊǗ��̂��߂Ɏg���B
    /// Job�V�X�e���ňꊇ�Ŏ��Ԍ��邩�A���ʂɃ��[�v���邩�i�C�x���g�͂���Ȃɐ����Ȃ������������ʂ����������j
    /// </summary>
    public UnsafeList<BrainEventContainer> eventContainer = new(7, Allocator.Persistent);

    /// <summary>
    /// �s������f�[�^�B
    /// Job�̏������ݐ�ŁA�^�[�Q�b�g�ύX�̔��f�Ƃ����S���͂����Ă�B<br/>
    /// ������󂯎���ăL�����N�^�[���s������B
    /// </summary>
    public UnsafeList<MovementInfo> judgeResult = new(7, Allocator.Persistent);

    /// <summary>
    /// �O�񔻒f�����s�����ۂ̎��Ԃ��L�^����B
    /// ������g�p���Ĕ��f���ʂ��󂯂��I�u�W�F�N�g�������O�񔻒莞�Ԃ��ēx�ݒ肷��B
    /// </summary>
    public float lastJudgeTime;

    /// <summary>
    /// �N�����ɃV���O���g���̃C���X�^���X�쐬�B
    /// </summary>
    private void Awake()
    {
        if ( instance == null )
        {
            instance = this; // this ����
            DontDestroyOnLoad(this); // �V�[���J�ڎ��ɔj������Ȃ��悤�ɂ���
        }
        else
        {
            Destroy(this);
        }
    }

    /// <summary>
    /// �����Ŗ��t���[���W���u�𔭍s����B
    /// </summary>
    private void Update()
    {
        // ���t���[���W���u���s
        this.BrainJobAct();
    }

    /// <summary>
    /// �V�K�L�����N�^�[��ǉ�����B
    /// </summary>
    /// <param name="data"></param>
    /// <param name="hashCode"></param>
    /// <param name="team"></param>
    public void CharacterAdd(JobAITestStatus status, GameObject addObject)
    {
        // ����������ǉ�
        int teamNum = (int)status.baseData.initialBelong;
        int hashCode = addObject.GetHashCode();

        // �L�����f�[�^��ǉ����A�G�΂���w�c�̃w�C�g���X�g�ɂ������B
        _ = this.charaDataDictionary.AddByHash(hashCode, new CharacterData(status, addObject));

        for ( int i = 0; i < (int)CharacterSide.�w��Ȃ�; i++ )
        {
            if ( teamNum == i )
            {
                continue;
            }

            // �G�΃`�F�b�N
            if ( this.CheckTeamHostility(i, teamNum) )
            {
                // �ЂƂ܂��w�C�g�̏����l��10�Ƃ���B
                this.teamHate.Add(new int2(i, hashCode), 10);
            }
        }
    }

    /// <summary>
    /// �ޏ�L�����N�^�[���폜����B
    /// Dispose()���Ă邩��A�w�c�ύX�̐Q�Ԃ菈���Ƃ��Ŏg���܂킳�Ȃ��悤�ɂ�
    /// </summary>
    /// <param name="hashCode"></param>
    /// <param name="team"></param>
    public void CharacterDead(int hashCode, CharacterSide team)
    {

        // �폜�̑O�ɒl����������B
        this.charaDataDictionary[hashCode].Dispose();

        // �L�����f�[�^���폜���A�G�΂���w�c�̃w�C�g���X�g����������B
        _ = this.charaDataDictionary.RemoveByHash(hashCode);

        for ( int i = 0; i < (int)CharacterSide.�w��Ȃ�; i++ )
        {
            int2 checkTeam = new(i, hashCode);

            // �܂ނ����`�F�b�N
            if ( this.teamHate.ContainsKey(checkTeam) )
            {
                _ = this.teamHate.Remove(checkTeam);
            }
        }

        // ������L�����ɕR�Â����C�x���g���폜�B
        // ���S�Ƀ��[�v���ō폜���邽�߂Ɍ�납��O�ւƃ��[�v����B
        for ( int i = this.eventContainer.Length - 1; i > 0; i-- )
        {
            // ������L�����Ƀn�b�V������v����Ȃ�B
            if ( this.eventContainer[i].targetHash == hashCode )
            {
                this.eventContainer.RemoveAtSwapBack(i);
            }
        }
    }

    /// <summary>
    /// ��̃`�[�����G�΂��Ă��邩���`�F�b�N���郁�\�b�h�B
    /// </summary>
    /// <param name="team1"></param>
    /// <param name="team2"></param>
    /// <returns></returns>
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    private bool CheckTeamHostility(int team1, int team2)
    {
        return (relationMap[team1] & (1 << team2)) > 0;
    }

    /// <summary>
    /// NativeContainer���폜����B
    /// </summary>
    public void Dispose()
    {
        for ( int i = 0; i < 3; i++ )
        {
            this.charaDataDictionary[i].Dispose();
            this.teamHate.Dispose();
        }

        this.eventContainer.Dispose();
        this.teamHate.Dispose();
        relationMap.Dispose();

        Destroy(instance);
    }

    /// <summary>
    /// ���t���[���W���u�����s����B
    /// </summary>
    private void BrainJobAct()
    {
        // �L�����̐��B
        int _characterCount = this.charaDataDictionary.Count;

        // �W���u�̏����Ώۃf�[�^�̕������B
        int _jobBatchCount;

        //  �L�����N�^�[���ɑ΂��ăo�b�`�J�E���g�̍œK��
        if ( _characterCount <= 32 )
        {
            _jobBatchCount = 1;
        }
        else if ( _characterCount <= 128 )
        {
            _jobBatchCount = 16;
        }
        else if ( _characterCount <= 512 )
        {
            _jobBatchCount = 64;
        }
        else // 513�`1000
        {
            _jobBatchCount = 128;
        }

        AITestJob brainJob = new()
        {
            // �f�[�^�̈����n���B
            relationMap = relationMap,
            characterData = this.charaDataDictionary.GetInternalList1ForJob(),
            teamHate = this.teamHate,
            nowTime = GameManager.instance.NowTime,
            judgeResult = this.judgeResult
        };

        // �W���u���s�B
        JobHandle handle = brainJob.Schedule(brainJob.characterData.Length, _jobBatchCount);

        // �W���u�̊�����ҋ@
        handle.Complete();

    }

}
