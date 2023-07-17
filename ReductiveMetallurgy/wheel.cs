using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using Quintessential;
using Quintessential.Settings;
using SDL2;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace ReductiveMetallurgy;

using PartType = class_139;
using PartTypes = class_191;
using Texture = class_256;

public static class Wheel
{
	public static PartType Ravari, RavariSpent;

	const float sixtyDegrees = 60f * (float)Math.PI / 180f;
	const string RavariWheelAtomsField = "ReductiveMetallurgy_RavariWheelAtoms";
	const string RavariWheelSpentField = "ReductiveMetallurgy_RavariWheelSpent";

	static Sound RavariSpend;
	static Texture[] RavariSeparateAnimation;
	static Texture[] RavariFlyAnimation;
	static class_126 atomCageBrokenLighting;
	static class_126 atomCageBrokenLightingAlt;
	static class_126 atomCageLighting => class_238.field_1989.field_90.field_232;
	static PartType Berlo => PartTypes.field_1771;
	static HexRotation[] HexArmRotations => PartTypes.field_1767.field_1534;
	static Molecule RavariMolecule()
	{
		Molecule molecule = new Molecule();
		molecule.method_1105(new Atom(API.leadAtomType()), new HexIndex(0, 1));
		molecule.method_1105(new Atom(API.tinAtomType()), new HexIndex(1, 0));
		molecule.method_1105(new Atom(API.ironAtomType()), new HexIndex(1, -1));
		molecule.method_1105(new Atom(API.copperAtomType()), new HexIndex(0, -1));
		molecule.method_1105(new Atom(API.silverAtomType()), new HexIndex(-1, 0));
		molecule.method_1105(new Atom(API.goldAtomType()), new HexIndex(-1, 1));
		return molecule;
	}
	// ============================= //
	// public methods called by main
	public static void LoadMirrorRules() => FTSIGCTU.MirrorTool.addRule(Ravari, FTSIGCTU.MirrorTool.mirrorVanBerlo);
	public static void manageSpentRavaris(Sim sim_self, Action action)
	{
		var SEB = new DynamicData(sim_self).Get<SolutionEditorBase>("field_3818");
		var partList = SEB.method_502().field_3919;
		foreach (var ravari in partList.Where(x => x.method_1159() == Ravari))
		{
			PartSimState partSimState = SEB.method_507().method_481(ravari);
			if (GetRavariWheelSpent(partSimState))
			{
				new DynamicData(ravari).Set("field_2691", RavariSpent);
			}
		}
		//=====//
		action();
		//=====//
		foreach (var ravari in partList.Where(x => x.method_1159() == RavariSpent))
		{
			new DynamicData(ravari).Set("field_2691", Ravari);
		}
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
		if (GetRavariWheelSpent(partSimState)) return;

		class_236 class236 = seb_self.method_1989(part, pos);
		Editor.method_925(GetRavariWheelAtoms(partSimState), class236.field_1984, new HexIndex(0,0), class236.field_1985, 1f, 1f, 1f, false, seb_self);
	}
	public static void spendRavariWheel(Sim sim_self, Part part)
	{
		if (part.method_1159() != Ravari) return;
		var sim_dyn = new DynamicData(sim_self);
		var SEB = sim_dyn.Get<SolutionEditorBase>("field_3818");
		PartSimState partSimState = SEB.method_507().method_481(part);
		if (GetRavariWheelSpent(partSimState)) return;

		SetRavariWheelData(partSimState, RavariWheelSpentField, true);

		// add atoms to the board
		var hexIndex = partSimState.field_2724;
		var rotation = partSimState.field_2726;
		var moleculeList = sim_dyn.Get<List<Molecule>>("field_3823");
		var conduitMoleculeList = sim_dyn.Get<List<Molecule>>("field_3828");

		foreach (var kvp in GetRavariWheelAtoms(partSimState).method_1100())
		{
			var hex = kvp.Key;
			var atom = kvp.Value;
			Molecule molecule = new Molecule();
			molecule.method_1105(atom.method_804(), hexIndex + hex.Rotated(rotation));
			moleculeList.Add(molecule);
			conduitMoleculeList.Add(molecule);
		}

		// play sound effect
		Sound simulationStop = class_238.field_1991.field_1863;
		float volumeFactor = SEB.method_506();
		simulationStop.method_28(0.75f * volumeFactor);
		RavariSpend.method_28(1f * volumeFactor);

		// draw separation animations
		foreach (var hex in HexIndex.AdjacentOffsets)
		{
			var hex_pos = class_187.field_1742.method_492(hexIndex + hex);
			Vector2 vec = class_162.method_413(class_187.field_1742.method_492(hexIndex), hex_pos, 0.67f);
			float angle = class_187.field_1742.method_492(hex).Angle();
			SEB.field_3936.Add(new class_228(SEB, (enum_7)1, hex_pos, RavariFlyAnimation, 75f, new Vector2(-32f, 0f), angle));
			SEB.field_3936.Add(new class_228(SEB, (enum_7)1, vec, RavariSeparateAnimation, 75f, new Vector2(1.5f, -2.5f), angle));
		}
	}

	public static Maybe<AtomReference> maybeFindRavariWheelAtom(Sim sim_self, Part part, HexIndex offset)
	{
		var sim_dyn = new DynamicData(sim_self);
		var SEB = sim_dyn.Get<SolutionEditorBase>("field_3818");
		var solution = sim_dyn.Get<SolutionEditorBase>("field_3818").method_502();
		var partList = solution.field_3919;
		var partSimStates = sim_dyn.Get<Dictionary<Part, PartSimState>>("field_3821");

		HexIndex key = part.method_1184(offset);
		foreach (var ravari in partList.Where(x => x.method_1159() == Ravari))
		{
			var hexKey = part.method_1184(offset);
			var partSimState = partSimStates[ravari];
			Molecule ravariAtoms = GetRavariWheelAtoms(partSimState);
			var hexIndex = partSimState.field_2724;
			var rotation = partSimState.field_2726;

			Atom atom;
			if (ravariAtoms.method_1100().TryGetValue((key - hexIndex).Rotated(rotation.Negative()), out atom))
			{
				return (Maybe<AtomReference>)new AtomReference(ravariAtoms, key, atom.field_2275, atom, true);
			}
		}
		return (Maybe<AtomReference>)struct_18.field_1431;
	}

	private static bool ContentLoaded = false;
	public static void LoadContent()
	{
		if (ContentLoaded) return;
		ContentLoaded = true;
		LoadSoundResources();
		LoadTextureResources();
		//=========================//
		string iconpath = "reductiveMetallurgy/textures/parts/icons/verrin";
		Ravari = new PartType()
		{
			/*ID*/field_1528 = "wheel-verrin-new",
			/*Name*/field_1529 = class_134.method_253("Ravari's Wheel", string.Empty),
			/*Desc*/field_1530 = class_134.method_253("By using Ravari's wheel with the glyphs of projection and rejection, quicksilver can be stored or discharged. The wheel also has a drop-mechanism that can release the metals.", string.Empty),
			/*Cost*/field_1531 = 30,
			/*Type*/field_1532 = (enum_2) 1,
			/*Programmable?*/field_1533 = true,
			/*Force-rotatable*/field_1536 = true,
			/*Berlo Atoms*/field_1544 = new Dictionary<HexIndex, AtomType>(),
			/*Icon*/field_1547 = class_235.method_615(iconpath),
			/*Hover Icon*/field_1548 = class_235.method_615(iconpath + "_hover"),
			/*Permissions*/field_1551 = API.perm_ravari,
			/*Only One Allowed?*/field_1552 = true,
		};
		foreach (var hex in HexIndex.AdjacentOffsets) Ravari.field_1544.Add(hex, API.quicksilverAtomType());

		RavariSpent = new PartType()
		{
			/*Type*/field_1532 = (enum_2) 1,
			/*Programmable?*/field_1533 = true,
		};

		QApi.AddPartTypeToPanel(Ravari, Berlo);

		QApi.AddPartType(Ravari, (part, pos, editor, renderer) =>
		{
			// draw arm stubs
			class_236 class236 = editor.method_1989(part, pos);
			API.PrivateMethod<SolutionEditorBase>("method_2005").Invoke(editor, new object[] { part.method_1165(), HexArmRotations, class236 });

			// draw atoms, if the simulation is stopped
			if (editor.method_503() == enum_128.Stopped)
			{
				drawRavariAtoms(editor, part, pos);
			}

			// draw cages
			PartSimState partSimState = editor.method_507().method_481(part);
			bool isSpent = GetRavariWheelSpent(partSimState);
			for (int i = 0; i < 6; i++)
			{
				float radians = renderer.field_1798 + (i * sixtyDegrees);
				Vector2 vector2_9 = renderer.field_1797 + class_187.field_1742.method_492(new HexIndex(1, 0)).Rotated(radians);
				var atomcages = atomCageLighting;
				if (isSpent) atomcages = MainClass.RavariAlternateTexture ? atomCageBrokenLightingAlt : atomCageBrokenLighting;
				API.PrivateMethod<SolutionEditorBase>("method_2003").Invoke(editor, new object[] { atomcages, vector2_9, new Vector2(39f, 33f), radians });
			}
		});
	}

	#region /*DynamicData access functions*/
	private static void SetRavariWheelData<T>(PartSimState state, string field, T data) => new DynamicData(state).Set(field, data);
	private static T GetRavariWheelData<T>(PartSimState state, string field, T initial)
	{
		var state_dyn = new DynamicData(state);
		var data = state_dyn.Get(field);

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
	private static bool GetRavariWheelSpent(PartSimState state) => GetRavariWheelData(state, RavariWheelSpentField, false);
	private static Molecule GetRavariWheelAtoms(PartSimState state) => GetRavariWheelData(state, RavariWheelAtomsField, RavariMolecule());
	#endregion







	// private methods
	private static void LoadTextureResources()
	{
		string dir = "reductiveMetallurgy/textures/parts/";
		Texture[] fetchTextureArray(int length, string path)
		{
			var ret = new Texture[length];
			for (int i = 0; i < ret.Length; i++)
			{
				ret[i] = class_235.method_615(dir + path + (i + 1).ToString("0000"));
			}
			return ret;
		}
		RavariSeparateAnimation = fetchTextureArray(28, "ravari_separate.array/separate_");
		RavariFlyAnimation = fetchTextureArray(32, "atom_cage_fly.array/fly_");

		class_126 fetchClass126(string path) => new class_126(
			class_235.method_615(path + "left"),
			class_235.method_615(path + "right"),
			class_235.method_615(path + "bottom"),
			class_235.method_615(path + "top")
		);
		atomCageBrokenLighting = fetchClass126(dir + "atom_cage_broken.lighting/");
		atomCageBrokenLightingAlt = fetchClass126(dir + "atom_cage_broken_alt.lighting/");
	}
	private static void LoadSoundResources()
	{
		//load the custom sound, and hook into stuff to make it work right
		string path = "Content/reductiveMetallurgy/sounds/ravari_release.wav";
		foreach (var dir in QuintessentialLoader.ModContentDirectories)
		{
			string filepath = Path.Combine(dir, path);
			if (File.Exists(filepath))
			{
				RavariSpend = new Sound()
				{
					field_4060 = Path.GetFileNameWithoutExtension(filepath),
					field_4061 = class_158.method_375(filepath)
				};
				break;
			}
		}
		var field = typeof(class_11).GetField("field_52", BindingFlags.Static | BindingFlags.NonPublic);
		var dictionary = (Dictionary<string, float>)field.GetValue(null);
		dictionary.Add("ravari_release", 0.15f);

		void Method_540(On.class_201.orig_method_540 orig, class_201 class201_self)
		{
			orig(class201_self);
			RavariSpend.field_4062 = false;
		}
		On.class_201.method_540 += Method_540;
	}
}