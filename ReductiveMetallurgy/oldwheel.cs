﻿using MonoMod.RuntimeDetour;
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
using AtomTypes = class_175;
using PartTypes = class_191;
using Texture = class_256;

public static class oldWheel
{
	public static PartType oldRavari, oldRavariSpent;
	public static Sound RavariSpend;
	public static Texture[] RavariSeparateAnimation;
	public static Texture[] RavariFlyAnimation;

	public static void drawSelectionGlow(SolutionEditorBase seb_self, Part part, Vector2 pos, float alpha)
	{
		var cageGlowTexture = class_238.field_1989.field_97.field_367; // textures/select/cage
		int armLength = 1; // part.method_1165()
		var armRotations = PartTypes.field_1767.field_1534;
		class_236 class236 = seb_self.method_1989(part, pos);
		Color color = Color.White.WithAlpha(alpha);

		API.PrivateMethod<SolutionEditorBase>("method_2006").Invoke(seb_self, new object[] { armLength, armRotations, class236, color });
		for (int index = 0; index < 6; ++index)
		{
			float num = index * 60 * ((float)Math.PI / 180f);
			API.PrivateMethod<SolutionEditorBase>("method_2016").Invoke(seb_self, new object[] { cageGlowTexture, color, class236.field_1984, class236.field_1985 + num });
		}
	}

	public static void manageSpentRavaris(Sim sim_self, Action action)
	{
		var sim_dyn = new DynamicData(sim_self);
		var SEB = sim_dyn.Get<SolutionEditorBase>("field_3818");
		var solution = SEB.method_502();
		var partList = solution.field_3919;
		var partSimStates = sim_dyn.Get<Dictionary<Part, PartSimState>>("field_3821");

		foreach (var ravari in partList.Where(x => x.method_1159() == oldRavari))
		{
			var metalWheel = new MetalWheel(partSimStates[ravari]);
			if (metalWheel.isSpent)
			{
				var ravari_dyn = new DynamicData(ravari);
				ravari_dyn.Set("field_2691", oldRavariSpent);
			}
		}
		//=====//
		action();
		//=====//
		foreach (var ravari in partList.Where(x => x.method_1159() == oldRavariSpent))
		{
			var ravari_dyn = new DynamicData(ravari);
			ravari_dyn.Set("field_2691", oldRavari);
		}
	}

	public struct MetalWheel
	{
		// APIs ////////////////////
		public bool isSpent => spent;
		public bool canProject(HexRotation rot) => tryProjectionOrRejection(rot, isProjection, onlyCheck);
		public bool canReject(HexRotation rot) => tryProjectionOrRejection(rot, isRejection, onlyCheck);
		public void project(HexRotation rot) => tryProjectionOrRejection(rot, isProjection, modifyData);
		public void reject(HexRotation rot) => tryProjectionOrRejection(rot, isRejection, modifyData);
		public void clearProjectionsAndRejections()
		{
			projections = new bool[6] { false, false, false, false, false, false };
			rejections = new bool[6] { false, false, false, false, false, false };
			savePackedWheel();
		}
		public void spendWheel(Sim sim_self)
		{
			if (spent) return;
			var sim_dyn = new DynamicData(sim_self);
			var SEB = sim_dyn.Get<SolutionEditorBase>("field_3818");
			var moleculeList = sim_dyn.Get<List<Molecule>>("field_3823");
			var conduitMoleculeList = sim_dyn.Get<List<Molecule>>("field_3828");

			int turns = partSimState.field_2726.GetNumberOfTurns() % 6;
			for (int i = 0; i < 6; i++)
			{
				var hex = hexes[i];
				var metal = metalIDs[wheel[(i - turns + 6) % 6]];
				//spawnAtomAtHex
				Molecule molecule = new Molecule();
				molecule.method_1105(new Atom(metal), partSimState.field_2724 + hex);
				moleculeList.Add(molecule);
				conduitMoleculeList.Add(molecule);
			}
			spent = true;
			savePackedWheel();

			Sound simulationStop = class_238.field_1991.field_1863;
			float volumeFactor = SEB.method_506();
			simulationStop.method_28(0.75f * volumeFactor);
			RavariSpend.method_28(1f * volumeFactor);

			//draw separation animations
			foreach (var hex in hexes)
			{
				var hex1 = partSimState.field_2724;
				var hex2 = hex1 + hex;
				var hex2_pos = class_187.field_1742.method_492(hex2);
				Vector2 vector2_6 = class_162.method_413(class_187.field_1742.method_492(hex1), hex2_pos, 0.67f);
				float angle = class_187.field_1742.method_492(hex2 - hex1).Angle();
				var vector2_5 = class_187.field_1742.method_492(hex2 - hex1);
				class_228 class228_1 = new class_228(SEB, (enum_7)1, hex2_pos, RavariFlyAnimation, 75f, new Vector2(-32f, 0f), angle);
				SEB.field_3936.Add(class228_1);
				class_228 class228_2 = new class_228(SEB, (enum_7)1, vector2_6, RavariSeparateAnimation, 75f, new Vector2(1.5f, -2.5f), angle);
				SEB.field_3936.Add(class228_2);
			}
		}
		public void getDrawData(out HexIndex[] Hexes, out Dictionary<HexIndex, AtomType> BaseAtoms, out Dictionary<HexIndex, AtomType> TransmutationAtoms, out bool isSpent)
		{
			Hexes = hexes;
			isSpent = spent;
			BaseAtoms = new Dictionary<HexIndex, AtomType>();
			TransmutationAtoms = new Dictionary<HexIndex, AtomType>();
			if (spent) return;

			Hexes = hexes;
			for (int i = 0; i < 6; i++)
			{
				int metal = wheel[i];
				BaseAtoms.Add(hexes[i], metalIDs[metal]);
				if (projections[i]) metal--;
				if (rejections[i]) metal++;
				TransmutationAtoms.Add(hexes[i], metalIDs[metal]);
			}
		}
		public MetalWheel(PartSimState _partSimState)
		{
			partSimState = _partSimState;
			int packedWheel = partSimState.field_2730;
			spent = packedWheel < 0;
			// starting configuration
			wheel = new int[6] { 2, 1, 6, 5, 4, 3 };
			projections = new bool[6] { false, false, false, false, false, false };
			rejections = new bool[6] { false, false, false, false, false, false };
			if (packedWheel == 0) //save the starting configuration
			{
				savePackedWheel();
			}
			else // load the existing configuration
			{
				for (int i = 5; i >= 0; i--)
				{
					//
					wheel[i] = packedWheel & metalMask;
					projections[i] = (packedWheel & projectionMask) == projectionMask;
					rejections[i] = (packedWheel & rejectionMask) == rejectionMask;
					packedWheel >>= shift;
				}
			}
		}

		// internal ////////////////////
		private bool tryProjectionOrRejection(HexRotation rot, bool isProjecting, bool isModifyingData)
		{
			if (spent) return false;
			HexRotation netRot = rot - partSimState.field_2726;
			var index = (netRot.GetNumberOfTurns() % 6 + 6) % 6;
			bool flag = isProjecting ? wheel[index] < 6 : wheel[index] > 1;
			if (flag && isModifyingData)
			{
				projections[index] = projections[index] || isProjecting;
				rejections[index] = rejections[index] || !isProjecting;
				wheel[index] += isProjecting ? 1 : -1;
				savePackedWheel();
			}
			return flag;
		}
		private void savePackedWheel()
		{
			int packedWheel = 0;
			if (spent)
			{
				packedWheel = int.MinValue;
			}
			else
			{
				for (int i = 0; i < 6; i++)
				{
					packedWheel <<= shift;
					if (rejections[i]) packedWheel += rejectionMask;
					if (projections[i]) packedWheel += projectionMask;
					packedWheel += wheel[i];
				}
			}
			partSimState.field_2730 = packedWheel;
		}

		// data ////////////////////
		private int[] wheel;
		private bool[] projections;
		private bool[] rejections;
		private bool spent;
		private PartSimState partSimState;

		const bool isProjection = true;
		const bool isRejection = false;
		const bool modifyData = true;
		const bool onlyCheck = false;
		const int metalMask = 0b00111;
		const int projectionMask = 0b01000;
		const int rejectionMask = 0b10000;
		const int shift = 5;
		readonly static HexIndex[] hexes = new HexIndex[6] {
			new HexIndex(1, 0),
			new HexIndex(0, 1),
			new HexIndex(-1, 1),
			new HexIndex(-1, 0),
			new HexIndex(0, -1),
			new HexIndex(1, -1)
		};
		readonly static AtomType[] metalIDs = new AtomType[8] {
			API.leadAtomType(), // array filler
			API.leadAtomType(),
			API.tinAtomType(),
			API.ironAtomType(),
			API.copperAtomType(),
			API.silverAtomType(),
			API.goldAtomType(),
			API.goldAtomType() // array filler
		};
		//==== DATA LAYOUT ====//
		// 32 bits total:
		//_______________________________________________________________________________________
		//| Header |   (0)          (1)          (2)          (3)          (4)          (5)      |
		//|  [][]  | [][][][][] , [][][][][] , [][][][][] , [][][][][] , [][][][][] , [][][][][] |
		//|________|_____________________________________________________________________________|
		//
		// Header Values:
		// [0][X] : Wheel is normal
		// [1][X] : Wheel is spent
		//
		// (0) => MetalInt at R0
		// (1) => MetalInt at R60
		// (2) => MetalInt at R120
		// (3) => MetalInt at R180
		// (4) => MetalInt at R240
		// (5) => MetalInt at R300
		//
		// (x) MetalInt format:
		// Rejection bit : Projection bit : Metal ID
		//       []      :       []       :  [][][]
	}

	private static bool ContentLoaded = false;
	public static void LoadContent()
	{
		if (ContentLoaded) return;
		ContentLoaded = true;

		string path;
		//=========================//
		//load the sound, and hook into stuff to make it work right
		path = "Content/reductiveMetallurgy/sounds/ravari_release.wav";
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

		void Method_540(On.class_201.orig_method_540 orig, class_201 class201_self)
		{
			orig(class201_self);
			RavariSpend.field_4062 = false;
		}
		On.class_201.method_540 += Method_540;
		//=========================//

		/*
		Texture blankTexture = class_238.field_1989.field_71;
		AtomType emptyAtom = new AtomType()
		{
			field_2284 = string.Empty, // non-local name
			field_2285 = class_134.method_254(string.Empty), // atomic name
			field_2287 = AtomTypes.field_1689.field_2287, // atom symbol
			field_2288 = blankTexture, // shadow
			field_2290 = new class_106()
			{
				field_994 = class_238.field_1989.field_81.field_596,//salt_diffuse
				field_995 = class_238.field_1989.field_81.field_597//salt_shade
			}
		};
		var tempAtom = emptyAtom;
		*/
		var tempAtom = API.quicksilverAtomType();

		RavariSeparateAnimation = new Texture[28];
		path = "reductiveMetallurgy/textures/parts/ravari_separate.array/separate_";
		for (int i = 0; i < RavariSeparateAnimation.Length; i++)
		{
			RavariSeparateAnimation[i] = class_235.method_615(path + (i+1).ToString("0000"));
		}

		RavariFlyAnimation = new Texture[32];
		path = "reductiveMetallurgy/textures/parts/atom_cage_fly.array/fly_";
		for (int i = 0; i < RavariFlyAnimation.Length; i++)
		{
			RavariFlyAnimation[i] = class_235.method_615(path + (i+1).ToString("0000"));
		}

		path = "reductiveMetallurgy/textures/parts/icons/";
		oldRavari = new PartType()
		{
			/*ID*/field_1528 = "wheel-verrin",
			/*Name*/field_1529 = class_134.method_253("Ravari's Wheel (old)", string.Empty),
			/*Desc*/field_1530 = class_134.method_253("By using Ravari's wheel with the glyphs of projection and rejection, quicksilver can be stored or discharged. The wheel also has a release mechanism.", string.Empty),
			/*Cost*/field_1531 = 30,
			/*Type*/field_1532 = (enum_2) 1,
			/*Programmable?*/field_1533 = true,
			/*Force-rotatable*/field_1536 = true,
			/*Berlo Atoms*/field_1544 = new Dictionary<HexIndex, AtomType>()
			{
				{new HexIndex(0, 1),  tempAtom},
				{new HexIndex(1, 0),  tempAtom},
				{new HexIndex(1, -1), tempAtom},
				{new HexIndex(0, -1), tempAtom},
				{new HexIndex(-1, 0), tempAtom},
				{new HexIndex(-1, 1), tempAtom},
			},
			/*Icon*/field_1547 = class_235.method_615(path + "verrin"),
			/*Hover Icon*/field_1548 = class_235.method_615(path + "verrin_hover"),
			/*Permissions*/field_1551 = API.perm_ravari,
			/*Only One Allowed?*/field_1552 = true,
		};

		oldRavariSpent = new PartType()
		{
			/*Type*/field_1532 = (enum_2) 1,
			/*Programmable?*/field_1533 = true,
		};

		var berlo = PartTypes.field_1771;
		QApi.AddPartTypeToPanel(oldRavari, berlo);

		path = "reductiveMetallurgy/textures/parts/atom_cage_broken.lighting/";

		var atomCageBrokenLighting = new class_126(
			class_235.method_615(path + "left"),
			class_235.method_615(path + "right"),
			class_235.method_615(path + "bottom"),
			class_235.method_615(path + "top")
		);
		path = "reductiveMetallurgy/textures/parts/atom_cage_broken_alt.lighting/";
		var atomCageBrokenLightingAlt = new class_126(
			class_235.method_615(path + "left"),
			class_235.method_615(path + "right"),
			class_235.method_615(path + "bottom"),
			class_235.method_615(path + "top")
		);

		// fetch vanilla textures
		var atomCageLighting = class_238.field_1989.field_90.field_232;
		var projectAtomAnimation = class_238.field_1989.field_81.field_614;

		QApi.AddPartType(oldRavari, (part, pos, editor, renderer) =>
		{
			class_236 class236 = editor.method_1989(part, pos);

			PartSimState partSimState = editor.method_507().method_481(part);
			var simTime = editor.method_504();

			//draw arm stubs
			var hexArmRotations = PartTypes.field_1767.field_1534;
			API.PrivateMethod<SolutionEditorBase>("method_2005").Invoke(editor, new object[] { part.method_1165(), hexArmRotations, class236 });

			// draw arms and their contents, if any
			int frameIndex = class_162.method_404((int)(simTime * projectAtomAnimation.Length), 0, projectAtomAnimation.Length);
			MetalWheel metalWheel = new MetalWheel(partSimState);
			HexIndex[] hexes;
			bool isSpent;
			Dictionary<HexIndex, AtomType> baseAtoms;
			Dictionary<HexIndex, AtomType> transmutationAtoms;
			metalWheel.getDrawData(out hexes, out baseAtoms, out transmutationAtoms, out isSpent);

			for (int i = 0; i < hexes.Length; i++)
			{
				var hex = hexes[i];

				if (!isSpent)
				{
					//draw atom
					AtomType baseAtom = baseAtoms[hex];
					AtomType transmutationAtom = transmutationAtoms[hex];

					Vector2 vector2 = renderer.field_1797 + class_187.field_1742.method_492(hex).Rotated(renderer.field_1798);
					float num1 = (Editor.method_922() - vector2).Angle() - 1.570796f;
					if (frameIndex < projectAtomAnimation.Length && baseAtom != transmutationAtom)
					{
						Texture animationFrame = projectAtomAnimation[frameIndex];
						Editor.method_927(frameIndex < 7 ? transmutationAtom : baseAtom, vector2, 1f, 1f, 1f, 1f, -21f, num1, null, null, false);
						class_135.method_272(animationFrame, vector2 - animationFrame.method_690());
					}
					else
					{
						Editor.method_927(baseAtom, vector2, 1f, 1f, 1f, 1f, -21f, num1, null, null, false);
					}
				}

				//draw cage
				float num4 = i * 60 * (float)Math.PI / 180f;
				float radians = renderer.field_1798 + num4;
				Vector2 vector2_9 = renderer.field_1797 + class_187.field_1742.method_492(new HexIndex(1, 0)).Rotated(radians);

				var atomcages = atomCageLighting;
				if (isSpent) atomcages = MainClass.RavariAlternateTexture ? atomCageBrokenLightingAlt : atomCageBrokenLighting;
				API.PrivateMethod<SolutionEditorBase>("method_2003").Invoke(editor, new object[] { atomcages, vector2_9, new Vector2(39f, 33f), radians });
			}
		});
	}
	public static void LoadMirrorRules()
	{
		FTSIGCTU.MirrorTool.addRule(oldRavari, FTSIGCTU.MirrorTool.mirrorVanBerlo);
	}
}