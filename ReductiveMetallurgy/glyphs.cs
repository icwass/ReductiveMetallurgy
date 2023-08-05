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
using PartTypes = class_191;
using Texture = class_256;

public static class Glyphs
{
	public static PartType Rejection, Deposition, Proliferation, ProliferationLeft, ProliferationRight;
	const string ProliferationPrevStateField = "ReductiveMetallurgy_ProliferationPrevState";
	const string ProliferationPrevCycleField = "ReductiveMetallurgy_ProliferationPrevCycle";

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
			/*Is a Glyph?*/field_1539 = true,
			/*Hex Footprint*/field_1540 = footprint,
			/*Icon*/field_1547 = icon,
			/*Hover Icon*/field_1548 = hover,
			/*Glow (Shadow)*/field_1549 = glow,
			/*Stroke (Outline)*/field_1550 = stroke,
			/*Permissions*/field_1551 = permissions,
			/*Only One Allowed?*/field_1552 = onlyOne,
		};
		return ret;
	}

	#region drawingHelpers
	private static Vector2 hexGraphicalOffset(HexIndex hex) => MainClass.hexGraphicalOffset(hex);
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
		Matrix4 matrixScreenPosition = Matrix4.method_1070(renderer.field_1797.ToVector3(0f));
		Matrix4 matrixTranslateOnScreen = Matrix4.method_1070(screenTranslation.ToVector3(0f));
		Matrix4 matrixRotatePart = Matrix4.method_1073(renderer.field_1798);
		Matrix4 matrixTranslateGraphic = Matrix4.method_1070(graphicTranslation.ToVector3(0f));
		Matrix4 matrixRotateGraphic = Matrix4.method_1073(graphicAngle);
		Matrix4 matrixPivotOffset = Matrix4.method_1070(-graphicPivot.ToVector3(0f));
		Matrix4 matrixScaling = Matrix4.method_1074(scaling.ToVector3(0f));
		Matrix4 matrixTextureSize = Matrix4.method_1074(tex.field_2056.ToVector3(0f));

		Matrix4 matrix4 = matrixScreenPosition * matrixTranslateOnScreen * matrixRotatePart * matrixTranslateGraphic * matrixRotateGraphic * matrixPivotOffset * matrixScaling * matrixTextureSize;
		class_135.method_262(tex, Color.White, matrix4);
	}

	private static void drawPartGraphicSpecular(class_195 renderer, Texture tex, Vector2 graphicPivot, float graphicAngle, Vector2 graphicTranslation, Vector2 screenTranslation)
	{
		float specularAngle = (renderer.field_1799 - (renderer.field_1797 + graphicTranslation.Rotated(renderer.field_1798))).Angle() - 1.570796f - renderer.field_1798;
		drawPartGraphic(renderer, tex, graphicPivot, graphicAngle + specularAngle, graphicTranslation, screenTranslation);
	}

	private static void drawPartGloss(class_195 renderer, Texture gloss, Texture glossMask, Vector2 offset)
	{
		drawPartGloss(renderer, gloss, glossMask, offset, new HexIndex(0, 0), 0f);
	}
	private static void drawPartGloss(class_195 renderer, Texture gloss, Texture glossMask, Vector2 offset, HexIndex hexOffset, float angle)
	{
		class_135.method_257().field_1692 = class_238.field_1995.field_1757; // MaskedGlossPS shader
		class_135.method_257().field_1693[1] = gloss;
		var hex = new HexIndex(0, 0);
		Vector2 method2001 = 0.0001f * (renderer.field_1797 + hexGraphicalOffset(hex).Rotated(renderer.field_1798) - 0.5f * class_115.field_1433);
		class_135.method_257().field_1695 = method2001;
		drawPartGraphic(renderer, glossMask, offset, angle, hexGraphicalOffset(hexOffset), Vector2.Zero);
		class_135.method_257().field_1692 = class_135.method_257().field_1696; // previous shader
		class_135.method_257().field_1693[1] = class_238.field_1989.field_71;
		class_135.method_257().field_1695 = Vector2.Zero;
	}

	private static void drawAtomIO(class_195 renderer, AtomType atomType, HexIndex hex, float num)
	{
		Molecule molecule = Molecule.method_1121(atomType);
		Vector2 method1999 = renderer.field_1797 + hexGraphicalOffset(hex).Rotated(renderer.field_1798);
		Editor.method_925(molecule, method1999, new HexIndex(0, 0), 0f, 1f, num, 1f, false, null);
	}
	#endregion


	private static bool ContentLoaded = false;
	public static void LoadContent()
	{
		if (ContentLoaded) return;
		ContentLoaded = true;

		//add rules for vanilla metals
		API.addRejectionRule(API.goldAtomType()		, API.silverAtomType());
		API.addRejectionRule(API.silverAtomType()	, API.copperAtomType());
		API.addRejectionRule(API.copperAtomType()	, API.ironAtomType());
		API.addRejectionRule(API.ironAtomType()		, API.tinAtomType());
		API.addRejectionRule(API.tinAtomType()		, API.leadAtomType());

		API.addDepositionRule(API.goldAtomType()	, new Pair<AtomType, AtomType>(API.ironAtomType()	, API.ironAtomType()));
		API.addDepositionRule(API.silverAtomType()	, new Pair<AtomType, AtomType>(API.ironAtomType()	, API.tinAtomType()));
		API.addDepositionRule(API.copperAtomType()	, new Pair<AtomType, AtomType>(API.tinAtomType()	, API.tinAtomType()));
		API.addDepositionRule(API.ironAtomType()	, new Pair<AtomType, AtomType>(API.tinAtomType()	, API.leadAtomType()));
		API.addDepositionRule(API.tinAtomType()		, new Pair<AtomType, AtomType>(API.leadAtomType()	, API.leadAtomType()));

		API.addProliferationRule(API.goldAtomType()		, new Pair<AtomType, AtomType>(API.goldAtomType()	, API.ironAtomType()));
		API.addProliferationRule(API.silverAtomType()	, new Pair<AtomType, AtomType>(API.silverAtomType()	, API.ironAtomType()));
		API.addProliferationRule(API.copperAtomType()	, new Pair<AtomType, AtomType>(API.copperAtomType()	, API.tinAtomType()));
		API.addProliferationRule(API.ironAtomType()		, new Pair<AtomType, AtomType>(API.ironAtomType()	, API.tinAtomType()));
		API.addProliferationRule(API.tinAtomType()		, new Pair<AtomType, AtomType>(API.tinAtomType()	, API.leadAtomType()));
		API.addProliferationRule(API.leadAtomType()		, new Pair<AtomType, AtomType>(API.leadAtomType()	, API.leadAtomType()));

		string path, iconpath, selectpath;
		path = "reductiveMetallurgy/textures/";
		iconpath = path + "parts/icons/";
		selectpath = path + "select/";

		Rejection = makeGlyph(
			"glyph-rejection",
			"Glyph of Rejection",
			"The glyph of rejection extracts quicksilver to demote an atom of metal to a lower form.",
			20, new HexIndex[2] { new HexIndex(0, 0), new HexIndex(1, 0) }, API.perm_rejection,
			class_235.method_615(iconpath + "rejection"),
			class_235.method_615(iconpath + "rejection_hover"),
			class_238.field_1989.field_97.field_374,// double_glow
			class_238.field_1989.field_97.field_375 // double_stroke
		);

		Deposition = makeGlyph(
			"glyph-deposition",
			"Glyph of Deposition",
			"The glyph of deposition can separate an atom of metal into two atoms of lower form.",
			20, new HexIndex[3] { new HexIndex(0, 0), new HexIndex(1, 0), new HexIndex(-1, 0) }, API.perm_deposition,
			class_235.method_615(iconpath + "deposition"),
			class_235.method_615(iconpath + "deposition_hover"),
			class_235.method_615(selectpath + "line_glow"),
			class_235.method_615(selectpath + "line_stroke")
		);

		ProliferationLeft = makeGlyph(
			"glyph-proliferation-left",
			"Glyph of Proliferation",
			"The glyph of proliferation consumes quicksilver to proliferate a metal atom from another.",
			40, new HexIndex[3] { new HexIndex(0, 0), new HexIndex(1, 0), new HexIndex(0, 1) }, API.perm_proliferation,
			class_235.method_615(iconpath + "proliferation"),
			class_235.method_615(iconpath + "proliferation_hover"),
			class_238.field_1989.field_97.field_386,// triple_glow
			class_238.field_1989.field_97.field_387, // triple_stroke
			true // only one!
		);
		ProliferationRight = makeGlyph(
			"glyph-proliferation-right",
			"Glyph of Proliferation",
			"The glyph of proliferation consumes quicksilver to proliferate a metal atom from another.",
			40, new HexIndex[3] { new HexIndex(0, 0), new HexIndex(1, 0), new HexIndex(0, 1) }, API.perm_proliferation,
			class_235.method_615(iconpath + "proliferation"),
			class_235.method_615(iconpath + "proliferation_hover"),
			class_238.field_1989.field_97.field_386,// triple_glow
			class_238.field_1989.field_97.field_387, // triple_stroke
			true // only one!
		);
		Proliferation = makeGlyph(
			"glyph-proliferation-ambi",
			"Glyph of Proliferation",
			"The glyph of proliferation consumes quicksilver to proliferate a metal atom from another.",
			40, new HexIndex[3] { new HexIndex(0, 0), new HexIndex(1, 0), new HexIndex(0, 1) }, API.perm_proliferation,
			class_235.method_615(iconpath + "proliferation"),
			class_235.method_615(iconpath + "proliferation_hover"),
			class_238.field_1989.field_97.field_386,// triple_glow
			class_238.field_1989.field_97.field_387, // triple_stroke
			true // only one!
		);

		var projector = PartTypes.field_1778;
		var purifier = PartTypes.field_1779;
		QApi.AddPartTypeToPanel(Rejection, projector);
		QApi.AddPartTypeToPanel(Deposition, purifier);
		QApi.AddPartTypeToPanel(Proliferation, purifier);
		//QApi.AddPartTypeToPanel(ProliferationLeft, purifier);
		//QApi.AddPartTypeToPanel(ProliferationRight, purifier);

		path = "reductiveMetallurgy/textures/parts/";
		Texture leadSymbolBowlDown = class_235.method_615(path + "lead_symbol_bowl_down");
		Texture rejection_metalBowlTarget = class_235.method_615(path + "rejection_metal_bowl_target");
		Texture rejection_quicksilverSymbol = class_235.method_615(path + "rejection_quicksilver_symbol");
		Texture leadSymbolInputDown = class_235.method_615(path + "lead_symbol_input_down");

		path = "reductiveMetallurgy/textures/parts/deposition/";
		Texture deposition_base = class_235.method_615(path + "base");
		Texture deposition_connectors = class_235.method_615(path + "connectors");
		Texture deposition_gloss = class_235.method_615(path + "gloss");
		Texture deposition_glossMask = class_235.method_615(path + "gloss_mask");

		path = "reductiveMetallurgy/textures/parts/proliferation/";
		Texture proliferationGlyph_symbols = class_235.method_615(path + "proliferator_symbols");

		// fetch vanilla textures
		Texture bonderShadow = class_238.field_1989.field_90.field_164;

		Texture calcinatorGlyph_bowl = class_238.field_1989.field_90.field_170;

		//Texture animismus_base = class_238.field_1989.field_90.field_228.field_265;
		//Texture animismus_connectors = class_238.field_1989.field_90.field_228.field_266;
		//Texture animismus_connectorsShadows = class_238.field_1989.field_90.field_228.field_267;
		//Texture animismus_gloss = class_238.field_1989.field_90.field_228.field_268;
		//Texture animismus_glossMask = class_238.field_1989.field_90.field_228.field_269;
		//Texture animismus_input = class_238.field_1989.field_90.field_228.field_270;
		Texture animismus_outputAboveIris = class_238.field_1989.field_90.field_228.field_271;
		Texture animismus_outputUnderIris = class_238.field_1989.field_90.field_228.field_272;
		Texture animismus_ringShadow = class_238.field_1989.field_90.field_228.field_273;

		Texture projectionGlyph_base = class_238.field_1989.field_90.field_255.field_288;
		Texture projectionGlyph_bond = class_238.field_1989.field_90.field_255.field_289;
		Texture projectionGlyph_glossMask = class_238.field_1989.field_90.field_255.field_290;
		Texture projectionGlyph_leadSymbol = class_238.field_1989.field_90.field_255.field_291;
		Texture projectionGlyph_metalBowl = class_238.field_1989.field_90.field_255.field_292;
		Texture projectionGlyph_quicksilverInput = class_238.field_1989.field_90.field_255.field_293;
		Texture projectionGlyph_quicksilverSymbol = class_238.field_1989.field_90.field_255.field_294;

		Texture purificationGlyph_base = class_238.field_1989.field_90.field_257.field_359;
		Texture purificationGlyph_connectors = class_238.field_1989.field_90.field_257.field_360;
		Texture purificationGlyph_gloss = class_238.field_1989.field_90.field_257.field_361;
		Texture purificationGlyph_glossMask = class_238.field_1989.field_90.field_257.field_362;
		//Texture purificationGlyph_leadSymbol = class_238.field_1989.field_90.field_257.field_363;

		Texture[] irisFullArray = class_238.field_1989.field_90.field_246;

		QApi.AddPartType(Rejection, (part, pos, editor, renderer) =>
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

		QApi.AddPartType(Deposition, (part, pos, editor, renderer) =>
		{
			PartSimState partSimState = editor.method_507().method_481(part);
			var simTime = editor.method_504();

			var originHex = new HexIndex(0, 0);
			var inputHex = originHex;
			var leftHex = new HexIndex(-1, 0);
			var rightHex = new HexIndex(1, 0);

			float partAngle = renderer.field_1798;
			Vector2 base_offset = new Vector2(123f, 48f);

			int index = irisFullArray.Length - 1;
			float num = 0f;
			bool flag = false;
			if (partSimState.field_2743)
			{
				index = class_162.method_404((int)(class_162.method_411(1f, -1f, simTime) * irisFullArray.Length), 0, irisFullArray.Length - 1);
				num = simTime;
				flag = (double)simTime > 0.5;
			}

			drawPartGraphic(renderer, deposition_base, base_offset, 0f, Vector2.Zero, new Vector2(-1f, -1f));
			drawPartGraphic(renderer, bonderShadow, textureCenter(bonderShadow), 0f, hexGraphicalOffset(inputHex), new Vector2(0f, -3f));
			foreach (var hex in new HexIndex[2] { leftHex, rightHex })
			{
				var i = hex == leftHex ? 0 : 1;
				drawPartGraphic(renderer, bonderShadow, textureCenter(bonderShadow), 0f, hexGraphicalOffset(hex), new Vector2(0f, -3f));
				drawPartGraphicSpecular(renderer, animismus_outputUnderIris, textureCenter(animismus_outputUnderIris), 0f, hexGraphicalOffset(hex), Vector2.Zero);
				if (partSimState.field_2743 && !flag)
				{
					drawAtomIO(renderer, partSimState.field_2744[i], hex, num);
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
					drawAtomIO(renderer, partSimState.field_2744[i], hex, num);
				}
			}
			drawPartGraphicSpecular(renderer, projectionGlyph_quicksilverInput, textureCenter(projectionGlyph_quicksilverInput), 0f, hexGraphicalOffset(inputHex), Vector2.Zero);
			drawPartGraphic(renderer, leadSymbolInputDown, textureCenter(leadSymbolInputDown), -partAngle, hexGraphicalOffset(inputHex), Vector2.Zero);
			drawPartGraphic(renderer, deposition_connectors, base_offset, 0f, Vector2.Zero, Vector2.Zero);
			drawPartGloss(renderer, deposition_gloss, deposition_glossMask, base_offset + new Vector2(0f, -1f));
		});

		void DrawProliferationChiral(Part part, Vector2 pos, SolutionEditorBase editor, class_195 renderer, bool lefty)
		{
			var interface2 = editor.method_507();
			PartSimState partSimState = interface2.method_481(part);
			var simTime = editor.method_504();

			var leftHex = new HexIndex(0, 0);
			var rightHex = new HexIndex(1, 0);
			var selectHex = new HexIndex(0, 1);

			var inputHex = lefty ? leftHex : rightHex;
			var outputHex = lefty ? rightHex : leftHex;
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

			var angle = (lefty ? -120f : 120f) * (float)Math.PI / 180f;
			var hexOffset = lefty ? new HexIndex(0, 1) : new HexIndex(1, 0);
			drawPartGraphic(renderer, purificationGlyph_base, base_offset, angle, hexGraphicalOffset(hexOffset), new Vector2(-1f, -1f));

			drawPartGraphic(renderer, animismus_ringShadow, textureCenter(animismus_ringShadow), 0f, hexGraphicalOffset(selectHex), new Vector2(0f, -3f));
			drawPartGraphicSpecular(renderer, calcinatorGlyph_bowl, textureCenter(calcinatorGlyph_bowl), 0f, hexGraphicalOffset(selectHex), Vector2.Zero);
			drawPartGraphic(renderer, proliferationGlyph_symbols, base_offset, -renderer.field_1798, hexGraphicalOffset(selectHex), Vector2.Zero);

			drawPartGraphic(renderer, bonderShadow, textureCenter(bonderShadow), 0f, hexGraphicalOffset(inputHex), new Vector2(0f, -3f));
			drawPartGraphic(renderer, bonderShadow, textureCenter(bonderShadow), 0f, hexGraphicalOffset(outputHex), new Vector2(0f, -3f));

			drawPartGraphicSpecular(renderer, projectionGlyph_quicksilverInput, textureCenter(projectionGlyph_quicksilverInput), 0f, hexGraphicalOffset(inputHex), Vector2.Zero);
			drawPartGraphic(renderer, projectionGlyph_quicksilverSymbol, textureCenter(projectionGlyph_quicksilverSymbol), -renderer.field_1798, hexGraphicalOffset(inputHex), Vector2.Zero);
			
			drawPartGraphicSpecular(renderer, animismus_outputUnderIris, textureCenter(animismus_outputUnderIris), 0f, hexGraphicalOffset(outputHex), Vector2.Zero);

			if (partSimState.field_2743)
			{
				if (!flag) drawAtomIO(renderer, partSimState.field_2744[0], outputHex, num);
			}
			drawPartGraphicSpecular(renderer, irisFullArray[index], textureCenter(irisFullArray[index]), 0f, hexGraphicalOffset(outputHex), Vector2.Zero);
			drawPartGraphicSpecular(renderer, animismus_outputAboveIris, textureCenter(animismus_outputAboveIris), 0f, hexGraphicalOffset(outputHex), Vector2.Zero);

			drawPartGraphic(renderer, purificationGlyph_connectors, base_offset, angle, hexGraphicalOffset(hexOffset), Vector2.Zero);
			drawPartGloss(renderer, purificationGlyph_gloss, purificationGlyph_glossMask, base_offset + new Vector2(0f, -1f), hexOffset, angle);
			if (flag) drawAtomIO(renderer, partSimState.field_2744[0], outputHex, num);
		}

		QApi.AddPartType(ProliferationLeft, (part, pos, editor, renderer) =>
		{
			DrawProliferationChiral(part, pos, editor, renderer, true);
		});
		QApi.AddPartType(ProliferationRight, (part, pos, editor, renderer) =>
		{
			DrawProliferationChiral(part, pos, editor, renderer, false);
		});
		QApi.AddPartType(Proliferation, (part, pos, editor, renderer) =>
		{
			var interface2 = editor.method_507();
			PartSimState partSimState = interface2.method_481(part);
			var simTime = editor.method_504();

			var leftHex = new HexIndex(0, 0);
			var rightHex = new HexIndex(1, 0);
			var selectHex = new HexIndex(0, 1);


			var currentCycle = 0;
			if (editor.method_503() != enum_128.Stopped && editor.GetType() == typeof(SolutionEditorScreen))
			{
				var maybeSim = new DynamicData(editor).Get<Maybe<Sim>>("field_4022");
				if (maybeSim.method_1085())
				{
					currentCycle = maybeSim.method_1087().method_1818();
				}
			}
			var state_dyn = new DynamicData(partSimState);
			var prevStateOb = state_dyn.Get(ProliferationPrevStateField);
			var prevCycleOb = state_dyn.Get(ProliferationPrevCycleField);

			int[] prevState = new int[4] {1, 1, 1, 1};
			int prevCycle = 0;
			if (prevStateOb != null)
			{
				prevState = (int[]) prevStateOb;
			}
			if (prevCycleOb != null)
			{
				prevCycle = (int)prevCycleOb;
			}

			bool[] quicksilverAbove = new bool[2] { false, false };
			foreach (var hex in new HexIndex[2] { leftHex, rightHex })
			{
				Atom atom;
				HexIndex key = part.method_1184(hex);
				foreach (Molecule molecule in interface2.method_483().Where(x => x.method_1100().Count == 1)) // foreach one-atom molecule
				{
					if (molecule.method_1100().TryGetValue(key, out atom) && atom.field_2275 == API.quicksilverAtomType())
					{
						quicksilverAbove[hex == leftHex ? 0 : 1] = true;
						break;
					}
				}
			}

			int currentLeftState = 1;
			int currentRightState = 1;
			if (quicksilverAbove[0] ^ quicksilverAbove[1])
			{
				currentLeftState = quicksilverAbove[0] ? 0 : 2;
				currentRightState = quicksilverAbove[1] ? 0 : 2;
			}

			if (currentCycle > prevCycle || simTime > 0.5)
			{
				prevState[0] = prevState[2];
				prevState[1] = prevState[3];
				state_dyn.Set(ProliferationPrevStateField, prevState);
				state_dyn.Set(ProliferationPrevCycleField, currentCycle);
			}
			prevState[2] = currentLeftState;
			prevState[3] = currentRightState;

			bool lefty = quicksilverAbove[0] && !quicksilverAbove[1];
			var inputHex = lefty ? leftHex : rightHex;
			var outputHex = lefty ? rightHex : leftHex;

			int[] index = new int[2] { irisFullArray.Length - 1, irisFullArray.Length - 1 };
			index[lefty ? 0 : 1] = 0;

			float num = 0f;
			bool flag = false;
			int ioIndex = 0;
			if (partSimState.field_2743)
			{
				ioIndex = partSimState.field_2744[0] == API.quicksilverAtomType() ? 1 : 0;
				index[ioIndex] = class_162.method_404((int)(class_162.method_411(1f, -1f, simTime) * irisFullArray.Length), 0, irisFullArray.Length - 1);
				num = simTime;
				outputHex = ioIndex == 1 ? rightHex : leftHex;
				flag = (double)simTime > 0.5;
			}
			else
			{
				index[0] = class_162.method_404((int)(class_162.method_411(prevState[0]/ 2f, currentLeftState - prevState[0] / 2f, simTime) * irisFullArray.Length), 0, irisFullArray.Length - 1);
				index[1] = class_162.method_404((int)(class_162.method_411(prevState[1]/ 2f, currentRightState - prevState[1] / 2f, simTime) * irisFullArray.Length), 0, irisFullArray.Length - 1);
			}

			Vector2 base_offset = new Vector2(41f, 48f);
			drawPartGraphic(renderer, purificationGlyph_base, base_offset, 0f, Vector2.Zero, new Vector2(-1f, -1f));

			drawPartGraphic(renderer, animismus_ringShadow, textureCenter(animismus_ringShadow), 0f, hexGraphicalOffset(selectHex), new Vector2(0f, -3f));
			drawPartGraphicSpecular(renderer, calcinatorGlyph_bowl, textureCenter(calcinatorGlyph_bowl), 0f, hexGraphicalOffset(selectHex), Vector2.Zero);
			drawPartGraphic(renderer, proliferationGlyph_symbols, base_offset, -renderer.field_1798, hexGraphicalOffset(selectHex), Vector2.Zero);

			drawPartGraphic(renderer, bonderShadow, textureCenter(bonderShadow), 0f, hexGraphicalOffset(inputHex), new Vector2(0f, -3f));
			drawPartGraphic(renderer, bonderShadow, textureCenter(bonderShadow), 0f, hexGraphicalOffset(outputHex), new Vector2(0f, -3f));

			foreach (var hex in new HexIndex[2] { leftHex, rightHex })
			{
				drawPartGraphicSpecular(renderer, projectionGlyph_quicksilverInput, textureCenter(projectionGlyph_quicksilverInput), 0f, hexGraphicalOffset(hex), Vector2.Zero);
				drawPartGraphicSpecular(renderer, animismus_outputUnderIris, textureCenter(animismus_outputUnderIris), 0f, hexGraphicalOffset(hex), Vector2.Zero);
				drawPartGraphic(renderer, projectionGlyph_quicksilverSymbol, textureCenter(projectionGlyph_quicksilverSymbol), -renderer.field_1798, hexGraphicalOffset(hex), Vector2.Zero);
			}



			if (partSimState.field_2743)
			{
				if (!flag) drawAtomIO(renderer, partSimState.field_2744[ioIndex], outputHex, num);
			}

			foreach (var hex in new HexIndex[2] { leftHex, rightHex })
			{
				var thisIndex = index[hex == leftHex ? 0 : 1];
				drawPartGraphicSpecular(renderer, irisFullArray[thisIndex], textureCenter(irisFullArray[thisIndex]), 0f, hexGraphicalOffset(hex), Vector2.Zero);
				drawPartGraphicSpecular(renderer, animismus_outputAboveIris, textureCenter(animismus_outputAboveIris), 0f, hexGraphicalOffset(hex), Vector2.Zero);
			}


			drawPartGraphic(renderer, purificationGlyph_connectors, base_offset, 0f, Vector2.Zero, Vector2.Zero);
			drawPartGloss(renderer, purificationGlyph_gloss, purificationGlyph_glossMask, base_offset + new Vector2(0f, -1f));
			if (flag) drawAtomIO(renderer, partSimState.field_2744[ioIndex], outputHex, num);
		});


	}

	public static void LoadMirrorRules()
	{
		FTSIGCTU.MirrorTool.addRule(Rejection, FTSIGCTU.MirrorTool.mirrorHorizontalPart0_0);
		FTSIGCTU.MirrorTool.addRule(Deposition, FTSIGCTU.MirrorTool.mirrorHorizontalPart0_0);
		FTSIGCTU.MirrorTool.addRule(Proliferation, FTSIGCTU.MirrorTool.mirrorVerticalPart0_5);
		//FTSIGCTU.MirrorTool.addRule(ProliferationLeft, FTSIGCTU.MirrorTool.mirrorVerticalPart0_5);
		//FTSIGCTU.MirrorTool.addRule(ProliferationRight, FTSIGCTU.MirrorTool.mirrorVerticalPart0_5);
	}
}