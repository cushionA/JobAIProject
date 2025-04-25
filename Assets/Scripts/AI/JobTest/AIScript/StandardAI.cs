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
public class StandardAI
{
    /// <summary>
    /// �ǂݎ���p�̃`�[�����Ƃ̑S�̃w�C�g
    /// </summary>
    public Dictionary<Vector2Int, int> teamHate;

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
        teamHate = new Dictionary<Vector2Int, int>();
        foreach ( var key in jobTeamHate.GetKeyArray(Allocator.Temp) )
        {
            Vector2Int newKey = new Vector2Int(key.x, key.y);
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
    /// ��̃R���X�g���N�^�i�蓮�Ńf�[�^��ݒ肷��ꍇ�p�j
    /// </summary>
    public StandardAI()
    {
        teamHate = new Dictionary<Vector2Int, int>();
        characterData = new List<CharacterData>();
        judgeResult = new List<MovementInfo>();
        relationMap = new int[0];
        nowTime = 0f;
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
                resultSpan[index] = resultData;
                continue;
            }

            // �s�������̒��őO��𖞂��������̂��擾����r�b�g
            int enableCondition = 0;

            // �O��ƂȂ鎩���ɂ��ẴX�L�b�v�������m�F
            // �Ō�̏����͕⌇�����Ȃ̂Ŗ���
            for ( int i = 0; i < charaSpan[index].brainData[nowMode].actCondition.Length - 1; i++ )
            {
                SkipJudgeData skipData = charaSpan[index].brainData[nowMode].actCondition[i].skipData;

                // �X�L�b�v���������߂��Ĕ��f
                if ( skipData.skipCondition == SkipJudgeCondition.�����Ȃ� || JudgeSkipByCondition(skipData, charaSpan[index]) == 1 )
                {
                    enableCondition |= 1 << i; // |= ���������i�r�b�g�𗧂Ă�j
                }
            }

            // �����𖞂������s���̒��ōł��D��I�Ȃ���
            // �����l�͍Ō�̏����A�܂�����Ȃ��̕⌇����
            int selectMove = charaSpan[index].brainData[nowMode].actCondition.Length - 1;

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
                    for ( int j = 0; j < charaSpan[index].brainData[nowMode].actCondition.Length - 1; j++ )
                    {
                        // �r�b�g�������Ă��邩�`�F�b�N
                        if ( (enableCondition & (1 << j)) != 0 )
                        {
                            // �����������������break���āA�ȍ~�͂���ȉ��̏����������Ȃ�
                            if ( CheckActCondition(
                                charaSpan[index].brainData[nowMode].actCondition[j],
                                charaSpan[index],
                                charaSpan[i],
                                teamHate) )
                            {
                                selectMove = j; // �����̃C���f�b�N�X

                                // enableCondition��bit������
                                // j���ڂ܂ł̃r�b�g�����ׂ�1�ɂ���}�X�N���쐬
                                int mask = (1 << j) - 1;

                                // �}�X�N�ƌ��̒l�̘_���ς���邱�Ƃŏ�ʃr�b�g���N���A
                                enableCondition = enableCondition & mask;
                                break;
                            }
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
            TargetJudgeData targetJudgeData = charaSpan[index].brainData[nowMode].actCondition[selectMove].targetCondition;
            int nowValue = targetJudgeData.isInvert == BitableBool.TRUE ? int.MaxValue : int.MinValue;
            int newTargetHash = 0;

            // ��ԕύX�̏ꍇ�����Ŗ߂�
            if ( targetJudgeData.judgeCondition == TargetSelectCondition.�s�v_��ԕύX )
            {
                // �w���ԂɈڍs
                resultData.result = JudgeResult.�V�������f������;
                resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;

                // ���f���ʂ�ݒ�
                resultSpan[index] = resultData;
                continue;
            }

            // ����ȊO�ł���΃^�[�Q�b�g�𔻒f
            if ( targetJudgeData.judgeCondition == TargetSelectCondition.���� )
            {
                // �����̈ʒu���L���b�V��
                float myPositionX = charaSpan[index].liveData.nowPosition.x;
                float myPositionY = charaSpan[index].liveData.nowPosition.y;

                // �^�[�Q�b�g�I�胋�[�v
                for ( int i = 0; i < charaSpan.Length; i++ )
                {
                    // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�
                    if ( i == index || targetJudgeData.filter.IsPassFilter(charaSpan[i]) == 0 )
                    {
                        continue;
                    }

                    // �}���n�b�^�������ŉ��ߔ��f
                    float distance = Math.Abs(myPositionX - charaSpan[i].liveData.nowPosition.x) +
                                   Math.Abs(myPositionY - charaSpan[i].liveData.nowPosition.y);

                    // ��ԍ���������߂�
                    if ( targetJudgeData.isInvert == BitableBool.FALSE )
                    {
                        if ( distance > nowValue )
                        {
                            nowValue = (int)distance;
                            newTargetHash = charaSpan[i].hashCode;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        if ( distance < nowValue )
                        {
                            nowValue = (int)distance;
                            newTargetHash = charaSpan[i].hashCode;
                        }
                    }
                }
            }
            else if ( targetJudgeData.judgeCondition == TargetSelectCondition.���� )
            {
                newTargetHash = charaSpan[index].hashCode;
            }
            else if ( targetJudgeData.judgeCondition == TargetSelectCondition.�v���C���[ )
            {
                // �v���C���[�̃n�b�V���̓V���O���g������擾����z��
                // newTargetHash = charaSpan[i].hashCode;
            }
            else if ( targetJudgeData.judgeCondition == TargetSelectCondition.�w��Ȃ�_�w�C�g�l )
            {
                // �^�[�Q�b�g�I�胋�[�v
                for ( int i = 0; i < charaSpan.Length; i++ )
                {
                    // �������g���A�t�B���^�[���p�X�ł��Ȃ���Ζ߂�
                    if ( i == index || targetJudgeData.filter.IsPassFilter(charaSpan[i]) == 0 )
                    {
                        continue;
                    }

                    // �w�C�g�l���m�F
                    int targetHash = charaSpan[i].hashCode;
                    int targetHate = 0;

                    if ( charaSpan[index].personalHate.ContainsKey(targetHash) )
                    {
                        targetHate += charaSpan[index].personalHate[targetHash];
                    }

                    Vector2Int hateKey = new Vector2Int((int)charaSpan[index].liveData.belong, targetHash);

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
                            newTargetHash = charaSpan[i].hashCode;
                        }
                    }
                    // ��ԒႢ������߂�
                    else
                    {
                        if ( targetHate < nowValue )
                        {
                            nowValue = targetHate;
                            newTargetHash = charaSpan[i].hashCode;
                        }
                    }
                }
            }
            // �ʏ�̃^�[�Q�b�g�I��
            else
            {
                // �^�[�Q�b�g�I�胋�[�v
                for ( int i = 0; i < charaSpan.Length; i++ )
                {
                    // �n�b�V�����擾
                    int targetValue = nowValue;
                    if ( JudgeTargetByCondition(targetJudgeData, charaSpan[i], ref targetValue) == 1 )
                    {
                        nowValue = targetValue;
                        newTargetHash = charaSpan[i].hashCode;
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
            else
            {
                // �^�[�Q�b�g��ݒ�
                resultData.result = JudgeResult.�V�������f������;
                resultData.targetHash = newTargetHash;
            }

            // ���f���ʂ�ݒ�
            resultSpan[index] = resultData;
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
    public static bool CheckActCondition(in BehaviorData condition, in CharacterData myData, in CharacterData targetData, Dictionary<Vector2Int, int> tHate)
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

                // �`�[���̃w�C�g��Vector2Int�Ŋm�F����B
                Vector2Int hateKey = new Vector2Int((int)myData.liveData.belong, targetHash);

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
    public static int JudgeTargetByCondition(in TargetJudgeData judgeData, in CharacterData targetData, ref int score)
    {
        TargetSelectCondition condition = judgeData.judgeCondition;
        switch ( condition )
        {
            case TargetSelectCondition.���x:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }

                int height = (int)targetData.liveData.nowPosition.y;
                int isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂� (isInvert == 1)
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

            // ���̃P�[�X�����l�Ɏ���...
            // ���̃R�[�h�Ɠ������W�b�N�Ŏc��̃P�[�X���������܂��B
            // �i�����Ȃ邽�߁A�����ł͏ȗ����܂����A���ۂɂ͑S�ẴP�[�X�𓯗l�Ɏ�������K�v������܂��j

            default:
                // �f�t�H���g�P�[�X�i����`�̏����̏ꍇ�j
                Debug.LogWarning($"����`�̃^�[�Q�b�g�I������: {condition}");
                return 0;
        }
    }

    #endregion �^�[�Q�b�g���f����
}