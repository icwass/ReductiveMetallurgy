﻿using Mono.Cecil.Cil;
using MonoMod.Cil;
//using MonoMod.RuntimeDetour;
//using MonoMod.Utils;
using Quintessential;
//using Quintessential.Settings;
//using SDL2;
using System;
using System.Linq;
using System.Collections.Generic;
//using System.Reflection;

namespace ReductiveMetallurgy;

using PartTypes = class_191;
using Texture = class_256;

public class MainClass : QuintessentialMod
{
	// resources
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

	public static Texture[] fetchTextureArray(int length, string path)
	{
		var ret = new Texture[length];
		for (int i = 0; i < ret.Length; i++)
		{
			ret[i] = class_235.method_615(path + (i + 1).ToString("0000"));
		}
		return ret;
	}

	// public main functions
	public override void Load()
	{
		//
	}

	public override void LoadPuzzleContent()
	{
		Glyphs.LoadContent();
		Wheel.LoadContent();

		QApi.AddPuzzlePermission(API.RejectionPermission, "Glyph of Rejection", "Reductive Metallurgy");
		QApi.AddPuzzlePermission(API.DepositionPermission, "Glyph of Deposition", "Reductive Metallurgy");
		QApi.AddPuzzlePermission(API.RavariPermission, "Ravari's Wheel", "Reductive Metallurgy");
		QApi.AddPuzzlePermission(API.ProliferationPermission, "Glyph of Proliferation", "Reductive Metallurgy");

		//------------------------- HOOKING -------------------------//
		QApi.RunAfterCycle(My_Method_1832);

		IL.SolutionEditorBase.method_1984 += drawRavariWheelAtoms;
	}
	private static void drawRavariWheelAtoms(ILContext il)
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

	private static void My_Method_1832(Sim sim_self, bool isConsumptionHalfstep)
	{
		var SEB = sim_self.field_3818;
		var solution = SEB.method_502();
		var partList = solution.field_3919;
		var partSimStates = sim_self.field_3821;
		var struct122List = sim_self.field_3826;
		var moleculeList = sim_self.field_3823;
		var gripperList = sim_self.HeldGrippers;

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

		void consumeAtomReference(AtomReference atomRef)
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
					&& atomInput.field_2280 == API.quicksilverAtomType // quicksilver atom
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
						consumeAtomReference(atomInput);
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
						spawnAtomAtHex(part, hexOutput, API.quicksilverAtomType);
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
				AtomType depositAtomTypeHi, depositAtomTypeLo;

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
					&& API.applyDepositionRule(atomDeposit.field_2280, out depositAtomTypeHi, out depositAtomTypeLo) // is depositable
				)
				{
					glyphNeedsToFire(partSimState);
					playSound(sim_self, purificationActivate);
					consumeAtomReference(atomDeposit);
					// take care of outputs
					partSimState.field_2744 = new AtomType[2] { depositAtomTypeHi, depositAtomTypeLo };
					addColliderAtHex(part, hexLeft);
					addColliderAtHex(part, hexRight);
				}
			}
			else if (partType == Glyphs.Proliferation)
			{
				HexIndex hexLeft = new HexIndex(0, 0);
				HexIndex hexRight = new HexIndex(1, 0);
				HexIndex hexSelect = new HexIndex(0, 1);
				if (glyphIsFiring(partSimState))
				{
					if (partSimState.field_2744[1] == API.quicksilverAtomType)
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
					// check if outputs are not blocked. the extra TRUE means we're checking for berlo and ravari wheels, etc
					bool lefty = !maybeFindAtom(part, hexLeft, new List<Part>(), true).method_99(out _);
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
							&& API.applyProliferationRule(atomSelect.field_2280)
							)
							||
							(Wheel.maybeFindRavariWheelAtom(sim_self, part, hexSelect).method_99(out atomSelect)
							&& API.applyProliferationRule(atomSelect.field_2280)
							)
						;

						bool foundQuicksilverInput =
						maybeFindAtom(part, hexInput, gripperList).method_99(out atomInput)
						&& atomInput.field_2280 == API.quicksilverAtomType // quicksilver atom
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
							Glyphs.DrawSelectorFlash(SEB, part, hexSelect);
							// take care of inputs
							if (foundQuicksilverInput)
							{
								consumeAtomReference(atomInput);
							}
							else // foundDemotableRavari
							{
								changeAtomTypeOfMetal(atomInputRavari, rejectionResult);
								Wheel.DrawRavariFlash(SEB, part, hexInput);
							}
							// take care of outputs
							if (lefty)
							{
								partSimState.field_2744 = new AtomType[2] { atomSelect.field_2280, API.quicksilverAtomType };
							}
							else
							{
								partSimState.field_2744 = new AtomType[2] { API.quicksilverAtomType, atomSelect.field_2280 };
							}
							addColliderAtHex(part, hexOutput);
						}
					}
				}
			}
		}
	}

	public override void Unload()
	{
		//
	}

	//------------------------- END HOOKING -------------------------//
	public override void PostLoad()
	{
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

	public void DrawPartSelectionGlows(On.SolutionEditorBase.orig_method_1997 orig, SolutionEditorBase seb_self, Part part, Vector2 pos, float alpha)
	{
		if (part.method_1159() == Wheel.Ravari) Wheel.drawSelectionGlow(seb_self, part, pos, alpha);
		orig(seb_self, part, pos, alpha);
	}
}
