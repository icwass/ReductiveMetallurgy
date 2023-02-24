﻿using MonoMod.RuntimeDetour;
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
//using BondType = enum_126;
//using BondSite = class_222;
using AtomTypes = class_175;
using PartTypes = class_191;
using Texture = class_256;




public class MainClass : QuintessentialMod
{
	// public resources, helper functions and APIs
	public static PartType glyphRejection;

	public static void addRejectionRule(AtomType hi, AtomType lo)
	{
		bool flag = demoteDict.ContainsKey(hi);
		if (flag && demoteDict[hi] != lo)
		{
			//throw an error
			string msg = "[ReductiveMetallurgy] ERROR: Preparing debug dump.";
			msg += "\n  Current list of Rejection Rules:";
			foreach (var kvp in demoteDict)
			{
				msg += "\n    " + kvp.Key.field_2284 + " => " + kvp.Value.field_2284;
			}
			msg += "\n\n  AtomType '" + hi.field_2284 + "' already has a rejection rule: '" + hi.field_2284 + " => " + demoteDict[hi].field_2284 + "'.";
			throw new class_266("addRejectionRule: Cannot add Rejection rule '" + hi.field_2284 + " => " + lo.field_2284 + "'.");
		}
		else if (!flag)
		{
			demoteDict.Add(hi, lo);
		}
	}


	// private resources
	private static IDetour hook_Sim_method_1832;

	private static Dictionary<AtomType, AtomType> demoteDict = new();
	private static HexIndex[] rejectionHexes = new HexIndex[2] {
		new HexIndex(0, 0),
		new HexIndex(1, 0)
	};
	private static Texture[] irisQuicksilver = new Texture[16];

	// private helper functions
	private static AtomType quicksilverAtomType() => AtomTypes.field_1680;

	private static bool glyphIsFiring(PartSimState partSimState) => partSimState.field_2743;
	private static void glyphNeedsToFire(PartSimState partSimState) => partSimState.field_2743 = true;
	private static void glyphHasFired(PartSimState partSimState) => partSimState.field_2743 = false;

	private static void playSound(Sim sim_self, Sound sound)
	{
		typeof(Sim).GetMethod("method_1856", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sim_self, new object[] { sound });
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

	private static Vector2 method_1999(class_195 renderer, HexIndex param_5572)
	{
		return renderer.field_1797 + hexGraphicalOffset(param_5572).Rotated(renderer.field_1798);
	}

	private static Vector2 method_2001(class_195 param_5576, HexIndex param_5577)
	{
		return 0.0001f * (param_5576.field_1797 + hexGraphicalOffset(param_5577).Rotated(param_5576.field_1798) - 0.5f * class_115.field_1433);
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

		string path;
		path = "reductiveMetallurgy/textures/parts/icons/";

		glyphRejection = makeGlyph(
			"glyph-rejection",
			"Glyph of Rejection",
			"The glyph of rejection extracts quicksilver to demote an atom of metal to a lower form.",
			20, rejectionHexes, Permissions.Projection,
			class_235.method_615(path + "rejection"),
			class_235.method_615(path + "rejection_hover"),
			class_238.field_1989.field_97.field_374,// double_glow
			class_238.field_1989.field_97.field_375 // double_stroke
		);

		var projector = PartTypes.field_1778;
		var purifier = PartTypes.field_1779;

		QApi.AddPartTypeToPanel(glyphRejection, projector);


		path = "reductiveMetallurgy/textures/parts/";
		Texture rejection_leadSymbol = class_235.method_615(path + "rejection_lead_symbol");
		Texture rejection_quicksilverSymbol = class_235.method_615(path + "rejection_quicksilver_symbol");
		/*
		for (int i = 0; i < 16; i++)
		{
			irisQuicksilver[i] = class_235.method_615(path + "iris_full_quicksilver.array/iris_full_" + (i + 1).ToString("0000"));
		}
		*/


		// fetch vanilla textures
		Texture bonderShadow = class_238.field_1989.field_90.field_164;

		Texture calcinationGlyph_bowl = class_238.field_1989.field_90.field_170;

		Texture projectionGlyph_base = class_238.field_1989.field_90.field_255.field_288;
		Texture projectionGlyph_bond = class_238.field_1989.field_90.field_255.field_289;
		Texture projectionGlyph_glossMask = class_238.field_1989.field_90.field_255.field_290;
		Texture projectionGlyph_metalBowl = class_238.field_1989.field_90.field_255.field_292;

		//Texture projectionGlyph_leadSymbol = class_238.field_1989.field_90.field_255.field_291;
		//Texture projectionGlyph_quicksilverInput = class_238.field_1989.field_90.field_255.field_293;
		//Texture projectionGlyph_quicksilverSymbol = class_238.field_1989.field_90.field_255.field_294;

		Texture animismus_outputAboveIris = class_238.field_1989.field_90.field_228.field_271;
		Texture animismus_outputUnderIris = class_238.field_1989.field_90.field_228.field_272;
		
		//Texture purificationGlyph_base = class_238.field_1989.field_90.field_257.field_359;
		//Texture purificationGlyph_connectors = class_238.field_1989.field_90.field_257.field_360;

		Texture purificationGlyph_gloss = class_238.field_1989.field_90.field_257.field_361;

		//Texture purificationGlyph_glossMask = class_238.field_1989.field_90.field_257.field_362;
		//Texture purificationGlyph_leadSymbol = class_238.field_1989.field_90.field_257.field_363;

		//Texture[] irisFullArray = class_238.field_1989.field_90.field_246;

		QApi.AddPartType(glyphRejection, (part, pos, editor, renderer) =>
		{
			PartSimState partSimState = editor.method_507().method_481(part);
			var simTime = editor.method_504(); 
			
			var originHex = new HexIndex(0, 0);
			var metalHex = originHex;
			var outputHex = new HexIndex(1, 0);

			Vector2 base_offset = new Vector2(41f, 48f);
			drawPartGraphic(renderer, projectionGlyph_base, base_offset, 0.0f, Vector2.Zero, new Vector2(-1f, -1f));

			//draw metal bowl
			drawPartGraphic(renderer, bonderShadow, textureDimensions(bonderShadow) / 2, 0f, hexGraphicalOffset(metalHex), new Vector2(0.0f, -3f));
			drawPartGraphicSpecular(renderer, projectionGlyph_metalBowl, textureCenter(projectionGlyph_metalBowl), 0f, hexGraphicalOffset(metalHex), Vector2.Zero);
			drawPartGraphic(renderer, rejection_leadSymbol, textureCenter(rejection_leadSymbol), -renderer.field_1798, hexGraphicalOffset(metalHex), Vector2.Zero);

			//draw quicksilver output
			drawPartGraphic(renderer, bonderShadow, textureDimensions(bonderShadow) / 2, 0f, hexGraphicalOffset(outputHex), new Vector2(0.0f, -3f));
			drawPartGraphicSpecular(renderer, calcinationGlyph_bowl, textureCenter(calcinationGlyph_bowl), 0f, hexGraphicalOffset(outputHex), Vector2.Zero);
			class_126 atomCageLighting = class_238.field_1989.field_90.field_232;
			float angle = renderer.field_1798;
			Vector2 translation = renderer.field_1797 + hexGraphicalOffset(new HexIndex(1, 0)).Rotated(angle);
			var Method_2003 = typeof(SolutionEditorBase).GetMethod("method_2003", BindingFlags.NonPublic | BindingFlags.Static);
			Method_2003.Invoke(null, new object[] { atomCageLighting, translation, new Vector2(39f, 33f), angle });
			drawPartGraphic(renderer, rejection_quicksilverSymbol, textureCenter(rejection_quicksilverSymbol), -renderer.field_1798, hexGraphicalOffset(outputHex), Vector2.Zero);

			drawPartGraphic(renderer, projectionGlyph_bond, base_offset + new Vector2(-73f, -37f), 0f, Vector2.Zero, Vector2.Zero);
			//gloss start
			class_135.method_257().field_1692 = class_238.field_1995.field_1757; // MaskedGlossPS shader
			class_135.method_257().field_1693[1] = purificationGlyph_gloss;
			class_135.method_257().field_1695 = method_2001(renderer, originHex);
			renderer.method_521(projectionGlyph_glossMask, base_offset);
			class_135.method_257().field_1692 = class_135.method_257().field_1696; // previous shader
			class_135.method_257().field_1693[1] = class_238.field_1989.field_71;
			class_135.method_257().field_1695 = Vector2.Zero;
			//gloss end
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
			foreach (Part key in part.field_2696)//for each gripper
			{
				if (partSimStates[key].field_2729.method_1085())//if part is holding onto a molecule
				{
					gripperList.Add(key);//add gripper to gripperList
					//expanded version of sim_self.method_1842(key);//release molecule from the gripper
					PartSimState partSimState = partSimStates[key];
					partSimState.field_2728 = false;
					partSimState.field_2729 = struct_18.field_1431;
				}
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

		//void addColliderAtHex(Part part, HexIndex hex)
		//{
		//	struct122List.Add(new Sim.struct_122()
		//	{
		//		field_3850 = (Sim.enum_190)0,
		//		field_3851 = class_187.field_1742.method_491(part.method_1184(hex), Vector2.Zero),
		//		field_3852 = 15f // Sim.field_3832;
		//	});
		//}

		void spawnAtomAtHex(Part part, HexIndex hex, AtomType atom)
		{
			Molecule molecule = new Molecule();
			molecule.method_1105(new Atom(atom), part.method_1184(hex));
			moleculeList.Add(molecule);
		}

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
				AtomReference atomOutput;
				if (maybeFindAtom(part, hexReject, new List<Part>()).method_99(out atomDemote) // demotable atom exists - don't care if it's held
				&& !maybeFindAtom(part, hexOutput, new List<Part>()).method_99(out atomOutput) // output not blocked
				&& (demoteDict.ContainsKey(atomDemote.field_2280)) // demote atom can be demoted
				) // then fire the rejection glyph!
				{
					// update the metal atom's type
					atomDemote.field_2277.method_1106(demoteDict[atomDemote.field_2280], atomDemote.field_2278);
					// play the atom-change animation
					Texture[] projectAtomAnimation = class_238.field_1989.field_81.field_614;
					atomDemote.field_2279.field_2276 = (Maybe<class_168>)new class_168(SEB, (enum_7)0, (enum_132)1, atomDemote.field_2280, projectAtomAnimation, 30f);
					// play the glyph-flash animation
					Vector2 hexPosition = class_187.field_1742.method_492(part.method_1161() + hexReject.Rotated(part.method_1163()));
					Texture[] projectionGlyphFlashAnimation = class_238.field_1989.field_90.field_256;
					float radians = (part.method_1163() + HexRotation.R180).ToRadians();
					SEB.field_3935.Add(new class_228(SEB, (enum_7)1, hexPosition, projectionGlyphFlashAnimation, 30f, Vector2.Zero, radians));
					playSound(sim_self, class_238.field_1991.field_1844); // projection sound
					spawnAtomAtHex(part, hexOutput, quicksilverAtomType());

					Texture[] disposalFlashAnimation = class_238.field_1989.field_90.field_240;
					Vector2 animationPosition = hexGraphicalOffset(part.method_1161() + hexOutput.Rotated(part.method_1163())) + new Vector2(80f, 0f);
					SEB.field_3936.Add(new class_228(SEB, (enum_7)1, animationPosition, disposalFlashAnimation, 30f, Vector2.Zero, 0f));
				}
			}








		}

		//----- BOILERPLATE-2 START -----//
		List<Molecule> source1 = new List<Molecule>();
		foreach (Molecule molecule9 in moleculeList)
		{
			if (molecule9.field_2638)
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
		}
		moleculeList.RemoveAll(Sim.class_301.field_2479 ?? (mol => mol.field_2638));
		moleculeList.AddRange((IEnumerable<Molecule>)source1);
		
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
		//
	}
}