using Unity.Jobs;
using UnityEngine;
using Unity.Collections;
using static JTestAIBase;
using Unity.Collections.LowLevel.Unsafe;
using static CombatManager;
using System.ComponentModel;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using System.Runtime.CompilerServices;
using static JobAITestStatus;

/// <summary>
/// AI�����f���s��Job
/// ����Ƃ��Ă̓w�C�g���f�i�����ň�ԑ������c�͏o���Ă����j���s�����f���Ώېݒ�i�U��/�h��̏ꍇ�w�C�g�A����ȊO�̏ꍇ�͔C�ӏ�����D�揇�ɔ��f�j
/// �w�C�g�����̓`�[���w�C�g����ԍ������w�c���Ƃɏo���Ă����āA�l�w�C�g�������炻��𒴂��邩�A�Ō��Ă�������
/// </summary>
public class AITestJob : IJobParallelFor
{
    /// <summary>
    /// �ǂݎ���p�̃`�[�����Ƃ̑S�̃w�C�g
    /// </summary>
    [ReadOnly]
    public NativeArray<NativeHashMap<int, int>> teamHate;

    /// <summary>
    /// CharacterDataDic�̕ϊ���
    /// </summary>
    [Unity.Collections.ReadOnly]
    public UnsafeList<CharacterData> characterData;

    /// <summary>
    /// ���ݎ���
    /// </summary>
    public float nowTime;

    /// <summary>
    /// �s������f�[�^�B
    /// �^�[�Q�b�g�ύX�̔��f�Ƃ����S���������ł��B
    /// </summary>
    [WriteOnly]
    public UnsafeList<MovementInfo> judgeResult;

    /// <summary>
    /// �v���C���[�A�G�A���̑��A���ꂼ�ꂪ�G�΂��Ă���w�c���r�b�g�ŕ\���B
    /// �L�����f�[�^�̃`�[���ݒ�ƈꏏ�Ɏg��
    /// </summary>
    [ReadOnly]
    public NativeArray<int> relationMap;

    /// <summary>
    /// characterData��judgeResult�̃C���f�b�N�X���x�[�X�ɏ�������B
    /// </summary>
    /// <param name="index"></param>
    public void Execute(int index)
    {
        // ���ʂ̍\���̂��쐬�B
        MovementInfo resultData = new MovementInfo();

        // ���݂̍s���̃X�e�[�g�𐔒l�ɕϊ�
        int nowMode = (int)characterData[index].liveData.actState;

        // ���f���Ԃ��o�߂��������m�F�B
        // �o�߂��ĂȂ��Ȃ珈�����Ȃ��B
        if ( nowTime - characterData[index].lastJudgeTime < characterData[index].brainData[nowMode].judgeInterval )
        {
            resultData.result = 0;

            // �ړ��������f�����͂���B
            if ( nowTime - characterData[index].lastMoveJudgeTime < characterData[index].moveJudgeInterval )
            {

            }

            // ���ʂ�ݒ�B
            judgeResult[index] = resultData;

            return;
        }

        // characterData[index].brainData[nowMode].judgeInterval �݂����Ȓl�͉�����g���Ȃ�ꎞ�ϐ��ɕۑ����Ă����B


        int maxHate = -1; // ��ԃw�C�g��������L�^�B�t�B���^�����O����

        // �܂����f���Ԃ̌o�߂��m�F
        // ���ɐ��`�T���ōs���Ԋu�̊m�F���s���A�G�ɂ̓w�C�g���f���s���B
        // �S�Ă̍s���������m�F���A�ǂ̏������m�肵������z��ɓ����
        // ���Ȃ݂�0�A�܂��ԗD��x�������ݒ肪���Ă͂܂����ꍇ�͗L�������킳�����[�v���f�B
        // �t�ɉ������Ă͂܂�Ȃ������ꍇ�͕⌇���������s�B
        // ���Ȃ݂ɓ����A�Ƃ��x���A�̃��[�h������܂萶�����ĂȂ���ȁB
        // ���[�h���Ƃɏ����ݒ�ł���悤�ɂ��邩�B
        // �ŁA����������Ȃ����[�h�ɂ��Ă͍s�����f���Ȃ�

        // �m�F�Ώۂ̏����̓r�b�g�l�ŕۑ��B
        // �����ăr�b�g�̐ݒ肪��������̂݊m�F����B
        // ��������������r�b�g�͏����B
        // ����ɁA1�Ƃ�2�ԖڂƂ��̂��D��x�������������t�����炻��ȉ��̏����͑S�������B
        // �ŁA���i�K�ň�ԗD��x��������������������ۑ����Ă���
        // ���̏�ԂōŌ�܂ő������ăw�C�g�l�ݒ����������B
        // ���Ȃ݂Ƀw�C�g�l�ݒ�͎������w�C�g�����Ă鑊��̃w�C�g�𑫂����l���m�F���邾������
        // �w�C�g�����̎d�g�ݍl���Ȃ��ƂȁB30�p�[�Z���g�����炷�H�@���[�[�[

        // �s�������̒��őO��𖞂��������̂��擾����r�b�g
        // �Ȃ��A���ۂ̔��f���ɂ��D��I�ȏ������������ꂽ�ꍇ�͏�ʃr�b�g�͂܂Ƃ߂ď����B
        int enableCondition = 0;

        // �O��ƂȂ鎩���ɂ��ẴX�L�b�v�������m�F�B
        for ( int i = 0; i < characterData[index].brainData[nowMode].actCondition.Length; i++ )
        {
            if ( characterData[index].brainData[nowMode].actCondition[i].skipData.skipCondition == JobAITestStatus.SkipJudgeCondition.�����Ȃ� )
            {
                enableCondition &= 1 << i;
            }

            SkipJudgeData skipData = characterData[index].brainData[nowMode].actCondition[i].skipData;

            if ( skipData.skipCondition == SkipJudgeCondition.������HP����芄���̎� )
            {

            }
            else if ( skipData.skipCondition == SkipJudgeCondition.������MP����芄���̎� )
            {

            }

        }

        // �����𖞂������s���̒��ōł��D��I�Ȃ��́B
        int selectmove;

        for ( int i = 0; i < characterData.Length; i++ )
        {

        }

        // ���̌�A���ڂ̃��[�v�ŏ����ɓ��Ă͂܂�L������T���B
        // ���ڂōςނ��ȁH�@���f�����̐������T���Ȃ��ƃ_������Ȃ��H
        // �����p�̃W���u�ň�ԍU���͂�����/�Ⴂ�A�Ƃ��̃L������w�c���ƂɒT���Ƃ��ׂ�����Ȃ��H
        // ����͖��m�ɂ��ׂ��B
        // ����A�ł��Ώۂ�����������Ńt�B���^�����O����Ȃ����ς�_������
        // ��l�����������Ƃɐ��`���邩�B
        // �~���ƂȂ�̂́A

        // �����Ɋւ��Ă͕ʏ�������������ƌ��߂��B
        // kd�؂��ԕ����f�[�^�\���Ƃ�����݂��������ǁA�X�V���דI�ɂ��܂������p�I����Ȃ��C������B
        // �œK�ȓG���͈̔͂��قȂ邩��
        // ������͋ߋ����̕����Z���T�[�Ő��b�Ɉ�񌟍��������������BNonalloc�n�̃T�[�`�Ńo�b�t�@�� stack alloc���g����
        // �G�S�̈ȏ㑝�₷�Ȃ�g���K�[�͂܂�������

        // ���ʂ�ݒ�B
        judgeResult[index] = resultData;
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
}
