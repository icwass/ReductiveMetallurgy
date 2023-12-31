﻿//using MonoMod.RuntimeDetour;
//using MonoMod.Utils;
using Quintessential;
//using Quintessential.Settings;
//using SDL2;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace ReductiveMetallurgy;

using AtomTypes = class_175;

public static class API
{
	public const string RejectionPermission = "ReductiveMetallurgy:rejection";
	public const string DepositionPermission = "ReductiveMetallurgy:deposition";
	public const string ProliferationPermission = "ReductiveMetallurgy:proliferation";
	public const string RavariPermission = "ReductiveMetallurgy:ravari";

	public static MethodInfo PrivateMethod<T>(string method) => typeof(T).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

	#region atomtype getters
	public static AtomType quicksilverAtomType => AtomTypes.field_1680;
	public static AtomType leadAtomType => AtomTypes.field_1681;
	public static AtomType tinAtomType => AtomTypes.field_1683;
	public static AtomType ironAtomType => AtomTypes.field_1684;
	public static AtomType copperAtomType => AtomTypes.field_1682;
	public static AtomType silverAtomType => AtomTypes.field_1685;
	public static AtomType goldAtomType => AtomTypes.field_1686;
	public static AtomType[] vanillaNonmetalAtomTypes => new AtomType[9] {
		AtomTypes.field_1675, // salt
		AtomTypes.field_1676, // air
		AtomTypes.field_1677, // earth
		AtomTypes.field_1678, // fire
		AtomTypes.field_1679, // water
		AtomTypes.field_1687, // vitae
		AtomTypes.field_1688, // mors
		AtomTypes.field_1689, // repeat
		AtomTypes.field_1690, // quintessence
	};
	#endregion

	#region ruleDictionaries and methods
	private static Dictionary<AtomType, AtomType> rejectDict = new();
	private static Dictionary<AtomType, Pair<AtomType, AtomType>> depositDict = new();
	private static Dictionary<AtomType, AtomType> prolifDict = new();

	/// <summary>
	/// Indicates whether Rejection can be applied to an AtomType.
	/// </summary>
	/// <param name="input">The AtomType to check Rejection against.</param>
	/// <param name="output">Will contain the result of Rejection, if it can be applied. Do not use output if the function returns false.</param>
	/// <returns>True, if Rejection can be applied to the input AtomType. False, otherwise.</returns>
	public static bool applyRejectionRule(AtomType input, out AtomType output) => applyTRule(input, rejectDict, out output);
	/// <summary>
	/// Indicates whether Deposition can be applied to an AtomType.
	/// </summary>
	/// <param name="input">The AtomType to check Deposition against.</param>
	/// <param name="outputHi">Will contain the higher-metallicity result of Deposition, if it can be applied. Do not use outputHi if the function returns false.</param>
	/// <param name="outputLo">Will contain the lower-metallicity result of Deposition, if it can be applied. Do not use outputLo if the function returns false.</param>
	/// <returns>True, if Deposition can be applied to the input AtomType. False, otherwise.</returns>
	public static bool applyDepositionRule(AtomType input, out AtomType outputHi, out AtomType outputLo)
	{
		Pair<AtomType, AtomType> output;
		bool ret = applyTRule(input, depositDict, out output);
		outputHi = ret ? output.Left : default(AtomType);
		outputLo = ret ? output.Right : default(AtomType);
		return ret;
	}
	/// <summary>
	/// Indicates whether an AtomType can be used as the selector for Proliferation.
	/// </summary>
	/// <param name="input">The AtomType to check Proliferation against.</param>
	/// <returns>True, if the input AtomType can be used as the selector for Proliferation. False, otherwise.</returns>
	public static bool applyProliferationRule(AtomType selector) => applyTRule(selector, prolifDict, out _);
	public static void addRejectionRule(AtomType hi, AtomType lo) => addTRule("rejection", hi, lo, rejectDict, new List<AtomType> { quicksilverAtomType, leadAtomType });
	public static void addDepositionRule(AtomType input, AtomType outputHi, AtomType outputLo) => addTRule("deposition", input, new Pair<AtomType, AtomType>(outputHi, outputLo), depositDict, new List<AtomType> { quicksilverAtomType, leadAtomType });
	public static void addProliferationRule(AtomType selector) => addTRule("proliferation", selector, selector, prolifDict, new List<AtomType> { quicksilverAtomType });

	//rule-dictionary generics
	private static bool applyTRule<T>(AtomType hi, Dictionary<AtomType, T> dict, out T lo)
	{
		bool ret = dict.ContainsKey(hi);
		lo = ret ? dict[hi] : default(T);
		return ret;
	}
	private static string ToString(AtomType A) => A.field_2284;

	private static string ruleToString<T>(AtomType hi, T lo)
	{
		if (typeof(T) == typeof(AtomType))
		{
			return ToString(hi) + " => " + ToString((AtomType)(object)lo);
		}
		else if (typeof(T) == typeof(Pair<AtomType, AtomType>))
		{
			return ToString(hi) + " => ( " + ToString(((Pair<AtomType, AtomType>)(object)lo).Left) + ", " + ToString(((Pair<AtomType, AtomType>)(object)lo).Right) + " )";
		}
		return "";
	}
	private static bool TEquality<T>(T A, T B)
	{
		if (typeof(T) == typeof(AtomType))
		{
			return (AtomType)(object)A == (AtomType)(object)B;
		}
		else if (typeof(T) == typeof(Pair<AtomType, AtomType>))
		{
			return (Pair<AtomType, AtomType>)(object)A == (Pair<AtomType, AtomType>)(object)B;
		}
		return false;
	}
	private static void addTRule<T>(string Tname, AtomType input, T output, Dictionary<AtomType, T> dict, List<AtomType> forbiddenInputs)
	{
		string TNAME = Tname.First().ToString().ToUpper() + Tname.Substring(1);
		//check if rule is forbidden
		if (forbiddenInputs.Contains(input) || vanillaNonmetalAtomTypes.Contains(input))
		{
			Logger.Log("[ReductiveMetallurgy] ERROR: A " + Tname + " rule for " + ToString(input) + " is not permitted.");
			throw new Exception("add" + TNAME + "Rule: Cannot add rule '" + ruleToString(input, output) + "'.");
		}
		//try to add rule
		bool flag = dict.ContainsKey(input);
		if (flag && !TEquality(dict[input], output))
		{
			//throw an error
			string msg = "[ReductiveMetallurgy] ERROR: Preparing debug dump.";
			msg += "\n  Current list of " + TNAME + " Rules:";
			foreach (var kvp in dict) msg += "\n\t" + ruleToString(kvp.Key, kvp.Value);
			msg += "\n\n  AtomType '" + ToString(input) + "' already has a " + Tname + " rule: '" + ruleToString(input, dict[input]) + "'.";
			Logger.Log(msg);
			throw new Exception("add" + TNAME + "Rule: Cannot add rule '" + ruleToString(input, output) + "'.");
		}
		else if (!flag)
		{
			dict.Add(input, output);
		}
	}
	#endregion
}