using Unity.Jobs;
using UnityEngine;
using Unity.Collections;
using static JTestAIBase;
using Unity.Collections.LowLevel.Unsafe;
using static CombatManager;
using System.ComponentModel;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using System.Runtime.CompilerServices;
using Unity.Burst;
using static JobAITestStatus;
using System;
using System.Runtime.InteropServices; // MarshalAs�����p

/// <summary>
/// FunctionPointer�p�̃��\�b�h�������N���X
/// �����ō����NativeArray��CombatManager�Ŏn������B
/// </summary>
public static class AiFunctionLibrary
{
    #region �X�L�b�v����

    /// <summary>
    /// �f���Q�[�g�̌^��`��ǉ�
    /// </summary>
    /// <param name="skipData"></param>
    /// <param name="charaData"></param>
    /// <returns></returns>
    public delegate int SkipJudgeDelegate(in SkipJudgeData skipData, in CharacterData charaData);

    /// <summary>
    /// �X�L�b�v������FunctionPointer�z��
    /// </summary>
    public static NativeArray<FunctionPointer<SkipJudgeDelegate>> skipFunctions = new NativeArray<FunctionPointer<SkipJudgeDelegate>>(2, Allocator.Persistent);

    #endregion �X�L�b�v����

    #region �^�[�Q�b�g�E�w�C�g����

    /// <summary>
    /// �f���Q�[�g�̌^��`��ǉ�
    /// �^�[�Q�b�g����p�̃f���Q�[�g
    /// ���݂̒l�𒆂ōX�V���Ă����B
    /// �X�V�����ꍇ��1�B1��������C���f�b�N�X�X�V
    /// </summary>
    /// <param name="targetData"></param>
    /// <param name="charaData"></param>
    /// <returns></returns>
    public delegate int TargetJudgeDelegate(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue);

    /// <summary>
    /// �^�[�Q�b�g������FunctionPointer�z��
    /// </summary>
    public static NativeArray<FunctionPointer<TargetJudgeDelegate>> targetFunctions = new NativeArray<FunctionPointer<TargetJudgeDelegate>>(20, Allocator.Persistent);

    #endregion �^�[�Q�b�g�E�w�C�g����

    #region �s�����f

    /// <summary>
    /// �f���Q�[�g�̌^��`��ǉ�
    /// </summary>
    /// <param name="actJudgeData"></param>
    /// <param name="charaData"></param>
    /// <returns></returns>
    public delegate int ActJudgeDelegate(in ActJudgeData actJudgeData, in CharacterData charaData);

    /// <summary>
    /// �s��������FunctionPointer�z��
    /// </summary>
    public static NativeArray<FunctionPointer<ActJudgeDelegate>> actFunctions = new NativeArray<FunctionPointer<ActJudgeDelegate>>(12, Allocator.Persistent);

    #endregion �s�����f

    // FunctionPointer�̏��������\�b�h��ǉ�
    static AiFunctionLibrary()
    {
        try
        {
            #region �X�L�b�v�������f�f���Q�[�g
            skipFunctions[(int)SkipJudgeCondition.������HP����芄���̎�] = BurstCompiler.CompileFunctionPointer<SkipJudgeDelegate>(HPSkipJudge);
            skipFunctions[(int)SkipJudgeCondition.������MP����芄���̎�] = BurstCompiler.CompileFunctionPointer<SkipJudgeDelegate>(MPSkipJudge);
            #endregion �X�L�b�v�������f�f���Q�[�g

            #region �w�C�g�E�^�[�Q�b�g����

            // �e�񋓎q�ɑΉ�����֐��|�C���^��ݒ�
            targetFunctions[(int)TargetSelectCondition.���x] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(HeightTargetJudge);
            targetFunctions[(int)TargetSelectCondition.HP����] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(HPRatioTargetJudge);
            targetFunctions[(int)TargetSelectCondition.HP] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(HPTargetJudge);
            targetFunctions[(int)TargetSelectCondition.�G�ɑ_���Ă鐔] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(AttentionTargetJudge);
            targetFunctions[(int)TargetSelectCondition.���v�U����] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(AtkTargetJudge);
            targetFunctions[(int)TargetSelectCondition.���v�h���] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(DefTargetJudge);

            // �����U���͊֘A
            targetFunctions[(int)TargetSelectCondition.�a���U����] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(SlashAtkTargetJudge);
            targetFunctions[(int)TargetSelectCondition.�h�ˍU����] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(PierceAtkTargetJudge);
            targetFunctions[(int)TargetSelectCondition.�Ō��U����] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(StrikeAtkTargetJudge);
            targetFunctions[(int)TargetSelectCondition.���U����] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(FireAtkTargetJudge);
            targetFunctions[(int)TargetSelectCondition.���U����] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(LightningAtkTargetJudge);
            targetFunctions[(int)TargetSelectCondition.���U����] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(LightAtkTargetJudge);
            targetFunctions[(int)TargetSelectCondition.�ōU����] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(DarkAtkTargetJudge);

            // �����h��͊֘A
            targetFunctions[(int)TargetSelectCondition.�a���h���] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(SlashDefTargetJudge);
            targetFunctions[(int)TargetSelectCondition.�h�˖h���] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(PierceDefTargetJudge);
            targetFunctions[(int)TargetSelectCondition.�Ō��h���] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(StrikeDefTargetJudge);
            targetFunctions[(int)TargetSelectCondition.���h���] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(FireDefTargetJudge);
            targetFunctions[(int)TargetSelectCondition.���h���] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(LightningDefTargetJudge);
            targetFunctions[(int)TargetSelectCondition.���h���] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(LightDefTargetJudge);
            targetFunctions[(int)TargetSelectCondition.�Ŗh���] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(DarkDefTargetJudge);

            #endregion �w�C�g�E�^�[�Q�b�g����
        }
        catch ( Exception ex )
        {
            // �f�o�b�O�p�ɗ�O�����o��
            Debug.LogError($"AiFunctionLibrary�������G���[: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    #region �X�L�b�v�������f

    /// <summary>
    /// �X�L�b�v�����̓��AHP�����𔻒f���郁�\�b�h
    /// </summary>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int HPSkipJudge(in SkipJudgeData skipData, in CharacterData charaData)
    {
        // �e�������ʂ� int �ŕ]��
        int equalCondition = skipData.judgeValue == charaData.liveData.hpRatio ? 1 : 0;
        int lessCondition = skipData.judgeValue < charaData.liveData.hpRatio ? 1 : 0;
        int invertCondition = skipData.isInvert == BitableBool.TRUE ? 1 : 0;

        // �����I�ɏ�����g�ݍ��킹��
        int condition1 = equalCondition;
        int condition2 = (lessCondition != 0) == (invertCondition != 0) ? 1 : 0;

        if ( condition1 != 0 || condition2 != 0 )
        {
            return 1;
        }
        return 0;
    }

    /// <summary>
    /// �X�L�b�v�����̓��AMP�����𔻒f���郁�\�b�h
    /// </summary>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int MPSkipJudge(in SkipJudgeData skipData, in CharacterData charaData)
    {
        // �e�������ʂ� int �ŕ]��
        int equalCondition = skipData.judgeValue == charaData.liveData.mpRatio ? 1 : 0;
        int lessCondition = skipData.judgeValue < charaData.liveData.mpRatio ? 1 : 0;
        int invertCondition = skipData.isInvert == BitableBool.TRUE ? 1 : 0;

        // �����I�ɏ�����g�ݍ��킹��
        int condition1 = equalCondition;
        int condition2 = (lessCondition != 0) == (invertCondition != 0) ? 1 : 0;

        if ( condition1 != 0 || condition2 != 0 )
        {
            return 1;
        }
        return 0;
    }
    #endregion �X�L�b�v�������f

    #region �w�C�g�E�^�[�Q�b�g����

    /// <summary>
    /// �ł����x�������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int HeightTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int height = (int)charaData.liveData.nowPosition.y;
        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂� (isInvert == 1)
        if ( isInvert != 0 )
        {
            int isGreater = height > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = height;
                return 1;
            }
        }
        // ��ԒႢ������߂� (isInvert == 0)
        else
        {
            int isLess = height < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = height;
                return 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// �ł�HP�����������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int HPRatioTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.hpRatio > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.hpRatio;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.hpRatio < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.hpRatio;
                return 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// �ł�HP�������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int HPTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.currentHp > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.currentHp;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.currentHp < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.currentHp;
                return 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// �ł��_���Ă���E�_���Ă��Ȃ��L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int AttentionTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.targetingCount > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.targetingCount;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.targetingCount < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.targetingCount;
                return 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// �ł��U���͂������E�U���͂��Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int AtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.dispAtk > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.dispAtk;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.dispAtk < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.dispAtk;
                return 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// �ł��h��͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int DefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.dispDef > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.dispDef;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.dispDef < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.dispDef;
                return 1;
            }
        }

        return 0;
    }

    #region �����U����

    /// <summary>
    /// �ł��a���U���͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int SlashAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.atk.slash > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.atk.slash;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.atk.slash < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.atk.slash;
                return 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// �ł��h�ˍU���͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int PierceAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.atk.pierce > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.atk.pierce;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.atk.pierce < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.atk.pierce;
                return 1;
            }
        }
        return 0;
    }

    /// <summary>
    /// �ł��Ō��U���͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int StrikeAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.atk.strike > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.atk.strike;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.atk.strike < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.atk.strike;
                return 1;
            }
        }
        return 0;
    }

    /// <summary>
    /// �ł����U���͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int FireAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.atk.fire > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.atk.fire;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.atk.fire < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.atk.fire;
                return 1;
            }
        }
        return 0;
    }

    /// <summary>
    /// �ł����U���͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int LightningAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.atk.lightning > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.atk.lightning;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.atk.lightning < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.atk.lightning;
                return 1;
            }
        }
        return 0;
    }

    /// <summary>
    /// �ł����U���͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int LightAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.atk.light > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.atk.light;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.atk.light < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.atk.light;
                return 1;
            }
        }
        return 0;
    }

    /// <summary>
    /// �ł��ōU���͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int DarkAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.atk.dark > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.atk.dark;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.atk.dark < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.atk.dark;
                return 1;
            }
        }
        return 0;
    }

    #endregion �����U����

    #region �����h���

    /// <summary>
    /// �ł��a���h��͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int SlashDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.def.slash > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.def.slash;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.def.slash < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.def.slash;
                return 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// �ł��h�˖h��͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int PierceDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.def.pierce > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.def.pierce;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.def.pierce < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.def.pierce;
                return 1;
            }
        }
        return 0;
    }

    /// <summary>
    /// �ł��Ō��h��͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int StrikeDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.def.strike > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.def.strike;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.def.strike < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.def.strike;
                return 1;
            }
        }
        return 0;
    }

    /// <summary>
    /// �ł����h��͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int FireDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.def.fire > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.def.fire;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.def.fire < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.def.fire;
                return 1;
            }
        }
        return 0;
    }

    /// <summary>
    /// �ł����h��͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int LightningDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.def.lightning > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.def.lightning;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.def.lightning < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.def.lightning;
                return 1;
            }
        }
        return 0;
    }

    /// <summary>
    /// �ł����h��͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int LightDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.def.light > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.def.light;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.def.light < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.def.light;
                return 1;
            }
        }
        return 0;
    }

    /// <summary>
    /// �ł��Ŗh��͂������E�Ⴂ�L����������o���B
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int DarkDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // ��ԍ���������߂�
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.def.dark > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.def.dark;
                return 1;
            }
        }
        // ��ԒႢ������߂�
        else
        {
            int isLess = charaData.liveData.def.dark < nowValue ? 1 : 0;
            if ( isLess != 0 )
            {
                nowValue = charaData.liveData.def.dark;
                return 1;
            }
        }
        return 0;
    }

    #endregion �����h���

    #endregion �w�C�g�E�^�[�Q�b�g����


}
