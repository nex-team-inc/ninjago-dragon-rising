//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

#if UIFX_TMPRO

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityInternal = UnityEngine.Internal;
using TMPro;

namespace ChocDino.UIFX
{
	/// <summary>
	/// Apply an affine skew transform to the vertex positions of a UGUI TextMeshPro component
	/// </summary>
	[ExecuteInEditMode]
	[RequireComponent(typeof(TextMeshProUGUI)), DisallowMultipleComponent]
	[HelpURL("https://www.chocdino.com/products/unity-assets/")]
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Effects/UIFX - Vertex Skew (TextMeshPro)", 200)]
	public class VertexSkewTextMeshPro : UIBehaviour
	{
		//[SerializeField] 
		SkewPivotBounds _pivotBounds = SkewPivotBounds.Mesh;
		[SerializeField] TextAnchor _pivot = TextAnchor.MiddleCenter;
		[SerializeField] SkewDirection _direction = SkewDirection.Vertical;

		[Range(-90f, 90f)]
		[SerializeField] float _angle = 16f;
		[SerializeField] float _offset = 0f;

		[Range(0f, 1f)]
		[SerializeField] float _strength = 1f;

		public SkewPivotBounds PivotBounds { get { return _pivotBounds; } set { if (value != _pivotBounds) { _pivotBounds = value; ForceVerticesUpdate(); } } }
		public float Angle { get { return _angle; } set { value = Mathf.Clamp(value, -90f, 90f); if (value != _angle) { _angle = value; ForceVerticesUpdate(); } } }
		public float Offset { get { return _offset; } set { if (value != _offset) { _offset = value; ForceVerticesUpdate(); } } }
		public SkewDirection Direction { get { return _direction; } set { if (value != _direction) { _direction = value; ForceVerticesUpdate(); } } }
		public TextAnchor Pivot { get { return _pivot; } set { if (value != _pivot) { _pivot = value; ForceVerticesUpdate(); } } }
		public float Strength { get { return _strength; } set { value = Mathf.Clamp01(value); if (value != _strength) { _strength = value; ForceVerticesUpdate(); } } }

		//public Vector3 PivotPoint { get { return _pivotPoint; } }
		//public Vector3 BoundsMin { get { return _boundsMin; } }
		//public Vector3 BoundsMax { get { return _boundsMax; } }

		private Graphic _graphic;
		private Graphic GraphicComponent { get { if (_graphic == null) _graphic = GetComponent<Graphic>(); return _graphic; } }

		private Matrix4x4 _matrix;
		private Vector3 _boundsMin, _boundsMax;
		private Vector3 _pivotPoint;

		private TextMeshProUGUI _textMeshPro;
		private List<TMP_SubMeshUI> _subMeshes = new List<TMP_SubMeshUI>(8);
		private static List<TMP_SubMeshUI> _subMeshTemp = new List<TMP_SubMeshUI>(8);

		[UnityInternal.ExcludeFromDocs]
		protected override void Awake()
		{
			_textMeshPro = GetComponent<TextMeshProUGUI>();
			Debug.Assert(_textMeshPro != null);
			base.Awake();
		}

		#if UNITY_EDITOR
		protected override void Reset()
		{
			ForceVerticesUpdate();
			base.Reset();
		}
		protected override void OnValidate()
		{
			ForceVerticesUpdate();
			base.OnValidate();
		}
		#endif

		[UnityInternal.ExcludeFromDocs]
		protected override void OnEnable()
		{
			ForceVerticesUpdate();

			TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextGeomeryRebuilt);

			ForceVerticesUpdate();

			base.OnEnable();
		}

		[UnityInternal.ExcludeFromDocs]
		protected override void OnDisable()
		{
			ForceVerticesUpdate();

			TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextGeomeryRebuilt);

			ForceVerticesUpdate();

			base.OnDisable();
		}

		protected override void OnDidApplyAnimationProperties()
		{
			ForceVerticesUpdate();
			base.OnDidApplyAnimationProperties();
		}

		private void ForceVerticesUpdate()
		{
			// This forces TMP to re-render
			if (_textMeshPro != null)
			{
				_textMeshPro.SetAllDirty();
			}
		}

		private void BuildMatrix()
		{
			_matrix = Matrix4x4.identity;

			float angle = Mathf.Tan(Mathf.Deg2Rad * _angle * _strength);
			Vector2 offset = Vector2.zero;
			if (_direction == SkewDirection.Horizontal)
			{
				_matrix[0, 1] = angle;
				offset.y = _offset;
			}
			else
			{
				_matrix[1, 0] = angle;
				offset.x = _offset;
			}

			_pivotPoint = GetAnchorPositionForBounds(_pivot, _boundsMin, _boundsMax);

			Matrix4x4 t = Matrix4x4.Translate(_pivotPoint);
			Matrix4x4 it = Matrix4x4.Translate(-_pivotPoint);

			_matrix = t * _matrix * it * Matrix4x4.Translate(offset * _strength);
		}


		private TMP_MeshInfo[] _cachedMeshInfo;

		private void UpdateMesh()
		{
			if (!this.isActiveAndEnabled) return;
			if (_strength <= 0f) return;
			if (_textMeshPro == null) return;
			if (_textMeshPro.textInfo == null) return;

			// Cache the vertex data of the text object as the Jitter FX is applied to the original position of the characters.
			_cachedMeshInfo = _textMeshPro.textInfo.CopyMeshInfoVertexData();

			int materialCount = _textMeshPro.textInfo.materialCount;
			if (materialCount > 1)
			{
				GatherSubMeshes();
			}

			if (_pivotBounds == SkewPivotBounds.Mesh)
			{
				GetBounds(_textMeshPro.textInfo, out _boundsMin, out _boundsMax);
				BuildMatrix();
				ApplyMatrix(_textMeshPro.textInfo);
			}
		}

		private void OnTextGeomeryRebuilt(Object obj)
		{
			if (obj == _textMeshPro)
			{
				//ApplyMeshModifiers();

				// For some reason we need to set things dirty here so that future changes to the filters will cause rerendering
				//GraphicComponent.SetAllDirty();

				UpdateMesh();
			}
		}

		private void ApplyMatrix(TMP_TextInfo textInfo)
		{
			int materialCount = textInfo.materialCount;
			for (int i = 0; i < materialCount; i++)
			{
				var meshInfo = textInfo.meshInfo[i];
				if (meshInfo.vertexCount > 0)
				{
					for (int j = 0; j < meshInfo.vertexCount; j++)
					{
						Vector3 v = meshInfo.vertices[j];
						Vector3 v2 = _matrix.MultiplyPoint3x4(v);
						_cachedMeshInfo[i].vertices[j] = v2;
					}

					textInfo.meshInfo[i].mesh.vertices = _cachedMeshInfo[i].vertices;
					_textMeshPro.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
				}
			}
			//_textMeshPro.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
			//_textMeshPro.UpdateVertexData();
		}

		private static void GetBounds(TMP_TextInfo textInfo, out Vector3 min, out Vector3 max)
		{
			min = new Vector2(float.MaxValue, float.MaxValue);
			max = new Vector2(float.MinValue, float.MinValue);
			int materialCount = textInfo.materialCount;
			for (int i = 0; i < materialCount; i++)
			{
				var meshInfo = textInfo.meshInfo[i];
				if (meshInfo.vertexCount > 0)
				{
					GetBounds(meshInfo.mesh.vertices, meshInfo.mesh.triangles, ref min, ref max);
				}
			}
		}

		private static void GetBounds(Vector3[] verts, int[] triangles, ref Vector3 min, ref Vector3 max)
		{
			if (verts != null && triangles != null)
			{
				if (verts.Length > 0 && triangles.Length >= 3 && (triangles.Length % 3) == 0)
				{
					for (int i = 0; i < triangles.Length; i += 3)
					{
						int ia = triangles[i+0];
						int ib = triangles[i+1];
						int ic = triangles[i+2];
						if (ia != ib && ib != ic && ic != ia)
						{
							Vector3 va = verts[ia];
							Vector3 vb = verts[ib];
							Vector3 vc = verts[ic];
							if (va != vb && vb != vc && vc != va)
							{
								min = Vector3.Min(min, va);
								max = Vector3.Max(max, va);
								min = Vector3.Min(min, vb);
								max = Vector3.Max(max, vb);
								min = Vector3.Min(min, vc);
								max = Vector3.Max(max, vc);
							}
						}
					}
				}
			}
		}

		void GatherSubMeshes()
		{
			GetComponentsInChildren<TMP_SubMeshUI>(false, _subMeshTemp);

			// SubMesh GameObject ordering doesn't always match the meshInfo order, so we have to reorder it
			_subMeshes.Clear();
			_subMeshes.Add(null);
			for (int i = 1; i < _textMeshPro.textInfo.materialCount; i++)
			{
				var meshInfo = _textMeshPro.textInfo.meshInfo[i];
				for (int j = 0; j < _subMeshTemp.Count; j++)
				{
					if (_subMeshTemp[j].mesh == meshInfo.mesh)
					{
						_subMeshes.Add(_subMeshTemp[j]);
						break;
					}
				}
			}
			Debug.Assert(_subMeshes.Count == _textMeshPro.textInfo.materialCount);
		}

		public static Vector3 GetAnchorPositionForBounds(TextAnchor anchor, Vector3 boundsMin, Vector3 boundsMax)
		{
			Vector3 result = boundsMin;
			switch (anchor)
			{
				case TextAnchor.UpperLeft:
					result.x = boundsMin.x;
					result.y = boundsMax.y;
					break;
				case TextAnchor.UpperCenter:
					result.x = boundsMin.x + (boundsMax.x - boundsMin.x) / 2f;
					result.y = boundsMax.y;
					break;
				case TextAnchor.UpperRight:
					result.x = boundsMax.x;
					result.y = boundsMax.y;
					break;
				case TextAnchor.MiddleLeft:
					result.x = boundsMin.x;
					result.y = boundsMin.y + (boundsMax.y - boundsMin.y) / 2f;
					break;
				case TextAnchor.MiddleCenter:
					result.x = boundsMin.x + (boundsMax.x - boundsMin.x) / 2f;
					result.y = boundsMin.y + (boundsMax.y - boundsMin.y) / 2f;
					break;
				case TextAnchor.MiddleRight:
					result.x = boundsMax.x;
					result.y = boundsMin.y + (boundsMax.y - boundsMin.y) / 2f;
					break;
				case TextAnchor.LowerLeft:
					result.x = boundsMin.x;
					result.y = boundsMin.y;
					break;
				case TextAnchor.LowerCenter:
					result.x = boundsMin.x + (boundsMax.x - boundsMin.x) / 2f;
					result.y = boundsMin.y;
					break;
				case TextAnchor.LowerRight:
					result.x = boundsMax.x;
					result.y = boundsMin.y;
					break;
			}
			return result;
		}
	}
}

#endif