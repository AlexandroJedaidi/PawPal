// Cylinder to Cube conversion utility
// Convert Cylinder with x and z of 0.11m or less to Cube
// You can reduce the number of polygons
// Cylinder = 80 Polygons Cube = 12 Polygons
// How to use
// 1.Press the "Cylinder to Cube" button
// Note 1. Prefab is edited

// CylinderからCubeへ変換ユーティリティ
// xとzが0.11m以下のCylinderを、Cubeに変換します
// ポリゴン数を減らすことができます
// Cylinder = 80 Polygons Cube = 12 Polygons
// 使い方
// 1.「Cylinder to Cube」ボタンを押します
// 注1.Prefabが編集されます

using UnityEngine;
using System.Collections;
using System.Collections.Generic; // List用に必要
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MyTown {

	public class CylinderToCubePrefab : MonoBehaviour {

		public bool UndoFlg = false;
		private float XZLessThan = 0.11f; // Sizes smaller than this size are target to change
		private Shader fromObjectShader;
		private Material fromObjectMaterial;
		private GameObject fromObjectParent; // Parent folder name
		private Vector3 fromObjectPosition;
		private Vector3 fromObjectScale;
		private Vector3 fromObjectAngle;
		private string fromObjectName;
		private int fromObjectIndex;
		private bool fromObjectActive;
		private int counter;
		private List<string> prefabPath = new List<string>() {
										"Assets/milltype/MyTown/Prefabs/House01R.prefab",
										"Assets/milltype/MyTown/Prefabs/House01L.prefab",
										"Assets/milltype/MyTown/Prefabs/House02R.prefab",
										"Assets/milltype/MyTown/Prefabs/House02L.prefab",
										"Assets/milltype/MyTown/Prefabs/House03R.prefab",
										"Assets/milltype/MyTown/Prefabs/House03L.prefab",
										"Assets/milltype/MyTown/Prefabs/House04R.prefab",
										"Assets/milltype/MyTown/Prefabs/House04L.prefab",
										"Assets/milltype/MyTown/Prefabs/House05R.prefab",
										"Assets/milltype/MyTown/Prefabs/House05L.prefab",
										"Assets/milltype/MyTown/Prefabs/House06R.prefab",
										"Assets/milltype/MyTown/Prefabs/House06L.prefab",
										"Assets/milltype/MyTown/Prefabs/House07R.prefab",
										"Assets/milltype/MyTown/Prefabs/House07L.prefab",
										"Assets/milltype/MyTown/Prefabs/ConvenienceR.prefab",
										"Assets/milltype/MyTown/Prefabs/ConvenienceL.prefab",
										"Assets/milltype/MyTown/Prefabs/ParkH.prefab",
										"Assets/milltype/MyTown/Prefabs/ParkV.prefab",
										"Assets/milltype/MyTown/Prefabs/Slope.prefab",
										"Assets/milltype/MyTown/Prefabs/RoadLong.prefab",
										"Assets/milltype/MyTown/Prefabs/Cross.prefab",
										"Assets/milltype/MyTown/Prefabs/Corner.prefab",
										"Assets/milltype/MyTown/Prefabs/Junction.prefab",
										"Assets/milltype/MyTown/Prefabs/EndRoad.prefab",
										"Assets/milltype/MyTown/Prefabs/RoadShortA.prefab",
										"Assets/milltype/MyTown/Prefabs/RoadShortB.prefab",
										"Assets/milltype/MyTown/Prefabs/RoadMiddleA.prefab",
										"Assets/milltype/MyTown/Prefabs/RoadMiddleB.prefab",
										"Assets/milltype/MyTown/Prefabs/CrankA.prefab",
										"Assets/milltype/MyTown/Prefabs/CrankB.prefab"};

		// Use this for initialization
		void Start () {
		}

		// Update is called once per frame
		void Update () {
		}

		private void OnDestroy () {
			if (fromObjectMaterial != null) {
				Destroy (fromObjectMaterial);
			}
		}

		[ContextMenu ("Cylinder to Cube")]
		void CubeChange () {
			// Get all the child elements of the prefab
			foreach (string prefabName in prefabPath) { // Loop per prefab
				GameObject prefabParent = null;
				try {
					prefabParent = PrefabUtility.LoadPrefabContents (prefabName);
				} catch (System.ArgumentException e) {
					Debug.Log ("Prefab not found  (" + prefabName + ") " + e); // Prefab name list object not found
				}
				if (prefabParent != null) {
					Transform[] transformArray = prefabParent.GetComponentsInChildren<Transform>();
					counter = 0;
					MeshFilter meshFilter = null;
					MeshRenderer meshRenderer = null;
					Renderer renderer = null;
					foreach (Transform child in transformArray) { // Loop of child elements
						meshFilter = child.GetComponent<MeshFilter>();
						meshRenderer = child.GetComponent<MeshRenderer>();
						renderer = child.GetComponent<Renderer>();
						if (meshFilter != null) {
							string ToObjectObjectName;
							if (UndoFlg == false) {
								ToObjectObjectName = "Cylinder";
							} else {
								ToObjectObjectName = "Cube";
							}
							if (meshFilter.sharedMesh.name.Equals(ToObjectObjectName)) {
								//Debug.Log (child.name);
								fromObjectScale = child.transform.localScale;
								//Debug.Log ("Scale x= " + fromObjectScale.x + " z=" + fromObjectScale.z + " Less than=" + XZLessThan);
								if ((fromObjectScale.x <= XZLessThan) && (fromObjectScale.z <= XZLessThan)) {
									fromObjectShader = meshRenderer.sharedMaterial.shader;
									fromObjectMaterial = renderer.sharedMaterial;
									//Debug.Log ("Find fromObject (" + meshFilter.sharedMesh.name + ")");
									//Debug.Log ("Find fromObject (" + meshRenderer.sharedMaterial.name + ")");
									if (child.transform.parent != null){
										fromObjectParent = child.transform.parent.gameObject;
									} else {
										fromObjectParent = null;
									}
									//Debug.Log ("Name = " + fromObjectParent.name + "/" + child.name);
									fromObjectPosition = child.transform.position;
									fromObjectAngle = child.transform.localEulerAngles;
									fromObjectName = child.name;
									fromObjectIndex = child.transform.GetSiblingIndex ();
									fromObjectActive = child.gameObject.activeSelf; // activeInHierarchy;
									child.transform.SetAsLastSibling (); // Set last index

									DestroyImmediate (child.gameObject);

									// Change Cylinder to Cube
									CubeCreate ();
									counter++;
								}
							}
						}
					}
					PrefabUtility.SaveAsPrefabAsset (prefabParent, prefabName);
					PrefabUtility.UnloadPrefabContents (prefabParent);
					if (meshFilter != null) { DestroyImmediate (meshFilter); }
					if (meshRenderer != null) { DestroyImmediate (meshRenderer); }
					if (renderer != null) { DestroyImmediate (renderer); }
					if (UndoFlg == false) {
						Debug.Log (prefabName + " changed (" + 80 * counter + " polygons to " + 12 * counter + " polygons)");
					} else {
						Debug.Log (prefabName + " changed (" + 12 * counter + " polygons to " + 80 * counter + " polygons)");
					}
				}
			}
		}

		void CubeCreate () {
			GameObject ToObject;
			if (UndoFlg == false) {
				ToObject = GameObject.CreatePrimitive (PrimitiveType.Cube); // Create Primitive
			} else {
				ToObject = GameObject.CreatePrimitive (PrimitiveType.Cylinder); // Create Primitive
			}
			if (fromObjectParent != null) {
				ToObject.transform.parent = fromObjectParent.transform;
			}
			MeshRenderer meshRenderer = ToObject.GetComponent<MeshRenderer>();
			meshRenderer.sharedMaterial.shader = fromObjectShader; // material -> sharedMaterial
			Renderer renderer = ToObject.GetComponent<Renderer>();
			renderer.material = fromObjectMaterial;
			ToObject.transform.position = new Vector3(fromObjectPosition.x, fromObjectPosition.y, fromObjectPosition.z);
			if (UndoFlg == false) {
				// Cylinder -> Cube
				ToObject.transform.localScale = new Vector3(fromObjectScale.x, fromObjectScale.y * 2.0f, fromObjectScale.z);
			} else {
				// Cube -> Cylinder
				ToObject.transform.localScale = new Vector3(fromObjectScale.x, fromObjectScale.y / 2.0f, fromObjectScale.z);
			}
			ToObject.transform.localEulerAngles = new Vector3(fromObjectAngle.x, fromObjectAngle.y, fromObjectAngle.z);
			ToObject.name = fromObjectName;
			ToObject.transform.SetSiblingIndex (fromObjectIndex);
			ToObject.SetActive (fromObjectActive);
		}

#if UNITY_EDITOR

		[CustomEditor (typeof (CylinderToCubePrefab))]
		public class CountStartEditor : Editor {

			CylinderToCubePrefab cylinderToCubePrefab;

			void OnEnable () {
				cylinderToCubePrefab = target as CylinderToCubePrefab;
			}

			public override void OnInspectorGUI () {
				base.OnInspectorGUI ();

				if (GUILayout.Button ("Cylinder to Cube")) {
					cylinderToCubePrefab.CubeChange ();
				}
			}
		}
#endif

	}
}
