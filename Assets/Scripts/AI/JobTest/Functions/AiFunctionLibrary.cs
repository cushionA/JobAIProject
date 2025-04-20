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
/// FunctionPointer用のメソッドを抱えるクラス
/// ここで作ったNativeArrayはCombatManagerで始末する。
/// </summary>
public static class AiFunctionLibrary
{
    #region スキップ条件

    /// <summary>
    /// デリゲートの型定義を追加
    /// </summary>
    /// <param name="skipData"></param>
    /// <param name="charaData"></param>
    /// <returns></returns>
    public delegate bool SkipJudgeDelegate(in SkipJudgeData skipData, in CharacterData charaData);

    /// <summary>
    /// スキップ条件のFunctionPointer配列
    /// </summary>
    public static NativeArray<FunctionPointer<SkipJudgeDelegate>> skipFunctions = new NativeArray<FunctionPointer<SkipJudgeDelegate>>(2, Allocator.Persistent);

    #endregion スキップ条件

    #region ターゲット・ヘイト条件

    /// <summary>
    /// デリゲートの型定義を追加
    /// ターゲット判定用のデリゲート
    /// 現在の値を中で更新してくれる。
    /// 更新した場合はTrue。Trueが来たらインデックス更新
    /// </summary>
    /// <param name="targetData"></param>
    /// <param name="charaData"></param>
    /// <returns></returns>
    public delegate bool TargetJudgeDelegate(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue);

    /// <summary>
    /// ターゲット条件のFunctionPointer配列
    /// </summary>
    public static NativeArray<FunctionPointer<TargetJudgeDelegate>> targetFunctions = new NativeArray<FunctionPointer<TargetJudgeDelegate>>(20, Allocator.Persistent);

    #endregion ターゲット・ヘイト条件

    #region 行動判断

    /// <summary>
    /// デリゲートの型定義を追加
    /// </summary>
    /// <param name="actJudgeData"></param>
    /// <param name="charaData"></param>
    /// <returns></returns>
    public delegate bool ActJudgeDelegate(in ActJudgeData actJudgeData, in CharacterData charaData);

    /// <summary>
    /// 行動条件のFunctionPointer配列
    /// </summary>
    public static NativeArray<FunctionPointer<ActJudgeDelegate>> actFunctions = new NativeArray<FunctionPointer<ActJudgeDelegate>>(12, Allocator.Persistent);

    #endregion 行動判断

    // FunctionPointerの初期化メソッドを追加
    static AiFunctionLibrary()
    {
        #region スキップ条件判断デリゲート
        skipFunctions[(int)SkipJudgeCondition.自分のHPが一定割合の時] = BurstCompiler.CompileFunctionPointer<SkipJudgeDelegate>(HPSkipJudge);
        skipFunctions[(int)SkipJudgeCondition.自分のMPが一定割合の時] = BurstCompiler.CompileFunctionPointer<SkipJudgeDelegate>(MPSkipJudge);
        #endregion スキップ条件判断デリゲート

        #region ヘイト・ターゲット判定

        // 各列挙子に対応する関数ポインタを設定
        targetFunctions[(int)TargetSelectCondition.高度] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(HeightTargetJudge);
        targetFunctions[(int)TargetSelectCondition.HP割合] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(HPRatioTargetJudge);
        targetFunctions[(int)TargetSelectCondition.HP] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(HPTargetJudge);
        targetFunctions[(int)TargetSelectCondition.敵に狙われてる数] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(AttentionTargetJudge);
        targetFunctions[(int)TargetSelectCondition.合計攻撃力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(AtkTargetJudge);
        targetFunctions[(int)TargetSelectCondition.合計防御力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(DefTargetJudge);

        // 属性攻撃力関連
        targetFunctions[(int)TargetSelectCondition.斬撃攻撃力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(SlashAtkTargetJudge);
        targetFunctions[(int)TargetSelectCondition.刺突攻撃力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(PierceAtkTargetJudge);
        targetFunctions[(int)TargetSelectCondition.打撃攻撃力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(StrikeAtkTargetJudge);
        targetFunctions[(int)TargetSelectCondition.炎攻撃力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(FireAtkTargetJudge);
        targetFunctions[(int)TargetSelectCondition.雷攻撃力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(LightningAtkTargetJudge);
        targetFunctions[(int)TargetSelectCondition.光攻撃力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(LightAtkTargetJudge);
        targetFunctions[(int)TargetSelectCondition.闇攻撃力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(DarkAtkTargetJudge);

        // 属性防御力関連
        targetFunctions[(int)TargetSelectCondition.斬撃防御力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(SlashDefTargetJudge);
        targetFunctions[(int)TargetSelectCondition.刺突防御力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(PierceDefTargetJudge);
        targetFunctions[(int)TargetSelectCondition.打撃防御力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(StrikeDefTargetJudge);
        targetFunctions[(int)TargetSelectCondition.炎防御力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(FireDefTargetJudge);
        targetFunctions[(int)TargetSelectCondition.雷防御力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(LightningDefTargetJudge);
        targetFunctions[(int)TargetSelectCondition.光防御力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(LightDefTargetJudge);
        targetFunctions[(int)TargetSelectCondition.闇防御力] = BurstCompiler.CompileFunctionPointer<TargetJudgeDelegate>(DarkDefTargetJudge);


        #endregion ヘイト・ターゲット判定

    }

    #region スキップ条件判断

    /// <summary>
    /// スキップ条件の内、HP条件を判断するメソッド
    /// </summary>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool HPSkipJudge(in SkipJudgeData skipData, in CharacterData charaData)
    {
        return (skipData.judgeValue == charaData.liveData.hpRatio || (skipData.judgeValue < charaData.liveData.hpRatio) == skipData.isInvert);
    }

    /// <summary>
    /// スキップ条件の内、MP条件を判断するメソッド
    /// </summary>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool MPSkipJudge(in SkipJudgeData skipData, in CharacterData charaData)
    {
        return (skipData.judgeValue == charaData.liveData.mpRatio || (skipData.judgeValue < charaData.liveData.mpRatio) == skipData.isInvert);
    }
    #endregion スキップ条件判断

    #region ヘイト・ターゲット判定

    /// <summary>
    /// 最も高度が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool HeightTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        int height = (int)charaData.liveData.nowPosition.y;

        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( height > nowValue )
            {
                nowValue = height;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最もHP割合が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool HPRatioTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.hpRatio > nowValue )
            {
                nowValue = charaData.liveData.hpRatio;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最もHPが高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool HPTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.currentHp > nowValue )
            {
                nowValue = charaData.liveData.currentHp;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最もMP割合が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool MPRatioTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.mpRatio > nowValue )
            {
                nowValue = charaData.liveData.mpRatio;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最もMPが高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool MPTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.currentMp > nowValue )
            {
                nowValue = charaData.liveData.currentMp;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も狙われている・狙われていないキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool AttentionTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.targetingCount > nowValue )
            {
                nowValue = charaData.targetingCount;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も攻撃力が高い・攻撃力が低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool AtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.dispAtk > nowValue )
            {
                nowValue = charaData.liveData.dispAtk;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も防御力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool DefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.dispDef > nowValue )
            {
                nowValue = charaData.liveData.dispDef;
                return true;
            }
        }
        // 一番低いやつを求める。
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

    #region 属性攻撃力

    /// <summary>
    /// 最も斬撃攻撃力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool SlashAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.atk.slash > nowValue )
            {
                nowValue = charaData.liveData.atk.slash;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も刺突攻撃力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool PierceAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.atk.pierce > nowValue )
            {
                nowValue = charaData.liveData.atk.pierce;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も打撃攻撃力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool StrikeAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.atk.strike > nowValue )
            {
                nowValue = charaData.liveData.atk.strike;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も炎攻撃力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool FireAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.atk.fire > nowValue )
            {
                nowValue = charaData.liveData.atk.fire;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も雷攻撃力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool LightningAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.atk.lightning > nowValue )
            {
                nowValue = charaData.liveData.atk.lightning;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も光攻撃力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool LightAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.atk.light > nowValue )
            {
                nowValue = charaData.liveData.atk.light;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も闇攻撃力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool DarkAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.atk.dark > nowValue )
            {
                nowValue = charaData.liveData.atk.dark;
                return true;
            }
        }
        // 一番低いやつを求める。
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

    #endregion 属性攻撃力

    #region 属性防御力

    /// <summary>
    /// 最も斬撃防御力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool SlashDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }

        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.def.slash > nowValue )
            {
                nowValue = charaData.liveData.def.slash;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も刺突防御力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool PierceDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.def.pierce > nowValue )
            {
                nowValue = charaData.liveData.def.pierce;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も打撃防御力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool StrikeDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.def.strike > nowValue )
            {
                nowValue = charaData.liveData.def.strike;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も炎防御力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool FireDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.def.fire > nowValue )
            {
                nowValue = charaData.liveData.def.fire;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も雷防御力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool LightningDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.def.lightning > nowValue )
            {
                nowValue = charaData.liveData.def.lightning;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も光防御力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool LightDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.def.light > nowValue )
            {
                nowValue = charaData.liveData.def.light;
                return true;
            }
        }
        // 一番低いやつを求める。
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
    /// 最も闇防御力が高い・低いキャラを割り出す。
    /// </summary>
    /// <param name="targetData">標的判断条件</param>
    /// <param name="charaData">敵キャラクター条件</param>
    /// <param name="nowValue">現在の値</param>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static bool DarkDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( !targetData.filter.IsPassFilter(charaData) )
        {
            return false;
        }
        // 一番高いやつを求める。
        if ( !targetData.isInvert )
        {
            if ( charaData.liveData.def.dark > nowValue )
            {
                nowValue = charaData.liveData.def.dark;
                return true;
            }
        }
        // 一番低いやつを求める。
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

    #endregion 属性防御力

    #endregion ヘイト・ターゲット判定

    #region 行動判断

    //指定のヘイト値の敵がいる時,
    //    対象が一定数の時,
    //    HPが一定割合の対象がいる時,
    //    設定距離に対象がいる時, 　//距離系の処理は別のやり方で事前にキャッシュを行う。AIの設定の範囲だけセンサーで調べる方法をとる
    //    任意のタイプの対象がいる時,
    //    対象が回復や支援を使用した時,// 回復魔法とかで回復した時に全体イベントを飛ばすか。
    //    対象が大ダメージを受けた時,
    //    対象が特定の特殊効果の影響を受けている時,//バフとかデバフ
    //    対象が死亡した時,
    //    対象が攻撃された時,
    //    特定の属性で攻撃する対象がいる時,
    //    特定の数の敵に狙われている時,// 陣営フィルタリングは有効

    #endregion 行動判断

}