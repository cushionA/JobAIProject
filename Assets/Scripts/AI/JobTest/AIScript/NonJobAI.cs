using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using static JTestAIBase;
using static CombatManager;
using static JobAITestStatus;
using static AiFunctionLibrary;
using System.Runtime.CompilerServices;
using Unity.Burst;

/// <summary>
/// AITestJob�̔�JobSystem�Ŏ���
/// </summary>
public struct NonJobAI
{
    /// <summary>
    /// �ǂݎ���p�̃`�[�����Ƃ̑S�̃w�C�g
    /// </summary>
    public NativeArray<NativeHashMap<int, int>> teamHate;

    /// <summary>
    /// CharacterDataDic�̕ϊ���
    /// </summary>
    public UnsafeList<CharacterData> characterData;

    /// <summary>
    /// ���ݎ���
    /// </summary>
    public float nowTime;

    /// <summary>
    /// �s������f�[�^�B
    /// �^�[�Q�b�g�ύX�̔��f�Ƃ����S���������ł��B
    /// </summary>
    public UnsafeList<MovementInfo> judgeResult;

    /// <summary>
    /// �v���C���[�A�G�A���̑��A���ꂼ�ꂪ�G�΂��Ă���w�c���r�b�g�ŕ\���B
    /// �L�����f�[�^�̃`�[���ݒ�ƈꏏ�Ɏg��
    /// </summary>
    public NativeArray<int> relationMap;

    /// <summary>
    /// �֐��|�C���^�̔z�񂩂珈������
    /// </summary>
    public NativeArray<FunctionPointer<SkipJudgeDelegate>> skipFunctions;

    /// <summary>
    /// �֐��|�C���^�̔z�񂩂珈������
    /// </summary>
    public NativeArray<FunctionPointer<TargetJudgeDelegate>> targetFunctions;

    /// <summary>
    /// AI�̔��f�����s����
    /// </summary>
    public void ExecuteAIDecision()
    {
        // �e�L�����N�^�[�ɂ���AI���f�����s
        for ( int index = 0; index < characterData.Length; index++ )
        {
            // ���ʂ̍\���̂��쐬
            MovementInfo resultData = new MovementInfo();

            // ���݂̍s���̃X�e�[�g�𐔒l�ɕϊ�
            int nowMode = (int)characterData[index].liveData.actState;

            // ���f���Ԃ��o�߂��������m�F
            // �o�߂��ĂȂ��Ȃ珈�����Ȃ�
            if ( nowTime - characterData[index].lastJudgeTime < characterData[index].brainData[nowMode].judgeInterval )
            {
                resultData.result = JudgeResult.�����Ȃ�;
                judgeResult[index] = resultData;
                continue;
            }

            // �s�������̒��őO��𖞂��������̂��擾����r�b�g
            int enableCondition = 0;

            // �O��ƂȂ鎩���ɂ��ẴX�L�b�v�������m�F
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

            // �����𖞂������s���̒��ōł��D��I�Ȃ���
            // �����l�͍Ō�̏����A�܂�����Ȃ��̕⌇����
            int selectMove = characterData[index].brainData[nowMode].actCondition.Length - 1;

            // �L�����f�[�^���m�F����
            for ( int i = 0; i < characterData.Length; i++ )
            {
                // �����̓X�L�b�v
                if ( index == i )
                {
                    continue;
                }

                // �s�����f
                if ( enableCondition != 0 )
                {
                    for ( int j = 0; j < characterData[index].brainData[nowMode].actCondition.Length - 1; j++ )
                    {
                        // �����������������break���āA�ȍ~�͂���ȉ��̏����������Ȃ�
                        if ( CheckActCondition(
                            characterData[index].brainData[nowMode].actCondition[j],
                            characterData[index],
                            characterData[i],
                            teamHate) )
                        {
                            selectMove = i;

                            // enableCondition��bit������
                            // i���ڂ܂ł̃r�b�g�����ׂ�1�ɂ���}�X�N���쐬
                            int mask = (1 << j) - 1;

                            // �}�X�N�ƌ��̒l�̘_���ς���邱�Ƃŏ�ʃr�b�g���N���A
                            enableCondition = enableCondition & mask;
                            break;
                        }
                    }
                }
                // �������������烋�[�v�I���B
                else
                {
                    break;
                }
            }

            // �ł������ɋ߂��^�[�Q�b�g���m�F����
            // ��r�p�����l��Invert�ɂ���ĕϓ�
            TargetJudgeData targetJudgeData = characterData[index].brainData[nowMode].actCondition[selectMove].targetCondition;
            int nowValue = targetJudgeData.isInvert ? int.MaxValue : int.MinValue;
            int newTargetHash = 0;

            // ��ԕύX�̏ꍇ�����Ŗ߂�
            if ( targetJudgeData.judgeCondition == TargetSelectCondition.�s�v_��ԕύX )
            {
                // �w���ԂɈڍs
                resultData.result = JudgeResult.�V�������f������;
                resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;

                // ���f���ʂ�ݒ�
                judgeResult[index] = resultData;
                continue;
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
                    // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�
                    if ( i == index || !targetJudgeData.filter.IsPassFilter(characterData[i]) )
                    {
                        continue;
                    }

                    // �}���n�b�^�������ŉ��ߔ��f
                    float distance = Math.Abs(myPositionX - characterData[i].liveData.nowPosition.x) +
                                    Math.Abs(myPositionY - characterData[i].liveData.nowPosition.y);

                    // ��ԍ���������߂�
                    if ( !targetJudgeData.isInvert )
                    {
                        if ( distance > nowValue )
                        {
                            nowValue = (int)distance;
                            newTargetHash = characterData[i].hashCode;
                        }
                    }
                    // ��ԒႢ������߂�
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
            else if ( targetJudgeData.judgeCondition == TargetSelectCondition.�v���C���[ )
            {
                // �v���C���[�̃n�b�V���̓V���O���g������擾����z��
                // newTargetHash = characterData[i].hashCode;
            }
            else if ( targetJudgeData.judgeCondition == TargetSelectCondition.�w��Ȃ�_�w�C�g�l )
            {
                // �^�[�Q�b�g�I�胋�[�v
                for ( int i = 0; i < characterData.Length; i++ )
                {
                    // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�
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

                    // ��ԍ���������߂�
                    if ( !targetJudgeData.isInvert )
                    {
                        if ( targetHate > nowValue )
                        {
                            nowValue = targetHate;
                            newTargetHash = characterData[i].hashCode;
                        }
                    }
                    // ��ԒႢ������߂�
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
                    if ( targetFunctions[(int)targetJudgeData.judgeCondition].Invoke(targetJudgeData, characterData[i], ref nowValue) )
                    {
                        newTargetHash = characterData[i].hashCode;
                    }
                }
            }

            // �����Ń^�[�Q�b�g�������ĂȂ���Αҋ@�Ɉڍs
            if ( newTargetHash == 0 )
            {
                // �ҋ@�Ɉڍs
                resultData.result = JudgeResult.�V�������f������;
                resultData.actNum = (int)ActState.�ҋ@;
            }

            // ���f���ʂ�ݒ�
            judgeResult[index] = resultData;
        }
    }

    /// <summary>
    /// �s���������`�F�b�N����
    /// </summary>
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool CheckActCondition(
        in BehaviorData condition,
        in CharacterData myData,
        in CharacterData targetData,
        NativeArray<NativeHashMap<int, int>> teamHate)
    {
        bool result = true;

        // �t�B���^�[�ʉ߂��Ȃ��Ȃ�߂�
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
                // ���̋����Ŕ��肷��
                int judgeDist = condition.actCondition.judgeValue * condition.actCondition.judgeValue;

                // ���̋����̓��
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
}