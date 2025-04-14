using Cysharp.Threading.Tasks;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static JobAITestStatus;
using static JTestAIBase;

/// <summary>
/// ���̃t���[���őS�̂ɓK�p����C�x���g���X�g�A�݂����Ȃ̂������āA�����ɃL�����N�^�[���f�[�^��n����悤�ɂ��邩
/// Dispose�K�{
/// </summary>
public class CombatManager : MonoBehaviour, IDisposable
{

    #region ��`

    /// <summary>
    /// AI�̃C�x���g�̃^�C�v�B
    /// </summary>
    public enum AIEventType
    {
        ��_���[�W��^����,
        �񕜂�x�����g�p����,
        �L���������S����,
        �w�������|���ꂽ,
        �U���Ώێw��,// �w�����ɂ�閽��
    }

    /// <summary>
    /// AI�̃C�x���g�̑��M��B
    /// �����ɓn����Job�V�X�e���ŏ������Ă����B
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct AIEventContainer
    {

        /// <summary>
        /// �C�x���g�̃^�C�v
        /// </summary>
        public AIEventType type;

        /// <summary>
        /// �G��|������A�U�����߂̎w��ɂȂ����肷��A�w�C�g�ҏW��̃n�b�V��
        /// �C�x���g�̎�ނɂ����
        /// </summary>
        public int targetHash;

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
    public CharacterDataDictionary<CharacterData, JTestAIBase>[] charaDataDictionary = new CharacterDataDictionary<CharacterData, JTestAIBase>[3];
    /// <summary>
    /// ������Ȋ�����
    /// </summary>
    NativeArray<UnsafeList<CharacterData>> team = new NativeArray<UnsafeList<CharacterData>>(3, Allocator.Persistent);

    /// <summary>
    /// �v���C���[�A�G�A���̑��A���ꂼ�ꂪ�G�΂��Ă���w�c���r�b�g�ŕ\���B
    /// </summary>
    public static readonly int[] relationMap = new int[3];

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
    /// </summary>
    public UnsafeList<AIEventContainer> eventContainer = new UnsafeList<AIEventContainer>(7, Allocator.Persistent);

    // 

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
        charaDataDictionary[teamNum].AddByHash(hashCode, new CharacterData(status, addObject));

        for ( int i = 0; i < teamHate.Length; i++ )
        {
            if ( teamNum == i )
            {
                continue;
            }

            // �G�΃`�F�b�N
            if ( HostileCheck(i, teamNum) )
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
    public void CharacterRemove(int hashCode, CharacterSide team)
    {
        int teamNum = (int)team;

        // �폜�̑O�ɒl����������B
        charaDataDictionary[teamNum][hashCode].Dispose();

        // �L�����f�[�^���폜���A�G�΂���w�c�̃w�C�g���X�g����������B
        charaDataDictionary[teamNum].RemoveByHash(hashCode);

        for ( int i = 0; i < teamHate.Length; i++ )
        {
            // �܂ނ����`�F�b�N
            if ( teamHate[i].ContainsKey(hashCode) )
            {
                teamHate[i].Remove(hashCode);
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
    private bool HostileCheck(int team1, int team2)
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

        Destroy(instance);
    }
}
