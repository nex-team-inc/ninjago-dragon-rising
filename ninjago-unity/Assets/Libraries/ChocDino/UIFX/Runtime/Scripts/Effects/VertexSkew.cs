//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityInternal = UnityEngine.Internal;

namespace ChocDino.UIFX
{
	public enum SkewDirection
	{
		Horizontal,
		Vertical,
	}

	public enum SkewPivotBounds
	{
		Mesh,
		Quads,
	}

	/// <summary>
	/// Apply an affine skew transform to the vertex positions of a UGUI component
	/// </summary>
	[ExecuteAlways]
	[RequireComponent(typeof(Graphic))]
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Effects/UIFX - Vertex Skew")]
	public class VertexSkew : UIBehaviour, IMeshModifier
	{
		[SerializeField] SkewPivotBounds _pivotBounds = SkewPivotBounds.Mesh;

		[SerializeField] TextAnchor _pivot = TextAnchor.MiddleCenter;

		[SerializeField] SkewDirection _direction = SkewDirection.Vertical;

		[Range(-90f, 90f)]
		[SerializeField] float _angle = 16f;

		[SerializeField] float _offset = 0f;

		[Range(0f, 1f)]
		[SerializeField] float _strength = 1f;

		private Matrix4x4 _matrix;
		private Vector3 _boundsMin, _boundsMax;
		private Vector3 _pivotPoint;

		public SkewPivotBounds PivotBounds { get { return _pivotBounds; } set { if (value != _pivotBounds) { _pivotBounds = value; ForceVerticesUpdate(); } } }
		public float Angle { get { return _angle; } set { value = Mathf.Clamp(value, -90f, 90f); if (value != _angle) { _angle = value; ForceVerticesUpdate(); } } }
		public float Offset { get { return _offset; } set { if (value != _offset) { _offset = value; ForceVerticesUpdate(); } } }
		public SkewDirection Direction { get { return _direction; } set { if (value != _direction) { _direction = value; ForceVerticesUpdate(); } } }
		public TextAnchor Pivot { get { return _pivot; } set { if (value != _pivot) { _pivot = value; ForceVerticesUpdate(); } } }
		public float Strength { get { return _strength; } set { value = Mathf.Clamp01(value); if (value != _strength) { _strength = value; ForceVerticesUpdate(); } } }

		public Vector3 PivotPoint { get { return _pivotPoint; } }
		public Vector3 BoundsMin { get { return _boundsMin; } }
		public Vector3 BoundsMax { get { return _boundsMax; } }

		private Graphic _graphic;
		private Graphic GraphicComponent { get { if (_graphic == null) _graphic = GetComponent<Graphic>(); return _graphic; } }

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

		protected override void OnDisable()
		{
			ForceVerticesUpdate();
			base.OnDisable();
		}

		protected override void OnEnable()
		{
			ForceVerticesUpdate();
			base.OnEnable();
		}

		protected override void OnDidApplyAnimationProperties()
		{
			ForceVerticesUpdate();
			base.OnDidApplyAnimationProperties();
		}

		private void ForceVerticesUpdate()
		{
			var graphic = GraphicComponent;
			graphic.SetVerticesDirty();
			graphic.SetMaterialDirty();
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

		[UnityInternal.ExcludeFromDocs]
		public void ModifyMesh(VertexHelper vh)
		{
			if (!this.isActiveAndEnabled) return;
			if (_strength <= 0f) return;

			if (_pivotBounds == SkewPivotBounds.Mesh)
			{
				GetBounds(vh, out _boundsMin, out _boundsMax);
				BuildMatrix();

				UIVertex v = UIVertex.simpleVert;
				int vertexCount = vh.currentVertCount;
				for (int i = 0; i < vertexCount; i++)
				{
					vh.PopulateUIVertex(ref v, i);
					v.position = _matrix.MultiplyPoint3x4(v.position);
					vh.SetUIVertex(v, i);
				}
			}
			else if (_pivotBounds == SkewPivotBounds.Quads)
			{
				UIVertex[] v = new UIVertex[4];
				int quadCount = vh.currentIndexCount / 6;
				for (int i = 0; i < quadCount; i++)
				{
					int vertexIdx = i * 4;
					for (int j = 0; j < 4; j++)
					{
						vh.PopulateUIVertex(ref v[j], vertexIdx + j);
					}
					
					GetBounds(v, out _boundsMin, out _boundsMax);
					BuildMatrix();

					for (int j = 0; j < 4; j++)
					{
						v[j].position = _matrix.MultiplyPoint3x4(v[j].position);
						vh.SetUIVertex(v[j], vertexIdx + j);
					}
				}
			}
		}

		private static void GetBounds(VertexHelper vh, out Vector3 min, out Vector3 max)
		{
			Vector3 boundsMin = new Vector2(float.MaxValue, float.MaxValue);
			Vector3 boundsMax = new Vector2(float.MinValue, float.MinValue);
			UIVertex v = UIVertex.simpleVert;
			int vertexCount = vh.currentVertCount;
			for (int i = 0; i < vertexCount; i++)
			{
				vh.PopulateUIVertex(ref v, i);

				boundsMin = Vector2.Min(v.position, boundsMin);
				boundsMax = Vector2.Max(v.position, boundsMax);
			}
			min = boundsMin;
			max = boundsMax;
		}

		private static void GetBounds(UIVertex[] v, out Vector3 min, out Vector3 max)
		{
			Vector3 boundsMin = new Vector2(float.MaxValue, float.MaxValue);
			Vector3 boundsMax = new Vector2(float.MinValue, float.MinValue);
			int vertexCount = v.Length;
			for (int i = 0; i < vertexCount; i++)
			{
				boundsMin = Vector2.Min(v[i].position, boundsMin);
				boundsMax = Vector2.Max(v[i].position, boundsMax);
			}
			min = boundsMin;
			max = boundsMax;
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

		[UnityInternal.ExcludeFromDocs]
		[System.Obsolete("use IMeshModifier.ModifyMesh (VertexHelper verts) instead", false)]
		public void ModifyMesh(Mesh mesh)
		{
			throw new System.NotImplementedException("use IMeshModifier.ModifyMesh (VertexHelper verts) instead");
		}
	}
}