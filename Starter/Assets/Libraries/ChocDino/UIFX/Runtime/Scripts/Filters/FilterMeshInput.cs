//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

#if UIFX_BETA

#if UNITY_2023_2_OR_NEWER || UNITY_2022_2_20 || UNITY_2022_2_21 || (UNITY_2022_3_OR_NEWER && !UNITY_2023_1_OR_NEWER)
#define UNITY_CANVASRENDERER_GETMESH
#endif

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using UnityInternal = UnityEngine.Internal;

namespace ChocDino.UIFX
{
	/// <summary>
	/// At IMeshModifier::ModifyMesh() grab the current CanvasRenderer mesh and output it via IMeshModifier.
	/// This is useful when you have a Graphic component that uses CanvasRenderer::SetMesh() directly, so this allows
	/// the mesh to processed by components that implement IMeshModifier.
	/// </summary>
	[ExecuteAlways]
	[RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
	[HelpURL("https://www.chocdino.com/products/unity-assets/")]
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Filters/UIFX - Filter Mesh Input (beta)", 1000)]
	public class FilterMeshInput : UIBehaviour, IMeshModifier
	{
		private Graphic _graphic;
		internal Graphic GraphicComponent { get { if (!_graphic) { _graphic = GetComponent<Graphic>(); } return _graphic; } }

		private CanvasRenderer _canvasRenderer;
		private CanvasRenderer CanvasRenderComponent { get { if (!_canvasRenderer) { if (GraphicComponent) { _canvasRenderer = _graphic.canvasRenderer; } else { _canvasRenderer = GetComponent<CanvasRenderer>(); } } return _canvasRenderer; } }

		protected override void OnEnable()
		{
			#if !UNITY_CANVASRENDERER_GETMESH
			Debug.LogError("This component is only supported in Unity 2023.2 and above, or 2022.3. Disabling component.");
			this.enabled = false;
			#else
			Canvas.willRenderCanvases += WillRenderCanvases;
			#endif
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			Canvas.willRenderCanvases -= WillRenderCanvases;
			base.OnDisable();
		}

		void WillRenderCanvases()
		{
			// In come cases we need to force IMeshModifier::ModifyMesh to run again to get the correct vertices.
			GraphicComponent.SetVerticesDirty();
		}

		void LateUpdate()
		{
			//GraphicComponent.SetVerticesDirty();
		}

		/// <summary>
		/// Note that ModifyMesh() is called BEFORE GetModifiedMaterial()
		/// </summary>
		[UnityInternal.ExcludeFromDocs]
		public void ModifyMesh(VertexHelper verts)
		{
			if (!this.isActiveAndEnabled) return;
		}

		[UnityInternal.ExcludeFromDocs]
		[System.Obsolete("use IMeshModifier.ModifyMesh (VertexHelper verts) instead, or set useLegacyMeshGeneration to false", false)]
		public void ModifyMesh(Mesh mesh)
		{
			if (!this.isActiveAndEnabled) return;
			
			Debug.Assert(mesh != null);
			#if UNITY_CANVASRENDERER_GETMESH
			Mesh m = CanvasRenderComponent.GetMesh();
			if (m)
			{
				mesh.Clear();
				mesh.SetVertices(m.vertices);
				mesh.SetColors(m.colors);
				mesh.SetUVs(0, m.uv);
				mesh.SetTriangles(m.GetIndices(0), 0);
				mesh.RecalculateBounds();
			}
			#endif
		}
	}
}

#endif