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

using PartType = class_139;
using Permissions = enum_149;
using AtomTypes = class_175;
using PartTypes = class_191;
using Texture = class_256;

public class MainClass : QuintessentialMod
{
	// public resources, helper functions and APIs
	public static PartType glyphRejection, glyphSplitting, glyphProliferation;

	Permissions perm_rejection = (Permissions)524288; // 0x00080000
	Permissions perm_splitting = (Permissions)1048576; // 0x00100000
	//Permissions perm_proliferation = (Permissions)2097152; // 0x00200000
	Permissions perm_proliferation = Permissions.None; // 0x00000000

	#region unused_permissions
	//Permissions unused = (Permissions)16; // 0x00000010
	//Permissions unused = (Permissions)32; // 0x00000020
	//Permissions unused = (Permissions)64; // 0x00000040
	//Permissions unused = (Permissions)128; // 0x00000080

	//Permissions unused = (Permissions)134217728; // 0x08000000

	//Permissions unused = (Permissions)536870912; // 0x20000000
	//Permissions unused = (Permissions)1073741824; // 0x40000000
	//Permissions unused = (Permissions)2147483648; // 0x80000000
	#endregion

	public static void addRejectionRule(AtomType hi, AtomType lo)
	{
		if (hi == quicksilverAtomType())
		{
			//throw an error
			string msg = "[ReductiveMetallurgy] ERROR: A rejection rule for quicksilver is not permitted.";
			Logger.Log(msg);
			throw new class_266("addRejectionRule: Cannot add rule '" + hi.field_2284 + " => " + lo.field_2284 + "'.");
		}
		var dict = demoteDict;
		bool flag = dict.ContainsKey(hi);
		if (flag && dict[hi] != lo)
		{
			//throw an error
			string msg = "[ReductiveMetallurgy] ERROR: Preparing debug dump.";
			msg += "\n  Current list of Rejection Rules:";
			foreach (var kvp in dict)
			{
				msg += "\n    " + kvp.Key.field_2284 + " => " + kvp.Value.field_2284;
			}
			msg += "\n\n  AtomType '" + hi.field_2284 + "' already has a rejection rule: '" + hi.field_2284 + " => " + dict[hi].field_2284 + "'.";
			Logger.Log(msg);
			throw new class_266("addRejectionRule: Cannot add rule '" + hi.field_2284 + " => " + lo.field_2284 + "'.");
		}
		else if (!flag)
		{
			dict.Add(hi, lo);
		}
	}
	public static bool applyRejectionRule(AtomType hi, out AtomType lo)
	{
		lo = hi;
		bool ret = demoteDict.ContainsKey(hi);
		if (ret) lo = demoteDict[hi];
		return ret;
	}
	public static void addSplittingRule(AtomType hi, Pair<AtomType, AtomType> lo)
	{
		if (hi == leadAtomType() || hi == quicksilverAtomType())
		{
			//throw an error
			string atom = hi == leadAtomType() ? "lead" : "quicksilver";
			string msg = "[ReductiveMetallurgy] ERROR: A splitting rule for " + atom + " is not permitted.";
			Logger.Log(msg);
			throw new class_266("addSplittingRule: Cannot add rule '" + hi.field_2284 + " => < " + lo.Left.field_2284 + ", " + lo.Right.field_2284 + ">'.");
		}
		var dict = splitDict;
		bool flag = dict.ContainsKey(hi);
		if (flag && dict[hi] != lo)
		{
			//throw an error
			string msg = "[ReductiveMetallurgy] ERROR: Preparing debug dump.";
			msg += "\n  Current list of Splitting Rules:";
			foreach (var kvp in dict)
			{
				msg += "\n    " + kvp.Key.field_2284 + " => < " + kvp.Value.Left.field_2284 + ", " + kvp.Value.Right.field_2284 + ">";
			}
			msg += "\n\n  AtomType '" + hi.field_2284 + "' already has a splitting rule: '" + hi.field_2284 + " => < " + dict[hi].Left.field_2284 + ", " + dict[hi].Right.field_2284 + ">'.";
			Logger.Log(msg);
			throw new class_266("addSplittingRule: Cannot add rule '" + hi.field_2284 + " => < " + lo.Left.field_2284 + ", " + lo.Right.field_2284 + ">'.");
		}
		else if (!flag)
		{
			dict.Add(hi, lo);
		}
	}
	public static bool applySplittingRule(AtomType hi, out Pair<AtomType, AtomType> lo)
	{
		lo = new Pair<AtomType, AtomType>(hi,hi);
		bool ret = splitDict.ContainsKey(hi);
		if (ret) lo = splitDict[hi];
		return ret;
	}
	public static void addProliferationRule(AtomType hi, Pair<AtomType, AtomType> lo)
	{
		if (hi == quicksilverAtomType())
		{
			//throw an error
			string msg = "[ReductiveMetallurgy] ERROR: A proliferation rule for quicksilver is not permitted.";
			Logger.Log(msg);
			throw new class_266("addProliferationRule: Cannot add rule '" + hi.field_2284 + " => < " + lo.Left.field_2284 + ", " + lo.Right.field_2284 + ">'.");
		}
		var dict = prolifDict;
		bool flag = dict.ContainsKey(hi);
		if (flag && dict[hi] != lo)
		{
			//throw an error
			string msg = "[ReductiveMetallurgy] ERROR: Preparing debug dump.";
			msg += "\n  Current list of Proliferation Rules:";
			foreach (var kvp in dict)
			{
				msg += "\n    " + kvp.Key.field_2284 + " => < " + kvp.Value.Left.field_2284 + ", " + kvp.Value.Right.field_2284 + ">";
			}
			msg += "\n\n  AtomType '" + hi.field_2284 + "' already has a proliferation rule: '" + hi.field_2284 + " => < " + dict[hi].Left.field_2284 + ", " + dict[hi].Right.field_2284 + ">'.";
			Logger.Log(msg);
			throw new class_266("addProliferationRule: Cannot add rule '" + hi.field_2284 + " => < " + lo.Left.field_2284 + ", " + lo.Right.field_2284 + ">'.");
		}
		else if (!flag)
		{
			dict.Add(hi, lo);
		}
	}
	public static bool applyProliferationRule(AtomType hi, out Pair<AtomType, AtomType> lo)
	{
		lo = new Pair<AtomType, AtomType>(hi, hi);
		bool ret = prolifDict.ContainsKey(hi);
		if (ret) lo = prolifDict[hi];
		return ret;
	}


	// private resources
	private static IDetour hook_Sim_method_1832;

	private static Dictionary<AtomType, AtomType> demoteDict = new();
	private static Dictionary<AtomType, Pair<AtomType, AtomType>> splitDict = new();
	private static Dictionary<AtomType, Pair<AtomType, AtomType>> prolifDict = new();

	// private helper functions
	private static AtomType quicksilverAtomType() => AtomTypes.field_1680;
	private static AtomType leadAtomType() => AtomTypes.field_1681;
	private static AtomType tinAtomType() => AtomTypes.field_1683;
	private static AtomType ironAtomType() => AtomTypes.field_1684;
	private static AtomType copperAtomType() => AtomTypes.field_1682;
	private static AtomType silverAtomType() => AtomTypes.field_1685;
	private static AtomType goldAtomType() => AtomTypes.field_1686;

	private static bool glyphIsFiring(PartSimState partSimState) => partSimState.field_2743;
	private static void glyphNeedsToFire(PartSimState partSimState) => partSimState.field_2743 = true;
	//private static void glyphHasFired(PartSimState partSimState) => partSimState.field_2743 = false;

	private static void changeAtomTypeOfAtom(AtomReference atomReference, AtomType newAtomType)
	{
		atomReference.field_2277.method_1106(newAtomType, atomReference.field_2278);
	}

	private static void playSound(Sim sim_self, Sound sound)
	{
		typeof(Sim).GetMethod("method_1856", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sim_self, new object[] { sound });
	}

	private void drawAndExecutePermissionCheckbox(PuzzleEditorScreen pes_self, Vector2 position, string label, Permissions perms)
	{
		var pes_dyn = new DynamicData(pes_self);
		var maybePuzzle = pes_dyn.Get<Maybe<Puzzle>>("field_2789");
		if (!maybePuzzle.method_1085()) return;

		var puzzle = maybePuzzle.method_1087();
		typeof(PuzzleEditorScreen).GetMethod("method_1261", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pes_self, new object[] { position, label, perms, puzzle });
	}

	private static PartType makeGlyph(
		string id,
		string name,
		string desc,
		int cost,
		HexIndex[] footprint,
		Permissions permissions,
		Texture icon,
		Texture hover,
		Texture glow,
		Texture stroke,
		bool onlyOne = false)
	{
		PartType ret = new PartType()
		{
			/*ID*/field_1528 = id,
			/*Name*/field_1529 = class_134.method_253(name, string.Empty),
			/*Desc*/field_1530 = class_134.method_253(desc, string.Empty),
			/*Cost*/field_1531 = cost,
			/*Type*/field_1532 = 0,//default=(enum_2)0; arm=(enum_2)1; track=(enum_2)2;
			/*Is a Glyph?*/field_1539 = true,//default=false
			/*Hex Footprint*/field_1540 = footprint,//default=emptyList
			/*Icon*/field_1547 = icon,
			/*Hover Icon*/field_1548 = hover,
			/*Glow (Shadow)*/field_1549 = glow,
			/*Stroke (Outline)*/field_1550 = stroke,
			/*Permissions*/field_1551 = permissions,
			/*Only One Allowed?*/field_1552 = onlyOne,//default=false
		};
		return ret;
	}

	//drawing helpers

	private static Vector2 hexGraphicalOffset(HexIndex hex) => class_187.field_1742.method_492(hex);
	private static Vector2 textureDimensions(Texture tex) => tex.field_2056.ToVector2();
	private static Vector2 textureCenter(Texture tex) => (textureDimensions(tex) / 2).Rounded();
	private static void drawPartGraphic(class_195 renderer, Texture tex, Vector2 graphicPivot, float graphicAngle, Vector2 graphicTranslation, Vector2 screenTranslation)
	{
		drawPartGraphicScaled(renderer, tex, graphicPivot, graphicAngle, graphicTranslation, screenTranslation, new Vector2(1f, 1f));
	}

	private static void drawPartGraphicScaled(class_195 renderer, Texture tex, Vector2 graphicPivot, float graphicAngle, Vector2 graphicTranslation, Vector2 screenTranslation, Vector2 scaling)
	{
		//for graphicPivot and graphicTranslation, rightwards is the positive-x direction and upwards is the positive-y direction
		//graphicPivot is an absolute position, with (0,0) denoting the bottom-left corner of the texture
		//graphicTranslation is a translation, so (5,-3) means "translate 5 pixels right and 3 pixels down"
		//graphicAngle is measured in radians, and counterclockwise is the positive-angle direction
		//screenTranslation is the final translation applied, so it is not affected by rotations
		Matrix4 matrixScreenPosition	= Matrix4.method_1070(renderer.field_1797.ToVector3(0f));
		Matrix4 matrixTranslateOnScreen = Matrix4.method_1070(screenTranslation.ToVector3(0f));
		Matrix4 matrixRotatePart		= Matrix4.method_1073(renderer.field_1798);
		Matrix4 matrixTranslateGraphic	= Matrix4.method_1070(graphicTranslation.ToVector3(0f));
		Matrix4 matrixRotateGraphic		= Matrix4.method_1073(graphicAngle);
		Matrix4 matrixPivotOffset		= Matrix4.method_1070(-graphicPivot.ToVector3(0f));
		Matrix4 matrixScaling			= Matrix4.method_1074(scaling.ToVector3(0f));
		Matrix4 matrixTextureSize		= Matrix4.method_1074(tex.field_2056.ToVector3(0f));

		Matrix4 matrix4 = matrixScreenPosition * matrixTranslateOnScreen * matrixRotatePart * matrixTranslateGraphic * matrixRotateGraphic * matrixPivotOffset * matrixScaling * matrixTextureSize;
		class_135.method_262(tex, Color.White, matrix4);
	}

	private void drawPartGraphicSpecular(class_195 renderer, Texture tex, Vector2 graphicPivot, float graphicAngle, Vector2 graphicTranslation, Vector2 screenTranslation)
	{
		float specularAngle = (renderer.field_1799 - (renderer.field_1797 + graphicTranslation.Rotated(renderer.field_1798))).Angle() - 1.570796f - renderer.field_1798;
		drawPartGraphic(renderer, tex, graphicPivot, graphicAngle + specularAngle, graphicTranslation, screenTranslation);
	}

	private void drawPartGloss(class_195 renderer, Texture gloss, Texture glossMask, Vector2 offset)
	{
		class_135.method_257().field_1692 = class_238.field_1995.field_1757; // MaskedGlossPS shader
		class_135.method_257().field_1693[1] = gloss;
		HexIndex hex = new HexIndex(0, 0);
		Vector2 method2001 = 0.0001f * (renderer.field_1797 + hexGraphicalOffset(hex).Rotated(renderer.field_1798) - 0.5f * class_115.field_1433);
		class_135.method_257().field_1695 = method2001;
		drawPartGraphic(renderer, glossMask, offset, 0f, Vector2.Zero, Vector2.Zero);
		class_135.method_257().field_1692 = class_135.method_257().field_1696; // previous shader
		class_135.method_257().field_1693[1] = class_238.field_1989.field_71;
		class_135.method_257().field_1695 = Vector2.Zero;
	}

	private void drawAtomIO(class_195 renderer, AtomType atomType, HexIndex hex, float num)
	{
		Molecule molecule = Molecule.method_1121(atomType);
		Vector2 method1999 = renderer.field_1797 + hexGraphicalOffset(hex).Rotated(renderer.field_1798);
		Editor.method_925(molecule, method1999, new HexIndex(0,0), 0f, 1f, num, 1f, false, null);
	}

	// private main functions


	// public main functions
	public override void Load()	{ }
	public override void LoadPuzzleContent()
	{
		//add rejection rules for vanilla metals
		foreach (var atomtype in AtomTypes.field_1691)
		{
			if (atomtype.field_2297.method_1085())
			{
				addRejectionRule(atomtype.field_2297.method_1087(), atomtype);
			}
		}

		//add splitting rules for vanilla metals
		addSplittingRule(goldAtomType(), new Pair<AtomType, AtomType>(ironAtomType(), ironAtomType()));
		addSplittingRule(silverAtomType(), new Pair<AtomType, AtomType>(ironAtomType(), tinAtomType()));
		addSplittingRule(copperAtomType(), new Pair<AtomType, AtomType>(tinAtomType(), tinAtomType()));
		addSplittingRule(ironAtomType(), new Pair<AtomType, AtomType>(tinAtomType(), leadAtomType()));
		addSplittingRule(tinAtomType(), new Pair<AtomType, AtomType>(leadAtomType(), leadAtomType()));

		//add proliferation rules for vanilla metals
		addProliferationRule(goldAtomType(), new Pair<AtomType, AtomType>(copperAtomType(), copperAtomType()));
		addProliferationRule(silverAtomType(), new Pair<AtomType, AtomType>(copperAtomType(), ironAtomType()));
		addProliferationRule(copperAtomType(), new Pair<AtomType, AtomType>(ironAtomType(), ironAtomType()));
		addProliferationRule(ironAtomType(), new Pair<AtomType, AtomType>(ironAtomType(), tinAtomType()));
		addProliferationRule(tinAtomType(), new Pair<AtomType, AtomType>(tinAtomType(), tinAtomType()));
		addProliferationRule(leadAtomType(), new Pair<AtomType, AtomType>(tinAtomType(), leadAtomType()));


		//make glyphs
		string path;
		path = "reductiveMetallurgy/textures/parts/icons/";

		glyphRejection = makeGlyph(
			"glyph-rejection",
			"Glyph of Rejection",
			"The glyph of rejection extracts quicksilver to demote an atom of metal to a lower form.",
			20, new HexIndex[2] {new HexIndex(0, 0), new HexIndex(1, 0)}, perm_rejection,
			class_235.method_615(path + "rejection"),
			class_235.method_615(path + "rejection_hover"),
			class_238.field_1989.field_97.field_374,// double_glow
			class_238.field_1989.field_97.field_375 // double_stroke
		);

		glyphSplitting = makeGlyph(
			"glyph-splitting",
			"Glyph of Splitting",
			"The glyph of splitting can separate an atom of metal into two atoms of lower form.",
			20, new HexIndex[3] { new HexIndex(0, 0), new HexIndex(1, 0), new HexIndex(0, 1) }, perm_splitting,
			class_235.method_615(path + "splitting"),
			class_235.method_615(path + "splitting_hover"),
			class_238.field_1989.field_97.field_386,// triple_glow
			class_238.field_1989.field_97.field_387 // triple_stroke
		);

		glyphProliferation = makeGlyph(
			"glyph-proliferation",
			"Glyph of proliferation",
			"The glyph of proliferation consumes quicksilver and an atom of metal to generate two atoms of lower form.",
			50, new HexIndex[4] { new HexIndex(0, 0), new HexIndex(1, 0), new HexIndex(0, 1), new HexIndex(1, -1) }, perm_proliferation,
			class_235.method_615(path + "proliferation"),
			class_235.method_615(path + "proliferation_hover"),
			class_238.field_1989.field_97.field_368,// diamond_glow
			class_238.field_1989.field_97.field_369 // diamond_stroke
		);

		var projector = PartTypes.field_1778;
		var purifier = PartTypes.field_1779;

		QApi.AddPartTypeToPanel(glyphRejection, projector);
		QApi.AddPartTypeToPanel(glyphSplitting, purifier);
		QApi.AddPartTypeToPanel(glyphProliferation, purifier);


		path = "reductiveMetallurgy/textures/parts/";
		Texture leadSymbolBowlDown = class_235.method_615(path + "lead_symbol_bowl_down");
		Texture rejection_metalBowlTarget = class_235.method_615(path + "rejection_metal_bowl_target");
		Texture rejection_quicksilverSymbol = class_235.method_615(path + "rejection_quicksilver_symbol");
		Texture leadSymbolInputDown = class_235.method_615(path + "lead_symbol_input_down");

		path = "reductiveMetallurgy/textures/parts/proliferation/";
		Texture[] proliferationSymbols = new Texture[5]{
			class_235.method_615(path + "symbol_divider"),
			class_235.method_615(path + "symbol_quicksilver_inactive"),
			class_235.method_615(path + "symbol_quicksilver_active"),
			class_235.method_615(path + "symbol_lead_inactive"),
			class_235.method_615(path + "symbol_lead_active")
		};

		// fetch vanilla textures
		Texture bonderShadow = class_238.field_1989.field_90.field_164;

		//Texture calcinationGlyph_bowl = class_238.field_1989.field_90.field_170;

		Texture animismus_base = class_238.field_1989.field_90.field_228.field_265;
		Texture animismus_connectors = class_238.field_1989.field_90.field_228.field_266;
		Texture animismus_connectorsShadows = class_238.field_1989.field_90.field_228.field_267;
		Texture animismus_gloss = class_238.field_1989.field_90.field_228.field_268;
		Texture animismus_glossMask = class_238.field_1989.field_90.field_228.field_269;
		Texture animismus_input = class_238.field_1989.field_90.field_228.field_270;
		Texture animismus_outputAboveIris = class_238.field_1989.field_90.field_228.field_271;
		Texture animismus_outputUnderIris = class_238.field_1989.field_90.field_228.field_272;
		Texture animismus_ringShadow = class_238.field_1989.field_90.field_228.field_273;

		Texture projectionGlyph_base = class_238.field_1989.field_90.field_255.field_288;
		Texture projectionGlyph_bond = class_238.field_1989.field_90.field_255.field_289;
		Texture projectionGlyph_glossMask = class_238.field_1989.field_90.field_255.field_290;
		Texture projectionGlyph_metalBowl = class_238.field_1989.field_90.field_255.field_292;

		Texture projectionGlyph_leadSymbol = class_238.field_1989.field_90.field_255.field_291;
		Texture projectionGlyph_quicksilverInput = class_238.field_1989.field_90.field_255.field_293;
		//Texture projectionGlyph_quicksilverSymbol = class_238.field_1989.field_90.field_255.field_294;

		Texture purificationGlyph_base = class_238.field_1989.field_90.field_257.field_359;
		Texture purificationGlyph_connectors = class_238.field_1989.field_90.field_257.field_360;
		Texture purificationGlyph_gloss = class_238.field_1989.field_90.field_257.field_361;
		Texture purificationGlyph_glossMask = class_238.field_1989.field_90.field_257.field_362;
		//Texture purificationGlyph_leadSymbol = class_238.field_1989.field_90.field_257.field_363;

		Texture[] irisFullArray = class_238.field_1989.field_90.field_246;

		QApi.AddPartType(glyphRejection, (part, pos, editor, renderer) =>
		{
			PartSimState partSimState = editor.method_507().method_481(part);
			var simTime = editor.method_504(); 
			
			var originHex = new HexIndex(0, 0);
			var metalHex = originHex;
			var outputHex = new HexIndex(1, 0);

			float partAngle = renderer.field_1798;
			Vector2 base_offset = new Vector2(41f, 48f);
			drawPartGraphic(renderer, projectionGlyph_base, base_offset, 0.0f, Vector2.Zero, new Vector2(-1f, -1f));

			//draw metal bowl
			drawPartGraphic(renderer, bonderShadow, textureDimensions(bonderShadow) / 2, 0f, hexGraphicalOffset(metalHex), new Vector2(0.0f, -3f));
			drawPartGraphicSpecular(renderer, projectionGlyph_metalBowl, textureCenter(projectionGlyph_metalBowl), 0f, hexGraphicalOffset(metalHex), Vector2.Zero);
			drawPartGraphic(renderer, leadSymbolBowlDown, textureCenter(leadSymbolBowlDown), -partAngle, hexGraphicalOffset(metalHex), Vector2.Zero);

			//draw quicksilver output
			drawPartGraphic(renderer, bonderShadow, textureDimensions(bonderShadow) / 2, 0f, hexGraphicalOffset(outputHex), new Vector2(0.0f, -3f));
			drawPartGraphicSpecular(renderer, projectionGlyph_metalBowl, textureCenter(projectionGlyph_metalBowl), 0f, hexGraphicalOffset(outputHex), Vector2.Zero);
			drawPartGraphic(renderer, rejection_metalBowlTarget, textureCenter(rejection_metalBowlTarget), -partAngle, hexGraphicalOffset(outputHex), Vector2.Zero);
			drawPartGraphic(renderer, rejection_quicksilverSymbol, textureCenter(rejection_quicksilverSymbol), -partAngle, hexGraphicalOffset(outputHex), Vector2.Zero);

			drawPartGraphic(renderer, projectionGlyph_bond, base_offset + new Vector2(-73f, -37f), 0f, Vector2.Zero, Vector2.Zero);
			drawPartGloss(renderer, purificationGlyph_gloss, projectionGlyph_glossMask, base_offset);
		});

		QApi.AddPartType(glyphSplitting, (part, pos, editor, renderer) =>
		{
			PartSimState partSimState = editor.method_507().method_481(part);
			var simTime = editor.method_504();

			var originHex = new HexIndex(0, 0);
			var leftHex = originHex;
			var rightHex = new HexIndex(1, 0);
			var inputHex = new HexIndex(0, 1);
			float partAngle = renderer.field_1798;
			Vector2 base_offset = new Vector2(41f, 48f);

			int index = irisFullArray.Length - 1;
			float num = 0f;
			bool flag = false;
			if (partSimState.field_2743)
			{
				index = class_162.method_404((int)(class_162.method_411(1f, -1f, simTime) * irisFullArray.Length), 0, irisFullArray.Length - 1);
				num = simTime;
				flag = (double)simTime > 0.5;
			}

			drawPartGraphic(renderer, purificationGlyph_base, base_offset, 0f, Vector2.Zero, new Vector2(-1f, -1f));
			drawPartGraphic(renderer, bonderShadow, textureCenter(bonderShadow), 0f, hexGraphicalOffset(inputHex), new Vector2(0f, -3f));
			foreach (var hex in new HexIndex[2] { leftHex, rightHex })
			{
				drawPartGraphic(renderer, bonderShadow, textureCenter(bonderShadow), 0f, hexGraphicalOffset(hex), new Vector2(0f, -3f));
				drawPartGraphicSpecular(renderer, animismus_outputUnderIris, textureCenter(animismus_outputUnderIris), 0f, hexGraphicalOffset(hex), Vector2.Zero);
				if (partSimState.field_2743 && !flag)
				{
					drawAtomIO(renderer, partSimState.field_2744[hex.Q], hex, num);
				}
				drawPartGraphicSpecular(renderer, irisFullArray[index], textureCenter(irisFullArray[index]), 0f, hexGraphicalOffset(hex), Vector2.Zero);
				drawPartGraphicSpecular(renderer, animismus_outputAboveIris, textureCenter(animismus_outputAboveIris), 0f, hexGraphicalOffset(hex), Vector2.Zero);
				if (index == irisFullArray.Length - 1)
				{
					Texture tex = hex == leftHex ? projectionGlyph_leadSymbol : leadSymbolBowlDown;
					drawPartGraphic(renderer, tex, textureCenter(tex), -partAngle, hexGraphicalOffset(hex), Vector2.Zero);
				}
				if (flag)
				{
					drawAtomIO(renderer, partSimState.field_2744[hex.Q], hex, num);
				}
			}
			drawPartGraphicSpecular(renderer, projectionGlyph_quicksilverInput, textureCenter(projectionGlyph_quicksilverInput), 0f, hexGraphicalOffset(inputHex), Vector2.Zero);
			drawPartGraphic(renderer, leadSymbolInputDown, textureCenter(leadSymbolInputDown), -partAngle, hexGraphicalOffset(inputHex), Vector2.Zero);
			drawPartGraphic(renderer, purificationGlyph_connectors, base_offset, 0f, Vector2.Zero, Vector2.Zero);
			drawPartGloss(renderer, purificationGlyph_gloss, purificationGlyph_glossMask, base_offset + new Vector2(0f, -1f));
		});

		QApi.AddPartType(glyphProliferation, (part, pos, editor, renderer) =>
		{
			var interface2 = editor.method_507();
			PartSimState partSimState = interface2.method_481(part);
			var simTime = editor.method_504();

			var originHex = new HexIndex(0, 0);
			var leftHex = originHex;
			var rightHex = new HexIndex(1, 0);
			var upHex = new HexIndex(0, 1);
			var downHex = new HexIndex(1, -1);
			float partAngle = renderer.field_1798;
			Vector2 base_offset = new Vector2(41f, 120f);

			int irisIndex = irisFullArray.Length - 1;
			float num = 0f;
			bool flag = false;
			if (partSimState.field_2743)
			{
				irisIndex = class_162.method_404((int)(class_162.method_411(1f, -1f, simTime) * irisFullArray.Length), 0, irisFullArray.Length - 1);
				num = simTime;
				flag = simTime > 0.5f;
			}

			List<AtomType> atomsOfferedAsInput = new List<AtomType>();
			foreach (var hex in new HexIndex[2] { upHex, downHex })
			{
				Atom atom;
				HexIndex key = part.method_1184(hex);
				foreach (Molecule molecule in interface2.method_483().Where(x => x.method_1100().Count == 1)) // foreach one-atom molecule
				{
					if (molecule.method_1100().TryGetValue(key, out atom))
						atomsOfferedAsInput.Add(atom.field_2275);
				}
			}

			Texture[] symbolTextures = new Texture[3]
			{
				proliferationSymbols[0],
				proliferationSymbols[ atomsOfferedAsInput.Contains(quicksilverAtomType()) ? 1 : 2],
				proliferationSymbols[ atomsOfferedAsInput.Any(x => prolifDict.Keys.Contains(x)) ? 3 : 4]
			};

			drawPartGraphic(renderer, animismus_base, base_offset, 0f, Vector2.Zero, new Vector2(-1f, -1f));
			drawPartGraphic(renderer, animismus_connectorsShadows, base_offset, 0f, Vector2.Zero, Vector2.Zero);

			foreach (var hex in new HexIndex[4] { leftHex, rightHex, upHex, downHex })
			{
				bool isInputHex = hex.R != 0;
				drawPartGraphic(renderer, animismus_ringShadow, textureCenter(animismus_ringShadow), 0f, hexGraphicalOffset(hex), new Vector2(0f, -3f));
				if (isInputHex)
				{
					drawPartGraphicSpecular(renderer, animismus_input, textureCenter(animismus_input), 0f, hexGraphicalOffset(hex), Vector2.Zero);
					foreach (Texture texture in symbolTextures)
						drawPartGraphic(renderer, texture, textureCenter(texture), -renderer.field_1798, hexGraphicalOffset(hex), Vector2.Zero);
				}
				else
				{
					drawPartGraphicSpecular(renderer, animismus_outputUnderIris, textureCenter(animismus_outputUnderIris), 0f, hexGraphicalOffset(hex), Vector2.Zero);
					if (partSimState.field_2743 && !flag)
						drawAtomIO(renderer, partSimState.field_2744[hex.Q], hex, num);
					Texture irisFrame = irisFullArray[irisIndex];
					drawPartGraphic(renderer, irisFrame, textureCenter(irisFrame), -renderer.field_1798, hexGraphicalOffset(hex), Vector2.Zero);
					drawPartGraphicSpecular(renderer, animismus_outputAboveIris, textureCenter(animismus_outputAboveIris), 0f, hexGraphicalOffset(hex), Vector2.Zero);
					if (irisIndex == irisFullArray.Length - 1)
					{
						Texture tex = hex == leftHex ? projectionGlyph_leadSymbol : leadSymbolBowlDown;
						drawPartGraphic(renderer, tex, textureCenter(tex), -partAngle, hexGraphicalOffset(hex), Vector2.Zero);
					}
					if (flag)
					{
						drawAtomIO(renderer, partSimState.field_2744[hex.Q], hex, num);
					}
				}
			}

			drawPartGraphic(renderer, animismus_connectors, base_offset, 0f, Vector2.Zero, Vector2.Zero);
			drawPartGloss(renderer, animismus_gloss, animismus_glossMask, base_offset + new Vector2(-1f, 0f));
		});



		//------------------------- HOOKING -------------------------//
		hook_Sim_method_1832 = new Hook(
			typeof(Sim).GetMethod("method_1832", BindingFlags.Instance | BindingFlags.NonPublic),
			typeof(MainClass).GetMethod("OnSimMethod1832", BindingFlags.Static | BindingFlags.NonPublic)
		);
	}

	private delegate void orig_Sim_method_1832(Sim self, bool param_5369);
	private static void OnSimMethod1832(orig_Sim_method_1832 orig, Sim sim_self, bool param_5369)
	{
		My_Method_1832(sim_self, param_5369);
		orig(sim_self, param_5369);
	}
	public static void My_Method_1832(Sim sim_self, bool isConsumptionHalfstep)
	{
		//----- BOILERPLATE-1 START -----//
		var sim_dyn = new DynamicData(sim_self);
		var SEB = sim_dyn.Get<SolutionEditorBase>("field_3818");
		var solution = SEB.method_502();
		var partList = solution.field_3919;
		var partSimStates = sim_dyn.Get<Dictionary<Part, PartSimState>>("field_3821");
		var struct122List = sim_dyn.Get<List<Sim.struct_122>>("field_3826");
		var moleculeList = sim_dyn.Get<List<Molecule>>("field_3823");

		List<Part> gripperList = new List<Part>();
		foreach (Part part in partList)
		{
			foreach (Part key in part.field_2696.Where(x=>partSimStates[x].field_2729.method_1085()))//for each gripper that is holding a molecule
			{
				//add gripper to gripperList
				gripperList.Add(key);
				//expanded version of sim_self.method_1842(key);//release molecule from the gripper
				PartSimState partSimState = partSimStates[key];
				partSimState.field_2728 = false;
				partSimState.field_2729 = struct_18.field_1431;
			}
		}
		//----- BOILERPLATE-1 END -----//

		//define some helpers
		Type simType = typeof(Sim);

		Maybe<AtomReference> maybeFindAtom(Part part, HexIndex hex, List<Part> gripperList)
		{
			MethodInfo Method_1850 = simType.GetMethod("method_1850", BindingFlags.NonPublic | BindingFlags.Instance);
			return (Maybe<AtomReference>)Method_1850.Invoke(sim_self, new object[] { part, hex, gripperList, false });
		}

		void addColliderAtHex(Part part, HexIndex hex)
		{
			struct122List.Add(new Sim.struct_122()
			{
				field_3850 = (Sim.enum_190)0,
				field_3851 = hexGraphicalOffset(part.method_1184(hex)),
				field_3852 = 15f // Sim.field_3832;
			});
		}

		void spawnAtomAtHex(Part part, HexIndex hex, AtomType atom)
		{
			Molecule molecule = new Molecule();
			molecule.method_1105(new Atom(atom), part.method_1184(hex));
			moleculeList.Add(molecule);
		}

		Sound animismusActivate = class_238.field_1991.field_1838;
		Sound projectionActivate = class_238.field_1991.field_1844;
		Sound purificationActivate = class_238.field_1991.field_1845;

		// fire the glyphs!
		foreach (Part part in partList)
		{
			PartSimState partSimState = partSimStates[part];
			var partType = part.method_1159();

			if (partType == glyphRejection)
			{
				HexIndex hexReject = new HexIndex(0, 0);
				HexIndex hexOutput = new HexIndex(1, 0);
				AtomReference atomDemote;
				AtomType rejectedAtomType;
				if (maybeFindAtom(part, hexReject, new List<Part>()).method_99(out atomDemote) // demotable atom exists - don't care if it's held
				&& !maybeFindAtom(part, hexOutput, new List<Part>()).method_99(out _) // output not blocked
				&& applyRejectionRule(atomDemote.field_2280, out rejectedAtomType)
				)
				{
					playSound(sim_self, projectionActivate);
					//demote input
					changeAtomTypeOfAtom(atomDemote, rejectedAtomType);
					Texture[] projectAtomAnimation = class_238.field_1989.field_81.field_614;
					atomDemote.field_2279.field_2276 = (Maybe<class_168>)new class_168(SEB, (enum_7)0, (enum_132)1, atomDemote.field_2280, projectAtomAnimation, 30f);
					//glyph-flash animation
					Vector2 hexPosition = hexGraphicalOffset(part.method_1161() + hexReject.Rotated(part.method_1163()));
					Texture[] projectionGlyphFlashAnimation = class_238.field_1989.field_90.field_256;
					float radians = (part.method_1163() + HexRotation.R180).ToRadians();
					SEB.field_3935.Add(new class_228(SEB, (enum_7)1, hexPosition, projectionGlyphFlashAnimation, 30f, Vector2.Zero, radians));
					//take care of outputs
					spawnAtomAtHex(part, hexOutput, quicksilverAtomType());
					Texture[] disposalFlashAnimation = class_238.field_1989.field_90.field_240;
					Vector2 animationPosition = hexGraphicalOffset(part.method_1161() + hexOutput.Rotated(part.method_1163())) + new Vector2(80f, 0f);
					SEB.field_3936.Add(new class_228(SEB, (enum_7)1, animationPosition, disposalFlashAnimation, 30f, Vector2.Zero, 0f));

				}
			}
			else if (partType == glyphSplitting)
			{
				HexIndex hexLeft = new HexIndex(0, 0);
				HexIndex hexRight = new HexIndex(1, 0);
				HexIndex hexInput = new HexIndex(0, 1);

				if (!glyphIsFiring(partSimState))
				{
					AtomReference atomSplit;
					Pair<AtomType, AtomType> splitAtomTypePair;

					if (isConsumptionHalfstep
					&& !maybeFindAtom(part, hexLeft, new List<Part>()).method_99(out _) // left output not blocked
					&& !maybeFindAtom(part, hexRight, new List<Part>()).method_99(out _) // right output not blocked
					&& maybeFindAtom(part, hexInput, gripperList).method_99(out atomSplit) // splittable atom exists
					&& !atomSplit.field_2281 // a single atom
					&& !atomSplit.field_2282 // not held by a gripper
					&& applySplittingRule(atomSplit.field_2280, out splitAtomTypePair) // is splittable
					)
					{
						glyphNeedsToFire(partSimState);
						playSound(sim_self, purificationActivate);
						// delete the input atom
						atomSplit.field_2277.method_1107(atomSplit.field_2278);
						// draw input getting consumed
						SEB.field_3937.Add(new class_286(SEB, atomSplit.field_2278, atomSplit.field_2280));
						// take care of outputs
						partSimState.field_2744 = new AtomType[2] { splitAtomTypePair.Left, splitAtomTypePair.Right };
						addColliderAtHex(part, hexLeft);
						addColliderAtHex(part, hexRight);
					}
				}
				else
				{
					spawnAtomAtHex(part, hexLeft, partSimState.field_2744[0]);
					spawnAtomAtHex(part, hexRight, partSimState.field_2744[1]);
				}
			}
			else if (partType == glyphProliferation)
			{
				HexIndex hexLeft = new HexIndex(0, 0);
				HexIndex hexRight = new HexIndex(1, 0);
				HexIndex hexUp = new HexIndex(0, 1);
				HexIndex hexDown = new HexIndex(1, -1);

				if (!glyphIsFiring(partSimState))
				{
					AtomReference atomUp;
					AtomReference atomDown;

					if (isConsumptionHalfstep
					&& !maybeFindAtom(part, hexLeft, new List<Part>()).method_99(out _) // left output not blocked
					&& !maybeFindAtom(part, hexRight, new List<Part>()).method_99(out _) // right output not blocked
					&& maybeFindAtom(part, hexUp, gripperList).method_99(out atomUp) // up atom exists
					&& !atomUp.field_2281 // a single atom
					&& !atomUp.field_2282 // not held by a gripper
					&& maybeFindAtom(part, hexDown, gripperList).method_99(out atomDown) // down atom exists
					&& !atomDown.field_2281 // a single atom
					&& !atomDown.field_2282 // not held by a gripper
					&& (atomUp.field_2280 == quicksilverAtomType() || atomDown.field_2280 == quicksilverAtomType())
					)
					{
						Pair<AtomType, AtomType> prolifAtomTypePair;
						void fireProliferate(AtomReference atomProlif, AtomReference atomQuicksilver)
						{
							glyphNeedsToFire(partSimState);
							playSound(sim_self, animismusActivate);
							// delete the input atoms
							atomProlif.field_2277.method_1107(atomProlif.field_2278);
							atomQuicksilver.field_2277.method_1107(atomQuicksilver.field_2278);
							// draw input getting consumed
							SEB.field_3937.Add(new class_286(SEB, atomProlif.field_2278, atomProlif.field_2280));
							SEB.field_3937.Add(new class_286(SEB, atomQuicksilver.field_2278, atomQuicksilver.field_2280));
							// take care of outputs
							partSimState.field_2744 = new AtomType[2] { prolifAtomTypePair.Left, prolifAtomTypePair.Right };
							addColliderAtHex(part, hexLeft);
							addColliderAtHex(part, hexRight);

						}
						
						if (applyProliferationRule(atomUp.field_2280, out prolifAtomTypePair))
						{
							fireProliferate(atomUp, atomDown);
						}
						else if (applyProliferationRule(atomDown.field_2280, out prolifAtomTypePair))
						{
							fireProliferate(atomDown, atomUp);
						}
					}
				}
				else
				{
					spawnAtomAtHex(part, hexLeft, partSimState.field_2744[0]);
					spawnAtomAtHex(part, hexRight, partSimState.field_2744[1]);
				}
			}
		}

		//----- BOILERPLATE-2 START -----//
		List<Molecule> source1 = new List<Molecule>();
		foreach (Molecule molecule9 in moleculeList.Where(x=>x.field_2638))
		{
			HashSet<HexIndex> source2 = new HashSet<HexIndex>(molecule9.method_1100().Keys);
			Queue<HexIndex> hexIndexQueue = new Queue<HexIndex>();
			while (source2.Count > 0)
			{
				if (hexIndexQueue.Count == 0)
				{
					HexIndex key = source2.First<HexIndex>();
					source2.Remove(key);
					hexIndexQueue.Enqueue(key);
					source1.Add(new Molecule());
					source1.Last<Molecule>().method_1105(molecule9.method_1100()[key], key);
				}
				HexIndex hexIndex = hexIndexQueue.Dequeue();
				foreach (class_277 class277 in (IEnumerable<class_277>)molecule9.method_1101())
				{
					Maybe<HexIndex> maybe = (Maybe<HexIndex>)struct_18.field_1431;
					if (class277.field_2187 == hexIndex)
						maybe = (Maybe<HexIndex>)class277.field_2188;
					else if (class277.field_2188 == hexIndex)
						maybe = (Maybe<HexIndex>)class277.field_2187;
					if (maybe.method_1085() && source2.Contains(maybe.method_1087()))
					{
						source2.Remove(maybe.method_1087());
						hexIndexQueue.Enqueue(maybe.method_1087());
						source1.Last<Molecule>().method_1105(molecule9.method_1100()[maybe.method_1087()], maybe.method_1087());
					}
				}
			}
			foreach (class_277 class277 in (IEnumerable<class_277>)molecule9.method_1101())
			{
				foreach (Molecule molecule10 in source1)
				{
					if (molecule10.method_1100().ContainsKey(class277.field_2187))
					{
						molecule10.method_1111(class277.field_2186, class277.field_2187, class277.field_2188);
						break;
					}
				}
			}
			
		}
		moleculeList.RemoveAll(Sim.class_301.field_2479 ?? (mol => mol.field_2638));
		moleculeList.AddRange(source1);
		
		foreach (Part part in gripperList)
		{
			//expanded version of sim_self.method_1841(part);//give gripper a molecule back
			PartSimState partSimState = partSimStates[part];
			HexIndex field2724 = partSimState.field_2724;
			partSimState.field_2728 = true;
			partSimState.field_2729 = sim_self.method_1848(field2724);
		}

		sim_dyn.Set("field_3821", partSimStates);
		sim_dyn.Set("field_3826", struct122List);
		sim_dyn.Set("field_3823", moleculeList);
		//----- BOILERPLATE-2 END -----//
	}

	public override void Unload()
	{
		hook_Sim_method_1832.Dispose();
	}

	//------------------------- END HOOKING -------------------------//
	public override void PostLoad()
	{
		On.PuzzleEditorScreen.method_50 += PES_Method_50;
	}

	public void PES_Method_50(On.PuzzleEditorScreen.orig_method_50 orig, PuzzleEditorScreen pes_self, float param_4993)
	{
		orig(pes_self, param_4993);
		Vector2 base_position = new Vector2(1516f, 922f);
		base_position = (class_115.field_1433 / 2 - base_position / 2 + new Vector2(-2f, -11f)).Rounded();
		base_position = base_position + new Vector2(494f, 184f);

		Vector2 offset(int x, int y) => new Vector2(236f * x, -37f * y);

		drawAndExecutePermissionCheckbox(pes_self, base_position + offset(0, -1), glyphRejection.field_1529, perm_rejection);
		drawAndExecutePermissionCheckbox(pes_self, base_position + offset(3, -1), glyphSplitting.field_1529, perm_splitting);
	}
}
