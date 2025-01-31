﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DynamicFieldModel : MonoBehaviour {

	public BodyLandmarks Body;
	public MainEnergyApp Hand;
	public ChiHandEnergyBall ChiBall;
	public ExcersizeSharedScheduler ExcersizeSystem;
	public ExcersizeActivityInst ExcersizeInst;

	public VolumeBuffer<DynFieldCell> FieldsCells = null;
	[Range(0,6)]
	public int CurrentFocusChakra = 0;
	public float UnitMagnitude = 45.0f;
	[Range(4,32)]
	public int VoxelSideRes = 8;
	public int VoxelSideResMobile = 8;
	public float DEBUG_AvgMagnitude = 0.0f;
	public bool SkipRandomPlacement = false;
	private bool mIsPaused = false;
	public float FieldOverallAlpha { get; private set; }
	public bool DEBUG_IsPaused;
	public bool IsStaticLayout { get; private set; }

	public float ParticleFlowTime { get; set; }
	public float ParticleFlowRate { get; set; }

	public struct DynFieldCell
	{
		public Int3 VoxelIndex;
		public Vector3 Pos;
		public Vector3 Direction;
		public Color LatestColor;
		public float Twist;
	};

	public int CellCount { get { return this.FieldsCells.Header.TotalCount; } }

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
		ParticleFlowRate = 1.0f;
		if (!this.Body) {
			this.Body = this.gameObject.GetComponentInParent<BodyLandmarks> ();
		}
		if (!this.Body) {
			if (!this.Hand) {
				this.Hand = this.gameObject.GetComponentInParent<MainEnergyApp> ();
			}
			this.ChiBall = this.gameObject.GetComponentInParent<ChiHandEnergyBall> ();
		}
		Debug.Assert ((this.Body) || (this.Hand) || (this.ChiBall));
		if (!this.ExcersizeSystem) {
			this.ExcersizeSystem = GameObject.FindObjectOfType<ExcersizeSharedScheduler> ();
		}
		bool isMobile = (Application.platform == UnityEngine.RuntimePlatform.IPhonePlayer);
		int sideRes = (isMobile ? this.VoxelSideResMobile : this.VoxelSideRes);// 8;
		if (this.Body) {
			this.Body.EnsureSetup ();
			int chakraToShow = CurrentFocusChakra; //3;
			this.FieldsCells = new VolumeBuffer<DynFieldCell> (Cubic<int>.CreateSame (sideRes));
			var scl = OneOver (this.FieldsCells.Header.Size.AsVector3 ());
			var l2w = this.transform.localToWorldMatrix;
			var cntr = Body.Chakras.AllChakras [chakraToShow].transform.position;//  l2w.MultiplyPoint (Vector3.zero);
			foreach (var nd in this.FieldsCells.AllIndices3()) {
				var cell = this.FieldsCells.Read (nd.AsCubic ());
				cell.Pos = this.transform.localToWorldMatrix.MultiplyPoint (FieldsCells.Header.CubicToDecimalUnit (nd) - (Vector3.one * 0.5f));
				if (!(SkipRandomPlacement)) {
					cell.Pos += Scale (Random.insideUnitSphere, scl); // add random offset
				}
				cell.Direction = cntr - cell.Pos;
				cell.LatestColor = Color.white;
				cell.Twist = 0.0f;
				cell.VoxelIndex = nd;
				this.FieldsCells.Write (nd.AsCubic (), cell);
			}
		} else if (this.Hand) {
			var arrows = this.Hand.FindAllFlowNodes ();
			var n = arrows.Count;
			this.IsStaticLayout = true;
			this.FieldsCells = new VolumeBuffer<DynFieldCell> (Cubic<int>.Create (n, 1, 1));
			for (int i = 0; i < n; i++) {
				var cell = this.FieldsCells.Array [i];
				var arrow = arrows [i];
				cell.Pos = arrow.transform.position;
				cell.Direction = arrow.transform.up * 50.0f;
				cell.LatestColor = Color.green;
				cell.Twist = 0.0f;
				cell.VoxelIndex = new Int3 (i, 0, 0);
				this.FieldsCells.Array [i] = cell;
			}
		} else if (this.ChiBall) {
			
			this.FieldsCells = new VolumeBuffer<DynFieldCell> (Cubic<int>.CreateSame (sideRes));
			var scl = OneOver (this.FieldsCells.Header.Size.AsVector3 ());
			var cntr = this.ChiBall.transform.position;
			var l2w = this.transform.localToWorldMatrix;
			foreach (var nd in this.FieldsCells.AllIndices3()) {
				var cell = this.FieldsCells.Read (nd.AsCubic ());
				cell.Pos = this.transform.localToWorldMatrix.MultiplyPoint (FieldsCells.Header.CubicToDecimalUnit (nd) - (Vector3.one * 0.5f));
				if (!(SkipRandomPlacement)) {
					cell.Pos += Scale (Random.insideUnitSphere, scl); // add random offset
				}
				cell.Direction = cntr - cell.Pos;
				cell.LatestColor = Color.white;
				cell.Twist = 0.0f;
				cell.VoxelIndex = nd;
				this.FieldsCells.Write (nd.AsCubic (), cell);
			}
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
		var chakraTwist = Vector3.Cross (chakraFwd, pos - chakraPos).normalized * -0.4f;
		var nearestPosOnLine = chakraPos + (chakraFwd * Vector3.Dot (chakraFwd, delta));
		var r = (pos - nearestPosOnLine);
		var dist = (3.0f / delta.magnitude);
		var inpct = Mathf.Min( dist * 6.0f, (3.0f / r.magnitude) );
		var toline = r.normalized * (-inpct);
		var tocenter = (chakraFwd + chakraTwist).normalized * (-dist) * Mathf.Sign(Vector3.Dot(chakraFwd,delta)); 
		return (toline + tocenter) * 1.0f;
	}

	public static Vector3 ChakraFieldV3_v2(Vector3 pos, Vector3 chakraPos, Quaternion chakraOrient, bool isOneWay) {
		var delta = (pos - chakraPos);
		var chakraFwd = (chakraOrient * -Vector3.up).normalized;
		var chakraTwist = Vector3.Cross (chakraFwd, pos - chakraPos).normalized * -0.4f;
		var nearestPosOnLine = chakraPos + (chakraFwd * Vector3.Dot (chakraFwd, delta));
		var r = (pos - nearestPosOnLine);
		var dist = (3.0f / delta.magnitude);
		var inpct = Mathf.Min( dist * 6.0f, (3.0f / r.magnitude) );
		var toline = r.normalized * (-inpct);
		var tocenter = (delta.normalized * 5.0f) * Mathf.Sign(Vector3.Dot(chakraFwd,delta)); 
		return (toline + tocenter) * 1.0f;
	}

	public static Vector3 ChakraFieldAlongLineV4(Vector3 pos, Vector3 chakraPos, Vector3 chakraEnd, bool isOneWay) {
		var delta = (pos - chakraPos);
		float chakraLength = (chakraEnd - chakraPos).magnitude;
		var chakraFwd = (chakraEnd - chakraPos).normalized;
		var alongDist = Vector3.Dot (chakraFwd, delta);

		// shift virtual position to near point along the line:
		pos += chakraFwd * Mathf.Min (alongDist, chakraLength);
		delta = (pos - chakraPos);

		var chakraTwist = Vector3.Cross (chakraEnd - chakraPos, pos - chakraPos).normalized * -0.4f;

		var nearestPosOnLine = chakraPos + (chakraFwd * alongDist * 0.3f);
		var r = (pos - nearestPosOnLine);
		var dist = (1.2f / delta.magnitude);
		var inpct = Mathf.Min( dist * 6.0f, (0.6f / (r.magnitude * r.magnitude)) );
		//var toline = r.normalized * (-inpct);
		//var tocenter = chakraFwd.normalized * (dist);
		var toline = r.normalized * (-dist);
		var tocenter = (chakraFwd + chakraTwist).normalized * (inpct);

		return (toline + tocenter) * 1.5f;
	}

	void UpdateCellFieldDir(bool snapToCurrent=false) {
		if (this.IsPaused)
			return;
		if (this.Hand)
			return;
		bool isShowing = true;
		if (this.ExcersizeInst) {
			isShowing = this.ExcersizeInst.IsUseVectorField ();
			if (!isShowing)
				return;
		}

		var mainTrans = this.transform;
		bool cOneWay = false;

		if (this.Body) {
			var chakras = this.Body.Chakras.AllChakras;
			var chakra = chakras [((int)(Time.time * 0.5f)) % chakras.Length];

			if ((CurrentFocusChakra >= 0)) {
					chakra = chakras [CurrentFocusChakra];
				} else {
					return;
				}

			mainTrans = chakra.transform;
			cOneWay = chakra.ChakraOneWay;
		}
		if (this.ChiBall) {
			mainTrans = this.ChiBall.transform;
		}
		var cpos = mainTrans.position;
		var cdir = -mainTrans.up;
		var crot = mainTrans.rotation;

		var avgSum = 0.0f;
		var avgCnt = 0;

		var cells = this.FieldsCells;
		var cnt = cells.Header.TotalCount;
		for (int i = 0; i < cnt; i++) {
			var c = cells.Array [i];
			//c.Direction = chakra.transform.position - c.Pos;
			//c.Direction = MagneticDipoleField(c.Pos, cpos, cdir) / UnitMagnitude;
			//var newDir = ChakraDipoleField(c.Pos, cpos, cdir, cOneWay);
			Vector3 newDir;
			Color primeColor = Color.white;
			if (this.ExcersizeInst) {
				newDir = this.ExcersizeInst.CalcVectorField (this, i, c.Pos, out primeColor);
			} else if (this.ChiBall) {
				newDir = this.ChiBall.CalcVectorField (this, i, c.Pos, out primeColor);
			} else {
				newDir = ChakraFieldV3 (c.Pos, cpos, crot, cOneWay);
			}


			var newClr = Color.Lerp (primeColor, Color.white, 0.61f); // should be white with hint of color for clean prana
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
		if (this.Hand) {
			this.FieldOverallAlpha = this.ExcersizeSystem.Breath.UnitFadeInPct;
			return;
		}
		if (this.ChiBall) {
			this.FieldOverallAlpha = 1.0f; 
			return;
		}
		this.ExcersizeInst = null;
		var bestInst = this.ExcersizeInst;
		float bestInstScore = 0.0f;
		if (this.ExcersizeSystem) {
			var cur = this.ExcersizeSystem.CurrentActivity;
			var wantPause = true;
			var overallAlpha = 1.0f;
			if (cur) {
				ChakraBreath chakraExcer = null;
				ChakraBreath infoChakra = null;
				foreach (var inst in cur.Instances) {
					{
						float score = Vector3.Dot ((inst.Body.SpineEnd.transform.position - Camera.main.transform.position).normalized, Camera.main.transform.forward);
						if ((!bestInst) || (score > bestInstScore)) {
							bestInst = inst;
							bestInstScore = score;
						}
					}
					if (inst.Body == this.Body) {
						this.ExcersizeInst = inst;
					}
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
				if (this.ExcersizeInst != bestInst) {
					this.ExcersizeInst = null; // disable the model that isn't in front of user
				}
				if (!chakraExcer) {
					//this.ExcersizeInst = null; // DISABLING NON CHAKRA EXCERSIZE
				}
				if (infoChakra && infoChakra.FocusChakra) {
					if (chakraExcer != infoChakra) {
						chakraExcer = null; // if teacher, and info is active, hide the teacher
						this.ExcersizeInst = null;
					}
				}
				if ((this.ExcersizeInst)) {
					if ((chakraExcer && chakraExcer.CurrentChakra)) {
						this.CurrentFocusChakra = chakraExcer.CurrentChakra.ChakraIndex - 1;
						overallAlpha = Mathf.Lerp (0.35f, 1.0f, chakraExcer.LatestBreathAlpha); // doesn't fully fade
					}
					overallAlpha = this.ExcersizeSystem.Breath.UnitFadeInPct;
					wantPause = false;
				}
			}
			this.IsPaused = (wantPause);
			this.FieldOverallAlpha = overallAlpha;
		}
	}
	
	// Update is called once per frame
	void Update () {
		DEBUG_IsPaused = this.IsPaused;
		this.EnsureSetup ();

		// update regarless of being paused (to keep particles moving):
		this.ParticleFlowTime += ParticleFlowRate * Time.deltaTime;

		this.UpdateCurrentSelection ();
		this.UpdateCellFieldDir	();

	}
}
