using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
	

	partial class Program : MyGridProgram
	{
        readonly double timeperframe = 0;

		/*void SetTimePerFrame() {
			switch (update_frequency)
			{ //saving it just in case
				case UpdateFrequency.Update1:
					timeperframe = 1.0 / 60.0;
					break;
				case UpdateFrequency.None:
					timeperframe = 1.0 / 60.0;
					break;
				case UpdateFrequency.Once:
					timeperframe = 1.0 / 60.0;
					break;
				case UpdateFrequency.Update10:
					timeperframe = 1.0 / 6.0;
					break;
				case UpdateFrequency.Update100:
					timeperframe = 1.0 / 0.6;
					break; 
			}
		}*/

		public Program()
		{
			string[] stg = Storage.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
			if (stg.Length == 2)
			{
				oldTag = stg[0]; //loading tag
				greedy = bool.Parse(stg[1]); //loading greedy
			}

			timeperframe = 1.0 / 60.0; //SetTimePerFrame();
			Runtime.UpdateFrequency = update_frequency;
			RT = new RuntimeTracker(this, 60, 0.005);
			BM = new SimpleTimerSM(this, BlockManager(), true);
			BS = new SimpleTimerSM(this, GetBatStatsSeq(), true);
			MainChecker = new SimpleTimerSM(this, CheckVectorThrustersSeq(), true);
			GetScreen = new SimpleTimerSM(this, GetScreensSeq(), true);
			GetControllers = new SimpleTimerSM(this, GetControllersSeq(), true);
			GetVectorThrusters = new SimpleTimerSM(this, GetVectorThrustersSeq(), true);
			Echo("Program()");
		}

		public void Save()
		{
			string save = string.Join(";", string.Join(":", tag, greedy));
			Storage = save; //saving the old tag and greedy to prevent recompile or script update confusion
		}

		public void Main(string argument, UpdateType runType)
		{

			// ========== STARTUP ==========

			globalAppend = false;
			RT.AddRuntime();
			pc++; if (pc % 999 == 0) log.Clear();

			// writes and clear outputs
			Echo(echosb.ToString());
			Write(screensb.ToString());
			echosb.Clear();
			screensb.Clear();
			// ------------------------

			argument = argument.ToLower();
			bool tagArg =
			argument.Contains(applyTagsArg) ||
			argument.Contains(cruiseArg) ||
			argument.Contains(removeTagsArg);

			if (justCompiled || RT.configtrigger)
			{
				RT.configtrigger = false;
				Config();
				ManageTag();
			}
			if ((justCompiled && (controllers.Count == 0 || argument.Contains(resetArg)) && !Init()) || shutdown) {
				Echo(log.ToString());
				Runtime.UpdateFrequency = UpdateFrequency.None;
				return;
			}

			// END STARTUP

			// GETTING ONLY NECESARY INFORMATION TO THE SCRIPT

			MyShipVelocities shipVelocities = controlledControllers[0].TheBlock.GetShipVelocities();
			shipVelocity = shipVelocities.LinearVelocity;
			sv = shipVelocity.Length();

			bool damp = controlledControllers[0].TheBlock.DampenersOverride;
			bool dampchanged = damp != oldDampeners;
			oldDampeners = controlledControllers[0].TheBlock.DampenersOverride;

			Vector3D desiredVec = GetMovementInput(argument);
			mvin = desiredVec.Length();

			// END NECESARY INFORMATION
			

			//START OUTPUT PRINTING
			if (ShowMetrics) screensb.GetSpinner(ref pc).Append($" {Runtime.LastRunTimeMs.Round(2)}ms ").GetSpinner(ref pc);
			screensb.Append(progressbar);

			screensb.Append("\n").GetSpinner(ref pc).Append(trueaccel).GetSpinner(ref pc);
			screensb.AppendLine($"\nCruise: {cruise}");
			if (normalThrusters.Count == 0) screensb.AppendLine($"Dampeners: {dampeners}");
			if (ShowMetrics)
			{
				screensb.AppendLine($"\nAM: {(accel / gravLength).Round(2)}g");
				screensb.AppendLine($"Active VectorThrusters: {vectorthrusters.Count}");
				screensb.AppendLine($"Main/Ref Cont: {mainController.TheBlock.CustomName}");
			}
			echosb.GetSpinner(ref pc).Append(" VTos ").GetSpinner(ref pc);
			echosb.AppendLine($"\n\n--- Main ---");
			echosb.AppendLine(" >Remaining: " + RT.tremaining);
			echosb.AppendLine(" >Greedy: " + greedy);
			echosb.AppendLine($" >Angle Objective: {totalVTThrprecision.Round(1)}%");
			echosb.AppendLine($" >Main/Reference Controller: {mainController.TheBlock.CustomName}");
			echosb.AppendLine($" >Parked: {parked}");
		
			if (isstation) echosb.AppendLine("CAN'T FLY A STATION, RUNNING WITH LESS RUNTIME.");

			//END PRINTER PART 1


			// SKIPFRAME AND PERFORMANCE HANDLER
			
			CheckWeight();
			MainChecker.Run();//RUNS VARIOUS PROCESSES SEPARATED BY A TIMER

			if (argument.Equals("") && !cruise && !dampchanged)
			{ //handler to skip frames, it passes out if the player doesn't parse any command or do something relevant.
				bool handlers = false;
				if (!shutdown && !isstation)
				{
					if (PerformanceHandler()) handlers = true;
					if (ParkHandler()) handlers = true;
					if (VTThrHandler()) handlers = true;
				}
				else return;

				if (handlers)
				{
					echosb.AppendLine("Required Force: ---N");
					echosb.AppendLine("Total Force: ---N\n");
					echosb = RT.Append(echosb);
					echosb.AppendLine("--- Log ---");
					echosb.Append(log);
					RT.AddInstructions();
					return;
				}
			}
			else if (tagArg && !MainTag(argument)) { //SEE IF IS NEEDED TO PASS THE ENTIRE RUN FRAME IF THERE'S AN ARGUMENT (IT IS)
				Runtime.UpdateFrequency = UpdateFrequency.None;
				return;
				//HANDLES TAG ARGUMENTS, IF IT FAILS, IT STOPS
			}
			// END SKIPFRAME




			// ========== PHYSICS ==========
			//TODO: SEE IF I CAN SPLIT AT LEAST SOME OF THE STEPS BY SEQUENCES


			float shipMass = myshipmass.PhysicalMass;

			Vector3D worldGrav = controlledControllers[0].TheBlock.GetNaturalGravity();
			gravLength = (float)worldGrav.Length();

			bool gravChanged = Math.Abs(lastGrav - gravLength) > 0.05f;
			foreach (VectorThrust n in vectorthrusters)
				if (!n.ValidateThrusters() || gravChanged) n.DetectThrustDirection();

			wgv = lastGrav = gravLength;

			// setup gravity
			if (gravLength < gravCutoff)
			{
				gravLength = zeroGAcceleration;
				thrustModifierAbove = thrustModifierAboveSpace;
				thrustModifierBelow = thrustModifierBelowSpace;
			}
			else
			{
				thrustModifierAbove = thrustModifierAboveGrav;
				thrustModifierBelow = thrustModifierBelowGrav;
			}

			// f=ma
			Vector3D shipWeight = shipMass * worldGrav;

			if (dampeners)
			{
				Vector3D dampVec = Vector3D.Zero;
				if (desiredVec != Vector3D.Zero)
				{
					// cancel movement opposite to desired movement direction
					if (Extensions.Dot(desiredVec, shipVelocity) < 0)
					{
						//if you want to go oppisite to velocity
						dampVec += shipVelocity.Project(desiredVec.Normalized());
					}
					// cancel sideways movement
					dampVec += shipVelocity.Reject(desiredVec.Normalized());
				}
				else
				{
					dampVec += shipVelocity;
				}


				if (cruise)
				{
					if (cruiseThr.Count > 0 && !cruisedNT)
					{
						cruisedNT = true;
						foreach (IMyFunctionalBlock b in cruiseThr) b.Enabled = false;
					}

					foreach (ShipController cont in controlledControllers)
					{
						if (OnlyMain() && cont != mainController) continue;
						if (!cont.TheBlock.IsUnderControl) continue;

						if (Extensions.Dot(dampVec, cont.TheBlock.WorldMatrix.Forward) > 0 || cruisePlane)
						{ // only front, or front+back if cruisePlane is activated
							dampVec -= dampVec.Project(cont.TheBlock.WorldMatrix.Forward);
						}

						if (cruisePlane)
						{
							shipWeight -= shipWeight.Project(cont.TheBlock.WorldMatrix.Forward);
						}
					}
				}
				else if (!cruise && cruisedNT)
				{
					cruisedNT = false;
					foreach (IMyFunctionalBlock b in cruiseThr) b.Enabled = true;
				}
				desiredVec -= dampVec * dampenersModifier;
			}

			// f=ma
			accel = GetAcceleration(gravLength);

			//if (ShowMetrics) (accel / gravLength);

			desiredVec *= shipMass * (float)accel;

			// point thrust in opposite direction, add weight. this is force, not acceleration
			Vector3D requiredVec = -desiredVec + shipWeight;

			// remove thrust done by normal thrusters
			foreach (IMyThrust t in normalThrusters)
			{
				requiredVec -= -1 * t.WorldMatrix.Backward * t.CurrentThrust;
			}

			double len = requiredVec.Length();
			echosb.AppendLine($"Required Force: {len.Round(0)}N");
			// ========== END OF PHYSICS ==========


			// ========== DISTRIBUTE THE FORCE EVENLY BETWEEN NACELLES ==========

			double force = gravCutoff * shipMass;
			double cutoffcruise = lowThrustCutCruiseOff * force;
			double cutoncruise = lowThrustCutCruiseOn * force;

			if (((!cruise && sv > lowThrustCutOn) || (cruise && len > cutoncruise)) || mvin != 0 || dampchanged && dampeners)
			{//this not longer causes problems if there are many small nacelles (SOLVED)
				thrustOn = true;
				accelExponent_A = Accelerations[gear];
			}
			if (mvin == 0)
			{
				accelExponent_A = 0;
				if ((wgv == 0 && ((!cruise && sv < lowThrustCutOff) || ((cruise || !dampeners) && len < cutoffcruise))) || (parked && alreadyparked))
					thrustOn = false;
			}

			if (!thrustOn)
			{// Zero G
				Vector3D zero_G_accel;
				if (mainController != null)
				{
					zero_G_accel = (mainController.TheBlock.WorldMatrix.Down + mainController.TheBlock.WorldMatrix.Backward) * zeroGAcceleration / 1.414f;
				}
				else
				{
					zero_G_accel = (controlledControllers[0].TheBlock.WorldMatrix.Down + controlledControllers[0].TheBlock.WorldMatrix.Backward) * zeroGAcceleration / 1.414f;
				}
				if (dampeners)
				{
					requiredVec = zero_G_accel * shipMass + requiredVec;
				}
				else
				{
					requiredVec = (requiredVec - shipVelocity) + zero_G_accel;
				}
				setTOV = true;
			}

			// Correct for misaligned VTS
			Vector3D asdf = Vector3D.Zero;
			// 1
			foreach (List<VectorThrust> g in VTThrGroups)
			{
				g[0].requiredVec = requiredVec.Reject(g[0].rotor.TheBlock.WorldMatrix.Up);
				asdf += g[0].requiredVec;
			}
			// 2
			asdf -= requiredVec;
			// 3
			foreach (List<VectorThrust> g in VTThrGroups)
			{
				g[0].requiredVec -= asdf;
			}
			// 4
			asdf /= VTThrGroups.Count;
			// 5
			foreach (List<VectorThrust> g in VTThrGroups)
			{
				g[0].requiredVec += asdf;
			}
			// apply first VT settings to rest in each group
			double total = 0;
			int j = 0;
			totalVTThrprecision = 0;
			string edge = Separator();

			StringBuilder info = new StringBuilder($"{Separator("[Metrics]")}\n");
			if (ShowMetrics) {
				info.Append("| Axis |=> | VTLength | MaxRPM | Far% |\n")
					.Append(edge);
			}

			totaleffectivethrust = 0;

			foreach (List<VectorThrust> g in VTThrGroups)
			{
				double precision = 0;
				Vector3D req = g[0].requiredVec / g.Count;
				for (int i = 0; i < g.Count; i++)
				{
					IMyMotorStator rt = g[i].rotor.TheBlock;
					//Echo(rt.CustomName + ": " + GridTerminalSystem.CanAccess(rt) + "/" + rt.Closed + "/" + rt.IsWorking+ "/" + rt.IsAlive());

					if (GridTerminalSystem.CanAccess(rt) && !rt.Closed && rt.IsWorking && rt.IsAlive() && rt.Top != null)
					{
						g[i].requiredVec = req;
						g[i].thrustModifierAbove = thrustModifierAbove;
						g[i].thrustModifierBelow = thrustModifierBelow;
						g[i].Go();

						totaleffectivethrust += g[i].totalEffectiveThrust;

						total += req.Length();

						totalVTThrprecision += g[i].old_angleCos;
						precision += g[i].old_angleCos;
						j++;

						if (i == 0 && ShowMetrics) info.Append($"\n| {g[i].Role} |=>")
								.Append($" | {(req.Length() / RotorStMultiplier).Round(1)}")
								.Append($" |  {g[i].rotor.maxRPM.Round(0)} ");
					}
				}
				if(ShowMetrics) info.Append($" |  {(precision / g.Count).Round(1)}%  |\n");
			}
			if (ShowMetrics) info.Append(edge);
			totalVTThrprecision /= j;

			totaleffectivethrust /= myshipmass.PhysicalMass;

			if (justCompiled || argument.Contains(gearArg)) GenerateProgressBar();
				
			echosb.AppendLine($"Total Force: {total.Round(0)}N\n");
			echosb = RT.Append(echosb);
			echosb.AppendLine("--- Log ---");
			echosb.Append(log);

			if (ShowMetrics) { 
				echosb.Append(info);
				screensb.Append(info);
			}

			//TODO: make activeNacelles account for the number of nacelles that are actually active (activeThrusters.Count > 0) (I GUESS SOLVED??)
			// ========== END OF MAIN ==========

			log.AppendNR(surfaceProviderErrorStr);
			justCompiled = false;
			RT.AddInstructions();
		}

		//arguments, you can change these to change what text you run the programmable block with
		const string dampenersArg = "dampeners";
		const string cruiseArg = "cruise";
		const string raiseAccelArg = "raiseaccel";
		const string lowerAccelArg = "loweraccel";
		const string resetAccelArg = "resetaccel";
		const string gearArg = "gear";
		const string resetArg = "reset"; //this one re-runs the initial setup (init() method) ... you probably want to use %resetAccel
		const string applyTagsArg = "applytags";
		const string applyTagsAllArg = "applytagsall";
		const string removeTagsArg = "removetags";

		// wether or not cruise mode is on when you start the script
		readonly float maxRotorRPM = 60f; // set to -1 for the fastest speed in the game (changes with mods)

		readonly List<IMyTerminalBlock> parkblocks = new List<IMyTerminalBlock>();
		readonly List<IMyTerminalBlock> tankblocks = new List<IMyTerminalBlock>();
		readonly List<IMyTerminalBlock> batteriesblocks = new List<IMyTerminalBlock>();
		readonly List<IMyTerminalBlock> gridbats = new List<IMyTerminalBlock>();
		readonly List<IMyTerminalBlock> cruiseThr = new List<IMyTerminalBlock>();
		readonly List<List<VectorThrust>> VTThrGroups = new List<List<VectorThrust>>();
		readonly List<double> MagicNumbers = new List<double> { -0.091, 0.748, -46.934, -0.073, 0.825, -4.502, -1.239, 1.124, 2.47 };

		readonly RuntimeTracker RT;
		readonly SimpleTimerSM BM;
		readonly SimpleTimerSM BS;

		bool parked = false;
		bool alreadyparked = false;
		bool cruisedNT = false;
		bool setTOV = false;
		bool TagAll = false;
		bool shutdown = false;
		bool oldDampeners = false;
		bool isstation = false;
		double wgv = 0;
		double mvin = 0;
		double accel = 0;
		float gravLength = 0;
		string oldTag = "";
		StringBuilder echosb = new StringBuilder();
		readonly StringBuilder screensb = new StringBuilder();
		readonly StringBuilder log = new StringBuilder();
		long pc = 0;
		MyShipMass myshipmass;


		readonly SimpleTimerSM MainChecker;
		readonly SimpleTimerSM GetScreen;
		readonly SimpleTimerSM GetControllers;
		readonly SimpleTimerSM GetVectorThrusters;

		double maxaccel = 0;
		readonly StringBuilder progressbar = new StringBuilder();
		string trueaccel = "";
		readonly bool ignoreHiddenBlocks = false; //OBSOLETE
												  // DEPRECATED: use the tags instead
												  // only use blocks that have 'show in terminal' set to true

		bool cruise = false;
		bool dampeners = true;
		string textSurfaceKeyword = "VT:";
		string LCDName = "VTLCD";
		const float defaultAccel = 1f;
		const float accelBase = 1.5f;//accel = defaultAccel * g * base^exponent
									 // your +, - and 0 keys increment, decrement and reset the exponent respectively
									 // this means increasing the base will increase the amount your + and - change target acceleration
		const float dampenersModifier = 0.1f; // multiplier for dampeners, higher is stronger dampeners		 
		const float zeroGAcceleration = 9.81f; // default acceleration in situations with 0 (or low) gravity				 
		const float gravCutoff = 0.1f * zeroGAcceleration;  // if gravity becomes less than this, zeroGAcceleration will kick in (I think it's deprecated)
		const bool onlyMainCockpit = true; // Almost deprecated, it assigns main cockpit to the first that it's being controlled
		const UpdateFrequency update_frequency = UpdateFrequency.Update1;
		// choose weather you want the script to											 
		// update once every frame, once every 10 frames, or once every 100 frames (Recommended not modifying it)

		// Control Module params... this can always be true, but it's deprecated
		bool controlModule = true;
		const string dampenersButton = "c.damping";
		const string cruiseButton = "c.cubesizemode";
		const string lowerAccel = "c.switchleft";
		const string raiseAccel = "c.switchright";
		const string resetAccel = "pipe";


		double totaleffectivethrust = 0;
		string surfaceProviderErrorStr = "";
		int accelExponent = 0;
		double accelExponent_A = 0;

		double totalVTThrprecision = 0;
		bool rotorsstopped = false;

		public bool dampenersIsPressed = false;
		public bool cruiseIsPressed = false;
		public bool plusIsPressed = false;
		public bool minusIsPressed = false;
		public bool globalAppend = false;

		ShipController mainController = null;
		List<ShipController> controllers = new List<ShipController>();
		readonly List<IMyShipController> controllerblocks = new List<IMyShipController>();
		readonly List<IMyShipController> ccontrollerblocks = new List<IMyShipController>();
		readonly List<ShipController> controlledControllers = new List<ShipController>();
		readonly List<VectorThrust> vectorthrusters = new List<VectorThrust>();
		readonly List<IMyThrust> normalThrusters = new List<IMyThrust>();
		readonly List<IMyTextPanel> screens = new List<IMyTextPanel>();
		readonly List<IMyTerminalBlock> rechargedblocks = new List<IMyTerminalBlock>();
		readonly List<IMyTerminalBlock> turnedoffthusters = new List<IMyTerminalBlock>();
		readonly List<IMyTerminalBlock> backupbats = new List<IMyTerminalBlock>();
		public List<IMyTextSurface> surfaces = new List<IMyTextSurface>();

		float oldMass = 0;
		int frame = 0;

		public struct BA
		{
			public string button;
			public float accel;

			public BA(string button, float accel)
			{
				this.button = button;
				this.accel = accel;
			}
		}

		const bool useBoosts = true;
		public BA[] boosts = {
			new BA("c.sprint", 3f),
			new BA("ctrl", 0.3f)
		};

		// ------- Default configs --------
		string myName = "VT";
		double TimeForRefresh = 10;
		bool ShowMetrics = false;
		int SkipFrames = 0;
		

		double RotorStMultiplier = 1000;
		bool SlowThrustOff = false;
		double SlowThrustOffRPM = 5;
		double lowThrustCutOn = 0.5;
		double lowThrustCutOff = 0.01;
		double lowThrustCutCruiseOn = 1;
		double lowThrustCutCruiseOff = 0.15;

		double[] Accelerations = new double[] { 0, 3.7, 5.22 };
		int gear = 0;
		double gearaccel = 0;

		bool TurnOffThrustersOnPark = true;
		bool PerformanceWhilePark = false;
		bool ConnectorNeedsSuffixToPark = true;
		bool UsePIDPark = true;

		double thrustModifierAboveSpace = 0.1;
		double thrustModifierBelowSpace = 0.1;

		double thrustModifierAboveGrav = 0.1;
		double thrustModifierBelowGrav = 0.1;

		int RotationAverageSamples = 1;
		string[] tagSurround = new string[] { "|", "|" };
		bool UsePID = false;
		bool cruisePlane = false; // make cruise mode act more like an airplane
		int FramesBetweenActions = 1;

		// ------- End default configs ---------


		// START CONFIG STRINGS AND VARS
		readonly MyIni config = new MyIni();
		readonly MyIni configCD = new MyIni();

		const string inistr = "--------Vector Thrust 2 Settings--------";

		const string detectstr = "Vector Thruster Stabilization";
		const string accelstr = "Acceleration Settings";
		const string parkstr = "Parking Settings";
		const string miscstr = "MISC";

		const string myNameStr = "Name Tag";
		const string TagSurroundStr = "Tag Surround Char(s)";
		const string ShowMetricsStr = "Show Metrics";
		const string TimeForRefreshStr = "Time For Each Refresh";
		const string SkipFramesStr = "Skip Frames";
		
		

		const string RotorStMultiplierStr = "Rotor Stabilization Multiplier";
		const string lowThrustCutStr = "Calculated Velocity To Turn On/Off VectorThrusters";
		const string lowThrustCutCruiseStr = "Calculated Velocity To Turn On/Off VectorThrusters In Cruise";
		const string SlowThrustOffStr = "Slow Reposition Of Rotors On Turn Off";
		const string SlowThrustOffRPMStr = "Slow Rotor Reposition Value (RPM)";

		const string AccelerationsStr = "Accelerations";
		const string gearStr = "Starting Acceleration Position";

		const string TurnOffThrustersOnParkStr = "Turn Off Thrusters On Park";
		const string PerformanceWhileParkStr = "Run Script Each 100 Frames When Parked";
		const string ConnectorNeedsSuffixToParkStr = "Connector Needs Suffix To Toggle Park Mode";
		const string UsePIDParkStr = "Use PID Controller to Handle Parking";

		const string thrustModifierSpaceStr = "Thruster Modifier Turn On/Off Space";
		const string thrustModifierGravStr = "Thruster Modifier Turn On/Off Gravity";

		const string RotationAverageSamplesStr = "Rotor Velocity Average Samples";
		const string UsePIDStr = "Use PID Controller";
		const string cruisePlaneStr = "Cruise Mode Act Like Plane";
		const string FramesBetweenActionsStr = "Frames Per Operation: Block Assigner";

		// END STRINGS AND VARS

		void Config()
		{
			config.Clear();
			double[] defaultltc = new double[] { 0.5, 0.01 };
			double[] defaultltcc = new double[] { 1, 0.15 };
			double[] defaultacc = new double[] { 0, 3.7, 5.22 };
			double[] defaulttms = new double[] { 0.1, 0.1 };
			double[] defaulttmg = new double[] { 0.1, 0.1 };

			bool force = false;
			KeepConfig();

			if (config.TryParse(Me.CustomData))
			{
				myName = config.Get(inistr, myNameStr).ToString(myName);
				if (string.IsNullOrEmpty(myName)) {
					myName = "VT";
					force = true;
				}
				textSurfaceKeyword = $"{myName}:";
				LCDName = $"{myName}LCD";

				string sstr = config.Get(inistr, TagSurroundStr).ToString();
				int sstrl = sstr.Length;

				if (sstrl == 1)
				{
					tagSurround = new string[] { sstr, sstr };
				}
				else if (sstrl > 1 && sstrl % 2 == 0)
				{
					string first = sstr.Substring(0, (int)(sstr.Length / 2));
					string last = sstr.Substring((int)(sstr.Length / 2), (int)(sstr.Length / 2));
					tagSurround = new string[] { first, last };
				}
				else
				{
					force = true;
					tagSurround = new string[] { "|", "|" };
				}

				TimeForRefresh = config.Get(inistr, TimeForRefreshStr).ToDouble(TimeForRefresh);
				ShowMetrics = config.Get(inistr, ShowMetricsStr).ToBoolean(ShowMetrics);
				SkipFrames = config.Get(inistr, SkipFramesStr).ToInt32(SkipFrames);

				RotorStMultiplier = config.Get(detectstr, RotorStMultiplierStr).ToDouble(RotorStMultiplier);
				SlowThrustOff = config.Get(detectstr, SlowThrustOffStr).ToBoolean(SlowThrustOff);
				SlowThrustOffRPM = config.Get(detectstr, SlowThrustOffRPMStr).ToDouble(SlowThrustOffRPM);
				string temp = config.Get(detectstr, lowThrustCutStr).ToString();
				try
				{
					double[] result = Array.ConvertAll(temp.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries), s => double.Parse(s));
					if (result.Length != 2) { force = true; result = defaultltc; }
					lowThrustCutOn = result[0];
					lowThrustCutOff = result[1];
				}
				catch
				{
					force = true;
					lowThrustCutOn = defaultltc[0];
					lowThrustCutOff = defaultltc[1];
				}

				temp = config.Get(detectstr, lowThrustCutCruiseStr).ToString();
				try
				{
					double[] result = Array.ConvertAll(temp.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries), s => double.Parse(s));
					if (result.Length != 2) { force = true; result = defaultltcc; }
					lowThrustCutCruiseOn = result[0];
					lowThrustCutCruiseOff = result[1];
				}
				catch
				{
					force = true;
					lowThrustCutCruiseOn = defaultltcc[0];
					lowThrustCutCruiseOff = defaultltcc[1];
				}

				temp = config.Get(accelstr, AccelerationsStr).ToString();
				try
				{
					Accelerations = Array.ConvertAll(temp.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries), s => double.Parse(s));
					if (Accelerations.Length < 2) { force = true; Accelerations = defaultacc; }
				}
				catch { force = true; Accelerations = defaultacc; }

				if (justCompiled)
				{
					int geardef = config.Get(accelstr, gearStr).ToInt32();
					gear = geardef > Accelerations.Length - 1 ? gear : geardef;
				}

				TurnOffThrustersOnPark = config.Get(parkstr, TurnOffThrustersOnParkStr).ToBoolean(TurnOffThrustersOnPark);
				PerformanceWhilePark = config.Get(parkstr, PerformanceWhileParkStr).ToBoolean(PerformanceWhilePark);
				ConnectorNeedsSuffixToPark = config.Get(parkstr, ConnectorNeedsSuffixToParkStr).ToBoolean(ConnectorNeedsSuffixToPark);
				UsePIDPark = config.Get(parkstr, UsePIDParkStr).ToBoolean(UsePIDPark);

				temp = config.Get(miscstr, thrustModifierSpaceStr).ToString();
				try
				{
					double[] result = Array.ConvertAll(temp.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries), s => double.Parse(s));
					if (result.Length != 2) { force = true; result = defaulttms; }
					thrustModifierAboveSpace = result[0];
					thrustModifierBelowSpace = result[1];
				}
				catch
				{
					force = true;
					thrustModifierAboveSpace = defaulttms[0];
					thrustModifierBelowSpace = defaulttms[1];
				}

				temp = config.Get(miscstr, thrustModifierGravStr).ToString();
				try
				{
					double[] result = Array.ConvertAll(temp.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries), s => double.Parse(s));
					if (result.Length != 2) { force = true; result = defaulttmg; }
					thrustModifierAboveGrav = result[0];
					thrustModifierBelowGrav = result[1];
				}
				catch
				{
					force = true;
					thrustModifierAboveGrav = defaulttmg[0];
					thrustModifierBelowGrav = defaulttmg[1];
				}
				RotationAverageSamples = config.Get(miscstr, RotationAverageSamplesStr).ToInt32(RotationAverageSamples);

				UsePID = config.Get(miscstr, UsePIDStr).ToBoolean(UsePID);
				cruisePlane = config.Get(miscstr, cruisePlaneStr).ToBoolean(cruisePlane);
				FramesBetweenActions = config.Get(miscstr, FramesBetweenActionsStr).ToInt32(FramesBetweenActions);
				if (FramesBetweenActions <= 0) {
					FramesBetweenActions = 1;
					force = true;
				}
				timepause = FramesBetweenActions * timeperframe;
			}

			SetConfig(force);
			RConfig(config.ToString(), force);
		}

		double timepause = 0;
		void SetConfig(bool force)
		{

			double[] defaultltc = new double[] { 0.5, 0.01 };
			double[] defaultltcc = new double[] { 1, 0.15 };
			double[] defaultacc = new double[] { 0, 3.7, 5.22 };
			double[] defaulttms = new double[] { 0.1, 0.1 };
			double[] defaulttmg = new double[] { 0.1, 0.1 };

			config.Set(inistr, myNameStr, myName);
			string sstr = tagSurround[0].Equals(tagSurround[1]) ? tagSurround[0] : tagSurround[0] + tagSurround[1];
			config.Set(inistr, TagSurroundStr, sstr);
			config.Set(inistr, TimeForRefreshStr, TimeForRefresh);
			config.Set(inistr, ShowMetricsStr, ShowMetrics);
			config.Set(inistr, SkipFramesStr, SkipFrames);

			config.Set(detectstr, RotorStMultiplierStr, RotorStMultiplier);
			config.Set(detectstr, SlowThrustOffStr, SlowThrustOff);
			config.Set(detectstr, SlowThrustOffRPMStr, SlowThrustOffRPM);
			string ltcstr = String.Join(",", force ? defaultltc : new double[] { lowThrustCutOn, lowThrustCutOff });
			config.Set(detectstr, lowThrustCutStr, ltcstr);
			string ltccstr = String.Join(",", force ? defaultltcc : new double[] { lowThrustCutCruiseOn, lowThrustCutCruiseOff });
			config.Set(detectstr, lowThrustCutCruiseStr, ltccstr);

			string accstr = String.Join(",", force ? defaultacc : Accelerations);
			config.Set(accelstr, AccelerationsStr, accstr);
			if (justCompiled) config.Set(accelstr, gearStr, gear);

			config.Set(parkstr, TurnOffThrustersOnParkStr, TurnOffThrustersOnPark);
			config.Set(parkstr, PerformanceWhileParkStr, PerformanceWhilePark);
			config.Set(parkstr, ConnectorNeedsSuffixToParkStr, ConnectorNeedsSuffixToPark);
			config.Set(parkstr, UsePIDParkStr, UsePIDPark);

			string tmsstr = String.Join(",", force ? defaulttms : new double[] { thrustModifierAboveSpace, thrustModifierBelowSpace });
			string tmgstr = String.Join(",", force ? defaulttms : new double[] { thrustModifierAboveGrav, thrustModifierBelowGrav });

			config.Set(miscstr, thrustModifierSpaceStr, tmsstr);
			config.Set(miscstr, thrustModifierGravStr, tmgstr);
			config.SetComment(miscstr, thrustModifierSpaceStr, "\n-Thruster Modifier-\nHow far needs the thruster to turn on and off from desired angle.\n Space:");
			config.SetComment(miscstr, thrustModifierGravStr, "\n Gravity:");
			config.Set(miscstr, RotationAverageSamplesStr, RotationAverageSamples);
			config.Set(miscstr, UsePIDStr, UsePID);
			config.Set(miscstr, cruisePlaneStr, cruisePlane);
			config.Set(miscstr, FramesBetweenActionsStr, FramesBetweenActions);
		}

		void RConfig(string output, bool force = false)
		{
			if (force || output != Me.CustomData) Me.CustomData = output;
			try { if (!force && !Me.CustomData.Contains($"\n---\n{textSurfaceKeyword}0")) Me.CustomData = Me.CustomData.Replace(Me.CustomData.Between("\n---\n", "0")[0], textSurfaceKeyword); }
			catch { if (!justCompiled) log.AppendNR("No tag found textSufaceKeyword\n"); }
			if (!force && !Me.CustomData.Contains($"\n---\n{textSurfaceKeyword}0")) Me.CustomData += $"\n---\n{textSurfaceKeyword}0";
		}

		void KeepConfig()
		{
			if (justCompiled && configCD.TryParse(Me.CustomData))
			{
				SetConfig(false);
				List<MyIniKey> ccd = new List<MyIniKey>();
				List<MyIniKey> ccfg = new List<MyIniKey>();

				configCD.GetKeys(ccd);
				config.GetKeys(ccfg);

				foreach (MyIniKey cd in ccd)
				{
					foreach (MyIniKey cfg in ccfg)
					{
						if (cd.Equals(cfg)) config.Set(cfg, configCD.Get(cd).ToString());
					}
				}
				RConfig(config.ToString(), true);
			}
			configCD.Clear();
		}

		// END ENTIRE CONFIG HANDLER


		// RUNTIME TRACKER BY WHIPLASH, THANK YOU!!! :)
		class RuntimeTracker
		{
			public int Capacity { get; set; }
			public double Sensitivity { get; set; }
			public double MaxRuntime { get; private set; }
			public double MaxInstructions { get; private set; }
			public double AverageRuntime { get; private set; }
			public double AverageInstructions { get; private set; }
			public double LastRuntime { get; private set; }
			public double LastInstructions { get; private set; }

			readonly Queue<double> _runtimes = new Queue<double>();
			readonly Queue<double> _instructions = new Queue<double>();
			readonly StringBuilder _sb = new StringBuilder();
			readonly int _instructionLimit;
			readonly Program _program;
			const double MS_PER_TICK = 16.6666;
			double sumlastrun;
			public double tremaining = 0;
			public bool configtrigger = false;

			public RuntimeTracker(Program program, int capacity = 100, double sensitivity = 0.005)
			{
				_program = program;
				Capacity = capacity;
				Sensitivity = sensitivity;
				_instructionLimit = _program.Runtime.MaxInstructionCount;
			}

			public void AddRuntime()
			{
				double tfr = _program.TimeForRefresh;
				bool config = sumlastrun < tfr;

				if (!configtrigger && !config) {
					sumlastrun = 0;
				}

				if (config)
				{
					double tslrs = _program.Runtime.TimeSinceLastRun.TotalSeconds;
					sumlastrun += tslrs;
					tremaining = (tfr - sumlastrun).Round(1);
				}
				else {
					configtrigger = true;
				}

				double runtime = _program.Runtime.LastRunTimeMs;

				LastRuntime = runtime;
				AverageRuntime += (Sensitivity * runtime);
				int roundedTicksSinceLastRuntime = (int)Math.Round(_program.Runtime.TimeSinceLastRun.TotalMilliseconds / MS_PER_TICK);
				if (roundedTicksSinceLastRuntime == 1)
				{
					AverageRuntime *= (1 - Sensitivity);
				}
				else if (roundedTicksSinceLastRuntime > 1)
				{
					AverageRuntime *= Math.Pow((1 - Sensitivity), roundedTicksSinceLastRuntime);
				}

				_runtimes.Enqueue(runtime);
				if (_runtimes.Count == Capacity)
				{
					_runtimes.Dequeue();
				}

				MaxRuntime = _runtimes.Max();
			}

			public void AddInstructions()
			{
				double instructions = _program.Runtime.CurrentInstructionCount;
				LastInstructions = instructions;
				AverageInstructions = Sensitivity * (instructions - AverageInstructions) + AverageInstructions;

				_instructions.Enqueue(instructions);
				if (_instructions.Count == Capacity)
				{
					_instructions.Dequeue();
				}

				MaxInstructions = _instructions.Max();
			}

			public string Write()
			{
				_sb.Clear();
				_sb.AppendLine($"---Performance---");
				_sb.AppendLine($" -Instructions-");
				_sb.AppendLine($"   Avg: {AverageInstructions:n2}");
				_sb.AppendLine($"   Last: {LastInstructions:n0}");
				_sb.AppendLine($"   Max: {MaxInstructions:n0}");
				_sb.AppendLine($"   Avg complexity: {MaxInstructions / _instructionLimit:0.000}%");
				_sb.AppendLine($" -Runtime-");
				_sb.AppendLine($"   Avg: {AverageRuntime:n4} ms");
				_sb.AppendLine($"   Last: {LastRuntime:n4} ms");
				_sb.AppendLine($"   Max [{Capacity}]: {MaxRuntime:n4} ms");
				return _sb.ToString();
			}

			public StringBuilder Append(StringBuilder sba)
			{
				sba.AppendLine($"--- Performance ---");
				sba.AppendLine($" - Instructions -");
				sba.AppendLine($"   >Avg: {AverageInstructions:n2}");
				sba.AppendLine($"   >Last: {LastInstructions:n0}");
				sba.AppendLine($"   >Max: {MaxInstructions:n0}");
				sba.AppendLine($"   >Avg complexity: {MaxInstructions / _instructionLimit:0.000}%");
				sba.AppendLine($" - Runtime -");
				sba.AppendLine($"   >Avg: {AverageRuntime:n4} ms");
				sba.AppendLine($"   >Last: {LastRuntime:n4} ms");
				sba.AppendLine($"   >Max [{Capacity}]: {MaxRuntime:n4} ms");
				return sba;
			}
		}

		string Separator(string title = "", int len=58) {
			int tl = title.Length;
			len = (len-tl)/2;
			string res = new string('-', len);
			return res+title+res; 
		}

		Vector3D shipVelocity = Vector3D.Zero;
		double sv = 0;
		double thrustModifierAbove = 0.1;// how close the rotor has to be to target position before the thruster gets to full power
		double thrustModifierBelow = 0.1;// how close the rotor has to be to opposite of target position before the thruster gets to 0 power
		bool justCompiled = true;
		string tag = "|VT|";

		bool applyTags = false;
		//bool removeTags = false;
		bool greedy = true;
		float lastGrav = 0;
		bool thrustOn = true;
		Dictionary<string, object> CMinputs = null;
		bool rechargecancelled = false;
		double dischargingtimer = 0;


		void ManageTag(bool force = false, bool logthis = true)
		{
			tag = tagSurround[0] + myName + tagSurround[1];
			bool cond1 = oldTag.Length > 0;
			bool cond2 = !tag.Equals(oldTag) && Me.CustomName.Contains(oldTag);
			bool cond3 = greedy && Me.CustomName.Contains(oldTag);

			if (cond1 && (cond2 || cond3 || force))
			{
				if (logthis) log.AppendNR(" -Cleaning Tags To Prevent Future Errors, just in case\n");
				else log.AppendNR(" -Removing Tags\n");
				List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
				GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);
				foreach (IMyTerminalBlock block in blocks)
					//RemoveTag(block);
					block.CustomName = block.CustomName.Replace(oldTag, "").Trim();
			}
			this.greedy = !HasTag(Me);
			oldTag = tag;
		}

		int cnparks = 0;

		void ResetParkingSeq() {
			BS.Start();
			BM.Start();
		}

		void ResetVTHandlers()
		{
			GetScreen.Start();
			GetControllers.Start();
			GetVectorThrusters.Start();
			MainChecker.Start();
		}

		bool ParkHandler()
		{

			if (parkblocks.Count > 0)
			{
				int parks = 0;
				cnparks = 0;
				
				foreach (IMyTerminalBlock cn in parkblocks)
				{
					bool cnpark = (cn is IMyShipConnector) && ((IMyShipConnector)cn).Status == MyShipConnectorStatus.Connected;
					bool lgpark = (cn is IMyLandingGear) && ((IMyLandingGear)cn).IsLocked;

					if (cnpark || lgpark) parks++;
					if (cnpark) cnparks++;
				}
				if (parks > 0) parked = true;
				else parked = false;

				//Echo("p:"+parked+"/"+alreadyparked+"/"+totalVTThrprecision.Round(1));

				if (parked && !alreadyparked)
				{
					alreadyparked = true;
					ResetParkingSeq();
				}
				else if (!parked && alreadyparked && !BM.Doneloop)
				{
					alreadyparked = false;
					thrustOn = true;
					Runtime.UpdateFrequency = update_frequency;
					ResetParkingSeq();
					BM.Doneloop = true;
					BM.Run();

				} else if (BM.Doneloop) { //Using it sideways of the objective
					BM.Run();
				}
				else if (parked && alreadyparked && totalVTThrprecision.Round(1) == 100)
				{
					BM.Run();

					if (BM.Doneloop) {
						BM.Doneloop = false;
						if (PerformanceWhilePark && gravLength == 0 && Runtime.UpdateFrequency != UpdateFrequency.Update100) Runtime.UpdateFrequency = UpdateFrequency.Update100;
						else if ((!PerformanceWhilePark || gravLength > 0) && Runtime.UpdateFrequency != UpdateFrequency.Update10) Runtime.UpdateFrequency = UpdateFrequency.Update10;
					}

					screensb.AppendLine("PARKED");
					return alreadyparked;
				}
				else if (parked && alreadyparked && totalVTThrprecision.Round(1) != 100)
				{
					screensb.GetSpinner(ref pc).Append(" PARKING ").GetSpinner(ref pc, after: "\n");
				}
			}
			return false;
		}

		bool PerformanceHandler()
		{

			if (SkipFrames > 0)
			{
				echosb.AppendLine($"--SkipFrame[{ SkipFrames}]--");
				echosb.AppendLine($" >Skipped: {frame}");
				echosb.AppendLine($" >Remaining: {SkipFrames - frame}");
			}
			if (!justCompiled && SkipFrames > 0 && SkipFrames > frame)
			{
				frame++;
				return true;
			}
			else if (SkipFrames > 0 && frame >= SkipFrames) frame = 0;
			return false;
		}

		bool VTThrHandler()
		{
			if (totalVTThrprecision.Round(1) == 100 && setTOV && ((!thrustOn && mvin == 0) || (parked && alreadyparked)))
			{
				echosb.AppendLine("\nEverything stopped, performance mode.\n");
				if (!rotorsstopped)
				{
					rotorsstopped = true;
					StabilizeRotors();
				}
				return true;
			}
			else if (rotorsstopped && (!parked && !alreadyparked) && setTOV)
			{
				setTOV = false;
				rotorsstopped = false;
				StabilizeRotors(rotorsstopped);
			}
			return rotorsstopped;
		}

		bool MainTag(string argument)
		{
			//tags and getting blocks
			TagAll = argument.Contains(applyTagsAllArg);
			this.applyTags = argument.Contains(applyTagsArg) || TagAll;
			//this.removeTags = !this.applyTags && argument.Contains(Program.removeTagsArg);
			// switch on: removeTags
			// switch off: applyTags
			this.greedy = (!this.applyTags && this.greedy);//|| this.removeTags;
														   // this automatically calls getVectorThrusters() as needed, and passes in previous GTS data
			if (this.applyTags)
			{
				AddTag(Me);
			}
			else if (argument.Contains(removeTagsArg)) ManageTag(true, false); // New remove tags.
			/*else if (this.removeTags)
			{
				RemoveTag(Me);
			}*/
			OneRunMainChecker();

			TagAll = false;
			this.applyTags = false; //this.removeTags = false;
			return !shutdown;
		}

		void GroupVectorThrusters()
		{
			VTThrGroups.Clear();

			foreach (VectorThrust na in vectorthrusters)
			{
				bool foundGroup = false;

				foreach (List<VectorThrust> g in VTThrGroups)
				{
					if (na.Role == g[0].Role)
					{
						g.Add(na);
						foundGroup = true;
						break;
					}
				}
				if (!foundGroup)
				{// if it never found a group, add a group
					VTThrGroups.Add(new List<VectorThrust>());
					VTThrGroups[VTThrGroups.Count - 1].Add(na);
				}
			}
		}

		bool HasTag(IMyTerminalBlock block)
		{
			return block.CustomName.Contains(tag);
		}

		void AddTag(IMyTerminalBlock block)
		{
			string name = block.CustomName;
			if (!name.Contains(tag)) block.CustomName = tag + " " + name;
		}

		void RemoveTag(IMyTerminalBlock block)
		{
			block.CustomName = block.CustomName.Replace(tag, "").Trim();
		}

		// true: only main cockpit can be used even if there is no one in the main cockpit
		// false: any cockpits can be used, but if there is someone in the main cockpit, it will only obey the main cockpit
		// no main cockpit: any cockpits can be used
		bool OnlyMain()
		{
			return mainController != null && (mainController.TheBlock.IsUnderControl || onlyMainCockpit);
		}

		void Write(params object[] obj)
		{
			string sep = ", ";
			string init = obj[0].ToString();
			if (init.Contains("%sep%")) sep = init.Replace("%sep%", "");
			string result = string.Join(sep, obj) + "\n";
			if (this.surfaces.Count > 0)
			{
				foreach (IMyTextSurface surface in this.surfaces)
				{
					surface.WriteText(result, globalAppend);
					surface.ContentType = ContentType.TEXT_AND_IMAGE;
					surface.Alignment = TextAlignment.CENTER;
				}
			}
			else if (!globalAppend)
			{
				string err = "\nNo text surfaces available";
				string errs = " because the ship is a station."; 
				if (!justCompiled) { 
					log.AppendNR(err);
					if (isstation) {
						Echo(err+ errs);
						log.AppendNR(errs, false);
					}
				}
			}
			globalAppend = true;
		}

		double GetAcceleration(double gravity, double exp = 0)
		{
			// look through boosts, applies acceleration of first one found
			if (useBoosts && this.controlModule)
			{
				for (int i = 0; i < this.boosts.Length; i++)
				{
					if (this.CMinputs.ContainsKey(this.boosts[i].button))
					{
						return this.boosts[i].accel * gravity * defaultAccel;
					}
				}
			}

			double accelexpaval = exp == 0 ? accelExponent_A : exp;
			double gravtdefac = gravity * defaultAccel;

			//getting max & gear accel
			gearaccel = Math.Pow(accelBase, accelExponent + Accelerations[gear]) * gravtdefac;
			maxaccel = Math.Pow(accelBase, accelExponent + Accelerations[Accelerations.Length - 1]) * gravtdefac;
			//none found or boosts not enabled, go for normal accel
			return Math.Pow(accelBase, accelExponent + accelexpaval) * gravtdefac;
		}

		Vector3D GetMovementInput(string arg, bool perf = false)
		{
			Vector3D moveVec = Vector3D.Zero;

			if (controlModule)
			{
				// setup control module
				Dictionary<string, object> inputs = new Dictionary<string, object>();
				try
				{
					this.CMinputs = Me.GetValue<Dictionary<string, object>>("ControlModule.Inputs");
					Me.SetValue<string>("ControlModule.AddInput", "all");
					Me.SetValue<bool>("ControlModule.RunOnInput", true);
					Me.SetValue<int>("ControlModule.InputState", 1);
					Me.SetValue<float>("ControlModule.RepeatDelay", 0.016f);
				}
				catch
				{
					controlModule = false;
				}
			}

			if (controlModule)
			{
				// non-movement controls
				if (this.CMinputs.ContainsKey(dampenersButton) && !dampenersIsPressed)
				{//inertia dampener key
					dampeners = !dampeners;//toggle
					dampenersIsPressed = true;
				}
				if (!this.CMinputs.ContainsKey(dampenersButton))
				{
					dampenersIsPressed = false;
				}


				if (this.CMinputs.ContainsKey(cruiseButton) && !cruiseIsPressed)
				{//cruise key
					cruise = !cruise;//toggle
					cruiseIsPressed = true;
				}
				if (!this.CMinputs.ContainsKey(cruiseButton))
				{
					cruiseIsPressed = false;
				}

				if (this.CMinputs.ContainsKey(raiseAccel) && !plusIsPressed)
				{//throttle up
					accelExponent++;
					plusIsPressed = true;
				}
				if (!this.CMinputs.ContainsKey(raiseAccel))
				{ //increase target acceleration
					plusIsPressed = false;
				}

				if (this.CMinputs.ContainsKey(lowerAccel) && !minusIsPressed)
				{//throttle down
					accelExponent--;
					minusIsPressed = true;
				}
				if (!this.CMinputs.ContainsKey(lowerAccel))
				{ //lower target acceleration
					minusIsPressed = false;
				}

				if (this.CMinputs.ContainsKey(resetAccel))
				{ //default target acceleration
					accelExponent = 0;
				}

			}

			bool changeDampeners = false;


			if (arg.Contains(dampenersArg))
			{
				dampeners = !dampeners;
				changeDampeners = true;
			}
			else if (arg.Contains(cruiseArg))
			{
				cruise = !cruise;
			}
			else if(arg.Contains(raiseAccelArg))
			{
				accelExponent++;
			}
			else if(arg.Contains(lowerAccelArg))
			{
				accelExponent--;
			}
			else if(arg.Contains(resetAccelArg))
			{
				accelExponent = 0;
			}
			else if(arg.Contains(gearArg))
			{
				if (gear == Accelerations.Length - 1) gear = 0;
				else gear++;
			}

			// dampeners (if there are any normal thrusters, the dampeners control works)
			if (normalThrusters.Count != 0)
			{

				if (OnlyMain())
				{

					if (changeDampeners)
					{
						mainController.TheBlock.DampenersOverride = dampeners;
					}
					else
					{
						dampeners = mainController.TheBlock.DampenersOverride;
					}
				}
				else
				{

					if (changeDampeners)
					{
						// make all conform
						foreach (ShipController cont in controlledControllers)
						{
							cont.SetDampener(dampeners);
						}
					}
					else
					{

						// check if any are different to us
						bool any_different = false;
						foreach (ShipController cont in controlledControllers)
						{
							if (cont.TheBlock.DampenersOverride != dampeners)
							{
								any_different = true;
								dampeners = cont.TheBlock.DampenersOverride;
								break;
							}
						}

						if (any_different)
						{
							// update all others to new value too
							foreach (ShipController cont in controlledControllers)
							{
								cont.SetDampener(dampeners);
							}
						}
					}
				}
			}


			// movement controls
			if (perf) return new Vector3D();

			if (OnlyMain())
			{
				moveVec = mainController.TheBlock.GetWorldMoveIndicator();
			}
			else
			{
				foreach (ShipController cont in controlledControllers)
				{
					if (cont.TheBlock.IsUnderControl)
					{
						moveVec += cont.TheBlock.GetWorldMoveIndicator();
					}
				}
			}

			return moveVec;
		}

		void RemoveSurface(IMyTextSurface surface)
		{
			if (this.surfaces.Contains(surface))
			{
				//need to check this, because otherwise it will reset panels
				//we aren't controlling
				this.surfaces.Remove(surface);
				surface.ContentType = ContentType.NONE;
				surface.WriteText("", false);
			}
		}

		bool RemoveSurfaceProvider(IMyTerminalBlock block)
		{
			if (!(block is IMyTextSurfaceProvider) /*|| (block is IMyRemoteControl)*/) return false;
			IMyTextSurfaceProvider provider = (IMyTextSurfaceProvider)block;

			for (int i = 0; i < provider.SurfaceCount; i++)
			{
				if (surfaces.Contains(provider.GetSurface(i)))
				{
					RemoveSurface(provider.GetSurface(i));
				}
			}
			return true;
		}

		
		bool AddSurfaceProvider(IMyTerminalBlock block)
		{
			if (!(block is IMyTextSurfaceProvider)) return false;
			IMyTextSurfaceProvider provider = (IMyTextSurfaceProvider)block;
			bool retval = true;
			if (block.CustomData.Length == 0)
			{
				return false;
			}

			bool[] to_add = new bool[provider.SurfaceCount];
			for (int i = 0; i < to_add.Length; i++)
			{
				to_add[i] = false;
			}
			int begin_search = 0;

			while (begin_search >= 0)
			{
				string data = block.CustomData;
				int start = data.IndexOf(textSurfaceKeyword, begin_search);

				if (start < 0)
				{
					retval = begin_search != 0;
					break;
				}
				int end = data.IndexOf("\n", start);
				begin_search = end;

				string display;
				if (end < 0)
				{
					display = data.Substring(start + textSurfaceKeyword.Length);
				}
				else
				{
					display = data.Substring(start + textSurfaceKeyword.Length, end - (start + textSurfaceKeyword.Length));
				}

				int display_num;
				if (Int32.TryParse(display, out display_num))
				{
					if (display_num >= 0 && display_num < provider.SurfaceCount)
					{
						// it worked, add the surface
						to_add[display_num] = true;

					}
					else
					{
                        // range check failed
                        string err_str;
                        if (end < 0)
						{
							err_str = data.Substring(start);
						}
						else
						{
							err_str = data.Substring(start, end - (start));
						}
						surfaceProviderErrorStr += $"\nDisplay number out of range: {display_num}\nshould be: 0 <= num < {provider.SurfaceCount}\non line: ({err_str})\nin block: {block.CustomName}\n";
					}

				}
				else
				{
                    //didn't parse
                    string err_str;
                    if (end < 0)
					{
						err_str = data.Substring(start);
					}
					else
					{
						err_str = data.Substring(start, end - (start));
					}
					surfaceProviderErrorStr += $"\nDisplay number invalid: {display}\non line: ({err_str})\nin block: {block.CustomName}\n";
				}
			}

			for (int i = 0; i < to_add.Length; i++)
			{
				if (to_add[i] && !this.surfaces.Contains(provider.GetSurface(i)))
				{
					this.surfaces.Add(provider.GetSurface(i));
				}
				else if (!to_add[i])
				{
					RemoveSurface(provider.GetSurface(i));
				}
			}
			return retval;
		}

		void InitControllers(List<IMyShipController> blocks = null) //New GetControllers(), only for using in init() purpose 
		{
			bool greedy = this.greedy || this.applyTags;// || this.removeTags;

			if (blocks == null) {
				blocks = new List<IMyShipController>();
				GridTerminalSystem.GetBlocksOfType<IMyShipController>(blocks);
			}

			List<ShipController> conts = new List<ShipController>();
			foreach (IMyShipController imy in blocks)
			{
				//vtcontrollers.Add(imy);
				controllerblocks.Add(imy);
				conts.Add(new ShipController(imy/*, this*/));
			}
			//return getControllers(conts);

			controllers = conts;

			StringBuilder reason = new StringBuilder();
			foreach (ShipController s in controllers)
			{
				bool canAdd = true;
				StringBuilder currreason = new StringBuilder(s.TheBlock.CustomName + "\n");
				if (!s.TheBlock.ShowInTerminal && ignoreHiddenBlocks)
				{
					currreason.AppendLine("  ShowInTerminal not set\n");
					canAdd = false;
				}
				if (!s.TheBlock.CanControlShip)
				{
					currreason.AppendLine("  CanControlShip not set\n");
					canAdd = false;
				}
				if (!s.TheBlock.ControlThrusters)
				{
					currreason.AppendLine("  Can't ControlThrusters\n");
					canAdd = false;
				}
				/*if (s.theBlock.IsMainCockpit)
				{ // I thiink this could make problems in the future
					mainController = s;
				}*/
				if (!(greedy || HasTag(s.TheBlock)))
				{
					currreason.AppendLine("  Doesn't match my tag\n");
					canAdd = false;
				}
				/*if (this.removeTags)
				{
					RemoveTag(s.TheBlock);
				}*/

				if (canAdd)
				{
					AddSurfaceProvider(s.TheBlock);
					s.Dampener = s.TheBlock.DampenersOverride;
					controlledControllers.Add(s);
					ccontrollerblocks.Add(s.TheBlock);

					if (this.applyTags)
					{
						AddTag(s.TheBlock);
					}
				}
				else
				{
					reason.Append(currreason);
				}
			}
			if (blocks.Count == 0)
			{
				reason.AppendLine("No Controller Found.\nEither for missing tag, not working or removed.");
			}

			if (controlledControllers.Count == 0 /*&& usableControllers.Count == 0*/)
			{
				log.AppendNR("ERROR: no usable ship controller found. Reason: \n");
				log.AppendNR(reason.ToString(), false);
				ManageTag(true);
				shutdown = true;
				return;
			}

			else if (controlledControllers.Count > 0)
			{
				foreach (ShipController s in controlledControllers)
				{
					if (s.TheBlock.IsUnderControl)
					{
						mainController = s;
						break;
					}
				}
				if (mainController == null)
				{
					mainController = controlledControllers[0];
				}
			}
			return;
		}

		ShipController FindACockpit()
		{
			foreach (ShipController cont in controlledControllers)
			{
				if (!cont.TheBlock.Closed && cont.TheBlock.IsWorking)
				{
					return cont;
				}
			}

			return null;
		}

		void OneRunMainChecker(bool run=true) {
			ResetVTHandlers();
			check = true;
			if (run) MainChecker.Run();
		}

		bool Init()
		{
			log.AppendLine("Initialising..");
			InitControllers();
			check = true;
			myshipmass = mainController.TheBlock.CalculateShipMass();
			OneRunMainChecker();
			log.AppendLine("Init " + (shutdown ? "Failed" : "Completed Sucessfully"));
			return !shutdown;
		}


		class VectorThrust
		{
			public Program program;
			readonly PID pid = new PID(1, 0, 0, (1.0 / 60.0));
			Lag avg;

			// physical parts
			public Rotor rotor;
			public List<Thruster> thrusters;// all the thrusters
			public List<Thruster> availableThrusters;// <= thrusters: the ones the user chooses to be used (ShowInTerminal)
			public List<Thruster> activeThrusters;// <= activeThrusters: the ones that are facing the direction that produces the most thrust (only recalculated if available thrusters changes)

			public double thrustModifierAbove = 0.1;// how close the rotor has to be to target position before the thruster gets to full power
			public double thrustModifierBelow = 0.1;// how close the rotor has to be to opposite of target position before the thruster gets to 0 power
			public Vector3D requiredVec = Vector3D.Zero;
			public string Role;

			public float totalEffectiveThrust = 0;
			public int detectThrustCounter = 0;
			public Vector3D currDir = Vector3D.Zero;

			public double old_angleCos = 0;
			double PreciseRpm = 0;
			int AngleCosCount = 0;
			int avgsamples;

			//public Nacelle() { }// don't use this if it is possible for the instance to be kept (Not necessary)
			public VectorThrust(Rotor rotor, Program program)
			{
				this.program = program;
				this.rotor = rotor;
				this.thrusters = new List<Thruster>();
				this.availableThrusters = new List<Thruster>();
				this.activeThrusters = new List<Thruster>();
				Role = GetVTThrRole(program);
				this.avgsamples = program.RotationAverageSamples;
				this.avg = new Lag(this.avgsamples);
			}

			// final calculations and setting physical components
			public void Go()
			{

				if (avgsamples != program.RotationAverageSamples) {
					avgsamples = program.RotationAverageSamples;
					avg = new Lag(avgsamples);
				}

				totalEffectiveThrust = (float)CalcTotalEffectiveThrust(activeThrusters);

				bool usepid = (program.parked && program.UsePIDPark) || program.UsePID;

				double angleCos = rotor.SetFromVec(requiredVec, !usepid);
				double angleCosPercent = angleCos * 100;

				bool dampeners = program.dampeners;
				bool TO = program.thrustOn;
				bool cruise = program.cruise;
				bool nthr = program.normalThrusters != null;
				bool movement = program.mvin > 0;
				bool slowThrustOff = program.SlowThrustOff;
				bool park = program.parked && program.alreadyparked && program.setTOV;
				double rVecLength = requiredVec.Length();
				double multiplier = program.RotorStMultiplier;
				float MaxRPM = program.maxRotorRPM;
				float STval = (float)program.SlowThrustOffRPM;
				float iarpm = (float)(AI(angleCosPercent, rVecLength / multiplier).NNaN());

				//TODO: MAKE IT WORK WITH PARK AND STOP
				if (!usepid)
				{
					if ((nthr && dampeners && !cruise && !movement && TO) && Math.Abs(angleCosPercent - old_angleCos) <= 15 && angleCosPercent < 90) AngleCosCount++;
					else AngleCosCount = 0;
					
				}
				old_angleCos = angleCosPercent;
				if (!usepid) { 
					if (AngleCosCount > 10 && angleCosPercent < 90) PreciseRpm = AngleCosCount;
					else if (angleCosPercent > 98 || iarpm <= 2.5) PreciseRpm = 0;
				}


				double rtangle = rotor.TheBlock.Angle;
				double angle = rtangle * 180 / Math.PI;
				double angleRad = Math.Acos(angleCos) * 2;
				double desiredRad = rtangle - angleRad;
				double error = (desiredRad - rtangle).NNaN();
				float result = (float)pid.Control(error);

				//I can't get PID to work, using it only to handle parking
				//program.screensb.AppendLine("rs: " + result.Round(2) + " drad: " + desiredRad.Round(2));
				//program.screensb.AppendLine("er:"+error);
				/*float dif = result + 3.1416f;
				if (dif < 0) {
					result = Math.Abs(dif);
				}*/
				//program.Echo("frpm: " + iarpm);

				if (!usepid)
				{
					double truerpm = PreciseRpm + iarpm;
					avg.Update(truerpm);
					float finalrpm = (float)avg.Value;
					rotor.maxRPM = (TO) ? finalrpm : ((!TO && (slowThrustOff || cruise)) ? STval : finalrpm);
				}
				else {
					rotor.TheBlock.TargetVelocityRad = Math.Abs(error) < 0.01 ? 0 : (float)result;
				}

				// the clipping value 'thrustModifier' defines how far the rotor can be away from the desired direction of thrust, and have the power still at max
				// if 'thrustModifier' is at 1, the thruster will be at full desired power when it is at 90 degrees from the direction of travel
				// if 'thrustModifier' is at 0, the thruster will only be at full desired power when it is exactly at the direction of travel, (it's never exactly in-line)
				// double thrustOffset = (angleCos + 1) / (1 + (1 - Program.thrustModifierAbove));//put it in some graphing calculator software where 'angleCos' is cos(x) and adjust the thrustModifier value between 0 and 1, then you can visualise it
				double abo = thrustModifierAbove;
				double bel = thrustModifierBelow;
				if (abo > 1) { abo = 1; }
				if (abo < 0) { abo = 0; }
				if (bel > 1) { bel = 1; }
				if (bel < 0) { bel = 0; }
				// put it in some graphing calculator software where 'angleCos' is cos(x) and adjust the thrustModifier values between 0 and 1, then you can visualise it
				double thrustOffset = ((((angleCos + 1) * (1 + bel)) / 2) - bel) * (((angleCos + 1) * (1 + abo)) / 2);// the other one is simpler, but this one performs better
																													  // double thrustOffset = (angleCos * (1 + abo) * (1 + bel) + abo - bel + 1) / 2;
				if (thrustOffset > 1)
				{
					thrustOffset = 1;
				}
				else if (thrustOffset < 0)
				{
					thrustOffset = 0;
				}

				//set the thrust for each engine
				foreach (Thruster thruster in activeThrusters)
				{
					// errStr += thrustOffset.progressBar();
					Vector3D thrust = thrustOffset * requiredVec * thruster.TheBlock.MaxEffectiveThrust / totalEffectiveThrust;
					bool noThrust = thrust.LengthSquared() < 0.001f;
					if (/*!jetpack || */!program.thrustOn || noThrust)
					{
						thruster.SetThrust(0);
						thruster.TheBlock.Enabled = false;
						thruster.IsOffBecauseDampeners = !program.thrustOn || noThrust;
					}
					else
					{
						thruster.SetThrust(thrust);
						thruster.TheBlock.Enabled = true;
						thruster.IsOffBecauseDampeners = false;
					}
				}
			}

			public float CalcTotalEffectiveThrust(IEnumerable<Thruster> thrusters)
			{
				float total = 0;
				foreach (Thruster t in thrusters)
				{
					total += t.TheBlock.MaxEffectiveThrust;
				}
				return total;
			}

			string GetVTThrRole(Program p)
			{
				string result = "";
				List<Base6Directions.Axis> cdirs = p.controlledControllers[0].Directions;
				List<Base6Directions.Axis> rdirs = rotor.Directions;

				for (int i = 0; i < cdirs.Count; i++)
				{
					if (cdirs[i] == rdirs[1])
					{
						switch (i)
						{
							case 0:
								//Echo("front/back mounted, rotor covers cockpit's up/down/left/right");
								result = "UDLR";
								break;
							case 1:
								result = "FBLR";
								//Echo("top/bottom mounted, rotor covers cockpit's forward/back/left/right");
								break;
							case 2:
								result = "FBUP";
								//Echo("side mounted, rotor covers cockpit's forward/back/up/down");
								break;

						}
					}
				}
				return result;
			}


			//true if all thrusters are good
			public bool ValidateThrusters()
			{
				bool needsUpdate = false;
				foreach (Thruster curr in thrusters)
				{

					bool shownAndFunctional = (curr.TheBlock.ShowInTerminal || !program.ignoreHiddenBlocks) && curr.TheBlock.IsFunctional;
					if (availableThrusters.Contains(curr))
					{//is available

						bool wasOnAndIsNowOff = curr.IsOn && !curr.TheBlock.Enabled && !curr.IsOffBecauseDampeners;

						if ((!shownAndFunctional || wasOnAndIsNowOff))
						{
							curr.IsOn = false;
							//remove the thruster
							availableThrusters.Remove(curr);
							needsUpdate = true;
						}

					}
					else
					{//not available
						bool wasOffAndIsNowOn = !curr.IsOn && curr.TheBlock.Enabled;
						if (shownAndFunctional && wasOffAndIsNowOn)
						{
							availableThrusters.Add(curr);
							needsUpdate = true;
							curr.IsOn = true;
						}
					}
				}
				return !needsUpdate;
			}

			double AI(double Acos, double VecL)
			{
				List<double> d = program.MagicNumbers;
				double result1 = (Acos * d[0]) + (VecL * d[1]) + d[2];
				double result2 = (Acos * d[3]) + (VecL * d[4]) + d[5];

				double mres1 = Math.Max(0, result1);
				double mres2 = Math.Max(0, result2);

				double sum = (mres1 * d[6]) + (mres2 * d[7]) + d[8];
				return Math.Max(0, sum);
			}

			public void DetectThrustDirection()
			{
				Vector3D engineDirection = Vector3D.Zero;
				Vector3D engineDirectionNeg = Vector3D.Zero;
				Vector3I thrustDir = Vector3I.Zero;
				Base6Directions.Direction rotTopUp = rotor.TheBlock.Top.Orientation.Up;

				// add all the thrusters effective power
				foreach (Thruster t in availableThrusters)
				{
					Base6Directions.Direction thrustForward = t.TheBlock.Orientation.Forward; // Exhaust goes this way

					//if its not facing rotor up or rotor down
					if (!(thrustForward == rotTopUp || thrustForward == Base6Directions.GetFlippedDirection(rotTopUp)))
					{
						// add it in
						var thrustForwardVec = Base6Directions.GetVector(thrustForward);
						if (thrustForwardVec.X < 0 || thrustForwardVec.Y < 0 || thrustForwardVec.Z < 0)
						{
							engineDirectionNeg += Base6Directions.GetVector(thrustForward) * t.TheBlock.MaxEffectiveThrust;
						}
						else
						{
							engineDirection += Base6Directions.GetVector(thrustForward) * t.TheBlock.MaxEffectiveThrust;
						}
					}
				}

				// get single most powerful direction
				double max = Math.Max(engineDirection.Z, Math.Max(engineDirection.X, engineDirection.Y));
				double min = Math.Min(engineDirectionNeg.Z, Math.Min(engineDirectionNeg.X, engineDirectionNeg.Y));
                double maxAbs;
                if (max > -1 * min)
				{
					maxAbs = max;
				}
				else
				{
					maxAbs = min;
				}

				// TODO: swap onbool for each thruster that isn't in this
				float DELTA = 0.1f;
				if (Math.Abs(maxAbs - engineDirection.X) < DELTA)
				{
					// DTerrStr += $"\nengineDirection.X";
					thrustDir.X = 1;
				}
				else if (Math.Abs(maxAbs - engineDirection.Y) < DELTA)
				{
					// DTerrStr += $"\nengineDirection.Y";
					thrustDir.Y = 1;
				}
				else if (Math.Abs(maxAbs - engineDirection.Z) < DELTA)
				{
					// DTerrStr += $"\nengineDirection.Z";
					thrustDir.Z = 1;
				}
				else if (Math.Abs(maxAbs - engineDirectionNeg.X) < DELTA)
				{
					// DTerrStr += $"\nengineDirectionNeg.X";
					thrustDir.X = -1;
				}
				else if (Math.Abs(maxAbs - engineDirectionNeg.Y) < DELTA)
				{
					// DTerrStr += $"\nengineDirectionNeg.Y";
					thrustDir.Y = -1;
				}
				else if (Math.Abs(maxAbs - engineDirectionNeg.Z) < DELTA)
				{
					// DTerrStr += $"\nengineDirectionNeg.Z";
					thrustDir.Z = -1;
				}
				else
				{
					// DTerrStr += $"\nERROR (detectThrustDirection):\nmaxAbs doesn't match any engineDirection\n{maxAbs}\n{engineDirection}\n{engineDirectionNeg}";
					return;
				}

				// use thrustDir to set rotor offset
				rotor.SetPointDir((Vector3D)thrustDir);
				// Base6Directions.Direction rotTopForward = rotor.theBlock.Top.Orientation.TransformDirection(Base6Directions.Direction.Forward);
				// Base6Directions.Direction rotTopLeft = rotor.theBlock.Top.Orientation.TransformDirection(Base6Directions.Direction.Left);
				// rotor.offset = (float)Math.Acos(rotor.angleBetweenCos(Base6Directions.GetVector(rotTopForward), (Vector3D)thrustDir));

				// disambiguate
				// if(false && Math.Acos(rotor.angleBetweenCos(Base6Directions.GetVector(rotTopLeft), (Vector3D)thrustDir)) > Math.PI/2) {
				// rotor.offset += (float)Math.PI;
				// 	rotor.offset = (float)(2*Math.PI - rotor.offset);
				// }

				foreach (Thruster t in thrusters)
				{
					t.TheBlock.Enabled = false;
					t.IsOn = false;
				}
				activeThrusters.Clear();

				// put thrusters into the active list
				Base6Directions.Direction thrDir = Base6Directions.GetDirection(thrustDir);
				foreach (Thruster t in availableThrusters)
				{
					Base6Directions.Direction thrustForward = t.TheBlock.Orientation.Forward; // Exhaust goes this way

					if (thrDir == thrustForward)
					{
						t.TheBlock.Enabled = true;
						t.IsOn = true;
						activeThrusters.Add(t);
					}
				}
			}

		}

		class Thruster : BlockWrapper<IMyThrust>
		{

			// stays the same when in standby, if not in standby, this gets updated to weather or not the thruster is on
			public bool IsOn;

			// this indicate the thruster was turned off from the script, and should be kept in the active list
			public bool IsOffBecauseDampeners = true;

			public Thruster(IMyThrust thruster) : base(thruster)
			{
				// this.IsOn = theBlock.Enabled;
				this.IsOn = false;
				this.TheBlock.Enabled = true;
			}

			// sets the thrust in newtons (N)
			// thrustVec is in worldspace, who'se length is the desired thrust
			public void SetThrust(Vector3D thrustVec)
			{
				SetThrust(thrustVec.Length());
			}

			// sets the thrust in newtons (N)
			public void SetThrust(double thrust)
			{

				if (thrust > TheBlock.MaxThrust)
				{
					thrust = TheBlock.MaxThrust;
					// errStr += $"\nExceeding max thrust";
				}
				else if (thrust < 0)
				{
					// errStr += $"\nNegative Thrust";
					thrust = 0;
				}

				TheBlock.ThrustOverride = (float)(thrust * TheBlock.MaxThrust / TheBlock.MaxEffectiveThrust);
				/*errStr += $"\nEffective {(100*theBlock.MaxEffectiveThrust / theBlock.MaxThrust).Round(1)}%";
				errStr += $"\nOverride {theBlock.ThrustOverride}N";*/
			}
		}

		public bool FilterThis(IMyTerminalBlock block)
		{
			return block.CubeGrid == Me.CubeGrid;
		}

		public void StabilizeRotors(bool rotorlock = true)
		{
			if (VTThrGroups.Count > 0)
			{
				foreach (List<VectorThrust> n in VTThrGroups)
				{
					foreach (VectorThrust na in n)
					{
						IMyMotorStator rt = na.rotor.TheBlock;
						List<Thruster> tr = na.thrusters;
						foreach (Thruster t in tr) { t.TheBlock.Enabled = !rotorlock; t.TheBlock.ThrustOverride = 0; }
						rt.TargetVelocityRPM = 0.0f;
						rt.Enabled = !rotorlock;
						if ((rotorlock && !rt.RotorLock) || (!rotorlock && rt.RotorLock)) rt.RotorLock = !rt.RotorLock;
					}
				}
			}
		}

		class Rotor : BlockWrapper<IMyMotorStator>
		{
			// don't want IMyMotorBase, that includes wheels

			// Depreciated, this is for the old setFromVec
			public float offset = 0;// radians

			public Program program;
			public Vector3D direction = Vector3D.Zero;//offset relative to the head

			//public string errStr = "";
			public float maxRPM;

			public Rotor(IMyMotorStator rotor, Program program) : base(rotor)
			{
				this.program = program;

				if (program.maxRotorRPM <= 0)
				{
					maxRPM = rotor.GetMaximum<float>("Velocity");
				}
				else
				{
					maxRPM = program.maxRotorRPM;
				}
			}

			public void SetPointDir(Vector3D dir)
			{
				// MatrixD inv = MatrixD.Invert(theBlock.Top.WorldMatrix);
				// direction = Vector3D.TransformNormal(dir, inv);
				this.direction = dir;
				//TODO: for some reason, this is equal to rotor.worldmatrix.up
			}

			/*===| Part of Rotation By Equinox on the KSH discord channel. |===*/
			private void PointRotorAtVector(IMyMotorStator rotor, Vector3D targetDirection, Vector3D currentDirection, float multiplier)
			{
				double errorScale = Math.PI * maxRPM;
				maxRPM = MathHelper.Clamp(Math.Abs(maxRPM), 0, 60);

				Vector3D angle = Vector3D.Cross(targetDirection, currentDirection);
				// Project onto rotor
				double err = Vector3D.Dot(angle, rotor.WorldMatrix.Up);
				double err2 = Vector3D.Dot(angle.Normalized(), rotor.WorldMatrix.Up);
				double diff = (rotor.WorldMatrix.Up - angle.Normalized()).Length();

				/*this.errStr += $"\nrotor.WorldMatrix.Up: {rotor.WorldMatrix.Up}";
				this.errStr += $"\nangle: {Math.Acos(angleBetweenCos(angle, rotor.WorldMatrix.Up)) * 180.0 / Math.PI}";
				this.errStr += $"\nerr: {err}";
				this.errStr += $"\ndirection difference: {diff}";

				this.errStr += $"\ncurrDir vs Up: {currentDirection.Dot(rotor.WorldMatrix.Up)}";
				this.errStr += $"\ntargetDir vs Up: {targetDirection.Dot(rotor.WorldMatrix.Up)}";

				this.errStr += $"\nmaxRPM: {maxRPM}";
				this.errStr += $"\nerrorScale: {errorScale}";
				this.errStr += $"\nmultiplier: {multiplier}";*/

				double result;

				double rpm = err * errorScale * multiplier;
				//double rpm = err2 * errorScale * multiplier;
				// errStr += $"\nSETTING ROTOR TO {err:N2}";
				if (rpm > maxRPM)
				{
					result = maxRPM;
					// this.errStr += $"\nRPM Exceedes Max";
				}
				else if ((rpm * -1) > maxRPM)
				{
					result = maxRPM * -1;
					// this.errStr += $"\nRPM Exceedes -Max";
				}
				else
				{
					result = (float)rpm;
				}

				result = MathHelper.Clamp(result, -program.maxRotorRPM, program.maxRotorRPM);
				result = MathHelper.Clamp(result, -maxRPM, maxRPM);

				rotor.TargetVelocityRPM = (float)(result.NNaN());
				// this.errStr += $"\nRPM: {(rotor.TargetVelocityRPM).Round(5)}";
			}

			// this sets the rotor to face the desired direction in worldspace
			// desiredVec doesn't have to be in-line with the rotors plane of rotation
			public double SetFromVec(Vector3D desiredVec, float multiplier, bool point = true)
			{
				desiredVec.Normalize();
				//errStr = "";
				//desiredVec = desiredVec.reject(theBlock.WorldMatrix.Up);
				//this.errStr += $"\ncurrent dir: {currentDir}\ntarget dir: {desiredVec}\ndiff: {currentDir - desiredVec}";
				//Vector3D currentDir = Vector3D.TransformNormal(this.direction, theBlock.Top.WorldMatrix);
				//                                    only correct if it was built from the head ^ 
				//                                    it needs to be based on the grid
				Vector3D currentDir = Vector3D.TransformNormal(this.direction, TheBlock.Top.CubeGrid.WorldMatrix);
				if (point) PointRotorAtVector(TheBlock, desiredVec, currentDir/*theBlock.Top.WorldMatrix.Forward*/, multiplier);

				return AngleBetweenCos(currentDir, desiredVec, desiredVec.Length());
			}

			public double SetFromVec(Vector3D desiredVec, bool point = true)
			{
				return SetFromVec(desiredVec, 1, point);
			}

			// gets cos(angle between 2 vectors)
			// cos returns a number between 0 and 1
			// use Acos to get the angle
			//THIS COULD BE NECESSARY IN SOME FUTURE.....
			public double AngleBetweenCos(Vector3D a, Vector3D b)
			{
				double dot = Vector3D.Dot(a, b);
				double Length = a.Length() * b.Length();
				return dot / Length;
			}

			// gets cos(angle between 2 vectors)
			// cos returns a number between 0 and 1
			// use Acos to get the angle
			// doesn't calculate length because thats expensive
			public double AngleBetweenCos(Vector3D a, Vector3D b, double len_a_times_len_b)
			{
				double dot = Vector3D.Dot(a, b);
				return dot / len_a_times_len_b;
			}

			// set the angle to be between 0 and 2pi radians (0 and 360 degrees)
			// this takes and returns radians
			/*float cutAngle(float angle)
			{
				while (angle > Math.PI)
				{
					angle -= 2 * (float)Math.PI;
				}
				while (angle < -Math.PI)
				{
					angle += 2 * (float)Math.PI;
				}
				return angle;
			}*/
		}
		class ShipController : BlockWrapper<IMyShipController>
		{
			public bool Dampener;


			public ShipController(IMyShipController theBlock/*, Program program*/) : base(theBlock)
			{
				Dampener = theBlock.DampenersOverride;
				//program.controllerblocks.Add(theBlock);
			}

			public void SetDampener(bool val)
			{
				Dampener = val;
				TheBlock.DampenersOverride = val;
			}
		}

		interface IBlockWrapper
		{
			IMyTerminalBlock TheBlock { get; set; }
			List<Base6Directions.Axis> Directions { get; }

			string CName { get; }
		}

		abstract class BlockWrapper<T> : IBlockWrapper where T : class, IMyTerminalBlock
		{
			public T TheBlock { get; set; }

			public List<Base6Directions.Axis> Directions { get; }
			
			public string CName { get; }

			public BlockWrapper(T block)
			{
				TheBlock = block;
				Directions = GetDirections(block);
				CName = block.CustomName;
			}

			// not allowed for some reason
			//public static implicit operator IMyTerminalBlock(BlockWrapper<T> wrap) => wrap.theBlock;

			IMyTerminalBlock IBlockWrapper.TheBlock
			{
				get { return TheBlock; }
				set { TheBlock = (T)value; }
			}

			public List<Base6Directions.Axis> GetDirections(IMyTerminalBlock block)
			{
				MyBlockOrientation o = block.Orientation;
				return new List<Base6Directions.Axis> { Base6Directions.GetAxis(o.Forward), Base6Directions.GetAxis(o.Up), Base6Directions.GetAxis(o.Left) };
			}

			string IBlockWrapper.CName
			{
				get { return TheBlock.CustomName; }
			}
		}
	}
}
