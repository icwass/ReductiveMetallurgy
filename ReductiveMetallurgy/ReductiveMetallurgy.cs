using Mono.Cecil.Cil;
using MonoMod.Cil;
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

using PartTypes = class_191;
using Texture = class_256;

public class MainClass : QuintessentialMod
{
	// resources
	static IDetour hook_Sim_method_1832;

	static Texture[] projectAtomAnimation => class_238.field_1989.field_81.field_614;
	static Sound animismusActivate => class_238.field_1991.field_1838;
	static Sound projectionActivate => class_238.field_1991.field_1844;
	static Sound purificationActivate => class_238.field_1991.field_1845;

	// helper functions
	private static bool glyphIsFiring(PartSimState partSimState) => partSimState.field_2743;
	private static void glyphNeedsToFire(PartSimState partSimState) => partSimState.field_2743 = true;
	private static void playSound(Sim sim_self, Sound sound) => API.PrivateMethod<Sim>("method_1856").Invoke(sim_self, new object[] { sound });

	//drawing helpers
	public static Vector2 hexGraphicalOffset(HexIndex hex) => class_187.field_1742.method_492(hex);


	// public main functions
	public override void Load()
	{
		//
	}
	public override void LoadPuzzleContent()
	{
		Glyphs.LoadContent();
		Wheel.LoadContent();

		//------------------------- HOOKING -------------------------//
		hook_Sim_method_1832 = new Hook(API.PrivateMethod<Sim>("method_1832"), OnSimMethod1832);

		IL.SolutionEditorBase.method_1984 += drawRavariWheelAtoms;
	}
	public static void drawRavariWheelAtoms(ILContext il)
	{
		ILCursor cursor = new ILCursor(il);
		// skip ahead to roughly where method_2015 is called
		cursor.Goto(658);

		// jump ahead to just after the method_2015 for-loop
		if (!cursor.TryGotoNext(MoveType.After, instr => instr.Match(OpCodes.Ldarga_S))) return;

		// load the SolutionEditorBase self and the class423 local onto the stack so we can use it
		cursor.Emit(OpCodes.Ldarg_0);
		cursor.Emit(OpCodes.Ldloc_0);
		// then run the new code
		cursor.EmitDelegate<Action<SolutionEditorBase, SolutionEditorBase.class_423>>((seb_self, class423) =>
		{
			if (seb_self.method_503() != enum_128.Stopped)
			{
				var partList = seb_self.method_502().field_3919;
				foreach (var ravari in partList.Where(x => x.method_1159() == Wheel.Ravari))
				{
					Wheel.drawRavariAtoms(seb_self, ravari, class423.field_3959);
				}
			}
		});
	}

	private delegate void orig_Sim_method_1832(Sim self, bool isConsumptionHalfstep);
	private static void OnSimMethod1832(orig_Sim_method_1832 orig, Sim sim_self, bool isConsumptionHalfstep)
	{
		My_Method_1832(sim_self, isConsumptionHalfstep);
		orig(sim_self, isConsumptionHalfstep);
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

		// find all grippers that are holding molecules
		// and make them temporarily release them
		List<Part> gripperList = new List<Part>();
		foreach (Part part in partList)
		{
			foreach (Part gripper in part.field_2696.Where(x=>partSimStates[x].field_2729.method_1085()))
			{
				gripperList.Add(gripper);
				API.PrivateMethod<Sim>("method_1842").Invoke(sim_self, new object[] { gripper });
			}
		}
		//----- BOILERPLATE-1 END -----//

		//define some helpers

		bool atomTypeIsProjectable(AtomReference atomReference) => atomReference.field_2280.field_2297.method_1085();
		AtomType projectionResult(AtomReference atomReference) => atomReference.field_2280.field_2297.method_1087();

		Maybe<AtomReference> maybeFindAtom(Part part, HexIndex hex, List<Part> list, bool checkWheels = false)
		{
			return (Maybe<AtomReference>)API.PrivateMethod<Sim>("method_1850").Invoke(sim_self, new object[] { part, hex, list, checkWheels });
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

		void consumeAtomRef(AtomReference atomRef)
		{
			// delete the input atom
			atomRef.field_2277.method_1107(atomRef.field_2278);
			// draw input getting consumed
			SEB.field_3937.Add(new class_286(SEB, atomRef.field_2278, atomRef.field_2280));
		}

		void changeAtomTypeOfMetal(AtomReference atomReference, AtomType newAtomType)
		{
			// change atom type
			var molecule = atomReference.field_2277;
			molecule.method_1106(newAtomType, atomReference.field_2278);
			// draw projection animation
			atomReference.field_2279.field_2276 = (Maybe<class_168>)new class_168(SEB, (enum_7)0, (enum_132)1, atomReference.field_2280, projectAtomAnimation, 30f);
		}

		// fire the glyphs!
		var GlyphProjection = PartTypes.field_1778;
		foreach (Part part in partList)
		{
			PartSimState partSimState = partSimStates[part];
			var partType = part.method_1159();

			bool theRavariSpecial = !isConsumptionHalfstep; // "direct-transferring" quicksilver to/from a ravariWheel should only happen on one of the two half-steps

			if (partType == GlyphProjection)
			{
				// check if we need to project a ravariWheel, or if we need to project by direct-rejection from a ravariWheel
				HexIndex hexInput = new HexIndex(0, 0);
				HexIndex hexProject = new HexIndex(1, 0);
				AtomReference atomInput = default(AtomReference);
				AtomReference atomInputRavari = default(AtomReference);
				AtomReference atomProject = default(AtomReference);
				AtomReference atomProjectRavari = default(AtomReference);
				AtomType rejectionResult = default(AtomType);

				bool foundQuicksilverInput =
					maybeFindAtom(part, hexInput, gripperList).method_99(out atomInput)
					&& atomInput.field_2280 == API.quicksilverAtomType() // quicksilver atom
					&& !atomInput.field_2281 // a single atom
					&& !atomInput.field_2282 // not held by a gripper
				;
				bool foundPromotableMetal =
					maybeFindAtom(part, hexProject, gripperList).method_99(out atomProject)
					&& atomTypeIsProjectable(atomProject) // atomType has a projection result
				;
				bool foundDemotableRavari =
					theRavariSpecial
					&& Wheel.maybeFindRavariWheelAtom(sim_self, part, hexInput).method_99(out atomInputRavari)
					&& API.applyRejectionRule(atomInputRavari.field_2280, out rejectionResult)
				;
				bool foundPromotableRavari =
					Wheel.maybeFindRavariWheelAtom(sim_self, part, hexProject).method_99(out atomProjectRavari)
					&& atomTypeIsProjectable(atomProjectRavari)
				;

				if (
					(foundQuicksilverInput || foundDemotableRavari) // found input
					&& (foundPromotableMetal || foundPromotableRavari) // found output
					&& (foundDemotableRavari || foundPromotableRavari) // ignore (quicksilver && metal atom) because that case was already covered
				)
				{
					// sounds and animation for firing the glyph
					playSound(sim_self, projectionActivate);
					Vector2 hexPosition = hexGraphicalOffset(part.method_1161() + hexProject.Rotated(part.method_1163()));
					Texture[] projectionGlyphFlashAnimation = class_238.field_1989.field_90.field_256;
					SEB.field_3935.Add(new class_228(SEB, (enum_7)1, hexPosition, projectionGlyphFlashAnimation, 30f, Vector2.Zero, part.method_1163().ToRadians()));

					// handle input
					if (foundQuicksilverInput)
					{
						consumeAtomRef(atomInput);
					}
					else
					{
						changeAtomTypeOfMetal(atomInputRavari, rejectionResult);
						Wheel.DrawRavariFlash(SEB, part, hexInput);
					}
					// handle output
					AtomReference promotableRef = foundPromotableMetal ? atomProject : atomProjectRavari;
					changeAtomTypeOfMetal(promotableRef, projectionResult(promotableRef));
				}
			}
			else if (partType == Glyphs.Rejection)
			{
				HexIndex hexReject = new HexIndex(0, 0);
				HexIndex hexOutput = new HexIndex(1, 0);
				AtomReference atomReject = default(AtomReference);
				AtomReference atomPromoteRavari = default(AtomReference);
				AtomType rejectionResult = default(AtomType);

				bool foundDemotableMetal =
					(maybeFindAtom(part, hexReject, gripperList).method_99(out atomReject)
					&& API.applyRejectionRule(atomReject.field_2280, out rejectionResult)
					)
					||
					(Wheel.maybeFindRavariWheelAtom(sim_self, part, hexReject).method_99(out atomReject)
					&& API.applyRejectionRule(atomReject.field_2280, out rejectionResult)
					)
				;
				bool outputNotBlocked = !maybeFindAtom(part, hexOutput, new List<Part>(), true).method_99(out _); // the extra TRUE means we're checking for berlo and ravari wheels, etc
				bool foundPromotableRavari =
					theRavariSpecial
					&& Wheel.maybeFindRavariWheelAtom(sim_self, part, hexOutput).method_99(out atomPromoteRavari)
					&& atomTypeIsProjectable(atomPromoteRavari)
				;

				if (
					foundDemotableMetal // found input
					&& (outputNotBlocked || foundPromotableRavari) // found output
				)
				{
					// sounds and animation for firing the glyph
					playSound(sim_self, projectionActivate);
					Vector2 hexPosition = hexGraphicalOffset(part.method_1161() + hexReject.Rotated(part.method_1163()));
					Texture[] projectionGlyphFlashAnimation = class_238.field_1989.field_90.field_256;
					float radians = (part.method_1163() + HexRotation.R180).ToRadians();
					SEB.field_3935.Add(new class_228(SEB, (enum_7)1, hexPosition, projectionGlyphFlashAnimation, 30f, Vector2.Zero, radians));

					changeAtomTypeOfMetal(atomReject, rejectionResult);
					if (outputNotBlocked)
					{
						spawnAtomAtHex(part, hexOutput, API.quicksilverAtomType());
						Texture[] disposalFlashAnimation = class_238.field_1989.field_90.field_240;
						Vector2 animationPosition = hexGraphicalOffset(part.method_1161() + hexOutput.Rotated(part.method_1163())) + new Vector2(80f, 0f);
						SEB.field_3936.Add(new class_228(SEB, (enum_7)1, animationPosition, disposalFlashAnimation, 30f, Vector2.Zero, 0f));
					}
					else // foundPromotableRavari
					{
						changeAtomTypeOfMetal(atomPromoteRavari, projectionResult(atomPromoteRavari));
						Wheel.DrawRavariFlash(SEB, part, hexOutput);
					}
				}
			}
			else if (partType == Glyphs.Deposition)
			{
				HexIndex hexInput = new HexIndex(0, 0);
				HexIndex hexLeft = new HexIndex(-1, 0);
				HexIndex hexRight = new HexIndex(1, 0);

				AtomReference atomDeposit;
				Pair<AtomType, AtomType> depositAtomTypePair;

				if (glyphIsFiring(partSimState))
				{
					spawnAtomAtHex(part, hexLeft, partSimState.field_2744[0]);
					spawnAtomAtHex(part, hexRight, partSimState.field_2744[1]);
				}
				else if (isConsumptionHalfstep
					&& !maybeFindAtom(part, hexLeft, new List<Part>()).method_99(out _) // left output not blocked
					&& !maybeFindAtom(part, hexRight, new List<Part>()).method_99(out _) // right output not blocked
					&& maybeFindAtom(part, hexInput, gripperList).method_99(out atomDeposit) // depositable atom exists
					&& !atomDeposit.field_2281 // a single atom
					&& !atomDeposit.field_2282 // not held by a gripper
					&& API.applyDepositionRule(atomDeposit.field_2280, out depositAtomTypePair) // is depositable
				)
				{
					glyphNeedsToFire(partSimState);
					playSound(sim_self, purificationActivate);
					consumeAtomRef(atomDeposit);
					// take care of outputs
					partSimState.field_2744 = new AtomType[2] { depositAtomTypePair.Left, depositAtomTypePair.Right };
					addColliderAtHex(part, hexLeft);
					addColliderAtHex(part, hexRight);
				}
			}
			else if (partType == Glyphs.ProliferationAmbi)
			{
				HexIndex hexLeft = new HexIndex(0, 0);
				HexIndex hexRight = new HexIndex(1, 0);
				HexIndex hexSelect = new HexIndex(0, 1);
				if (glyphIsFiring(partSimState))
				{
					if (partSimState.field_2744[1] == API.quicksilverAtomType())
					{
						spawnAtomAtHex(part, hexLeft, partSimState.field_2744[0]);
					}
					else
					{
						spawnAtomAtHex(part, hexRight, partSimState.field_2744[1]);
					}

				}
				else if (isConsumptionHalfstep)
				{
					bool lefty = !maybeFindAtom(part, hexLeft, new List<Part>(), true).method_99(out _);// output not blocked. the extra TRUE means we're checking for berlo and ravari wheels, etc
					bool righty = !maybeFindAtom(part, hexRight, new List<Part>(), true).method_99(out _);
					// we use an XOR condition because the glyph can only fire if one of the hexes is empty and the other is covered by a quicksilver source!
					if (lefty ^ righty)
					{
						HexIndex hexInput = !lefty ? hexLeft : hexRight;
						HexIndex hexOutput = lefty ? hexLeft : hexRight;

						AtomReference atomSelect;
						AtomReference atomInput;
						AtomReference atomInputRavari = default(AtomReference);
						AtomType rejectionResult = default(AtomType);

						bool foundAtomSelect =
							(maybeFindAtom(part, hexSelect, gripperList).method_99(out atomSelect)
							&& API.applyProliferationRule(atomSelect.field_2280, out _)
							)
							||
							(Wheel.maybeFindRavariWheelAtom(sim_self, part, hexSelect).method_99(out atomSelect)
							&& API.applyProliferationRule(atomSelect.field_2280, out _)
							)
						;

						bool foundQuicksilverInput =
						maybeFindAtom(part, hexInput, gripperList).method_99(out atomInput)
						&& atomInput.field_2280 == API.quicksilverAtomType() // quicksilver atom
						&& !atomInput.field_2281 // a single atom
						&& !atomInput.field_2282 // not held by a gripper
						;

						bool foundDemotableRavari =
							Wheel.maybeFindRavariWheelAtom(sim_self, part, hexInput).method_99(out atomInputRavari)
							&& API.applyRejectionRule(atomInputRavari.field_2280, out rejectionResult)
						;

						if (foundAtomSelect
							&& (foundQuicksilverInput || foundDemotableRavari)
						)
						{
							glyphNeedsToFire(partSimState);
							playSound(sim_self, animismusActivate);
							// take care of inputs
							if (foundQuicksilverInput)
							{
								consumeAtomRef(atomInput);
							}
							else // foundDemotableRavari
							{
								changeAtomTypeOfMetal(atomInputRavari, rejectionResult);
								Wheel.DrawRavariFlash(SEB, part, hexInput);
							}
							// take care of outputs
							if (lefty)
							{
								partSimState.field_2744 = new AtomType[2] { atomSelect.field_2280, API.quicksilverAtomType() };
							}
							else
							{
								partSimState.field_2744 = new AtomType[2] { API.quicksilverAtomType(), atomSelect.field_2280 };
							}
							addColliderAtHex(part, hexOutput);
						}
					}
				}



			}
			else if (partType == Glyphs.ProliferationLeft || partType == Glyphs.ProliferationRight)
			{
				bool lefty = partType == Glyphs.ProliferationLeft;
				HexIndex hexLeft = new HexIndex(0, 0);
				HexIndex hexRight = new HexIndex(1, 0);
				HexIndex hexSelect = new HexIndex(0, 1);

				HexIndex hexInput = lefty ? hexLeft : hexRight;
				HexIndex hexOutput = lefty ? hexRight : hexLeft;

				if (glyphIsFiring(partSimState))
				{
					spawnAtomAtHex(part, hexOutput, partSimState.field_2744[0]);
				}
				else if (
					isConsumptionHalfstep
					&& !maybeFindAtom(part, hexOutput, new List<Part>(), true).method_99(out _) // output not blocked. the extra TRUE means we're checking for berlo and ravari wheels, etc
				)
				{
					AtomReference atomSelect;
					AtomReference atomInput;
					AtomReference atomInputRavari = default(AtomReference);
					AtomType rejectionResult = default(AtomType);

					bool foundAtomSelect =
						(maybeFindAtom(part, hexSelect, gripperList).method_99(out atomSelect)
						&& API.applyProliferationRule(atomSelect.field_2280, out _)
						)
						||
						(Wheel.maybeFindRavariWheelAtom(sim_self, part, hexSelect).method_99(out atomSelect)
						&& API.applyProliferationRule(atomSelect.field_2280, out _)
						)
					;

					bool foundQuicksilverInput =
					maybeFindAtom(part, hexInput, gripperList).method_99(out atomInput)
					&& atomInput.field_2280 == API.quicksilverAtomType() // quicksilver atom
					&& !atomInput.field_2281 // a single atom
					&& !atomInput.field_2282 // not held by a gripper
					;

					bool foundDemotableRavari =
						Wheel.maybeFindRavariWheelAtom(sim_self, part, hexInput).method_99(out atomInputRavari)
						&& API.applyRejectionRule(atomInputRavari.field_2280, out rejectionResult)
					;

					if (foundAtomSelect
						&& (foundQuicksilverInput || foundDemotableRavari)
					)
					{
						glyphNeedsToFire(partSimState);
						playSound(sim_self, animismusActivate);
						// take care of inputs
						if (foundQuicksilverInput)
						{
							consumeAtomRef(atomInput);
						}
						else // foundDemotableRavari
						{
							changeAtomTypeOfMetal(atomInputRavari, rejectionResult);
							Wheel.DrawRavariFlash(SEB, part, hexInput);
						}
						// take care of outputs
						partSimState.field_2744 = new AtomType[1] { atomSelect.field_2280 };
						addColliderAtHex(part, hexOutput);
					}
				}
			}
			else if (partType == Glyphs.Proliferation)
			{
				HexIndex hexLeft = new HexIndex(0, 0);
				HexIndex hexRight = new HexIndex(1, 0);
				HexIndex hexUp = new HexIndex(0, 1);
				HexIndex hexDown = new HexIndex(1, -1);

				if (glyphIsFiring(partSimState))
				{
					spawnAtomAtHex(part, hexLeft, partSimState.field_2744[0]);
					spawnAtomAtHex(part, hexRight, partSimState.field_2744[1]);
				}
				else if (
					isConsumptionHalfstep
					&& !maybeFindAtom(part, hexLeft, new List<Part>()).method_99(out _) // left output not blocked
					&& !maybeFindAtom(part, hexRight, new List<Part>()).method_99(out _) // right output not blocked
				)
				{
					AtomReference atomUp;
					AtomReference atomDown;
					bool foundAtomUp = maybeFindAtom(part, hexUp, gripperList).method_99(out atomUp)
						&& !atomUp.field_2281 // a single atom
						&& !atomUp.field_2282 // not held by a gripper
					;
					bool foundAtomDown = maybeFindAtom(part, hexDown, gripperList).method_99(out atomDown) // down atom exists
						&& !atomDown.field_2281 // a single atom
						&& !atomDown.field_2282 // not held by a gripper
					;

					bool proliferateUp = foundAtomUp && API.applyProliferationRule(atomUp.field_2280, out _);
					bool proliferateDown = foundAtomDown && API.applyProliferationRule(atomDown.field_2280, out _);

					if (proliferateUp ^ proliferateDown) // found metal input
					{
						// XOR, since proliferation takes precisely one Quicksilver (via atom or Ravari) and one NON-quicksilver atom
						// so finding zero or two proliferable atoms is no good

						HexIndex hexProliferate = proliferateUp ? hexUp : hexDown;
						HexIndex hexQuicksilver = proliferateUp ? hexDown : hexUp;

						AtomReference atomProlif = proliferateUp ? atomUp : atomDown;
						AtomReference atomQuicksilver = proliferateUp ? atomDown : atomUp;
						AtomReference atomDemotableRavari = default(AtomReference);
						AtomType rejectionResult = default(AtomType);

						bool foundQuicksilver = (proliferateUp ? foundAtomDown : foundAtomUp) && atomQuicksilver.field_2280 == API.quicksilverAtomType();

						bool foundDemotableRavari =
							Wheel.maybeFindRavariWheelAtom(sim_self, part, hexQuicksilver).method_99(out atomDemotableRavari)
							&& API.applyRejectionRule(atomDemotableRavari.field_2280, out rejectionResult)
						;

						if (foundQuicksilver || foundDemotableRavari)
						{
							//fire the glyph!
							Pair<AtomType, AtomType> prolifAtomTypePair;
							API.applyProliferationRule(atomProlif.field_2280, out prolifAtomTypePair);
							glyphNeedsToFire(partSimState);
							playSound(sim_self, animismusActivate);

							//take care of inputs
							consumeAtomRef(atomProlif);
							if (foundQuicksilver)
							{
								consumeAtomRef(atomQuicksilver);
							}
							else // foundDemotableRavari
							{
								changeAtomTypeOfMetal(atomDemotableRavari, rejectionResult);
								Wheel.DrawRavariFlash(SEB, part, hexQuicksilver);
							}

							// take care of outputs
							partSimState.field_2744 = new AtomType[2] { prolifAtomTypePair.Left, prolifAtomTypePair.Right };
							addColliderAtHex(part, hexLeft);
							addColliderAtHex(part, hexRight);
						}
					}
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
		On.SolutionEditorBase.method_1997 += DrawPartSelectionGlows;

		//optional dependencies
		if (QuintessentialLoader.CodeMods.Any(mod => mod.Meta.Name == "FTSIGCTU"))
		{
			Logger.Log("[ReductiveMetallurgy] Detected optional dependency 'FTSIGCTU' - adding mirror rules for parts.");
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
	public void DrawPartSelectionGlows(On.SolutionEditorBase.orig_method_1997 orig, SolutionEditorBase seb_self, Part part, Vector2 pos, float alpha)
	{
		if (part.method_1159() == Wheel.Ravari)
		{
			Wheel.drawSelectionGlow(seb_self, part, pos, alpha);
		}

		orig(seb_self, part, pos, alpha);
	}
}
