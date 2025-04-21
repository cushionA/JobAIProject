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
using static AiFunctionLibrary;
using Unity.Burst;
using System.Runtime.InteropServices.WindowsRuntime;
using System;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using Unity.Plastic.Newtonsoft.Json.Linq;

/// <summary>
/// AI�����f���s��Job
/// ����Ƃ��Ă̓w�C�g���f�i�����ň�ԑ������c�͏o���Ă����j���s�����f���Ώېݒ�i�U��/�h��̏ꍇ�w�C�g�A����ȊO�̏ꍇ�͔C�ӏ�����D�揇�ɔ��f�j
/// �w�C�g�����̓`�[���w�C�g����ԍ������w�c���Ƃɏo���Ă����āA�l�w�C�g�������炻��𒴂��邩�A�Ō��Ă�������
/// </summary>
[BurstCompile]
public struct AITestJob : IJobParallelFor
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
    /// �֐��|�C���^�̔z�񂩂珈������
    /// </summary>
    [ReadOnly]
    public NativeArray<FunctionPointer<SkipJudgeDelegate>> skipFunctions;

    [ReadOnly]
    public NativeArray<FunctionPointer<TargetJudgeDelegate>> targetFunctions;


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
        // ���邢�̓^�[�Q�b�g�������ꍇ�����肵�����B�`�[���w�C�g�Ɋ܂܂�ĂȂ���΁B���ꂾ�Ɩ������w�C�g�̎��ǂ�����́B
        // �L�������S���ɑS�L�����ɑ΂��^�[�Q�b�g���Ă邩�ǂ������m�F����悤�ɂ��悤�B�ŁA�^�[�Q�b�g��������O�񔻒f���Ԃ��}�C�i�X�ɂ���B
        if ( nowTime - characterData[index].lastJudgeTime < characterData[index].brainData[nowMode].judgeInterval )
        {
            resultData.result = JudgeResult.�����Ȃ�;

            // �ړ��������f�����͂���B
            //�@���m�ɂ͋�������B
            // �n�b�V���l�����Ă񂾂���W���u����o����ł�낤�B
            // Result�����߂���

            // ���ʂ�ݒ�B
            judgeResult[index] = resultData;

            return;
        }

        // characterData[index].brainData[nowMode].judgeInterval �݂����Ȓl�͉�����g���Ȃ�ꎞ�ϐ��ɕۑ����Ă����B


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
        // �w�C�g�����̎d�g�ݍl���Ȃ��ƂȁB30�p�[�Z���g�����炷�H�@���[�[�[�[�[�[�[�[

        // �s�������̒��őO��𖞂��������̂��擾����r�b�g
        // �Ȃ��A���ۂ̔��f���ɂ��D��I�ȏ������������ꂽ�ꍇ�͏�ʃr�b�g�͂܂Ƃ߂ď����B
        int enableCondition = 0;

        // �O��ƂȂ鎩���ɂ��ẴX�L�b�v�������m�F�B
        // �Ō�̏����͕⌇�����Ȃ̂Ŗ���
        for ( int i = 0; i < characterData[index].brainData[nowMode].actCondition.Length - 1; i++ )
        {
            if ( characterData[index].brainData[nowMode].actCondition[i].skipData.skipCondition == JobAITestStatus.SkipJudgeCondition.�����Ȃ� )
            {
                enableCondition &= 1 << i;
            }

            SkipJudgeData skipData = characterData[index].brainData[nowMode].actCondition[i].skipData;

            // �X�L�b�v���������߂��Ĕ��f
            if ( skipFunctions[(int)skipData.skipCondition].Invoke(skipData, characterData[index]) )
            {
                enableCondition &= 1 << i;
            }

        }

        // �����𖞂������s���̒��ōł��D��I�Ȃ��́B
        // �����l�͍Ō�̏����A�܂�����Ȃ��̕⌇����
        int selectMove = characterData[index].brainData[nowMode].actCondition.Length - 1;

        // �w�C�g�����m�F�p�̈ꎞ�o�b�t�@
        NativeArray<Vector2Int> hateIndex = new NativeArray<Vector2Int>(characterData[index].brainData[nowMode].hateCondition.Length, Allocator.Temp);
        NativeArray<TargetJudgeData> hateCondition = characterData[index].brainData[nowMode].hateCondition;

        // �w�C�g�m�F�o�b�t�@�̏�����
        for ( int i = 0; i < hateIndex.Length; i++ )
        {
            if ( hateCondition[i].isInvert )
            {
                hateIndex[i].Set(int.MaxValue, -1);
            }
            else
            {
                hateIndex[i].Set(int.MinValue, -1);
            }
        }

        // �L�����f�[�^���m�F����B
        for ( int i = 0; i < characterData.Length; i++ )
        {
            // �����̓X�L�b�v
            if ( index == i )
            {
                continue;
            }

            // �܂��w�C�g���f�B
            // �e�w�C�g�����ɂ��āA�����X�V���L�^����B
            for ( int j = 0; j < hateCondition.Length; j++ )
            {
                int value = hateIndex[j].x;
                if ( targetFunctions[(int)hateCondition[j].judgeCondition].Invoke(hateCondition[j], characterData[i], ref value) )
                {
                    hateIndex[j].Set(value, i);
                }
            }

            // ���ɍs�����f�B
            // �����̓X�C�b�`���g�����B�A������Int�l�Ȃ�R���p�C�����W�����v�e�[�u������Ă����̂�
            if ( enableCondition != 0 )
            {
                for ( int j = 0; j < characterData[index].brainData[nowMode].actCondition.Length - 1; j++ )
                {
                    // �����������������break���āA�ȍ~�͂���ȉ��̏����������Ȃ��B
                    if ( CheckActCondition(characterData[index].brainData[nowMode].actCondition[j], characterData[index], characterData[i]) )
                    {
                        selectMove = i;

                        // enableCondition��bit�������B
                        // i���ڂ܂ł̃r�b�g�����ׂ�1�ɂ���}�X�N���쐬
                        // (1 << (i + 1)) - 1 �� 0���� i-1���ڂ܂ł̃r�b�g�����ׂ�1
                        int mask = (1 << i) - 1;

                        // �}�X�N�ƌ��̒l�̘_���ς���邱�Ƃŏ�ʃr�b�g���N���A
                        enableCondition = enableCondition & mask;
                        break;
                    }
                }
            }

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


        // �ł������ɋ߂��^�[�Q�b�g���m�F����B
        // ��r�p�����l��Invert�ɂ���ĕϓ��B
        TargetJudgeData targetJudgeData = characterData[index].brainData[nowMode].actCondition[selectMove].targetCondition;
        int nowValue = targetJudgeData.isInvert ? int.MaxValue : int.MinValue;
        int newTargetHash = 0;

        // ��ԕύX�̏ꍇ�����Ŗ߂�B
        if ( targetJudgeData.judgeCondition == TargetSelectCondition.�s�v_��ԕύX )
        {
            // �w���ԂɈڍs
            resultData.result = JudgeResult.�V�������f������;
            resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;

            // ���f���ʂ�ݒ�B
            judgeResult[index] = resultData;
            return;
        }

        // ����ȊO�ł���΃^�[�Q�b�g�𔻒f
        if ( targetJudgeData.judgeCondition == TargetSelectCondition.���� )
        {
            // �����̈ʒu���L���b�V��
            float myPositionX = characterData[index].liveData.nowPosition.x;
            float myPositionY = characterData[index].liveData.nowPosition.y;

            // �^�[�Q�b�g�I�胋�[�v
            for ( int i = 0; i < characterData.Length; i++ )
            {
                // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( i == index || !targetJudgeData.filter.IsPassFilter(characterData[i]) )
                {
                    continue;
                }

                // �}���n�b�^�������ŉ��ߔ��f
                float distance = Math.Abs(myPositionX - characterData[i].liveData.nowPosition.x) + Math.Abs(myPositionY - characterData[i].liveData.nowPosition.y);

                // ��ԍ���������߂�B
                if ( !targetJudgeData.isInvert )
                {
                    if ( distance > nowValue )
                    {
                        nowValue = (int)distance;
                        newTargetHash = characterData[i].hashCode;
                    }
                }
                // ��ԒႢ������߂�B
                else
                {
                    if ( distance < nowValue )
                    {
                        nowValue = (int)distance;
                        newTargetHash = characterData[i].hashCode;
                    }
                }

            }
        }
        else if ( targetJudgeData.judgeCondition == TargetSelectCondition.���� )
        {
            newTargetHash = characterData[index].hashCode;
        }
        // ��������̃V���O���g���Ƀv���C���[��Hash�͎������Ƃ�
        else if ( targetJudgeData.judgeCondition == TargetSelectCondition.�v���C���[ )
        {
            // newTargetHash = characterData[i].hashCode;
        }
        else if ( targetJudgeData.judgeCondition == TargetSelectCondition.�w��Ȃ�_�w�C�g�l )
        {
            // �^�[�Q�b�g�I�胋�[�v
            for ( int i = 0; i < characterData.Length; i++ )
            {
                // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( i == index || !targetJudgeData.filter.IsPassFilter(characterData[i]) )
                {
                    continue;
                }

                // �w�C�g�l���m�F
                int targetHash = characterData[i].hashCode;
                int targetHate = 0;

                if ( characterData[index].personalHate.ContainsKey(targetHash) )
                {
                    targetHate += (int)characterData[index].personalHate[targetHash];
                }

                if ( teamHate[(int)characterData[index].liveData.belong].ContainsKey(targetHash) )
                {
                    targetHate += teamHate[(int)characterData[index].liveData.belong][targetHash];
                }

                // ��ԍ���������߂�B
                if ( !targetJudgeData.isInvert )
                {
                    if ( targetHate > nowValue )
                    {
                        nowValue = targetHate;
                        newTargetHash = characterData[i].hashCode;
                    }
                }
                // ��ԒႢ������߂�B
                else
                {
                    if ( targetHate < nowValue )
                    {
                        nowValue = targetHate;
                        newTargetHash = characterData[i].hashCode;
                    }
                }

            }
        }
        // �ʏ�̃^�[�Q�b�g�I��
        else
        {

            // �^�[�Q�b�g�I�胋�[�v
            for ( int i = 0; i < characterData.Length; i++ )
            {
                // �n�b�V�����擾
                // �ق�Ƃ͂����Ń^�[�Q�b�g�J�E���g�ő_���߂��̂�e������������
                if ( targetFunctions[(int)targetJudgeData.judgeCondition].Invoke(targetJudgeData, characterData[i], ref nowValue) )
                {
                    newTargetHash = characterData[i].hashCode;
                }
            }
        }

        // �����Ń^�[�Q�b�g�������ĂȂ���Αҋ@�Ɉڍs�B
        if ( newTargetHash == 0 )
        {
            // �ҋ@�Ɉڍs
            resultData.result = JudgeResult.�V�������f������;
            resultData.actNum = (int)ActState.�ҋ@;
        }

        // ���f���ʂ�ݒ�B
        judgeResult[index] = resultData;

        // �e�X�g�d�l�L�^
        // �v�f����10 �` 1000��
        // �X�e�[�^�X�͂������x�[�X�ƂȂ�e���v����CharacterData����āA���̐��l��������R�[�h�����Ă��B
        // �ŁAJob�V�X�e�����܂�܃x�^�ڐA�������ʂ̃N���X���쐬���āA���x���r
        // �Ō�͓�̃e�X�g�ɂ��쐬���ꂽpublic UnsafeList<MovementInfo> judgeResult�@�̓��ꐫ�������ɂ񂵂āA���x�̃`�F�b�N�܂ŏI���

    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="conditions"></param>
    /// <param name="charaData"></param>
    /// <param name="nowHate"></param>
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public bool CheckActCondition(in BehaviorData condition, in CharacterData myData, in CharacterData targetData)
    {
        bool result = true;

        // �t�B���^�[�ʉ߂��Ȃ��Ȃ�߂�B
        if ( !condition.actCondition.filter.IsPassFilter(targetData) )
        {
            return false;
        }

        switch ( condition.actCondition.judgeCondition )
        {
            case ActJudgeCondition.�w��̃w�C�g�l�̓G�����鎞:

                int targetHash = targetData.hashCode;
                int targetHate = 0;

                if ( myData.personalHate.ContainsKey(targetHash) )
                {
                    targetHate += myData.personalHate[targetHash];
                }

                if ( teamHate[(int)myData.liveData.belong].ContainsKey(targetHash) )
                {
                    targetHate += teamHate[(int)myData.liveData.belong][targetHash];
                }

                // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                if ( !condition.actCondition.isInvert )
                {
                    result = targetHate >= condition.actCondition.judgeValue;
                }
                else
                {
                    result = targetHate <= condition.actCondition.judgeValue;
                }

                return result;

            // �W�v�͔p�~
            //case ActJudgeCondition.�Ώۂ���萔�̎�:
            //    return HasRequiredNumberOfTargets(context);

            case ActJudgeCondition.HP����芄���̑Ώۂ����鎞:

                // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                if ( !condition.actCondition.isInvert )
                {
                    result = targetData.liveData.hpRatio >= condition.actCondition.judgeValue;
                }
                else
                {
                    result = targetData.liveData.hpRatio <= condition.actCondition.judgeValue;
                }

                return result;

            case ActJudgeCondition.MP����芄���̑Ώۂ����鎞:

                // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                if ( !condition.actCondition.isInvert )
                {
                    result = targetData.liveData.mpRatio >= condition.actCondition.judgeValue;
                }
                else
                {
                    result = targetData.liveData.mpRatio <= condition.actCondition.judgeValue;
                }
                return result;

            case ActJudgeCondition.�ݒ苗���ɑΏۂ����鎞:

                // ���̋����Ŕ��肷��B
                int judgeDist = condition.actCondition.judgeValue * condition.actCondition.judgeValue;

                // ���̋����̓��B
                int distance = (int)(Mathf.Pow(targetData.liveData.nowPosition.x - myData.liveData.nowPosition.x, 2) +
                               Mathf.Pow(targetData.liveData.nowPosition.y - myData.liveData.nowPosition.y, 2));

                // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                if ( !condition.actCondition.isInvert )
                {
                    result = distance >= judgeDist;
                }
                else
                {
                    result = distance <= judgeDist;
                }
                return result;

            case ActJudgeCondition.����̑����ōU������Ώۂ����鎞:

                // �ʏ�͂��鎞�A�t�̏ꍇ�͂��Ȃ��Ƃ�
                if ( !condition.actCondition.isInvert )
                {
                    result = ((int)targetData.solidData.attackElement & condition.actCondition.judgeValue) > 0;
                }
                else
                {
                    result = ((int)targetData.solidData.attackElement & condition.actCondition.judgeValue) == 0;
                }
                return result;

            case ActJudgeCondition.����̐��̓G�ɑ_���Ă��鎞:
                // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                if ( !condition.actCondition.isInvert )
                {
                    result = targetData.targetingCount >= condition.actCondition.judgeValue;
                }
                else
                {
                    result = targetData.targetingCount <= condition.actCondition.judgeValue;
                }
                return result;

            default: // �����Ȃ� (0) �܂��͖���`�̒l
                return result;
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
}
