//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ChocDino.UIFX;

namespace ChocDino.UIFX.Demos
{
	public class Demo01Gooey : MonoBehaviour
	{
		[SerializeField] Text _text = null;
		[SerializeField] GooeyFilter _gooeyBox = null;

		void Update()
		{
			float time = Time.time * 2.5f;

			if (_gooeyBox)
			{
				float t1 = Mathf.PingPong(time * 0.5f, 1f);
				t1 = 1.0f - DemoUtils.InOutExpo(t1);
				_gooeyBox.Strength = t1;
			}
			
			if (_text)
			{
				float t3 = Mathf.PingPong(time * 0.125f, 1f);
				t3 = DemoUtils.InOutCubic(t3);
				_text.lineSpacing = Mathf.Lerp(1.0f, -0.1f, t3);
			}
		}
	}
}