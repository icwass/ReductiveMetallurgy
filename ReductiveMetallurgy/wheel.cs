//using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using Quintessential;
//using Quintessential.Settings;
//using SDL2;
using System;
//using System.IO;
using System.Linq;
using System.Collections.Generic;
//using System.Reflection;

namespace ReductiveMetallurgy;

using PartType = class_139;
using PartTypes = class_191;
using Texture = class_256;

public static class Wheel
{
	public static PartType Ravari;

	const float sixtyDegrees = 60f * (float)Math.PI / 180f;
	const string RavariWheelAtomsField = "ReductiveMetallurgy_RavariWheelAtoms";

	static Texture[] RavariFlashAnimation;
	static class_126 atomCageLighting => class_238.field_1989.field_90.field_232;
	static PartType Berlo => PartTypes.field_1771;
	static HexRotation[] HexArmRotations => PartTypes.field_1767.field_1534;
	static Molecule RavariMolecule()
	{
		Molecule molecule = new Molecule();
		molecule.method_1105(new Atom(API.leadAtomType), new HexIndex(0, 1));
		molecule.method_1105(new Atom(API.tinAtomType), new HexIndex(1, 0));
		molecule.method_1105(new Atom(API.ironAtomType), new HexIndex(1, -1));
		molecule.method_1105(new Atom(API.copperAtomType), new HexIndex(0, -1));
		molecule.method_1105(new Atom(API.silverAtomType), new HexIndex(-1, 0));
		molecule.method_1105(new Atom(API.goldAtomType), new HexIndex(-1, 1));
		return molecule;
	}
	// ============================= //
	// public methods called by main
	public static void LoadMirrorRules() => FTSIGCTU.MirrorTool.addRule(Ravari, FTSIGCTU.MirrorTool.mirrorVanBerlo);
	public static void DrawRavariFlash(SolutionEditorBase SEB, Part part, HexIndex hex)
	{
		SEB.field_3935.Add(new class_228(SEB, (enum_7)1, MainClass.hexGraphicalOffset(hex.Rotated(part.method_1163()) + part.method_1161()), RavariFlashAnimation, 30f, Vector2.Zero, 0f));
	}
	public static void drawSelectionGlow(SolutionEditorBase seb_self, Part part, Vector2 pos, float alpha)
	{
		var cageSelectGlowTexture = class_238.field_1989.field_97.field_367;
		int armLength = 1; // part.method_1165()
		class_236 class236 = seb_self.method_1989(part, pos);
		Color color = Color.White.WithAlpha(alpha);

		API.PrivateMethod<SolutionEditorBase>("method_2006").Invoke(seb_self, new object[] { armLength, HexArmRotations, class236, color });
		for (int index = 0; index < 6; ++index)
		{
			float num = index * sixtyDegrees;
			API.PrivateMethod<SolutionEditorBase>("method_2016").Invoke(seb_self, new object[] { cageSelectGlowTexture, color, class236.field_1984, class236.field_1985 + num });
		}
	}

	public static void drawRavariAtoms(SolutionEditorBase seb_self, Part part, Vector2 pos)
	{
		if (part.method_1159() != Ravari) return;
		PartSimState partSimState = seb_self.method_507().method_481(part);

		class_236 class236 = seb_self.method_1989(part, pos);
		Editor.method_925(GetRavariWheelAtoms(partSimState), class236.field_1984, new HexIndex(0,0), class236.field_1985, 1f, 1f, 1f, false, seb_self);
	}

	public static Maybe<AtomReference> maybeFindRavariWheelAtom(Sim sim_self, Part part, HexIndex offset)
	{
		var SEB = sim_self.field_3818;
		var solution = SEB.method_502();
		var partList = solution.field_3919;
		var partSimStates = sim_self.field_3821;

		HexIndex key = part.method_1184(offset);
		foreach (var ravari in partList.Where(x => x.method_1159() == Ravari))
		{
			var partSimState = partSimStates[ravari];
			Molecule ravariAtoms = GetRavariWheelAtoms(partSimState);
			var hexIndex = partSimState.field_2724;
			var rotation = partSimState.field_2726;
			var hexKey = (key - hexIndex).Rotated(rotation.Negative());

			Atom atom;
			if (ravariAtoms.method_1100().TryGetValue(hexKey, out atom))
			{
				return (Maybe<AtomReference>)new AtomReference(ravariAtoms, hexKey, atom.field_2275, atom, true);
			}
		}
		return (Maybe<AtomReference>)struct_18.field_1431;
	}

	private static bool ContentLoaded = false;
	public static void LoadContent()
	{
		if (ContentLoaded) return;
		ContentLoaded = true;
		LoadTextureResources();
		//=========================//
		string iconpath = "reductiveMetallurgy/textures/parts/icons/verrin";
		Ravari = new PartType()
		{
			/*ID*/field_1528 = "reductive-metallurgy-ravari",
			/*Name*/field_1529 = class_134.method_253("Ravari's Wheel", string.Empty),
			/*Desc*/field_1530 = class_134.method_253("By using Ravari's wheel with the glyphs of projection and rejection, quicksilver can be stored or discharged.", string.Empty),
			/*Cost*/field_1531 = 30,
			/*Type*/field_1532 = (enum_2) 1,
			/*Programmable?*/field_1533 = true,
			/*Force-rotatable*/field_1536 = true,
			/*Berlo Atoms*/field_1544 = new Dictionary<HexIndex, AtomType>(),
			/*Icon*/field_1547 = class_235.method_615(iconpath),
			/*Hover Icon*/field_1548 = class_235.method_615(iconpath + "_hover"),
			/*Only One Allowed?*/field_1552 = true,
			CustomPermissionCheck = perms => perms.Contains(API.RavariPermission)
		};
		foreach (var hex in HexIndex.AdjacentOffsets) Ravari.field_1544.Add(hex, API.quicksilverAtomType);

		QApi.AddPartTypeToPanel(Ravari, Berlo);
		QApi.AddPartType(Ravari, DrawRavariPart);
	}

	// private methods
	private static void SetRavariWheelData<T>(PartSimState state, string field, T data) => new DynamicData(state).Set(field, data);
	private static T GetRavariWheelData<T>(PartSimState state, string field, T initial)
	{
		var data = new DynamicData(state).Get(field);
		if (data == null)
		{
			SetRavariWheelData(state, field, initial);
			return initial;
		}
		else
		{
			return (T)data;
		}
	}
	private static Molecule GetRavariWheelAtoms(PartSimState state) => GetRavariWheelData(state, RavariWheelAtomsField, RavariMolecule());

	private static void LoadTextureResources()
	{
		RavariFlashAnimation = MainClass.fetchTextureArray(10, "reductiveMetallurgy/textures/parts/ravari_flash.array/flash_");
	}
	
	static void DrawRavariPart (Part part, Vector2 pos, SolutionEditorBase editor, class_195 renderer)
	{
		// draw atoms, if the simulation is stopped - otherwise, the running simulation will draw them
		if (editor.method_503() == enum_128.Stopped)
		{
			drawRavariAtoms(editor, part, pos);
		}

		// draw arm stubs
		class_236 class236 = editor.method_1989(part, pos);
		API.PrivateMethod<SolutionEditorBase>("method_2005").Invoke(editor, new object[] { part.method_1165(), HexArmRotations, class236 });

		// draw cages
		PartSimState partSimState = editor.method_507().method_481(part);
		for (int i = 0; i < 6; i++)
		{
			float radians = renderer.field_1798 + (i * sixtyDegrees);
			Vector2 vector2_9 = renderer.field_1797 + MainClass.hexGraphicalOffset(new HexIndex(1, 0)).Rotated(radians);
			API.PrivateMethod<SolutionEditorBase>("method_2003").Invoke(editor, new object[] { atomCageLighting, vector2_9, new Vector2(39f, 33f), radians });
		}
	}
}