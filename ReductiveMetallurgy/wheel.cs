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
//using Permissions = enum_149;
using AtomTypes = class_175;
using PartTypes = class_191;
using Texture = class_256;

public static class Wheel
{
	public static PartType Ravari, RavariSpent;

	public static void drawGlow(SolutionEditorBase seb_self, Part part, Vector2 pos, float alpha)
	{
		var sebType = typeof(SolutionEditorBase);
		MethodInfo Method_2006 = sebType.GetMethod("method_2006", BindingFlags.NonPublic | BindingFlags.Static);
		MethodInfo Method_2016 = sebType.GetMethod("method_2016", BindingFlags.NonPublic | BindingFlags.Static);

		var cageGlow = class_238.field_1989.field_97.field_367; // textures/select/cage
		Color color = Color.White.WithAlpha(alpha);
		class_236 class236 = seb_self.method_1989(part, pos);

		Method_2006.Invoke(seb_self, new object[] { part.method_1165(), PartTypes.field_1767.field_1534, class236, color });
		for (int index = 0; index < 6; ++index)
		{
			float num = index * 60 * ((float)Math.PI / 180f);
			Method_2016.Invoke(seb_self, new object[] { cageGlow, color, class236.field_1984, class236.field_1985 + num });
		}
	}

	public static void manageSpentRavaris(Sim sim_self, Action action)
	{
		var sim_dyn = new DynamicData(sim_self);
		var SEB = sim_dyn.Get<SolutionEditorBase>("field_3818");
		var solution = SEB.method_502();
		var partList = solution.field_3919;
		var partSimStates = sim_dyn.Get<Dictionary<Part, PartSimState>>("field_3821");

		foreach (var ravari in partList.Where(x => x.method_1159() == Ravari))
		{
			var metalWheel = new MetalWheel(partSimStates[ravari]);
			if (metalWheel.isSpent)
			{
				var ravari_dyn = new DynamicData(ravari);
				ravari_dyn.Set("field_2691", RavariSpent);
			}
		}
		//=====//
		action();
		//=====//
		foreach (var ravari in partList.Where(x => x.method_1159() == RavariSpent))
		{
			var ravari_dyn = new DynamicData(ravari);
			ravari_dyn.Set("field_2691", Ravari);
		}
	}

	//private static AtomType quicksilverAtomType() => AtomTypes.field_1680;
	private static AtomType leadAtomType() => AtomTypes.field_1681;
	private static AtomType tinAtomType() => AtomTypes.field_1683;
	private static AtomType ironAtomType() => AtomTypes.field_1684;
	private static AtomType copperAtomType() => AtomTypes.field_1682;
	private static AtomType silverAtomType() => AtomTypes.field_1685;
	private static AtomType goldAtomType() => AtomTypes.field_1686;

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

			Sound unbondingActivate = class_238.field_1991.field_1849;
			Sound simulationStop = class_238.field_1991.field_1863;
			//MainClass.playSound(sim_self, unbondingActivate);
			float volumeFactor = SEB.method_506();
			simulationStop.method_28(0.75f * volumeFactor);
			unbondingActivate.method_28(2f * volumeFactor);
			//draw separation animations
			Texture[] unbondingAnimation = class_238.field_1989.field_83.field_154; // or class_238.field_1989.field_83.field_156
			foreach (var hex in hexes)
			{
				var hex1 = partSimState.field_2724;
				var hex2 = hex1 + hex;
				Vector2 vector2_6 = class_162.method_413(class_187.field_1742.method_492(hex1), class_187.field_1742.method_492(hex2), 0.62f);
				var vector2_5 = class_187.field_1742.method_492(hex2 - hex1);
				class_228 class228 = new class_228(SEB, (enum_7)1, vector2_6, unbondingAnimation, 75f, new Vector2(1.5f, -5f), vector2_5.Angle());
				SEB.field_3935.Add(class228);
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
			leadAtomType(), // for array safety
			leadAtomType(),
			tinAtomType(),
			ironAtomType(),
			copperAtomType(),
			silverAtomType(),
			goldAtomType(),
			goldAtomType() // for array safety
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

		//string path;
		//path = "reductiveMetallurgy/textures/parts/icons/";

		string path;
		path = "reductiveMetallurgy/textures/parts/icons/";

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

		Ravari = new PartType()
		{
			/*ID*/field_1528 = "wheel-verrin",
			/*Name*/field_1529 = class_134.method_253("Ravari's Wheel", string.Empty),
			/*Desc*/field_1530 = class_134.method_253("By using Ravari's Wheel with the glyphs of projection and rejection, quicksilver can be stored or released.", string.Empty),
			/*Cost*/field_1531 = 30,
			/*Type*/field_1532 = (enum_2) 1,
			/*Programmable?*/field_1533 = true,
			/*Force-rotatable*/field_1536 = true,
			/*Berlo Atoms*/field_1544 = new Dictionary<HexIndex, AtomType>()
			{
				{new HexIndex(0, 1),  emptyAtom},
				{new HexIndex(1, 0),  emptyAtom},
				{new HexIndex(1, -1), emptyAtom},
				{new HexIndex(0, -1), emptyAtom},
				{new HexIndex(-1, 0), emptyAtom},
				{new HexIndex(-1, 1), emptyAtom},
			},
			/*Icon*/field_1547 = class_235.method_615(path + "verrin"),
			/*Hover Icon*/field_1548 = class_235.method_615(path + "verrin_hover"),
			/*Permissions*/field_1551 = API.perm_ravari,
			/*Only One Allowed?*/field_1552 = true,
		};

		RavariSpent = new PartType()
		{
			//*ID*/field_1528 = "wheel-verrin-spent",
			//*Name*/field_1529 = class_134.method_253("Ravari's Wheel, Spent", string.Empty),
			//*Desc*/field_1530 = class_134.method_253("This wheel used to have metal atoms. Alas, they are gone.", string.Empty),
			//*Cost*/field_1531 = 30,
			/*Type*/field_1532 = (enum_2) 1,
			/*Programmable?*/field_1533 = true,
			//*Force-rotatable*/field_1536 = true,
			//*Icon*/field_1547 = class_235.method_615(path + "verrin"),
			//*Hover Icon*/field_1548 = class_235.method_615(path + "verrin_hover"),
			//*Permissions*/field_1551 = API.perm_ravari,
			//*Only One Allowed?*/field_1552 = true,
		};

		var berlo = PartTypes.field_1771;
		QApi.AddPartTypeToPanel(Ravari, berlo);

		// fetch vanilla textures
		var atomCageLighting = class_238.field_1989.field_90.field_232;
		var projectAtomAnimation = class_238.field_1989.field_81.field_614;

		QApi.AddPartType(Ravari, (part, pos, editor, renderer) =>
		{
			class_236 class236 = editor.method_1989(part, pos);

			PartSimState partSimState = editor.method_507().method_481(part);
			var simTime = editor.method_504();

			var sebType = typeof(SolutionEditorBase);
			MethodInfo Method_2003 = sebType.GetMethod("method_2003", BindingFlags.NonPublic | BindingFlags.Static);
			MethodInfo Method_2005 = sebType.GetMethod("method_2005", BindingFlags.NonPublic | BindingFlags.Static);

			//draw arm stubs
			var hexArmRotations = PartTypes.field_1767.field_1534;
			Method_2005.Invoke(editor, new object[] { part.method_1165(), hexArmRotations, class236 });

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

				if (isSpent)
				{
					// add stuff to here later
				}
				else
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
					//draw cage
					float num4 = i * 60 * (float)Math.PI / 180f;
					float radians = renderer.field_1798 + num4;
					Vector2 vector2_9 = renderer.field_1797 + class_187.field_1742.method_492(new HexIndex(1, 0)).Rotated(radians);
					Method_2003.Invoke(editor, new object[] { atomCageLighting, vector2_9, new Vector2(39f, 33f), radians });
				}
			}
		});
	}
	public static void LoadMirrorRules()
	{
		FTSIGCTU.MirrorTool.addRule(Ravari, FTSIGCTU.MirrorTool.mirrorVanBerlo);
	}
}