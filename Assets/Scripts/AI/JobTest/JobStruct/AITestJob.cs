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
using Unity.Burst;
using System.Runtime.InteropServices.WindowsRuntime;
using System;
using Unity.Mathematics;


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
    public NativeHashMap<int2, int> teamHate;

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

            SkipJudgeData skipData = characterData[index].brainData[nowMode].actCondition[i].skipData;

            // �X�L�b�v���������߂��Ĕ��f
            if ( skipData.skipCondition == SkipJudgeCondition.�����Ȃ� || JudgeSkipByCondition(skipData, characterData[index]) == 1 )
            {
                enableCondition |= 1 << i;
            }

        }

        // �����𖞂������s���̒��ōł��D��I�Ȃ��́B
        // �����l�͍Ō�̏����A�܂�����Ȃ��̕⌇����
        int selectMove = characterData[index].brainData[nowMode].actCondition.Length - 1;

        //// �w�C�g�����m�F�p�̈ꎞ�o�b�t�@
        //NativeArray<Vector2Int> hateIndex = new NativeArray<Vector2Int>(characterData[index].brainData[nowMode].hateCondition.Length, Allocator.Temp);
        //NativeArray<TargetJudgeData> hateCondition = characterData[index].brainData[nowMode].hateCondition;

        //// �w�C�g�m�F�o�b�t�@�̏�����
        //for ( int i = 0; i < hateIndex.Length; i++ )
        //{
        //    if ( hateCondition[i].isInvert )
        //    {
        //        hateIndex[i].Set(int.MaxValue, -1);
        //    }
        //    else
        //    {
        //        hateIndex[i].Set(int.MinValue, -1);
        //    }
        //}

        // �L�����f�[�^���m�F����B
        for ( int i = 0; i < characterData.Length; i++ )
        {
            // �����̓X�L�b�v
            if ( index == i )
            {
                continue;
            }

            // �ǂݎ���p��NativeContainer�ւ̃A�N�Z�X������邽�߂Ƀw�C�g�n�̏����͕������邱�Ƃ�

            //// �܂��w�C�g���f�B
            //// �e�w�C�g�����ɂ��āA�����X�V���L�^����B
            //for ( int j = 0; j < hateCondition.Length; j++ )
            //{
            //    int value = hateIndex[j].x;
            //    if ( targetFunctions[(int)hateCondition[j].judgeCondition].Invoke(hateCondition[j], characterData[i], ref value) )
            //    {
            //        hateIndex[j].Set(value, i);
            //    }
            //}

            // �s�����f�B
            // �����̓X�C�b�`���g�����B�A������Int�l�Ȃ�R���p�C�����W�����v�e�[�u������Ă����̂�
            if ( enableCondition != 0 )
            {
                for ( int j = 0; j < characterData[index].brainData[nowMode].actCondition.Length - 1; j++ )
                {
                    // �����������������break���āA�ȍ~�͂���ȉ��̏����������Ȃ��B
                    if ( CheckActCondition(characterData[index].brainData[nowMode].actCondition[j], characterData[index], characterData[i], teamHate) )
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
            // �������������烋�[�v�I���B
            else
            {
                break;
            }

        }

        //// �w�C�g�l�̔��f
        //for ( int i = 0; i < hateIndex.Length; i++ )
        //{
        //    int targetHate = 0;
        //    int targetHash = characterData[hateIndex[i].y].hashCode;

        //    if ( characterData[index].personalHate.ContainsKey(targetHash) )
        //    {
        //        targetHate += (int)characterData[index].personalHate[targetHash];
        //    }

        //    if ( teamHate[(int)characterData[index].liveData.belong].ContainsKey(targetHash) )
        //    {
        //        targetHate += teamHate[(int)characterData[index].liveData.belong][targetHash];
        //    }

        //    // �Œ�10�͕ۏ؁B
        //    targetHate = Math.Min(10,targetHate);

        //    int newHate = (int)(targetHate * hateCondition[i].useAttackOrHateNum);

        //}


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
        int nowValue = targetJudgeData.isInvert == BitableBool.TRUE ? int.MaxValue : int.MinValue;
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
                if ( i == index || targetJudgeData.filter.IsPassFilter(characterData[i]) == 0 )
                {
                    continue;
                }

                // �}���n�b�^�������ŉ��ߔ��f
                float distance = Math.Abs(myPositionX - characterData[i].liveData.nowPosition.x) + Math.Abs(myPositionY - characterData[i].liveData.nowPosition.y);

                // ��ԍ���������߂�B
                if ( targetJudgeData.isInvert == BitableBool.FALSE )
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

                // �`�[���̃w�C�g��int2�Ŋm�F����B
                int2 hateKey = new int2((int)characterData[index].liveData.belong, targetHash);

                if ( teamHate.ContainsKey(hateKey) )
                {
                    targetHate += teamHate[hateKey];
                }

                // ��ԍ���������߂�B
                if ( targetJudgeData.isInvert == BitableBool.FALSE )
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
                if ( JudgeTargetByCondition(targetJudgeData, characterData[i], ref nowValue) == 1 )
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

    #region �X�L�b�v�������f

    /// <summary>
    /// SkipJudgeCondition�Ɋ�Â��Ĕ�����s�����\�b�h
    /// </summary>
    /// <param name="skipData">�X�L�b�v����p�f�[�^</param>
    /// <param name="charaData">�L�����N�^�[�f�[�^</param>
    /// <returns>�����ɍ��v����ꍇ��1�A����ȊO��0</returns>
    [BurstCompile]
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
    [BurstCompile]
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
    [BurstCompile]
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

            case TargetSelectCondition.�G�ɑ_���Ă鐔:
                // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
                if ( judgeData.filter.IsPassFilter(targetData) == 0 )
                {
                    return 0;
                }

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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

                isInvert = judgeData.isInvert == BitableBool.TRUE ? 1 : 0;

                // ��ԍ���������߂�
                if ( isInvert != 0 )
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
