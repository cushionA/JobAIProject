using Cysharp.Threading.Tasks;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using static JobAITestStatus;
using static JTestAIBase;

/// <summary>
/// ���̃t���[���őS�̂ɓK�p����C�x���g���X�g�A�݂����Ȃ̂������āA�����ɃL�����N�^�[���f�[�^��n����悤�ɂ��邩
/// Dispose�K�{�BAI�֘A��Dispose()�͂����ł��ӔC������B
/// </summary>
public class CombatManager : MonoBehaviour, IDisposable
{

    #region ��`

    /// <summary>
    /// AI�̃C�x���g�̃^�C�v�B
    /// �`�[���̃w�C�g�������邩�A�C�x���g�t���O�𗧂Ă邩�̂ǂ�����
    /// �e�L�������`�[���̐��Ɠ��������C�x���g�t���O���ꕨ������āA�C�x���g�������Ƀr�b�g���Z�Ŕ��f����B
    /// �|�����n�̃t���O�͌��j���̃`�[���w�C�g�㏸�őΉ�����B
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
        �U���Ώێw�� = 1 << 5,        // �w�����ɂ��U���Ώۂ̎w��
        �Ј� = 1 << 6,//�Ј���Ԃ��ƓG���|����H ����̓o�b�h�X�e�[�^�X�ł������Ƃ͎v��
    }

    /// <summary>
    /// AI�̃C�x���g�̑��M��B
    /// ������V���O���g���ɓn����Job�V�X�e���ŏ������Ă����B
    /// ���Ԍo�߂����炻�̃L��������t���O���������߂̐ݒ�B�������߂̋L�^������B
    /// �C�x���g�ǉ����ɑΏۃL�����Ƀt���O��ݒ肵�A�������ɏ����B
    /// ���Ԍo�߂̑��A�L�������S���������ɖ₢���킹�Ȃ��ƂȁB
    /// �n�b�V������v����C�x���g��S�T�����č폜�B
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
            eventType = brainEvent;
            targetHash = hashCode;
            startTime = GameManager.instance.NowTime;
            eventHoldTime = holdTime;
        }

    }

    #endregion ��`

    /// <summary>
    /// �V���O���g���B
    /// </summary>
    public static CombatManager instance;

    /// <summary>
    /// �L�����f�[�^��ێ�����R���N�V�����B
    /// �w�c���Ƃɕ�����
    /// Job�V�X�e���ɓn�����͏�肭CharacterData��NativeArray�ɂ��Ă�낤�B
    /// </summary>
    public CharacterDataDictionary<CharacterData, JTestAIBase> charaDataDictionary = new CharacterDataDictionary<CharacterData, JTestAIBase>(7);

    /// <summary>
    /// �v���C���[�A�G�A���̑��A���ꂼ�ꂪ�G�΂��Ă���w�c���r�b�g�ŕ\���B
    /// �L�����f�[�^�̃`�[���ݒ�ƈꏏ�Ɏg��
    /// </summary>
    public static NativeArray<int> relationMap = new Unity.Collections.NativeArray<int>(3, Allocator.Persistent);

    /// <summary>
    /// �w�c���Ƃɐݒ肳�ꂽ�w�C�g�l�B
    /// �n�b�V���L�[�ɂ̓Q�[���I�u�W�F�N�g�̃n�b�V���l��n���B
    /// �z��̗v�f���ƂɃW���u�����s
    /// ���₽�Ԃ�W���u�V�X�e������Ȃ��ăn�b�V���ŌʂɑΏۃI�u�W�F�N�g�Ƀw�C�g�ݒ肵����������
    /// �C�x���g�̐������n�b�V���}�b�v�����[�v���āA
    /// </summary>
    public NativeArray<NativeHashMap<int, int>> teamHate = new NativeArray<NativeHashMap<int, int>>(3, Allocator.Persistent);

    /// <summary>
    /// AI�̃C�x���g���󂯕t������ꕨ�B
    /// ���ԊǗ��̂��߂Ɏg���B
    /// Job�V�X�e���ňꊇ�Ŏ��Ԍ��邩�A���ʂɃ��[�v���邩�i�C�x���g�͂���Ȃɐ����Ȃ������������ʂ����������j
    /// </summary>
    public UnsafeList<BrainEventContainer> eventContainer = new UnsafeList<BrainEventContainer>(7, Allocator.Persistent);

    /// <summary>
    /// �s������f�[�^�B
    /// Job�̏������ݐ�ŁA�^�[�Q�b�g�ύX�̔��f�Ƃ����S����������ƂɎ󂯎�����L�����N�^�[�����B
    /// </summary>
    public UnsafeList<MovementInfo> judgeResult = new UnsafeList<MovementInfo>(7, Allocator.Persistent);

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


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // HashMap�̏�����
        for ( int i = 0; i < teamHate.Length; i++ )
        {
            teamHate[i] = new NativeHashMap<int, int>(7, Allocator.Persistent);
        }
    }

    /// <summary>
    /// �����Ŗ��t���[���W���u�𔭍s����B
    /// </summary>
    void Update()
    {
        // ���t���[���W���u���s
        BrainJobAct();
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
        charaDataDictionary.AddByHash(hashCode, new CharacterData(status, addObject));

        for ( int i = 0; i < teamHate.Length; i++ )
        {
            if ( teamNum == i )
            {
                continue;
            }

            // �G�΃`�F�b�N
            if ( CheckTeamHostility(i, teamNum) )
            {
                // �ЂƂ܂��w�C�g�̏����l��10�Ƃ���B
                teamHate[i].Add(hashCode, 10);
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
        int teamNum = (int)team;

        // �폜�̑O�ɒl����������B
        charaDataDictionary[hashCode].Dispose();

        // �L�����f�[�^���폜���A�G�΂���w�c�̃w�C�g���X�g����������B
        charaDataDictionary.RemoveByHash(hashCode);

        for ( int i = 0; i < teamHate.Length; i++ )
        {
            // �܂ނ����`�F�b�N
            if ( teamHate[i].ContainsKey(hashCode) )
            {
                teamHate[i].Remove(hashCode);
            }
        }

        // �������ɕR�Â����C�x���g���폜�B
        // ���S�Ƀ��[�v���ō폜���邽�߂Ɍ�납��O�ւƃ��[�v����B
        for ( int i = eventContainer.Length - 1; i > 0; i-- )
        {
            // �������Ƀn�b�V������v����Ȃ�B
            if ( eventContainer[i].targetHash == hashCode )
            {
                eventContainer.RemoveAtSwapBack(i);
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
        return (relationMap[team1] & 1 << team2) > 0;
    }

    /// <summary>
    /// NativeContainer���폜����B
    /// </summary>
    public void Dispose()
    {
        for ( int i = 0; i < 3; i++ )
        {
            charaDataDictionary[i].Dispose();
            teamHate[i].Dispose();

        }
        eventContainer.Dispose();
        teamHate.Dispose();
        relationMap.Dispose();

        // �f���Q�[�g�z����ӔC�������Ĕj��
        AiFunctionLibrary.targetFunctions.Dispose();
        AiFunctionLibrary.skipFunctions.Dispose();

        Destroy(instance);
    }

    /// <summary>
    /// �W���u�����s����B
    /// </summary>
    private void BrainJobAct()
    {
        AITestJob brainJob = new AITestJob
        {
            // �f�[�^�̈����n���B
            relationMap = relationMap,
            characterData = this.charaDataDictionary.GetInternalList1ForJob(),
            teamHate = this.teamHate,
            targetFunctions = AiFunctionLibrary.targetFunctions,
            skipFunctions = AiFunctionLibrary.skipFunctions,
            nowTime = GameManager.instance.NowTime,
            judgeResult = this.judgeResult
        };

        JobHandle handle = brainJob.Schedule(brainJob.characterData.Length, 64);

        // �W���u�̊�����ҋ@
        handle.Complete();

    }

}
