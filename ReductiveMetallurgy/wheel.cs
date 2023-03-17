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
using AtomTypes = class_175;
using PartTypes = class_191;
using Texture = class_256;

public static class Wheel
{
	public static PartType Ravari;

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
			float num = (float)(index * 60) * ((float)Math.PI / 180f);
			Method_2016.Invoke(seb_self, new object[] { cageGlow, color, class236.field_1984, class236.field_1985 + num });
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
		//APIs
		public Dictionary<HexIndex, AtomType> getWheel()
		{
			var metals = new AtomType[6] { leadAtomType(), tinAtomType(), ironAtomType(), copperAtomType(), silverAtomType(), goldAtomType() };
			return new Dictionary<HexIndex, AtomType>()
			{
				{new HexIndex(1, 0),  metals[wheel[0]]},
				{new HexIndex(0, 1),  metals[wheel[1]]},
				{new HexIndex(-1, 1), metals[wheel[2]]},
				{new HexIndex(-1, 0), metals[wheel[3]]},
				{new HexIndex(0, -1), metals[wheel[4]]},
				{new HexIndex(1, -1), metals[wheel[5]]},
			};
		}

		public bool tryProjection(HexRotation rot, bool run = true)
		{
			HexRotation netRot = rot - partSimState.field_2726;
			var index = (netRot.GetNumberOfTurns() % 6 + 6) % 6;
			bool flag = wheel[index] < 5;
			if (flag && run) wheel[index]++;
			savePackedWheel();
			return flag;
		}
		public bool tryRejection(HexRotation rot, bool run = true)
		{
			HexRotation netRot = rot - partSimState.field_2726;
			var index = (netRot.GetNumberOfTurns() % 6 + 6) % 6;
			bool flag = wheel[index] > 0;
			if (flag && run) wheel[index]--;
			savePackedWheel();
			return flag;
		}

		// data
		private int[] wheel;
		private PartSimState partSimState;

		const int shift = 4;
		const int header = 0x01 << 6*shift;
		const int mask  = 0xFFFFFF;

		//constructor
		public MetalWheel(PartSimState _partSimState)
		{
			partSimState = _partSimState;
			int packedWheel = partSimState.field_2730;
			wheel = new int[6] { 1, 0, 5, 4, 3, 2 }; // starting configuration
			if ((packedWheel & 0xFF000000) == header)
			{
				packedWheel = packedWheel & mask;
				for (int i = 0; i < 6; i++)
				{
					wheel[5-i] = Math.Min(packedWheel & 0xF, 5);
					packedWheel = packedWheel >> shift;
				}
			}
			savePackedWheel();
		}

		// methods
		private void savePackedWheel()
		{
			int packedWheel = 0;
			for (int i = 0; i < 6; i++)
			{
				packedWheel = packedWheel << shift;
				packedWheel += wheel[i];
			}
			partSimState.field_2730 = packedWheel + header;
		}
	}



	//private static Vector2 hexGraphicalOffset(HexIndex hex) => MainClass.hexGraphicalOffset(hex);



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
			/*Permissions*/field_1551 = Permissions.None,
			/*Only One Allowed?*/field_1552 = true,
		};

		var berlo = PartTypes.field_1771;
		QApi.AddPartTypeToPanel(Ravari, berlo);

		// fetch vanilla textures
		var atomCageLighting = class_238.field_1989.field_90.field_232;

		QApi.AddPartType(Ravari, (part, pos, editor, renderer) =>
		{
			class_236 class236 = editor.method_1989(part, pos);

			PartSimState partSimState = editor.method_507().method_481(part);

			var sebType = typeof(SolutionEditorBase);
			MethodInfo Method_2003 = sebType.GetMethod("method_2003", BindingFlags.NonPublic | BindingFlags.Static);
			MethodInfo Method_2005 = sebType.GetMethod("method_2005", BindingFlags.NonPublic | BindingFlags.Static);

			var hexArmRotations = PartTypes.field_1767.field_1534;

			//draw arm stubs
			Method_2005.Invoke(editor, new object[] { part.method_1165(), hexArmRotations, class236 });

			//draw atoms
			MetalWheel metalWheel = new MetalWheel(partSimState);

			foreach (var kvp in metalWheel.getWheel())
			{
				Vector2 vector2 = renderer.field_1797 + class_187.field_1742.method_492(kvp.Key).Rotated(renderer.field_1798);
				float num = (Editor.method_922() - vector2).Angle();
				Editor.method_927(kvp.Value, vector2, 1f, 1f, 1f, 1f, -21f, num - 1.570796f, null, null, false);
			}

			//draw cages
			for (int index = 0; index < 6; ++index)
			{
				float num4 = index * 60 * (float)Math.PI / 180f;
				float radians = renderer.field_1798 + num4;
				Vector2 vector2_9 = renderer.field_1797 + class_187.field_1742.method_492(new HexIndex(1, 0)).Rotated(radians);
				Method_2003.Invoke(editor, new object[] { atomCageLighting, vector2_9, new Vector2(39f, 33f), radians });
			}
		});








	}


	private static bool mirrorRulesLoaded = false;
	public static void LoadMirrorRules()
	{
		if (mirrorRulesLoaded) return;
		mirrorRulesLoaded = true;

		FTSIGCTU.MirrorTool.addRule(Ravari, FTSIGCTU.MirrorTool.mirrorVanBerlo);
	}





}