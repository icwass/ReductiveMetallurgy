using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using Quintessential;
using Quintessential.Settings;
using SDL2;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace ReductiveMetallurgy;

using Permissions = enum_149;
using AtomTypes = class_175;

public static class API
{
	public static Permissions perm_rejection = (Permissions)	0x00080000;
	public static Permissions perm_deposition = (Permissions)	0x00100000;
	public static Permissions perm_proliferation = (Permissions)0x00200000;
	public static Permissions perm_ravari = (Permissions)		0x20000000;

	#region unused_permissions
	//public static Permissions unused = (Permissions)			0x00000010;
	//public static Permissions unused = (Permissions)			0x00000020;
	//public static Permissions unused = (Permissions)			0x00000040;
	//public static Permissions unused = (Permissions)			0x00000080;

	//public static Permissions unused = (Permissions)			0x08000000;

	//public static Permissions unused = (Permissions)			0x40000000;
	//public static Permissions unused = (Permissions)			0x80000000;
	#endregion
	public static MethodInfo PrivateMethod<T>(string method) => typeof(T).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

	private static void drawAndExecutePermissionCheckbox(PuzzleEditorScreen pes_self, Vector2 position, string label, Permissions perms)
	{
		var pes_dyn = new DynamicData(pes_self);
		var maybePuzzle = pes_dyn.Get<Maybe<Puzzle>>("field_2789");
		if (GameLogic.field_2434.method_938() is MoleculeEditorScreen
			|| GameLogic.field_2434.method_952<PuzzleInfoScreen>()
			|| !maybePuzzle.method_1085()
		) return;
		
		var puzzle = maybePuzzle.method_1087();
		API.PrivateMethod<PuzzleEditorScreen>("method_1261").Invoke(pes_self, new object[] { position, label, perms, puzzle });
	}
	public static void drawPermissionCheckboxes(PuzzleEditorScreen pes_self)
	{
		Vector2 base_position = new Vector2(1516f, 922f);
		base_position = (class_115.field_1433 / 2 - base_position / 2 + new Vector2(-2f, -11f)).Rounded();
		base_position = base_position + new Vector2(494f, 184f);

		Vector2 offset(float x, float y) => new Vector2(236f * x, -37f * y);

		drawAndExecutePermissionCheckbox(pes_self, base_position + offset(0, -1), "Rejection", API.perm_rejection);
		drawAndExecutePermissionCheckbox(pes_self, base_position + offset(0.5f, -1), "Deposition", API.perm_deposition);
		drawAndExecutePermissionCheckbox(pes_self, base_position + offset(3, -1), "Ravari", API.perm_ravari);
		drawAndExecutePermissionCheckbox(pes_self, base_position + offset(3.5f, -1), "Proliferation", API.perm_proliferation);
	}

	#region ruleDictionaries
	public static AtomType quicksilverAtomType() => AtomTypes.field_1680;
	public static AtomType leadAtomType() => AtomTypes.field_1681;
	public static AtomType tinAtomType() => AtomTypes.field_1683;
	public static AtomType ironAtomType() => AtomTypes.field_1684;
	public static AtomType copperAtomType() => AtomTypes.field_1682;
	public static AtomType silverAtomType() => AtomTypes.field_1685;
	public static AtomType goldAtomType() => AtomTypes.field_1686;

	private static Dictionary<AtomType, AtomType> rejectDict = new();
	private static Dictionary<AtomType, Pair<AtomType, AtomType>> depositDict = new();
	private static Dictionary<AtomType, Pair<AtomType, AtomType>> prolifDict = new();

	public static bool applyRejectionRule(AtomType hi, out AtomType lo) => applyTRule(hi, rejectDict, out lo);
	public static bool applyDepositionRule(AtomType hi, out Pair<AtomType, AtomType> lo) => applyTRule(hi, depositDict, out lo);
	public static bool applyProliferationRule(AtomType hi, out Pair<AtomType, AtomType> lo) => applyTRule(hi, prolifDict, out lo);
	public static void addRejectionRule(AtomType hi, AtomType lo) => addTRule("rejection", hi, lo, rejectDict, new List<AtomType> { quicksilverAtomType(), leadAtomType() });
	public static void addDepositionRule(AtomType hi, Pair<AtomType, AtomType> lo) => addTRule("deposition", hi, lo, depositDict, new List<AtomType> { quicksilverAtomType(), leadAtomType() });
	public static void addProliferationRule(AtomType hi, Pair<AtomType, AtomType> lo) => addTRule("proliferation", hi, lo, prolifDict, new List<AtomType> { quicksilverAtomType() });

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
	private static void addTRule<T>(string Tname, AtomType hi, T lo, Dictionary<AtomType, T> dict, List<AtomType> forbiddenInputs)
	{
		string TNAME = Tname.First().ToString().ToUpper() + Tname.Substring(1);
		//check if rule is forbidden
		if (forbiddenInputs.Contains(hi))
		{
			Logger.Log("[ReductiveMetallurgy] ERROR: A " + Tname + " rule for " + ToString(hi) + " is not permitted.");
			throw new Exception("add" + TNAME + "Rule: Cannot add rule '" + ruleToString(hi, lo) + "'.");
		}
		//try to add rule
		bool flag = dict.ContainsKey(hi);
		if (flag && !TEquality(dict[hi], lo))
		{
			//throw an error
			string msg = "[ReductiveMetallurgy] ERROR: Preparing debug dump.";
			msg += "\n  Current list of " + TNAME + " Rules:";
			foreach (var kvp in dict) msg += "\n\t" + ruleToString(kvp.Key, kvp.Value);
			msg += "\n\n  AtomType '" + ToString(hi) + "' already has a " + Tname + " rule: '" + ruleToString(hi, dict[hi]) + "'.";
			Logger.Log(msg);
			throw new Exception("add" + TNAME + "Rule: Cannot add rule '" + ruleToString(hi, lo) + "'.");
		}
		else if (!flag)
		{
			dict.Add(hi, lo);
		}
	}
	#endregion
}