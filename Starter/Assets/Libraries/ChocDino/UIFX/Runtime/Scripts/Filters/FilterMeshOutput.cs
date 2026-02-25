//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

#if UIFX_BETA

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using UnityInternal = UnityEngine.Internal;

namespace ChocDino.UIFX
{
	/// <summary>
	/// This component takes the mesh at IMeshModifier::ModifyMesh() and applies it via CanvasRenderer::SetMesh().
	/// This is useful when used with the FilterMeshInput component and the IMeshModifier mesh needs to be assigned
	/// to override the existing CanvasRenderer mesh.
	/// </summary>
	[ExecuteAlways]
	[RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
	[HelpURL("https://www.chocdino.com/products/unity-assets/")]
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Filters/UIFX - Filter Mesh Output (beta)", 1000)]
	public class FilterMeshOutput : UIBehaviour, IMeshModifier
	{
		private Mesh _quadMesh;

		private Graphic _graphic;
		internal Graphic GraphicComponent { get { if (!_graphic) { _graphic = GetComponent<Graphic>(); } return _graphic; } }

		private CanvasRenderer _canvasRenderer;
		private CanvasRenderer CanvasRenderComponent { get { if (!_canvasRenderer) { if (GraphicComponent) { _canvasRenderer = _graphic.canvasRenderer; } else { _canvasRenderer = GetComponent<CanvasRenderer>(); } } return _canvasRenderer; } }

		protected override void OnEnable()
		{
			Canvas.willRenderCanvases += WillRenderCanvases;
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			Canvas.willRenderCanvases -= WillRenderCanvases;
			base.OnDisable();
		}

		protected override void OnDestroy()
		{
			ObjectHelper.Destroy(ref _quadMesh);
			base.OnDestroy();
		}

		void WillRenderCanvases()
		{
			if (_quadMesh)
			{
				CanvasRenderComponent.SetMesh(_quadMesh);
			}
		}

		/// <summary>
		/// Note that ModifyMesh() is called BEFORE GetModifiedMaterial()
		/// </summary>
		[UnityInternal.ExcludeFromDocs]
		public void ModifyMesh(VertexHelper verts)
		{
			if (!this.isActiveAndEnabled) return;
			Debug.LogWarning("Shouldn't get here...");
		}

		[UnityInternal.ExcludeFromDocs]
		[System.Obsolete("use IMeshModifier.ModifyMesh (VertexHelper verts) instead, or set useLegacyMeshGeneration to false", false)]
		public void ModifyMesh(Mesh mesh)
		{
			if (!this.isActiveAndEnabled) return;

			if (mesh)
			{
				if (_quadMesh == null)
				{
					_quadMesh = new Mesh();
				}
				_quadMesh.Clear();
				_quadMesh.SetVertices(mesh.vertices);
				_quadMesh.SetColors(mesh.colors);
				_quadMesh.SetUVs(0, mesh.uv);
				_quadMesh.SetTriangles(mesh.GetIndices(0), 0);
				_quadMesh.RecalculateBounds();
			}
		}
	}
}

#endif