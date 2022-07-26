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
		const string dampenersArg = "dampeners";
		const string cruiseArg = "cruise";
		const string raiseAccelArg = "raiseaccel";
		const string lowerAccelArg = "loweraccel";
		const string resetAccelArg = "resetaccel";
		const string gearArg = "gear";
		const string applyTagsArg = "applytags";
		const string applyTagsAllArg = "applytagsall";
		const string removeTagsArg = "removetags";

		// wether or not cruise mode is on when you start the script
		readonly float maxRotorRPM = 60f; // set to -1 for the fastest speed in the game (changes with mods)
		readonly List<double> MagicNumbers = new List<double> { -0.091, 0.748, -46.934, -0.073, 0.825, -4.502, -1.239, 1.124, 2.47 };

		readonly RuntimeTracker _RuntimeTracker;
		readonly SimpleTimerSM BlockManager;
		readonly SimpleTimerSM BatteryStats;

		double thrustontimer = 0;
		bool scriptslower = false; //I am pretty sure that this script will never work on Update100
		readonly double timeperframe = 1.0 / 60.0;
		Vector3D desiredVec = new Vector3D();
		bool dampchanged = false;

		bool parked = false;
		bool alreadyparked = false;
		bool cruisedNT = false;
		bool setTOV = false;
		bool TagAll = false;
		bool error = false;
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
		readonly SimpleTimerSM CheckParkBlocks;

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
		readonly UpdateFrequency update_frequency = UpdateFrequency.Update1;
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
		readonly StringBuilder surfaceProviderErrorStr = new StringBuilder();
		int accelExponent = 0;
		double accelExponent_A = 0;

		double totalVTThrprecision = 0;
		bool rotorsstopped = false;

		bool dampenersIsPressed = false;
		bool cruiseIsPressed = false;
		bool plusIsPressed = false;
		bool minusIsPressed = false;
		bool globalAppend = false;

		ShipController mainController = null;
		List<ShipController> controllers = new List<ShipController>();
		List<IMyShipController> controllerblocks = new List<IMyShipController>();
		readonly List<IMyShipController> ccontrollerblocks = new List<IMyShipController>();
		readonly List<ShipController> controlledControllers = new List<ShipController>();
		List<VectorThrust> vectorthrusters = new List<VectorThrust>();
		List<IMyThrust> normalThrusters = new List<IMyThrust>();
		List<IMyTextPanel> screens = new List<IMyTextPanel>();
		readonly List<IMyShipConnector> connectors = new List<IMyShipConnector>();
		readonly List<IMyLandingGear> landinggears = new List<IMyLandingGear>();
		readonly List<IMyGasTank> tankblocks = new List<IMyGasTank>();
		readonly List<IMyTerminalBlock> cruiseThr = new List<IMyTerminalBlock>();
		readonly List<List<VectorThrust>> VTThrGroups = new List<List<VectorThrust>>();
		public List<IMyTextSurface> surfaces = new List<IMyTextSurface>();

		List<IMyThrust> vtthrusters = new List<IMyThrust>();
		List<IMyMotorStator> vtrotors = new List<IMyMotorStator>();

		readonly List<IMyBatteryBlock> taggedbats = new List<IMyBatteryBlock>();
		readonly List<IMyBatteryBlock> normalbats = new List<IMyBatteryBlock>();

		List<IMyThrust> thrusters_input = new List<IMyThrust>();
		List<IMyMotorStator> rotors_input = new List<IMyMotorStator>();
		readonly List<ShipController> controllers_input = new List<ShipController>();
		readonly List<IMyTextPanel> input_screens = new List<IMyTextPanel>();
		readonly List<IMyMotorStator> abandonedrotors = new List<IMyMotorStator>();
		readonly List<IMyThrust> abandonedthrusters = new List<IMyThrust>();

		List<IMyTerminalBlock> batsseq = new List<IMyTerminalBlock>();
		List<double> outputbatsseq = new List<double>();

		bool pauseseq = false;
		bool check = true;

		Vector3D shipVelocity = Vector3D.Zero;
		double sv = 0;
		double thrustModifierAbove = 0.1;// how close the rotor has to be to target position before the thruster gets to full power
		double thrustModifierBelow = 0.1;// how close the rotor has to be to opposite of target position before the thruster gets to 0 power
		bool justCompiled = true;
		string tag = "|VT|";

		bool applyTags = false;
		bool greedy = true;
		float lastGrav = 0;
		bool thrustOn = true;
		Dictionary<string, object> CMinputs = null;
		bool rechargecancelled = false;
		bool parkedwithcn = false;

		float oldMass = 0;
		int frame = 0;

		bool unparkedcompletely = true;
		bool parkedcompletely = false;
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

		public Program()
		{
			log.AppendLine("Program() Start");

			string[] stg = Storage.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
			if (stg.Length == 2)
			{
				oldTag = stg[0]; //loading tag
				greedy = bool.Parse(stg[1]); //loading greedy
			}

			timeperframe = scriptslower ?  1.0 / 6.0 : 1.0 / 60.0; //SetTimePerFrame();
			
			_RuntimeTracker = new RuntimeTracker(this, 60, 0.005);
			BlockManager = new SimpleTimerSM(this, BlockManagerSeq(), true);
			BatteryStats = new SimpleTimerSM(this, GetBatStatsSeq(), true);
			MainChecker = new SimpleTimerSM(this, CheckVectorThrustersSeq(), true);
			GetScreen = new SimpleTimerSM(this, GetScreensSeq(), true);
			GetControllers = new SimpleTimerSM(this, GetControllersSeq(), true);
			GetVectorThrusters = new SimpleTimerSM(this, GetVectorThrustersSeq(), true);
			CheckParkBlocks = new SimpleTimerSM(this, CheckParkBlocksSeq(), true);

			Init();
			update_frequency = scriptslower ? UpdateFrequency.Update10 : update_frequency;
			if (!error) Runtime.UpdateFrequency = update_frequency;
			else Echo(log.ToString());

			log.AppendLine("--VTOS Started--");
		}

		public void Save()
		{
			string save = string.Join(";", string.Join(":", tag, greedy));
			Storage = save; //saving the old tag and greedy to prevent recompile or script update confusion
		}

		public void Main(string argument/*, UpdateType runType*/)
		{
			// ========== STARTUP ==========
			_RuntimeTracker.AddRuntime();

			// writes and clears outputs
			WriteOutput();

			argument = argument.ToLower();
			bool tagArg =
			argument.Contains(applyTagsArg) ||
			argument.Contains(cruiseArg) ||
			argument.Contains(removeTagsArg);

			if (_RuntimeTracker.configtrigger)
			{
				_RuntimeTracker.configtrigger = false;
				Config();
				ManageTag();
			}

			// GETTING ONLY NECESARY INFORMATION TO THE SCRIPT
			if (!parkedcompletely) {
				MyShipVelocities shipVelocities = controlledControllers[0].TheBlock.GetShipVelocities();
				shipVelocity = shipVelocities.LinearVelocity;
				sv = shipVelocity.Length();

				bool damp = controlledControllers[0].TheBlock.DampenersOverride;
				dampchanged = damp != oldDampeners;
				oldDampeners = controlledControllers[0].TheBlock.DampenersOverride;

				desiredVec = GetMovementInput(argument, parked);
				mvin = desiredVec.Length();
			}
			// END NECESARY INFORMATION


			//START OUTPUT PRINTING
			Printer();
			//END PRINTER PART 1


			// SKIPFRAME AND PERFORMANCE HANDLER: handler to skip frames, it passes out if the player doesn't parse any command or do something relevant.
			if (!justCompiled) CheckWeight(); //Block Updater must-have
			if (SkipFrameHandler(tagArg, argument)) return; 
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

				cruise = BlockManager.Doneloop || parked || alreadyparked || cruisebyarg || cruiseThr.Count == 0 ? cruise : cruiseThr.All(x => !(x as IMyFunctionalBlock).Enabled); //New cruise toggle mode

				desiredVec -= dampVec * dampenersModifier;
			}
			// f=ma
			accel = GetAcceleration(gravLength);
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
				if (wgv != 0 && sv == 0 && !parked)
				{
					if (thrustOn)
					{
						thrustontimer += Runtime.TimeSinceLastRun.TotalSeconds;
						screensb.GetSpinner(ref pc).Append(" STABILIZING ").GetSpinner(ref pc, after: "\n");
					}
					else screensb.AppendLine(" - (NOT) PARKED -");
				}
				else thrustontimer = 0;
				if ((wgv == 0 && ((!cruise && sv < lowThrustCutOff) || ((cruise || !dampeners) && len < cutoffcruise))) || !(!parked || !alreadyparked) || thrustontimer > 0.1)
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
				if (!g.Empty()) { 
					g[0].requiredVec = requiredVec.Reject(g[0].rotor.TheBlock.WorldMatrix.Up);
					asdf += g[0].requiredVec;
				}
			}
			// 2
			asdf -= requiredVec;
			// 3

			foreach (List<VectorThrust> g in VTThrGroups)
			{
				if (!g.Empty())
				{
					g[0].requiredVec -= asdf;
				}
			}
			// 4
			asdf /= VTThrGroups.Count;
			// 5
			foreach (List<VectorThrust> g in VTThrGroups)
			{
				if (!g.Empty())
				{
					g[0].requiredVec += asdf;
				}
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
				if (g.Empty()) continue;
				double precision = 0;
				Vector3D req = g[0].requiredVec / g.Count;
				for (int i = 0; i < g.Count; i++)
				{
					VectorThrust vt = g[i];
					IMyMotorStator rt = vt.rotor.TheBlock;
					if (GridTerminalSystem.CanAccess(rt) && !rt.Closed && rt.IsWorking && rt.IsAlive() && rt.Top != null)
					{
						vt.requiredVec = req;
						vt.thrustModifierAbove = thrustModifierAbove;
						vt.thrustModifierBelow = thrustModifierBelow;
						vt.Go();

						totaleffectivethrust += vt.totalEffectiveThrust;

						total += req.Length();

						totalVTThrprecision += vt.old_angleCos;
						precision += vt.old_angleCos;
						j++;

						if (i == 0 && ShowMetrics) info.Append($"\n| {vt.Role} |=>")
								.Append($" | {(req.Length() / RotorStMultiplier).Round(1)}")
								.Append($" |  {vt.rotor.maxRPM.Round(0)} ");
								//.Append($" |  {vt.pid._lastError} ");
					}
				}
				if(ShowMetrics) info.Append($" |  {(precision / g.Count).Round(1)}%  |\n");
			}

			if (ShowMetrics) info.Append(edge);
			totalVTThrprecision /= j;

			totaleffectivethrust /= myshipmass.PhysicalMass;

			if (justCompiled || argument.Contains(gearArg)) GenerateProgressBar();
				
			echosb.AppendLine($"Total Force: {total.Round(0)}N\n");
			echosb = _RuntimeTracker.Append(echosb);
			echosb.AppendLine("--- Log ---");
			echosb.Append(log);

			if (ShowMetrics) { 
				echosb.Append(info);
				screensb.Append(info);
			}

			//TODO: make activeNacelles account for the number of nacelles that are actually active (activeThrusters.Count > 0) (I GUESS SOLVED??)
			// ========== END OF MAIN ==========

			log.Append(surfaceProviderErrorStr);
			justCompiled = false;
			_RuntimeTracker.AddInstructions();
		}
	}
}
