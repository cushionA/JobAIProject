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
using System.Runtime.InteropServices; // MarshalAs属性用

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
    public delegate int SkipJudgeDelegate(in SkipJudgeData skipData, in CharacterData charaData);

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
    /// 更新した場合は1。1が来たらインデックス更新
    /// </summary>
    /// <param name="targetData"></param>
    /// <param name="charaData"></param>
    /// <returns></returns>
    public delegate int TargetJudgeDelegate(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue);

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
    public delegate int ActJudgeDelegate(in ActJudgeData actJudgeData, in CharacterData charaData);

    /// <summary>
    /// 行動条件のFunctionPointer配列
    /// </summary>
    public static NativeArray<FunctionPointer<ActJudgeDelegate>> actFunctions = new NativeArray<FunctionPointer<ActJudgeDelegate>>(12, Allocator.Persistent);

    #endregion 行動判断

    // FunctionPointerの初期化メソッドを追加
    static AiFunctionLibrary()
    {
        try
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
        catch ( Exception ex )
        {
            // デバッグ用に例外情報を出力
            Debug.LogError($"AiFunctionLibrary初期化エラー: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    #region スキップ条件判断

    /// <summary>
    /// スキップ条件の内、HP条件を判断するメソッド
    /// </summary>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int HPSkipJudge(in SkipJudgeData skipData, in CharacterData charaData)
    {
        // 各条件を個別に int で評価
        int equalCondition = skipData.judgeValue == charaData.liveData.hpRatio ? 1 : 0;
        int lessCondition = skipData.judgeValue < charaData.liveData.hpRatio ? 1 : 0;
        int invertCondition = skipData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 明示的に条件を組み合わせる
        int condition1 = equalCondition;
        int condition2 = (lessCondition != 0) == (invertCondition != 0) ? 1 : 0;

        if ( condition1 != 0 || condition2 != 0 )
        {
            return 1;
        }
        return 0;
    }

    /// <summary>
    /// スキップ条件の内、MP条件を判断するメソッド
    /// </summary>
    /// <returns></returns>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int MPSkipJudge(in SkipJudgeData skipData, in CharacterData charaData)
    {
        // 各条件を個別に int で評価
        int equalCondition = skipData.judgeValue == charaData.liveData.mpRatio ? 1 : 0;
        int lessCondition = skipData.judgeValue < charaData.liveData.mpRatio ? 1 : 0;
        int invertCondition = skipData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 明示的に条件を組み合わせる
        int condition1 = equalCondition;
        int condition2 = (lessCondition != 0) == (invertCondition != 0) ? 1 : 0;

        if ( condition1 != 0 || condition2 != 0 )
        {
            return 1;
        }
        return 0;
    }
    #endregion スキップ条件判断

    #region ヘイト・ターゲット判定

    /// <summary>
    /// 最も高度が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int HeightTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int height = (int)charaData.liveData.nowPosition.y;
        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める (isInvert == 1)
        if ( isInvert != 0 )
        {
            int isGreater = height > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = height;
                return 1;
            }
        }
        // 一番低いやつを求める (isInvert == 0)
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
    /// 最もHP割合が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int HPRatioTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.hpRatio > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.hpRatio;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最もHPが高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int HPTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.currentHp > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.currentHp;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も狙われている・狙われていないキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int AttentionTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.targetingCount > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.targetingCount;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も攻撃力が高い・攻撃力が低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int AtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.dispAtk > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.dispAtk;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も防御力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int DefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.dispDef > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.dispDef;
                return 1;
            }
        }
        // 一番低いやつを求める
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

    #region 属性攻撃力

    /// <summary>
    /// 最も斬撃攻撃力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int SlashAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.atk.slash > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.atk.slash;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も刺突攻撃力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int PierceAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.atk.pierce > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.atk.pierce;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も打撃攻撃力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int StrikeAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.atk.strike > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.atk.strike;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も炎攻撃力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int FireAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.atk.fire > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.atk.fire;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も雷攻撃力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int LightningAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.atk.lightning > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.atk.lightning;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も光攻撃力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int LightAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.atk.light > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.atk.light;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も闇攻撃力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int DarkAtkTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.atk.dark > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.atk.dark;
                return 1;
            }
        }
        // 一番低いやつを求める
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

    #endregion 属性攻撃力

    #region 属性防御力

    /// <summary>
    /// 最も斬撃防御力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int SlashDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.def.slash > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.def.slash;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も刺突防御力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int PierceDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.def.pierce > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.def.pierce;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も打撃防御力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int StrikeDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.def.strike > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.def.strike;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も炎防御力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int FireDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.def.fire > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.def.fire;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も雷防御力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int LightningDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.def.lightning > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.def.lightning;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も光防御力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int LightDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.def.light > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.def.light;
                return 1;
            }
        }
        // 一番低いやつを求める
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
    /// 最も闇防御力が高い・低いキャラを割り出す。
    /// </summary>
    [BurstCompile]
    [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
    public static int DarkDefTargetJudge(in TargetJudgeData targetData, in CharacterData charaData, ref int nowValue)
    {
        // フィルターをパスできなければ戻る。
        if ( targetData.filter.IsPassFilter(charaData) == 0 )
        {
            return 0;
        }

        int isInvert = targetData.isInvert == BitableBool.TRUE ? 1 : 0;

        // 一番高いやつを求める
        if ( isInvert != 0 )
        {
            int isGreater = charaData.liveData.def.dark > nowValue ? 1 : 0;
            if ( isGreater != 0 )
            {
                nowValue = charaData.liveData.def.dark;
                return 1;
            }
        }
        // 一番低いやつを求める
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

    #endregion 属性防御力

    #endregion ヘイト・ターゲット判定


}
