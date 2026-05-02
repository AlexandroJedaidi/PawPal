// Antenna Direction Change Utility
// Antennas point in the same direction in all homes
// With this script, you can change in the same direction all at once
// How to use
// 1.Enter "Direction" from 0 to 360
// 2.Press the "Direction Change" button
// Note 1. Please execute in "Edit mode"
// Note 2. Please activate the object to be changed in Inspector in advance

// アンテナ方向変更ユーティリティ
// アンテナはすべての家で同じ方向を向いています
// このスクリプトを使用すると、同じ方向に一度に変更できます
// 使い方
// 1.「方向」を0から360まで入力します
// 2.「方向変更」ボタンを押します
// 注1.「編集モード」で実行してください
// 注2.事前にInspectorで変更するオブジェクトを有効にしてください

using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MyTown {

	public class AntennaDirection : MonoBehaviour {

		public float Direction = 0.0f;
		private int findCount;

		// Use this for initialization
		void Start () {
		}

		// Update is called once per frame
		void Update () {
		}

		[ContextMenu ("Direction Change")]
		void DirectionChange () {
			// Antenna Direction Change
			GameObject[] antennaList = GameObject.FindGameObjectsWithTag ("HouseAntenna");
			findCount = 0;
			foreach (GameObject antenna in antennaList) {
				//Debug.Log ("HouseAntenna");
				antenna.transform.rotation = Quaternion.identity; // Base direction
				antenna.transform.Rotate (0.0f, Direction, 0.0f);
				findCount++;
			}
			Debug.Log ("Direction Changed = " + findCount);
		}

#if UNITY_EDITOR

		[CustomEditor (typeof (AntennaDirection))]
		public class CountStartEditor : Editor {

			AntennaDirection antennaDirection;

			void OnEnable () {
				antennaDirection = target as AntennaDirection;
			}

			public override void OnInspectorGUI () {
				base.OnInspectorGUI ();

				if (GUILayout.Button ("Direction Change")) {
					antennaDirection.DirectionChange ();
				}
			}
		}
#endif

	}
}
