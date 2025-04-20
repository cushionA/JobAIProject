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
    public delegate bool SkipJudgeDelegate(in SkipJudgeData skipData, in CharacterData charaData);

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
    /// �X�V�����ꍇ��True�BTrue��������C���f�b�N�X�X�V
    /// </summary>
    /// <param name="targetData"></param>
    /// <param name="charaData"></param>
    /// <returns></returns>
    public delegate bool TargetJudgeDelegate(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue);

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
    public delegate bool ActJudgeDelegate(in ActJudgeData actJudgeData, in CharacterData charaData);

    /// <summary>
    /// �s��������FunctionPointer�z��
    /// </summary>
    public static NativeArray<FunctionPointer<ActJudgeDelegate>> actFunctions = new NativeArray<FunctionPointer<ActJudgeDelegate>>(12, Allocator.Persistent);

    #endregion �s�����f

    // FunctionPointer�̏��������\�b�h��ǉ�
    static AiFunctionLibrary()
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

    #region �X�L�b�v�������f

    /// <summary>
    /// �X�L�b�v�����̓��AHP�����𔻒f���郁�\�b�h
    /// </summary>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool HPSkipJudge(in SkipJudgeData skipData, in CharacterData charaData)
    {
        return (skipData.judgeValue == charaData.liveData.hpRatio || (skipData.judgeValue < charaData.liveData.hpRatio) == skipData.isInvert);
    }

    /// <summary>
    /// �X�L�b�v�����̓��AMP�����𔻒f���郁�\�b�h
    /// </summary>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool MPSkipJudge(in SkipJudgeData skipData, in CharacterData charaData)
    {
        return (skipData.judgeValue == charaData.liveData.mpRatio || (skipData.judgeValue < charaData.liveData.mpRatio) == skipData.isInvert);
    }
    #endregion �X�L�b�v�������f

    #region �w�C�g�E�^�[�Q�b�g����

    /// <summary>
    /// �ł����x�������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool HeightTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        int height = (int)charaData.liveData.nowPosition.y;

        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( height > nowValue )
            {
                nowValue = height;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( height < nowValue )
            {
                nowValue = height;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// �ł�HP�����������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool HPRatioTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.hpRatio > nowValue )
            {
                nowValue = charaData.liveData.hpRatio;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.hpRatio < nowValue )
            {
                nowValue = charaData.liveData.hpRatio;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// �ł�HP�������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool HPTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.currentHp > nowValue )
            {
                nowValue = charaData.liveData.currentHp;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.currentHp < nowValue )
            {
                nowValue = charaData.liveData.currentHp;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// �ł�MP�����������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool MPRatioTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.mpRatio > nowValue )
            {
                nowValue = charaData.liveData.mpRatio;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.mpRatio < nowValue )
            {
                nowValue = charaData.liveData.mpRatio;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// �ł�MP�������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool MPTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.currentMp > nowValue )
            {
                nowValue = charaData.liveData.currentMp;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.currentMp < nowValue )
            {
                nowValue = charaData.liveData.currentMp;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// �ł��_���Ă���E�_���Ă��Ȃ��L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool AttentionTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.targetingCount > nowValue )
            {
                nowValue = charaData.targetingCount;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.targetingCount < nowValue )
            {
                nowValue = charaData.targetingCount;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// �ł��U���͂������E�U���͂��Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool AtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.dispAtk > nowValue )
            {
                nowValue = charaData.liveData.dispAtk;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.dispAtk < nowValue )
            {
                nowValue = charaData.liveData.dispAtk;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// �ł��h��͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool DefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.dispDef > nowValue )
            {
                nowValue = charaData.liveData.dispDef;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.dispDef < nowValue )
            {
                nowValue = charaData.liveData.dispDef;
                return true;
            }
        }

        return false;
    }

    #region �����U����

    /// <summary>
    /// �ł��a���U���͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool SlashAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.atk.slash > nowValue )
            {
                nowValue = charaData.liveData.atk.slash;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.atk.slash < nowValue )
            {
                nowValue = charaData.liveData.atk.slash;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// �ł��h�ˍU���͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool PierceAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.atk.pierce > nowValue )
            {
                nowValue = charaData.liveData.atk.pierce;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.atk.pierce < nowValue )
            {
                nowValue = charaData.liveData.atk.pierce;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// �ł��Ō��U���͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool StrikeAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.atk.strike > nowValue )
            {
                nowValue = charaData.liveData.atk.strike;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.atk.strike < nowValue )
            {
                nowValue = charaData.liveData.atk.strike;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// �ł����U���͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool FireAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.atk.fire > nowValue )
            {
                nowValue = charaData.liveData.atk.fire;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.atk.fire < nowValue )
            {
                nowValue = charaData.liveData.atk.fire;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// �ł����U���͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool LightningAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.atk.lightning > nowValue )
            {
                nowValue = charaData.liveData.atk.lightning;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.atk.lightning < nowValue )
            {
                nowValue = charaData.liveData.atk.lightning;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// �ł����U���͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool LightAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.atk.light > nowValue )
            {
                nowValue = charaData.liveData.atk.light;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.atk.light < nowValue )
            {
                nowValue = charaData.liveData.atk.light;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// �ł��ōU���͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool DarkAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.atk.dark > nowValue )
            {
                nowValue = charaData.liveData.atk.dark;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.atk.dark < nowValue )
            {
                nowValue = charaData.liveData.atk.dark;
                return true;
            }
        }
        return false;
    }

    #endregion �����U����

    #region �����h���

    /// <summary>
    /// �ł��a���h��͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool SlashDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.def.slash > nowValue )
            {
                nowValue = charaData.liveData.def.slash;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.def.slash < nowValue )
            {
                nowValue = charaData.liveData.def.slash;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// �ł��h�˖h��͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool PierceDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.def.pierce > nowValue )
            {
                nowValue = charaData.liveData.def.pierce;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.def.pierce < nowValue )
            {
                nowValue = charaData.liveData.def.pierce;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// �ł��Ō��h��͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool StrikeDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.def.strike > nowValue )
            {
                nowValue = charaData.liveData.def.strike;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.def.strike < nowValue )
            {
                nowValue = charaData.liveData.def.strike;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// �ł����h��͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool FireDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.def.fire > nowValue )
            {
                nowValue = charaData.liveData.def.fire;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.def.fire < nowValue )
            {
                nowValue = charaData.liveData.def.fire;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// �ł����h��͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool LightningDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.def.lightning > nowValue )
            {
                nowValue = charaData.liveData.def.lightning;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.def.lightning < nowValue )
            {
                nowValue = charaData.liveData.def.lightning;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// �ł����h��͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool LightDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.def.light > nowValue )
            {
                nowValue = charaData.liveData.def.light;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.def.light < nowValue )
            {
                nowValue = charaData.liveData.def.light;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// �ł��Ŗh��͂������E�Ⴂ�L����������o���B
    /// </summary>
    /// <param name="targetData">�W�I���f����</param>
    /// <param name="charaData">�G�L�����N�^�[����</param>
    /// <param name="nowValue">���݂̒l</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool DarkDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // �t�B���^�[���p�X�ł��Ȃ���Ζ߂�B
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // ��ԍ���������߂�B
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.def.dark > nowValue )
            {
                nowValue = charaData.liveData.def.dark;
                return true;
            }
        }
        // ��ԒႢ������߂�B
        else
        {
            if ( charaData.liveData.def.dark < nowValue )
            {
                nowValue = charaData.liveData.def.dark;
                return true;
            }
        }
        return false;
    }

    #endregion �����h���

    #endregion �w�C�g�E�^�[�Q�b�g����

    #region �s�����f

    //�w��̃w�C�g�l�̓G�����鎞,
    //    �Ώۂ���萔�̎�,
    //    HP����芄���̑Ώۂ����鎞,
    //    �ݒ苗���ɑΏۂ����鎞, �@//�����n�̏����͕ʂ̂����Ŏ��O�ɃL���b�V�����s���BAI�̐ݒ�͈̔͂����Z���T�[�Œ��ׂ���@���Ƃ�
    //    �C�ӂ̃^�C�v�̑Ώۂ����鎞,
    //    �Ώۂ��񕜂�x�����g�p������,// �񕜖��@�Ƃ��ŉ񕜂������ɑS�̃C�x���g���΂����B
    //    �Ώۂ���_���[�W���󂯂���,
    //    �Ώۂ�����̓�����ʂ̉e�����󂯂Ă��鎞,//�o�t�Ƃ��f�o�t
    //    �Ώۂ����S������,
    //    �Ώۂ��U�����ꂽ��,
    //    ����̑����ōU������Ώۂ����鎞,
    //    ����̐��̓G�ɑ_���Ă��鎞,// �w�c�t�B���^�����O�͗L��

    #endregion �s�����f

}