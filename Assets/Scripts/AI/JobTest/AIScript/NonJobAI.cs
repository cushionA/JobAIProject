using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using static JTestAIBase;
using static CombatManager;
using static JobAITestStatus;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

/// <summary>
/// AITestJob�̔�JobSystem�Ŏ���
/// </summary>
public struct NonJobAI
{
    /// <summary>
    /// �ǂݎ���p�̃`�[�����Ƃ̑S�̃w�C�g
    /// </summary>
    public NativeHashMap<int2, int> teamHate;

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

                SkipJudgeData skipData = characterData[index].brainData[nowMode].actCondition[i].skipData;

                // �X�L�b�v���������߂��Ĕ��f
                if ( skipData.skipCondition == SkipJudgeCondition.�����Ȃ� || JudgeSkipByCondition(skipData, characterData[index]) == 1 )
                {
                    enableCondition |= 1 << i;
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
                            selectMove = j;

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
            int nowValue = targetJudgeData.isInvert == BitableBool.TRUE ? int.MaxValue : int.MinValue;
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
                    if ( i == index || targetJudgeData.filter.IsPassFilter(characterData[i]) == 0 )
                    {
                        continue;
                    }

                    // �}���n�b�^�������ŉ��ߔ��f
                    float distance = Math.Abs(myPositionX - characterData[i].liveData.nowPosition.x) +
                                    Math.Abs(myPositionY - characterData[i].liveData.nowPosition.y);

                    // ��ԍ���������߂�
                    if ( targetJudgeData.isInvert == BitableBool.FALSE )
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
                    if ( i == index || targetJudgeData.filter.IsPassFilter(characterData[i]) == 0 )
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

                    int2 hateKey = new int2((int)characterData[index].liveData.belong, targetHash);

                    if ( teamHate.ContainsKey(hateKey) )
                    {
                        targetHate += teamHate[hateKey];
                    }

                    // ��ԍ���������߂�
                    if ( targetJudgeData.isInvert == BitableBool.FALSE )
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
                    if ( JudgeTargetByCondition(targetJudgeData, characterData[i], ref nowValue) == 1 )
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
                return;
            }

            resultData.result = JudgeResult.�V�������f������;
            resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;
            resultData.targetHash = newTargetHash;

            // ���f���ʂ�ݒ�
            judgeResult[index] = resultData;
        }
    }

    #region �X�L�b�v�������f

    /// <summary>
    /// SkipJudgeCondition�Ɋ�Â��Ĕ�����s�����\�b�h
    /// </summary>
    /// <param name="skipData">�X�L�b�v����p�f�[�^</param>
    /// <param name="charaData">�L�����N�^�[�f�[�^</param>
    /// <returns>�����ɍ��v����ꍇ��1�A����ȊO��0</returns>
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int JudgeSkipByCondition(in SkipJudgeData skipData, in CharacterData charaData)
    {
        SkipJudgeCondition condition = skipData.skipCondition;
        switch ( condition )
        {
            case SkipJudgeCondition.������HP����芄���̎�:
                // �e�������ʂ� int �ŕ]��
                int equalConditionHP = skipData.judgeValue == charaData.liveData.hpRatio ? 1 : 0;
                int lessConditionHP = skipData.judgeValue < charaData.liveData.hpRatio ? 1 : 0;
                int invertConditionHP = skipData.isInvert == BitableBool.TRUE ? 1 : 0;
                // �����I�ɏ�����g�ݍ��킹��
                int condition1HP = equalConditionHP;
                int condition2HP = (lessConditionHP != 0) == (invertConditionHP != 0) ? 1 : 0;
                if ( condition1HP != 0 || condition2HP != 0 )
                {
                    return 1;
                }
                return 0;

            case SkipJudgeCondition.������MP����芄���̎�:
                // �e�������ʂ� int �ŕ]��
                int equalConditionMP = skipData.judgeValue == charaData.liveData.mpRatio ? 1 : 0;
                int lessConditionMP = skipData.judgeValue < charaData.liveData.mpRatio ? 1 : 0;
                int invertConditionMP = skipData.isInvert == BitableBool.TRUE ? 1 : 0;
                // �����I�ɏ�����g�ݍ��킹��
                int condition1MP = equalConditionMP;
                int condition2MP = (lessConditionMP != 0) == (invertConditionMP != 0) ? 1 : 0;
                if ( condition1MP != 0 || condition2MP != 0 )
                {
                    return 1;
                }
                return 0;

            default:
                // �f�t�H���g�P�[�X�i����`�̏����̏ꍇ�j
                Debug.LogWarning($"����`�̃X�L�b�v����: {condition}");
                return 0;
        }
    }

    #endregion �X�L�b�v�������f

    /// <summary>
    /// �s�����f�̏������u���������\�b�h
    /// </summary>
    /// <param name="conditions"></param>
    /// <param name="charaData"></param>
    /// <param name="nowHate"></param>
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool CheckActCondition(in BehaviorData condition, in CharacterData myData, in CharacterData targetData, in NativeHashMap<int2, int> tHate)
    {
        bool result = true;

        // �t�B���^�[�ʉ߂��Ȃ��Ȃ�߂�B
        if ( condition.actCondition.filter.IsPassFilter(targetData) == 0 )
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

                // �`�[���̃w�C�g��int2�Ŋm�F����B
                int2 hateKey = new int2((int)myData.liveData.belong, targetHash);

                if ( tHate.ContainsKey(hateKey) )
                {
                    targetHate += tHate[hateKey];
                }

                // �ʏ�͈ȏ�A�t�̏ꍇ�͈ȉ�
                if ( condition.actCondition.isInvert == BitableBool.FALSE )
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
                if ( condition.actCondition.isInvert == BitableBool.FALSE )
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
                if ( condition.actCondition.isInvert == BitableBool.FALSE )
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
                if ( condition.actCondition.isInvert == BitableBool.FALSE )
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
                if ( condition.actCondition.isInvert == BitableBool.FALSE )
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
                if ( condition.actCondition.isInvert == BitableBool.FALSE )
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

    #region�@�^�[�Q�b�g���f����


    /// <summary>
    /// TargetCondition�Ɋ�Â��Ĕ�����s�����\�b�h
    /// </summary>
    /// <param name="judgeData"></param>
    /// <param name="targetData"></param>
    /// <param name="score"></param>
    /// <param name="condition"></param>
    /// <returns></returns>
    // TargetCondition�Ɋ�Â��Ĕ�����s�����\�b�h
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int JudgeTargetByCondition(in TargetJudgeData judgeData, in CharacterData targetData, ref int score)
    {
        TargetSelectCondition condition = judgeData.judgeCondition;
        int isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;
        switch ( condition )
        {
            case TargetSelectCondition.���x:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }

                int height = (int)targetData.liveData.nowPosition.y;


                // ��ԍ���������߂� (isInvert == 1)
                if ( isInvert == 0 )
                {
                    int isGreater = height > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = height;
                        return 1;
                    }
                }
                // ��ԒႢ������߂� (isInvert == 0)
                else
                {
                    int isLess = height < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = height;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.HP����:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.hpRatio > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.hpRatio;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.hpRatio < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.hpRatio;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.HP:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.currentHp > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.currentHp;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.currentHp < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.currentHp;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.�G�ɑ_���Ă鐔:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.targetingCount > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.targetingCount;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.targetingCount < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.targetingCount;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.���v�U����:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.dispAtk > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.dispAtk;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.dispAtk < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.dispAtk;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.���v�h���:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.dispDef > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.dispDef;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.dispDef < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.dispDef;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.�a���U����:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.atk.slash > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.atk.slash;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.atk.slash < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.atk.slash;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.�h�ˍU����:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.atk.pierce > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.atk.pierce;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.atk.pierce < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.atk.pierce;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.�Ō��U����:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.atk.strike > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.atk.strike;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.atk.strike < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.atk.strike;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.���U����:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.atk.fire > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.atk.fire;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.atk.fire < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.atk.fire;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.���U����:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.atk.lightning > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.atk.lightning;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.atk.lightning < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.atk.lightning;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.���U����:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.atk.light > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.atk.light;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.atk.light < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.atk.light;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.�ōU����:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.atk.dark > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.atk.dark;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.atk.dark < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.atk.dark;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.�a���h���:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.def.slash > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.def.slash;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.def.slash < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.def.slash;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.�h�˖h���:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.def.pierce > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.def.pierce;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.def.pierce < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.def.pierce;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.�Ō��h���:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.def.strike > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.def.strike;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.def.strike < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.def.strike;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.���h���:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.def.fire > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.def.fire;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.def.fire < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.def.fire;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.���h���:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.def.lightning > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.def.lightning;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.def.lightning < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.def.lightning;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.���h���:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.def.light > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.def.light;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.def.light < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.def.light;
                        return 1;
                    }
                }
                return 0;

            case TargetSelectCondition.�Ŗh���:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }



                // ��ԍ���������߂�
                if ( isInvert == 0 )
                {
                    int isGreater = targetData.liveData.def.dark > score ? 1 : 0;
                    if ( isGreater != 0 )
                    {
                        score = targetData.liveData.def.dark;
                        return 1;
                    }
                }
                // ��ԒႢ������߂�
                else
                {
                    int isLess = targetData.liveData.def.dark < score ? 1 : 0;
                    if ( isLess != 0 )
                    {
                        score = targetData.liveData.def.dark;
                        return 1;
                    }
                }
                return 0;

            default:
                // �f�t�H���g�P�[�X�i����`�̏����̏ꍇ�j
                Debug.LogWarning($"����`�̃^�[�Q�b�g�I������: {condition}");
                return 0;
        }
    }

    #endregion �^�[�Q�b�g���f����


}