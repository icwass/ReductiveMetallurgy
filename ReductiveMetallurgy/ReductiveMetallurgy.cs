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
	//public static PartType wheelDaas;


	// private resources
	private static IDetour hook_Sim_method_1828;
	private static IDetour hook_Sim_method_1832;

	// private helper functions
	private static AtomType quicksilverAtomType() => AtomTypes.field_1680;

	private static bool glyphIsFiring(PartSimState partSimState) => partSimState.field_2743;
	private static void glyphNeedsToFire(PartSimState partSimState) => partSimState.field_2743 = true;
	//private static void glyphHasFired(PartSimState partSimState) => partSimState.field_2743 = false;

	private static void changeAtomTypeOfAtom(AtomReference atomReference, AtomType newAtomType)
	{
		var molecule = atomReference.field_2277;
		molecule.method_1106(newAtomType, atomReference.field_2278);
	}

	private static void playSound(Sim sim_self, Sound sound)
	{
		typeof(Sim).GetMethod("method_1856", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sim_self, new object[] { sound });
	}

	//drawing helpers
	public static Vector2 hexGraphicalOffset(HexIndex hex) => class_187.field_1742.method_492(hex);


	// private main functions


	// public main functions
	public override void Load()	{ }
	public override void LoadPuzzleContent()
	{
		Glyphs.LoadContent();
		Wheel.LoadContent();

		//------------------------- HOOKING -------------------------//
		hook_Sim_method_1828 = new Hook(
		typeof(Sim).GetMethod("method_1828", BindingFlags.Instance | BindingFlags.NonPublic),
		typeof(MainClass).GetMethod("OnSimMethod1828", BindingFlags.Static | BindingFlags.NonPublic)
		);
		hook_Sim_method_1832 = new Hook(
		typeof(Sim).GetMethod("method_1832", BindingFlags.Instance | BindingFlags.NonPublic),
		typeof(MainClass).GetMethod("OnSimMethod1832", BindingFlags.Static | BindingFlags.NonPublic)
		);
	}

	private delegate void orig_Sim_method_1828(Sim self);
	private delegate void orig_Sim_method_1832(Sim self, bool param_5369);
	private static void OnSimMethod1828(orig_Sim_method_1828 orig, Sim sim_self)
	{
		My_Method_1828(sim_self);
		orig(sim_self);
	}
	private static void OnSimMethod1832(orig_Sim_method_1832 orig, Sim sim_self, bool param_5369)
	{
		My_Method_1832(sim_self, param_5369);
		orig(sim_self, param_5369);
	}
	public static void My_Method_1828(Sim sim_self)
	{
		var sim_dyn = new DynamicData(sim_self);
		var partSimStates = sim_dyn.Get<Dictionary<Part, PartSimState>>("field_3821");
		foreach (var kvp in partSimStates.Where(x => x.Key.method_1159() == Wheel.Ravari))
		{
			var partSimState = kvp.Value;
			Wheel.MetalWheel metalWheel = new Wheel.MetalWheel(partSimState);
			metalWheel.clearProjectionsAndRejections();
		}
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

		Maybe<AtomReference> maybeFindAtom(Part part, HexIndex hex, List<Part> gripperList, bool checkWheels = false)
		{
			MethodInfo Method_1850 = simType.GetMethod("method_1850", BindingFlags.NonPublic | BindingFlags.Instance);
			return (Maybe<AtomReference>)Method_1850.Invoke(sim_self, new object[] { part, hex, gripperList, checkWheels });
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

		List<Part> ravariWheels = new List<Part>();
		foreach (Part ravariWheel in partList.Where(x => x.method_1159() == Wheel.Ravari))
		{
			ravariWheels.Add(ravariWheel);
		}


		void findSatisfactoryWheel(HexIndex target, bool checkProjection, List<Part> wheelList, out Part wheelResult, out HexRotation rot, out bool successFlag) {
			//////////// this should probably be moved to wheel.cs at some point
			// based somewhat on method_1850

			var dict = new Dictionary<HexIndex, HexRotation>()
				{
					{new HexIndex(1, 0),  HexRotation.R0},
					{new HexIndex(0, 1),  HexRotation.R60},
					{new HexIndex(-1, 1), HexRotation.R120},
					{new HexIndex(-1, 0), HexRotation.R180},
					{new HexIndex(0, -1), HexRotation.R240},
					{new HexIndex(1, -1), HexRotation.R300},
				};
			bool actionIsPossible(Wheel.MetalWheel metalWheel, HexRotation rot) => checkProjection ? metalWheel.canProject(rot) : metalWheel.canReject(rot);

			foreach (var wheel in wheelList)
			{
				foreach (var hex in dict.Keys)
				{
					rot = dict[hex];
					var wheelPartSimState = partSimStates[wheel];
					var metalWheel = new Wheel.MetalWheel(wheelPartSimState);
					if ((wheelPartSimState.field_2724 + hex) == target && actionIsPossible(metalWheel,rot))
					{
						wheelResult = wheel;
						successFlag = true;
						return;
					}
				}
			}
			rot = default(HexRotation);
			wheelResult = default(Part);
			successFlag = false;
		}




		// fire the glyphs!
		var GlyphProjection = PartTypes.field_1778;
		foreach (Part part in partList)
		{
			PartSimState partSimState = partSimStates[part];
			var partType = part.method_1159();

			if (partType == GlyphProjection)
			{
				//check if we need to project metal wheels
				HexIndex hexInput = new HexIndex(0, 0);
				HexIndex hexProject = new HexIndex(1, 0);
				AtomReference atomInput = default(AtomReference);
				Part ravariWheel;
				HexRotation rot;
				bool foundQuicksilverInput = maybeFindAtom(part, hexInput, gripperList).method_99(out atomInput);
				bool foundPromotableRavari = false;
				findSatisfactoryWheel(part.method_1184(hexProject), true, ravariWheels, out ravariWheel, out rot, out foundPromotableRavari);

				if (foundQuicksilverInput
				&& !atomInput.field_2281 // a single atom
				&& !atomInput.field_2282 // not held by a gripper
				&& foundPromotableRavari
				)
				{
					playSound(sim_self, projectionActivate);
					//glyph-flash animation
					Vector2 hexPosition = hexGraphicalOffset(part.method_1161() + hexProject.Rotated(part.method_1163()));
					Texture[] projectionGlyphFlashAnimation = class_238.field_1989.field_90.field_256;
					SEB.field_3935.Add(new class_228(SEB, (enum_7)1, hexPosition, projectionGlyphFlashAnimation, 30f, Vector2.Zero, part.method_1163().ToRadians()));
					// delete the input atom
					atomInput.field_2277.method_1107(atomInput.field_2278);
					// draw input getting consumed
					SEB.field_3937.Add(new class_286(SEB, atomInput.field_2278, atomInput.field_2280));
					// take care of outputs
					var metalWheel = new Wheel.MetalWheel(partSimStates[ravariWheel]);
					metalWheel.project(rot);
				}
			}
			else if (partType == Glyphs.Rejection)
			{
				HexIndex hexReject = new HexIndex(0, 0);
				HexIndex hexOutput = new HexIndex(1, 0);
				AtomReference atomDemote = default(AtomReference);
				AtomType rejectedAtomType = default(AtomType);
				Part ravariWheel;
				HexRotation rot = HexRotation.R0;
				bool outputNotBlocked = !maybeFindAtom(part, hexOutput, new List<Part>(), true).method_99(out _);
				bool foundDemotableAtom = maybeFindAtom(part, hexReject, new List<Part>()).method_99(out atomDemote);
				bool foundDemotableRavari = false;

				findSatisfactoryWheel(part.method_1184(hexReject), false, ravariWheels, out ravariWheel, out rot, out foundDemotableRavari);

				if (outputNotBlocked // output not blocked
				&& (foundDemotableRavari || (foundDemotableAtom && API.applyRejectionRule(atomDemote.field_2280, out rejectedAtomType)))
				)
				{
					playSound(sim_self, projectionActivate);
					//demote input
					if (foundDemotableAtom)
					{
						changeAtomTypeOfAtom(atomDemote, rejectedAtomType);
						Texture[] projectAtomAnimation = class_238.field_1989.field_81.field_614;
						atomDemote.field_2279.field_2276 = (Maybe<class_168>)new class_168(SEB, (enum_7)0, (enum_132)1, atomDemote.field_2280, projectAtomAnimation, 30f);
					}
					else // ravari
					{
						var metalWheel = new Wheel.MetalWheel(partSimStates[ravariWheel]);
						metalWheel.reject(rot);
					}
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
			else if (partType == Glyphs.Splitting)
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
					&& API.applySplittingRule(atomSplit.field_2280, out splitAtomTypePair) // is splittable
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
			else if (partType == Glyphs.Proliferation)
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
						if (API.applyProliferationRule(atomUp.field_2280, out prolifAtomTypePair))
						{
							fireProliferate(atomUp, atomDown);
						}
						else if (API.applyProliferationRule(atomDown.field_2280, out prolifAtomTypePair))
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
					source1.Last().method_1105(molecule9.method_1100()[key], key);
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

		//sim_dyn.Set("field_3821", partSimStates);
		//sim_dyn.Set("field_3826", struct122List);
		//sim_dyn.Set("field_3823", moleculeList);
		//----- BOILERPLATE-2 END -----//
	}

	public override void Unload()
	{
		hook_Sim_method_1832.Dispose();
		hook_Sim_method_1828.Dispose();
	}

	//------------------------- END HOOKING -------------------------//
	public override void PostLoad()
	{
		On.PuzzleEditorScreen.method_50 += PES_Method_50;
		On.SolutionEditorBase.method_1997 += SES_Method_1997;

		//optional dependencies
		if (QuintessentialLoader.CodeMods.Any(mod => mod.Meta.Name == "FTSIGCTU"))
		{
			Logger.Log("[ReductiveMetallurgy] Detected optional dependency 'FTSIGCTU' - will add mirror rules for parts.");
			Glyphs.LoadMirrorRules();
			Wheel.LoadMirrorRules();
		}
		else
		{
			Logger.Log("[ReductiveMetallurgy] Did not detect optional dependency 'FTSIGCTU'.");
		}
	}

	public void PES_Method_50(On.PuzzleEditorScreen.orig_method_50 orig, PuzzleEditorScreen pes_self, float param_4993)
	{
		orig(pes_self, param_4993);
		API.drawPermissionCheckboxes(pes_self);
	}
	public void SES_Method_1997(On.SolutionEditorBase.orig_method_1997 orig, SolutionEditorBase seb_self, Part part, Vector2 pos, float alpha)
	{
		if (part.method_1159() == Wheel.Ravari)
		{
			Wheel.drawGlow(seb_self, part, pos, alpha);
		}

		orig(seb_self, part, pos, alpha);
	}
}
