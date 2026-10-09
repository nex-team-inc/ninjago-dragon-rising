//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ChocDino.UIFX;

namespace ChocDino.UIFX.Demos
{
	public class Demo01Balls : MonoBehaviour
	{
		[Header("Elements")]
		[SerializeField] float _speed = 4f;
		[SerializeField] int _ballCount = 128;
		[SerializeField] bool _animateGroupTransform = true;
		[SerializeField] Transform _ballGroup = null;
		[SerializeField] Transform _ballPrefab = null;
		[SerializeField] float _ballSpeedFactor = 10f;
		[SerializeField] Gradient _gradient = null;

		[Header("UI")]
		[SerializeField] Button _buttonNone = null;
		[SerializeField] Button _buttonSimple = null;
		[SerializeField] Button _buttonReal = null;
		[SerializeField] Slider _sliderMotionSpeed = null;
		[SerializeField] Slider _sliderRenderSpeed = null;
		[SerializeField] Button _buttonPause = null;

		private List<Vector3> _ballSpeeds;
		private List<Transform> _balls;

		void Start()
		{
			// Reset globals in case they were changed by other demos
			MotionBlurSimple.GlobalDebugFreeze = false;
			MotionBlurReal.GlobalDebugFreeze = false;
			Time.timeScale = 1f;

			// Reset others
			_renderSpeed = 1f;
			_renderTimer = 0f;

			// Create UI
			_buttonNone.onClick.AddListener(OnButtonNoneClick);
			_buttonSimple.onClick.AddListener(OnButtonSimpleClick);
			_buttonReal.onClick.AddListener(OnButtonRealClick);
			_buttonPause.onClick.AddListener(OnButtonPauseClick);
			_sliderMotionSpeed.onValueChanged.AddListener(OnSliderMotionSpeed);
			_sliderRenderSpeed.onValueChanged.AddListener(OnSliderRenderSpeed);
			UpdatePauseButtonText();

			// Create speed list
			_ballSpeeds = new List<Vector3>(_ballCount);
			for (int i = 0; i < _ballCount; i++)
			{
				float scale = Random.Range(0.1f, 4.0f);
				_ballSpeeds.Add(Random.insideUnitCircle.normalized * scale);
			}

			// Instantiate balls
			_balls = new List<Transform>(_ballCount);
			float screenWidthHalf = 1920.0f / 2f; // Screen.width / 2f;
			float screenHeightHalf = 1080.0f / 2f; // Screen.height / 2f;
			float dim = Mathf.Max(screenWidthHalf, screenHeightHalf) / 2f;
			for (int i = 0; i < _ballCount; i++)
			{
				Vector3 position = new Vector3(Random.Range(-dim, dim), Random.Range(-dim,dim / 2f), _ballPrefab.localPosition.z);
				GameObject go = Instantiate<GameObject>(_ballPrefab.gameObject, position, Quaternion.identity);
				go.GetComponent<UnityEngine.UI.Graphic>().color = _gradient.Evaluate(Random.value);
				//Color.HSVToRGB(Random.value, Random.Range(0.0f, 0.1f), Random.Range(0.8f, 1f));
				float scale = Random.Range(0.1f, 1.5f);
				go.transform.localScale = Vector3.one * scale;
				go.transform.SetParent(_ballGroup, false);
				_balls.Add(go.transform);
			}

			// Hide prefab
			_ballPrefab.gameObject.SetActive(false);

			// Set initial mode
			OnButtonRealClick();
		}

		private float _renderSpeed = 1f;
		private float _renderTimer = 0f;

		void Update()
		{
			if (Input.GetKeyDown(KeyCode.Space))
			{
				TogglePause();
			}

			if (Input.GetKeyDown(KeyCode.R))
			{
				RandomisePositions();
			}

			if (_renderSpeed > 0f && _renderSpeed < 1f)
			{
				_renderTimer += Time.unscaledDeltaTime * _renderSpeed;
				if (_renderTimer > 0.016f)
				{
					_renderTimer = 0f;
					MotionBlurReal.GlobalDebugFreeze = MotionBlurSimple.GlobalDebugFreeze = false;
					Time.timeScale = MotionBlurSimple.GlobalDebugFreeze ? 0f : 1f;
				}
				else
				{
					MotionBlurReal.GlobalDebugFreeze = MotionBlurSimple.GlobalDebugFreeze = true;
					Time.timeScale = MotionBlurSimple.GlobalDebugFreeze ? 0f : 1f;
				}
			}

			//if (Time.timeScale > 0f)
			{
				MoveBalls();
			}
		}

		void TogglePause()
		{
			MotionBlurSimple.GlobalDebugFreeze = !MotionBlurSimple.GlobalDebugFreeze;
			MotionBlurReal.GlobalDebugFreeze = !MotionBlurReal.GlobalDebugFreeze;
			Time.timeScale = MotionBlurSimple.GlobalDebugFreeze ? 0f : 1f;
		}

		void RandomisePositions()
		{
			float screenWidthHalf = 1920.0f / 2f; // Screen.width / 2f;
			float screenHeightHalf = 1080.0f / 2f; // Screen.height / 2f;
			float dim = Mathf.Max(screenWidthHalf, screenHeightHalf) / 2f;
			for (int i = 0; i < _ballCount; i++)
			{
				Vector3 position = new Vector3(Random.Range(-dim, dim), Random.Range(-dim,dim / 2f), _ballPrefab.localPosition.z);
				_balls[i].localPosition = position;

				{
					var mb = _balls[i].GetComponent<MotionBlurSimple>();
					if (mb && mb.enabled) { mb.ResetMotion(); }
				}
				{
					var mb = _balls[i].GetComponent<MotionBlurReal>();
					if (mb && mb.enabled) { mb.ResetMotion(); }
				}
			}
		}

		readonly Vector3 NegX = new Vector3(-1f, 1f, 1f);
		readonly Vector3 NegY = new Vector3(1f, -1f, 1f);

		void MoveBalls()
		{
			// Move and bounce balls on screen edges
			float screenWidthHalf = 1920.0f / 2f;
			float screenHeightHalf = 1080.0f / 2f;
			for (int i = 0; i < _ballCount; i++)
			{
				Vector3 ballSpeed = _ballSpeeds[i];
				Transform ball = _balls[i];
				Vector3 delta = ballSpeed * Time.deltaTime * _ballSpeedFactor;
				Vector3 newPos = ball.localPosition + delta;

				if (newPos.x > screenWidthHalf && delta.x > 0f)
				{
					ballSpeed.Scale(NegX);
				}
				if (newPos.x < -screenWidthHalf && delta.x < 0f)
				{
					ballSpeed.Scale(NegX);
				}
				if (newPos.y > screenHeightHalf && delta.y > 0f)
				{
					ballSpeed.Scale(NegY);
				}
				if (newPos.y < -screenHeightHalf && delta.y < 0f)
				{
					ballSpeed.Scale(NegY);
				}
				ball.localPosition += ballSpeed * Time.deltaTime * _ballSpeedFactor;
				_ballSpeeds[i] = ballSpeed;
			}

			// Animate parent transform
			if (_animateGroupTransform)
			{
				_transformAnimTime += Time.deltaTime * _speed;
				float duration = 5.0f;
				float delay = 1f;
				float t = Mathf.PingPong(_transformAnimTime, (duration + delay)) / duration;
				float t2 = 1f-DemoUtils.EaseExpo(1f-t);
				float t3 = DemoUtils.InOutExpo(t);
				float angle = Mathf.Lerp(180f, 0f, t3);
				float scale = Mathf.Lerp(0.2f, 1f, t2);
				_ballGroup.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
				//angle = _transformAnimTime * 100f;
				//_ballGroup.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
				//_ballGroup.localRotation = Quaternion.AngleAxis(angle / 1.53f, Vector3.right);
				//* Quaternion.AngleAxis(angle * 1.27f, Vector3.up);
				_ballGroup.localScale = new Vector3(scale, scale, 1f);
			}
		}

		private float _transformAnimTime = 0f;

		void OnButtonNoneClick()
		{
			SetComponentEnabled<MotionBlurSimple>(false);
			SetComponentEnabled<MotionBlurReal>(false);

			_buttonNone.image.color = Color.yellow;
			_buttonSimple.image.color = Color.white;
			_buttonReal.image.color = Color.white;
		}

		void OnButtonSimpleClick()
		{
			SetComponentEnabled<MotionBlurReal>(false);
			SetComponentEnabled<MotionBlurSimple>(true);

			_buttonNone.image.color = Color.white;
			_buttonSimple.image.color = Color.yellow;
			_buttonReal.image.color = Color.white;
		}

		void OnButtonRealClick()
		{
			SetComponentEnabled<MotionBlurSimple>(false);
			SetComponentEnabled<MotionBlurReal>(true);

			_buttonNone.image.color = Color.white;
			_buttonSimple.image.color = Color.white;
			_buttonReal.image.color = Color.yellow;
		}

		void OnButtonPauseClick()
		{
			TogglePause();
			UpdatePauseButtonText();
		}

		void UpdatePauseButtonText()
		{
			if (MotionBlurReal.GlobalDebugFreeze && MotionBlurSimple.GlobalDebugFreeze && Time.timeScale <= 0f)
			{
				_buttonPause.GetComponentInChildren<Text>().text = "Resume";
			}
			else
			{
				_buttonPause.GetComponentInChildren<Text>().text = "Pause";
			}
		}

		void OnSliderMotionSpeed(float value)
		{
			_speed = value * 10f;
			_ballSpeedFactor = value * 500f;
		}

		void OnSliderRenderSpeed(float value)
		{
			_renderSpeed = value;
			MotionBlurReal.GlobalDebugFreeze = MotionBlurSimple.GlobalDebugFreeze = (_renderSpeed <= 0f);
			Time.timeScale = MotionBlurSimple.GlobalDebugFreeze ? 0f : 1f;
			//Debug.Log(_renderSpeed);
		}

		void SetComponentEnabled<T>(bool enabled) where T : Behaviour
		{
			foreach (Transform xform in _balls)
			{
				var component = xform.gameObject.GetComponent<T>();
				if (component)
				{
					component.enabled = enabled;
				}
			}
		}
	}
}
