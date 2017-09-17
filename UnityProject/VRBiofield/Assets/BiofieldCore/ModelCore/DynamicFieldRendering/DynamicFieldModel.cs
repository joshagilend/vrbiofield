﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DynamicFieldModel : MonoBehaviour {

	public BodyLandmarks Body;
	public ExcersizeSharedScheduler ExcersizeSystem;

	public VolumeBuffer<DynFieldCell> FieldsCells = null;
	[Range(0,6)]
	public int CurrentFocusChakra = 0;
	public float UnitMagnitude = 45.0f;
	[Range(4,32)]
	public int VoxelSideRes = 8;
	public float DEBUG_AvgMagnitude = 0.0f;
	public bool SkipRandomPlacement = false;
	private bool mIsPaused = false;
	public float FieldOverallAlpha { get; private set; }

	public struct DynFieldCell
	{
		public Int3 VoxelIndex;
		public Vector3 Pos;
		public Vector3 Direction;
		public Color LatestColor;
		public float Twist;
	};

	static Vector3 OneOver(Vector3 v) {
		return new Vector3 (1.0f / v.x, 1.0f / v.y, 1.0f / v.z);
	}

	static Vector3 Scale(Vector3 a, Vector3 scl) {
		return new Vector3 (a.x * scl.x, a.y * scl.y, a.z * scl.z);
	}

	private bool isSetup = false;
	public void EnsureSetup() {
		if (isSetup)
			return;
		isSetup = true;

		FieldOverallAlpha = 1.0f;
		if (!this.Body) {
			this.Body = this.gameObject.GetComponentInParent<BodyLandmarks> ();
		}
		if (!this.ExcersizeSystem) {
			this.ExcersizeSystem = GameObject.FindObjectOfType<ExcersizeSharedScheduler> ();
		}
		this.Body.EnsureSetup ();
		int sideRes = this.VoxelSideRes;// 8;
		int chakraToShow = CurrentFocusChakra; //3;
		this.FieldsCells = new VolumeBuffer<DynFieldCell> (Cubic<int>.CreateSame (sideRes));
		var scl = OneOver (this.FieldsCells.Header.Size.AsVector3 ());
		var l2w = this.transform.localToWorldMatrix;
		var cntr = Body.Chakras.AllChakras [chakraToShow].transform.position;//  l2w.MultiplyPoint (Vector3.zero);
		foreach (var nd in this.FieldsCells.AllIndices3()) {
			var cell = this.FieldsCells.Read (nd.AsCubic());
			cell.Pos = this.transform.localToWorldMatrix.MultiplyPoint (FieldsCells.Header.CubicToDecimalUnit(nd) - (Vector3.one * 0.5f));
			if (!(SkipRandomPlacement)) {
				cell.Pos += Scale (Random.insideUnitSphere, scl); // add random offset
			}
			cell.Direction = cntr - cell.Pos;
			cell.LatestColor = Color.white;
			cell.Twist = 0.0f;
			cell.VoxelIndex = nd;
			this.FieldsCells.Write (nd.AsCubic(), cell);
		}

		this.UpdateCellFieldDir (snapToCurrent:true);
	}

	public delegate void PausedChangedEvent(bool isNowPaused);
	public event PausedChangedEvent OnPausedChanged;
	public bool IsPaused {
		get { return this.mIsPaused; }
		set {
			if (this.mIsPaused != value) {
				this.mIsPaused = value;
				if (OnPausedChanged != null) {
					OnPausedChanged (value);
				}
			}
		}
	}

	// Use this for initialization
	void Start () {
		
	}

	public static Vector3 MagneticDipoleField(Vector3 pos, Vector3 opos, Vector3 odip) {

		Vector3 r = (pos - opos);
		Vector3 rhat = r.normalized;

		Vector3 res = (3.0f * ((float)(Vector3.Dot (odip, rhat))) * rhat - odip) / Mathf.Pow (r.magnitude, 3);// * 1e-7f;
		return res;
	}

	public static Vector3 ChakraDipoleField(Vector3 pos, Vector3 opos, Vector3 odip, bool isOneWay) {
		Vector3 r = (pos - opos);
		Vector3 rhat = r.normalized;

		float radiusScale = 3.0f; //3.0f;
		float localDot = Vector3.Dot(rhat, odip);
		float coreDot = localDot;
		float localDotScale = Mathf.Lerp (0.2f, 1.0f, Mathf.Pow (Mathf.Abs (localDot), 2));
		float sideScale = ((!isOneWay) ? (localDotScale) : ((localDot < 0) ? (localDotScale) : (localDotScale * 0.1f)));
		float distScale = sideScale / Mathf.Pow (r.magnitude, 3);
		Vector3 res = (radiusScale * ((float)((coreDot )) * rhat) - odip) * distScale;// * 1e-7f;
		res *= -Mathf.Sign(localDot); // flow in in both ways.
		return res;
	}

	public static Vector3 ChakraFieldV2(Vector3 pos, Vector3 chakraPos, Quaternion chakraOrient, bool isOneWay) {
		var delta = (pos - chakraPos);
		var chakraFwd = chakraOrient * -Vector3.up;
		var d1 = Vector3.Cross (chakraFwd, delta).normalized;
		var d2 = Vector3.Cross (chakraFwd, d1).normalized;
		var d3 = Vector3.Cross (d1, d2).normalized;
		var s1 = Vector3.Dot (d1, delta);
		var s2 = Vector3.Dot (d2, delta);
		var sf = (delta - (chakraFwd * Vector3.Dot (chakraFwd, delta))).magnitude;
		var s3 = 2.0f * (1.0f / Mathf.Max (0.2f, (Mathf.Pow (s1, 2) + Mathf.Pow (s2, 2))));// * Mathf.Sign( Vector3.Dot(delta, chakraFwd));//  Mathf.Abs( Vector3.Dot (delta, chakraFwd) ) * 2.0f;
		//return (d1 + d2);
		return (chakraFwd * sf * -20.0f);
	}

	public static Vector3 ChakraFieldV3(Vector3 pos, Vector3 chakraPos, Quaternion chakraOrient, bool isOneWay) {
		var delta = (pos - chakraPos);
		var chakraFwd = (chakraOrient * -Vector3.up).normalized;
		var nearestPosOnLine = chakraPos + (chakraFwd * Vector3.Dot (chakraFwd, delta));
		var r = (pos - nearestPosOnLine);
		var dist = (3.0f / delta.magnitude);
		var inpct = Mathf.Min( dist * 6.0f, (3.0f / r.magnitude) );
		var toline = r.normalized * (-inpct);
		var tocenter = chakraFwd.normalized * (-dist) * Mathf.Sign(Vector3.Dot(chakraFwd,delta)); 
		return (toline + tocenter) * 1.0f;
	}

	void UpdateCellFieldDir(bool snapToCurrent=false) {
		if (this.IsPaused)
			return;
		
		var chakras = this.Body.Chakras.AllChakras;
		var chakra = chakras [((int)(Time.time * 0.5f)) % chakras.Length];
		if (CurrentFocusChakra >= 0) {
			chakra = chakras [CurrentFocusChakra];
		} else {
			return;
		}

		var cpos = chakra.transform.position;
		var cdir = -chakra.transform.up;
		var crot = chakra.transform.rotation;
		var cOneWay = chakra.ChakraOneWay;

		var avgSum = 0.0f;
		var avgCnt = 0;

		var cells = this.FieldsCells;
		var cnt = cells.Header.TotalCount;
		for (int i = 0; i < cnt; i++) {
			var c = cells.Array [i];
			//c.Direction = chakra.transform.position - c.Pos;
			//c.Direction = MagneticDipoleField(c.Pos, cpos, cdir) / UnitMagnitude;
			//var newDir = ChakraDipoleField(c.Pos, cpos, cdir, cOneWay);
			var newDir = ChakraFieldV3(c.Pos, cpos, crot, cOneWay);
			var newClr = chakra.ChakraColor;
			var lf = (snapToCurrent ? 1.0f : Time.deltaTime * 1.0f);
			c.Direction = Vector3.Lerp (c.Direction, newDir, lf);
			c.LatestColor = Color.Lerp (c.LatestColor, newClr, lf);
			cells.Array [i] = c;

			avgSum += c.Direction.magnitude;
			avgCnt += 1;
		}

		this.DEBUG_AvgMagnitude = (avgSum / ((float)avgCnt));
	}

	void UpdateCurrentSelection() {
		if (this.ExcersizeSystem) {
			var cur = this.ExcersizeSystem.CurrentActivity;
			var wantPause = true;
			var overallAlpha = 1.0f;
			if (cur) {
				ChakraBreath chakraExcer = null;
				ChakraBreath infoChakra = null;
				foreach (var inst in cur.Instances) {
					var ce = (inst as ChakraBreath);
					if (ce) {
						if (ce.Body == this.Body) {
							chakraExcer = ce;
						}
						if (ce.IsInfoAvatar) {
							infoChakra = ce;
						}
					}
				}
				if (infoChakra && infoChakra.FocusChakra) {
					if (chakraExcer != infoChakra) {
						chakraExcer = null; // if teacher, and info is active, hide the teacher
					}
				}
				if (chakraExcer && chakraExcer.CurrentChakra) {
					this.CurrentFocusChakra = chakraExcer.CurrentChakra.ChakraIndex - 1;
					overallAlpha = Mathf.Lerp (0.35f, 1.0f, chakraExcer.LatestBreathAlpha); // doesn't fully fade
					wantPause = false;
				}
			}
			this.IsPaused = (wantPause);
			this.FieldOverallAlpha = overallAlpha;
		}
	}
	
	// Update is called once per frame
	void Update () {
		this.EnsureSetup ();
		this.UpdateCurrentSelection ();
		this.UpdateCellFieldDir	();

	}
}
