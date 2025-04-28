using System;
using System.Collections.Generic;
using UnityEngine;
using static JTestAIBase;
using static CombatManager;
using static JobAITestStatus;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.InteropServices;

/// <summary>
/// AITestJob�̔�JobSystem�Ŏ����i�W���R���N�V�����g�p�j
/// </summary>
public struct StandardAI
{
    /// <summary>
    /// �ǂݎ���p�̃`�[�����Ƃ̑S�̃w�C�g
    /// </summary>
    public Dictionary<int2, int> teamHate;

    /// <summary>
    /// CharacterDataDic�̕ϊ���
    /// </summary>
    public List<CharacterData> characterData;

    /// <summary>
    /// ���ݎ���
    /// </summary>
    public float nowTime;

    /// <summary>
    /// �s������f�[�^�B
    /// �^�[�Q�b�g�ύX�̔��f�Ƃ����S���������ł��B
    /// </summary>
    public List<MovementInfo> judgeResult;

    /// <summary>
    /// �v���C���[�A�G�A���̑��A���ꂼ�ꂪ�G�΂��Ă���w�c���r�b�g�ŕ\���B
    /// �L�����f�[�^�̃`�[���ݒ�ƈꏏ�Ɏg��
    /// </summary>
    public int[] relationMap;


    /// <summary>
    /// JobAI�̃f�[�^����StandardAI������������R���X�g���N�^
    /// </summary>
    /// <param name="jobTeamHate">JobAI�̃`�[���w�C�g�f�[�^</param>
    /// <param name="jobCharacterData">JobAI�̃L�����N�^�[�f�[�^</param>
    /// <param name="time">���ݎ���</param>
    /// <param name="jobRelationMap">JobAI�̊֌W�}�b�v</param>
    public StandardAI(NativeHashMap<int2, int> jobTeamHate, UnsafeList<CharacterData> jobCharacterData, float time, NativeArray<int> jobRelationMap)
    {
        // ���ݎ��Ԃ��R�s�[
        nowTime = time;

        // �`�[���w�C�g�f�[�^�̃R�s�[
        teamHate = new Dictionary<int2, int>();
        foreach ( var key in jobTeamHate.GetKeyArray(Allocator.Temp) )
        {
            int2 newKey = new int2(key.x, key.y);
            teamHate[newKey] = jobTeamHate[key];
        }

        // �L�����N�^�[�f�[�^�̃R�s�[
        characterData = new List<CharacterData>(jobCharacterData.Length);
        for ( int i = 0; i < jobCharacterData.Length; i++ )
        {
            characterData.Add(jobCharacterData[i]);
        }

        // ���茋�ʗp�̃��X�g���쐬
        judgeResult = new List<MovementInfo>(characterData.Count);
        for ( int i = 0; i < characterData.Count; i++ )
        {
            judgeResult.Add(new MovementInfo());
        }

        // �֌W�}�b�v�̃R�s�[
        relationMap = new int[jobRelationMap.Length];
        for ( int i = 0; i < jobRelationMap.Length; i++ )
        {
            relationMap[i] = jobRelationMap[i];
        }
    }


    /// <summary>
    /// ���茋�ʂ�JobAI�̌��ʃR���e�i�ɃR�s�[���郁�\�b�h
    /// </summary>
    /// <param name="jobResult">JobAI�̌��ʃR���e�i</param>
    public void CopyResultToJobContainer(ref UnsafeList<MovementInfo> jobResult)
    {
        // Span���g�p���ă��X�g�ɃA�N�Z�X
        Span<MovementInfo> resultsSpan = judgeResult.AsSpan();

        for ( int i = 0; i < resultsSpan.Length && i < jobResult.Length; i++ )
        {
            jobResult[i] = resultsSpan[i];
        }
    }

    /// <summary>
    /// AI�̔��f�����s����
    /// </summary>
    public void ExecuteAIDecision()
    {
        // Span���g�p���ă��X�g�ɃA�N�Z�X
        Span<CharacterData> charaSpan = CollectionMarshal.AsSpan(characterData);
        Span<MovementInfo> resultSpan = CollectionMarshal.AsSpan(judgeResult);

        // �e�L�����N�^�[�ɂ���AI���f�����s
        for ( int index = 0; index < charaSpan.Length; index++ )
        {
            // ���ʂ̍\���̂��쐬
            MovementInfo resultData = new MovementInfo();

            // ���݂̍s���̃X�e�[�g�𐔒l�ɕϊ�
            int nowMode = (int)charaSpan[index].liveData.actState;

            // ���f���Ԃ��o�߂��������m�F
            // �o�߂��ĂȂ��Ȃ珈�����Ȃ�
            if ( nowTime - charaSpan[index].lastJudgeTime < charaSpan[index].brainData[nowMode].judgeInterval )
            {
                resultData.result = JudgeResult.�����Ȃ�;
                judgeResult[index] = resultData;
                continue;
            }

            CharacterData myData = charaSpan[index];

            // �s�������̒��őO��𖞂��������̂��擾����r�b�g
            int enableCondition = 0;

            // �O��ƂȂ鎩���ɂ��ẴX�L�b�v�������m�F
            // �Ō�̏����͕⌇�����Ȃ̂Ŗ���
            for ( int i = 0; i < myData.brainData[nowMode].actCondition.Length - 1; i++ )
            {

                SkipJudgeData skipData = myData.brainData[nowMode].actCondition[i].skipData;

                // �X�L�b�v���������߂��Ĕ��f
                if ( skipData.skipCondition == SkipJudgeCondition.�����Ȃ� || JudgeSkipByCondition(skipData, myData) == 1 )
                {
                    enableCondition |= 1 << i;
                }
            }

            // �����𖞂������s���̒��ōł��D��I�Ȃ���
            // �����l�͍Ō�̏����A�܂�����Ȃ��̕⌇����
            int selectMove = myData.brainData[nowMode].actCondition.Length - 1;

            // �L�����f�[�^���m�F����
            for ( int i = 0; i < charaSpan.Length; i++ )
            {
                // �����̓X�L�b�v
                if ( index == i )
                {
                    continue;
                }

                // �s�����f
                if ( enableCondition != 0 )
                {
                    for ( int j = 0; j < myData.brainData[nowMode].actCondition.Length - 1; j++ )
                    {
                        // �����������������break���āA�ȍ~�͂���ȉ��̏����������Ȃ�
                        if ( CheckActCondition(
                            myData.brainData[nowMode].actCondition[j],
                            myData,
                            charaSpan[i],
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
            TargetJudgeData targetJudgeData = myData.brainData[nowMode].actCondition[selectMove].targetCondition;
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
            else
            {
                int tIndex = JudgeTargetByCondition(targetJudgeData, charaSpan, myData, teamHate);
                if ( tIndex >= 0 )
                {
                    newTargetHash = charaSpan[tIndex].hashCode;

                    //   Debug.Log($"�^�[�Q�b�g���f����:{tIndex}�̂�B  Hash�F{newTargetHash}");
                }
                // �����Ń^�[�Q�b�g�������ĂȂ���Αҋ@�Ɉڍs�B
                else
                {
                    // �ҋ@�Ɉڍs
                    resultData.result = JudgeResult.�V�������f������;
                    resultData.actNum = (int)ActState.�ҋ@;
                    //  Debug.Log($"�^�[�Q�b�g���f���s�@�s���ԍ�{selectMove}");
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
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool CheckActCondition(in BehaviorData condition, in CharacterData myData, in CharacterData targetData, Dictionary<int2, int> tHate)
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
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int JudgeTargetByCondition(in TargetJudgeData judgeData, in ReadOnlySpan<CharacterData> cData, in CharacterData myData, Dictionary<int2, int> tHate)
    {

        int index = -1;

        TargetSelectCondition condition = judgeData.judgeCondition;

        int isInvert;
        int score;

        // �t�����珬�����̂�T���̂ōő�l�����
        if ( judgeData.isInvert == BitableBool.TRUE )
        {
            isInvert = 1;
            score = int.MaxValue;
        }
        // �傫���̂�T���̂ōŏ��l�X�^�[�g
        else
        {
            isInvert = 0;
            score = int.MinValue;
        }

        //if ( judgeData.judgeCondition == TargetSelectCondition.���x && isInvert == 1 )
        //{
        //    Debug.Log($" �t{judgeData.isInvert == BitableBool.TRUE} �X�R�A����{score}");
        //}


        switch ( condition )
        {
            case TargetSelectCondition.���x:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    int height = (int)cData[i].liveData.nowPosition.y;


                    // ��ԍ���������߂� (isInvert == 1)
                    if ( isInvert == 0 )
                    {
                        int isGreater = height > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = height;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂� (isInvert == 0)
                    else
                    {
                        //   Debug.Log($" �ԍ�{index} ����{score} ���݂̍���{height}�@����{height < score}");
                        int isLess = height < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = height;
                            index = i;
                        }
                    }
                }



                return index;

            case TargetSelectCondition.HP����:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.hpRatio > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.hpRatio;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.hpRatio < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.hpRatio;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.HP:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.currentHp > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.currentHp;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.currentHp < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.currentHp;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.�G�ɑ_���Ă鐔:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].targetingCount > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].targetingCount;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].targetingCount < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].targetingCount;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.���v�U����:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.dispAtk > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.dispAtk;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.dispAtk < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.dispAtk;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.���v�h���:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.dispDef > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.dispDef;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.dispDef < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.dispDef;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.�a���U����:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.atk.slash > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.atk.slash;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.atk.slash < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.atk.slash;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.�h�ˍU����:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.atk.pierce > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.atk.pierce;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.atk.pierce < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.atk.pierce;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.�Ō��U����:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.atk.strike > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.atk.strike;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.atk.strike < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.atk.strike;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.���U����:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.atk.fire > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.atk.fire;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.atk.fire < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.atk.fire;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.���U����:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.atk.lightning > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.atk.lightning;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.atk.lightning < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.atk.lightning;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.���U����:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.atk.light > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.atk.light;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.atk.light < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.atk.light;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.�ōU����:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.atk.dark > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.atk.dark;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.atk.dark < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.atk.dark;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.�a���h���:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.def.slash > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.def.slash;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.def.slash < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.def.slash;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.�h�˖h���:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.def.pierce > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.def.pierce;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.def.pierce < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.def.pierce;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.�Ō��h���:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.def.strike > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.def.strike;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.def.strike < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.def.strike;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.���h���:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.def.fire > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.def.fire;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.def.fire < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.def.fire;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.���h���:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.def.lightning > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.def.lightning;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.def.lightning < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.def.lightning;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.���h���:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.def.light > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.def.light;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.def.light < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.def.light;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.�Ŗh���:
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }

                    // ��ԍ���������߂�
                    if ( isInvert == 0 )
                    {
                        int isGreater = cData[i].liveData.def.dark > score ? 1 : 0;
                        if ( isGreater != 0 )
                        {
                            score = cData[i].liveData.def.dark;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        int isLess = cData[i].liveData.def.dark < score ? 1 : 0;
                        if ( isLess != 0 )
                        {
                            score = cData[i].liveData.def.dark;
                            index = i;
                        }
                    }
                }
                return index;

            case TargetSelectCondition.����:
                // �����̈ʒu���L���b�V��
                float myPositionX = myData.liveData.nowPosition.x;
                float myPositionY = myData.liveData.nowPosition.y;
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( cData[i].hashCode == myData.hashCode || judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }
                    // �}���n�b�^�������ŉ��ߔ��f
                    float distance = Math.Abs(myPositionX - cData[i].liveData.nowPosition.x) +
                                    Math.Abs(myPositionY - cData[i].liveData.nowPosition.y);

                    // ��ԍ���������߂�B
                    if ( isInvert == 0 )
                    {
                        if ( distance > score )
                        {
                            score = (int)distance;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�B
                    else
                    {
                        if ( distance < score )
                        {
                            score = (int)distance;
                            index = i;
                        }
                    }
                }

                return index;

            case TargetSelectCondition.����:
                return myData.hashCode;

            case TargetSelectCondition.�v���C���[:
                // ��������̃V���O���g���Ƀv���C���[��Hash�͎������Ƃ�
                // newTargetHash = characterData[i].hashCode;
                return -1;

            case TargetSelectCondition.�w��Ȃ�_�w�C�g�l:
                // �^�[�Q�b�g�I�胋�[�v
                for ( int i = 0; i < cData.Length; i++ )
                {
                    // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                    if ( i == index || judgeData.filter.IsPassFilter(cData[i]) == 0 )
                    {
                        continue;
                    }
                    // �w�C�g�l���m�F
                    int targetHash = cData[i].hashCode;
                    int targetHate = 0;
                    if ( cData[index].personalHate.ContainsKey(targetHash) )
                    {
                        targetHate += (int)cData[index].personalHate[targetHash];
                    }
                    // �`�[���̃w�C�g��int2�Ŋm�F����B
                    int2 hateKey = new int2((int)cData[index].liveData.belong, targetHash);
                    if ( tHate.ContainsKey(hateKey) )
                    {
                        targetHate += tHate[hateKey];
                    }
                    // ��ԍ���������߂�B
                    if ( judgeData.isInvert == BitableBool.FALSE )
                    {
                        if ( targetHate > score )
                        {
                            score = targetHate;
                            index = i;
                        }
                    }
                    // ��ԒႢ������߂�B
                    else
                    {
                        if ( targetHate < score )
                        {
                            score = targetHate;
                            index = i;
                        }
                    }
                }
                break;

            default:
                // �f�t�H���g�P�[�X�i����`�̏����̏ꍇ�j
                Debug.LogWarning($"����`�̃^�[�Q�b�g�I������: {condition}");
                return -1;
        }

        return -1;
    }

    #endregion �^�[�Q�b�g���f����
}