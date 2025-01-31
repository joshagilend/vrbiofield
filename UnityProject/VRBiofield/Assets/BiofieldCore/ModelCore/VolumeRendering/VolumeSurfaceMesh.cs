﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class VolumeSurfaceMesh : MonoBehaviour {

	public bool UpdateNow = false;

	[ContextMenu("Test Field Meshing")]
	void TestField() {
		int s = 3;
		VolumeBuffer<bool> test = new VolumeBuffer<bool> (new Cubic<int> (s, s, s));
		test.Write (new Int3 (0, 0, 0), true);
		test.Write (new Int3 (1, 0, 0), true);
		test.Write (new Int3 (0, 1, 0), true);
		test.Write (new Int3 (0, 0, 1), true);
		test.Write (new Int3 (0, 0, 2), true);

		test.Write (new Int3 (2, 2, 2), true);
		//test.Write (new Int3 (1, 2, 2), true);

		var mesh = VolumeTetrahedraSurfacer.GenerateSurfaceVolume (test, 
			           (t => (t ? 1.0f : -3.0f)), 
			           GetRelativeCameraPos ());
		SetMeshFromVolume (mesh);
		Debug.Log ("Done meshing.");
	}



	public Vector3 GetRelativeCameraPos() {
		return this.transform.worldToLocalMatrix.MultiplyPoint (Camera.main.transform.position);
	}

	void SetMeshFromVolume(Mesh m) {
		var mf = this.GetComponent<MeshFilter> ();
		mf.mesh = m;
		var mr = this.GetComponent<MeshRenderer> ();
		mr.enabled = true;
	}

	[Range(0.0f,70.0f)]
	public float IsosurfaceRootValue = 5.0f;
	[ContextMenu("From Dynamic Field")]
	void FromDynamicField() {
		var df = this.GetComponentInParent<DynamicFieldModel> ();
		df.EnsureSetup ();
		var cells = df.FieldsCells;

		System.Func<DynamicFieldModel.DynFieldCell,float> f = ((cl) => {
			bool isValid = true;// ( cl.VoxelIndex.X > (cells.Size.X / 2) );
			float sign = (cl.Direction.magnitude - IsosurfaceRootValue);
			return (isValid ? sign : -Mathf.Sign(sign));

			//bool isOdd = ((cl.VoxelIndex.X % 2) == 1);
			//return (isOdd ? 1.0f : -1.0f);
		});

		var mesh = VolumeTetrahedraSurfacer.GenerateSurfaceVolume (cells, 
			(t => f(t)),// t.Direction.magnitude - IsosurfaceRootValue), 
			GetRelativeCameraPos (), df);
		SetMeshFromVolume (mesh);

	}


	// Use this for initialization
	void Start () {
		
	}

	public bool AutoUpdate = false;
	private float PreviousRootValue = -1.0f;
	// Update is called once per frame
	void Update () {
		
		if (AutoUpdate) {
			if (PreviousRootValue != this.IsosurfaceRootValue) {
				this.UpdateNow = true;
			}
		}
		if (UpdateNow) {
			UpdateNow = false;
			this.PreviousRootValue = this.IsosurfaceRootValue;
			this.FromDynamicField ();
		}
	}
}
