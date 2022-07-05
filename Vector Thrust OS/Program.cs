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
		/*arguments, you can change these to change what text you run the programmable block with
		public const string standbytogArg = "standby";
		public const string standbyonArg = "standbyenter";
		public const string standbyoffArg = "standbyexit";*/
		const string dampenersArg = "dampeners";
		const string cruiseArg = "cruise";
		//public const string jetpackArg = "jetpack";
		const string raiseAccelArg = "raiseaccel";
		const string lowerAccelArg = "loweraccel";
		const string resetAccelArg = "resetaccel";
		const string resetArg = "reset"; //this one re-runs the initial setup (init() method) ... you probably want to use %resetAccel
		const string applyTagsArg = "applytags";
		const string applyTagsAllArg = "applytagsall";
		const string removeTagsArg = "removetags";

		// weather or not cruise mode is on when you start the script
		bool cruise = false;
		bool dampeners = true;
		string textSurfaceKeyword = "VT:";
		string LCDName = "VTLCD";
		float maxRotorRPM = 60f; // set to -1 for the fastest speed in the game (changes with mods)
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

		// DEPRECATED: use the tags instead
		// only use blocks that have 'show in terminal' set to true
		bool ignoreHiddenBlocks = false;

		// Control Module params... this can always be true, but it's deprecated
		bool controlModule = true;
		const string dampenersButton = "c.damping";
		const string cruiseButton = "c.cubesizemode";
		const string lowerAccel = "c.switchleft";
		const string raiseAccel = "c.switchright";
		const string resetAccel = "pipe";
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


		 /*this is used to identify blocks as belonging to this programmable block.
		 pass the '%applyTags' argument, and the program will spread its tag across all blocks it controls.
		 the program then won't touch any blocks that don't have the tag. unless you pass the '%removeTags' argument.
		 if you add or remove a tag manually, pass the '%reset' argument to force a re-check
		 if you make this tag unique to this ship, it won't interfere with your other vector thrust ships
		 normal: |VT|
		 standby: .VT.
		 public const string standbySurround = ".";
		 put this in custom data of a cockpit to instruct the script to use a display in that cockpit
		 it has to be on a line of its own, and have an integer after the ':'
		 the integer must be 0 <= integer <= total # of displays in the cockpit
		 eg:
				%Vector:0
		 this would make the script use the 1st display. the 1st display is #0, 2nd #1 etc..
		 if you have trouble, look in the bottom right of the PB terminal, it will print errors there
		 standby stops all calculations and safely turns off all nacelles, good if you want to stop flying
		 but dont want to turn the craft off.
		public const bool startInStandby = false;
		 change this is you don't want the script to start in standby... please only use this if you have permission from the server owner
		 this is the default target acceleration you see on the display
		 if you want to change the default, change this
		 note, values higher than 1 will mean your nacelles will face the ground when you want to go
		 down rather than just lower thrust
		 '1g' is acceleration caused by current gravity (not nessicarily 9.81m/s) although
		 if current gravity is less than 0.1m/s it will ignore this setting and be 9.81m/s anywayºº
		 true: only main cockpit can be used even if there is no one in the main cockpit
		 false: any cockpits can be used, but if there is someone in the main cockpit, it will only obey the main cockpit
		 no main cockpit: any cockpits can be used
		 should be 1 of:
		 UpdateFrequency.Update1
		 UpdateFrequency.Update10
		 UpdateFrequency.Update100
		 ALWAYS CHECK FOR UPDATES
		 to update, simply load it from the workshop tab again (no need to actually go to the workshop page)
		 weather or not dampeners are on when you start the script
		 weather or not thrusters are on when you start the script
		public bool jetpack = true;
		 control module gamepad bindings
		 type "/cm showinputs" into chat
		 press the desired button
		 put that text EXACTLY as it is in the quotes for the control you want
		public const string jetpackButton = "c.thrusts";
		 boost settings (this only works with control module)
		 you can use this to set target acceleration values that you can quickly jump to by holding down the specified button
		 there are defaults here:
		 	c.sprint (shift)	3g
		 	ctrl 			0.3g
		 this can't be const, because it causes unreachable code warning, which now can't be disabled
		                              V 180 degrees
		              V 0 degrees                      V 360 degrees
		 				|-----\                    /------
		 desired power|----------------------------------------- value of 0.1
		 				|       \                /
		 				|        \              /
		 				|         \            /
		 no power 	|-----------------------------------------
		
		
		 				|-----\                    /------stuff above desired power gets set to desired power
		 				|      \                  /
		 				|       \                /
		 desired power|----------------------------------------- value of 0.8
		 				|         \            /
		 no power 	|-----------------------------------------
		 the above pictures are for 'thrustModifierAbove', the same principle applies for 'thrustModifierBelow', except it goes below the 0 line, instead of above the max power line.
		 the clipping value 'thrustModifier' defines how far the thruster can be away from the desired direction of thrust, and have the power still at desired power, otherwise it will be less
		 these values can only be between 0 and 1
		 another way to look at it is:
		 set above to 1, its at 100% of desired power far from the direction of thrust
		 set below to 1, its at 000% of desired power far from the opposite direction of thrust
		 set above to 0, its at 100% of desired power only when it is exactly in the direction of thrust
		 set below to 0, its at 000% of desired power only when it is exactly in the opposite direction of thrust
		public long gotNacellesCount;
		public long updateNacellesCount;
		string spinner = "";
		at 60 fps this will last for 9000+ hrs before going negative (just set them back to 0)*/

		List<IMyTerminalBlock> parkblocks = new List<IMyTerminalBlock>();
		List<IMyTerminalBlock> tankblocks = new List<IMyTerminalBlock>();
		List<IMyTerminalBlock> batteriesblocks = new List<IMyTerminalBlock>();
		List<IMyTerminalBlock> gridbats = new List<IMyTerminalBlock>();
		List<IMyTerminalBlock> cruiseThr = new List<IMyTerminalBlock>();
		readonly List<List<VectorThrust>> VTThrGroups = new List<List<VectorThrust>>();
		List<double> MagicNumbers = new List<double> { -0.091, 0.748, -46.934, -0.073, 0.825, -4.502, -1.239, 1.124, 2.47 };

		readonly RuntimeTracker RT;
		readonly SimpleTimerSM ST;

		bool parked = false;
		bool alreadyparked = false;
		bool cruisedNT = false;
		bool setTOV = false;
		bool docheck = false;
		bool TagAll = false;
		bool shutdown = false;
		bool oldDampeners = false;
		double wgv = 0;
		double oldsv = 0;
		double maxaccel = 0;
		double mvin = 0;
		double accel = 0;
		double accelval = 0;
		float gravLength = 0;
		string oldTag = "";
		StringBuilder echosb = new StringBuilder();
		StringBuilder screensb = new StringBuilder();
		StringBuilder log = new StringBuilder();
		long pc = 0;

		public Program()
		{
			/*gotNacellesCount = 0;
			updateNacellesCount = 0;
			this.greedy = !hasTag(Me);
			if (Me.CustomData.Equals(""))
			{Me.CustomData = textSurfaceKeyword + 0;}
			programCounter = 0;
			Storage = "";
			if (stg.Length > 1) maxaccel = double.Parse(stg[1]);
			string temp = stg[0];
			string[] stg_tag = temp.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
			print(stg[0], stg.Length);
			print(stg[0], stg.Length);
			InitNacelles(); It was leavestandby, but now it doesn't do anything relevant*/

			string[] stg = Storage.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
			if (stg.Length == 2)
			{
				oldTag = stg[0]; //loading tag
				greedy = bool.Parse(stg[1]); //loading greedy
			}
			Runtime.UpdateFrequency = update_frequency;
			RT = new RuntimeTracker(this, 60, 0.005);
			ST = new SimpleTimerSM(this, MainTagSeq(), true);
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
			sumlastrun += Runtime.TimeSinceLastRun.TotalSeconds;
			RT.AddRuntime();
			pc++;

			// writes and clear outputs
			Echo(echosb.ToString());
			write(screensb.ToString());
			echosb.Clear();
			screensb.Clear();
			// ------------------------

			argument = argument.ToLower();
			bool tagArg =
			argument.Contains(applyTagsArg) ||
			argument.Contains(cruiseArg) ||
			argument.Contains(removeTagsArg);

			if (justCompiled || sumlastrun >= TimeForRefresh)
			{
				sumlastrun = 0;
				Config();
				ManageTag();
			}
			if ((justCompiled && (controllers.Count == 0 || argument.Contains(resetArg)) && !init()) || shutdown) {
				Echo(log.ToString());
				Runtime.UpdateFrequency = UpdateFrequency.None; 
				return; 
			}

			// END STARTUP



			// GETTING ONLY NECESARY INFORMATION TO THE SCRIPT

			MyShipVelocities shipVelocities = controlledControllers[0].theBlock.GetShipVelocities();
			shipVelocity = shipVelocities.LinearVelocity;
			sv = shipVelocity.Length();

			bool damp = usableControllers[0].theBlock.DampenersOverride;
			bool dampchanged = damp != oldDampeners;
			oldDampeners = usableControllers[0].theBlock.DampenersOverride;

			Vector3D desiredVec = getMovementInput(argument);
			mvin = desiredVec.Length();
            double realacc = Math.Abs((sv - oldsv) / Runtime.TimeSinceLastRun.TotalSeconds).Round(2);
            if (realacc > maxaccel) maxaccel = realacc;
			oldsv = sv;
			double re = (realacc / maxaccel).NNaN();

			// END NECESARY INFORMATION



			//START OUTPUT PRINTING

			screensb.getSpinner(ref pc).Append($" {Runtime.LastRunTimeMs.Round(2)}ms ").getSpinner(ref pc);
			screensb.AppendLine($"\nAccel: {realacc.Round(1)} / {maxaccel.Round(2)} (m/s^2)");
			screensb.AppendLine($"Gear [{gear}]=> Around: [ {gearaccel.Round(0)} (m/s^2) ]");
			screensb.AppendLine($"Cruise: {cruise}");
			if (normalThrusters.Count == 0) screensb.AppendLine($"Dampeners: {dampeners}");
			if (ShowMetrics)
			{
				screensb.AppendLine($"\nAM: {accelval.Round(2)}g");
				screensb.AppendLine($"Active VectorThrusters: {vectorthrusters.Count}");
				screensb.AppendLine($"Main/Ref Cont: {mainController.theBlock.CustomName}");
			}
			echosb.getSpinner(ref pc).Append(" VTos ").getSpinner(ref pc);
			echosb.AppendLine($"\n\n--- Main ---");
			echosb.AppendLine(" >Remaining: " + (TimeForRefresh - sumlastrun).Round(1));
			echosb.AppendLine(" >Greedy: " + greedy);
			echosb.AppendLine($" >Angle Objective: {totalVTThrprecision.Round(1)}%");
			echosb.AppendLine($" >Main/Reference Controller: {mainController.theBlock.CustomName}");
			echosb.AppendLine($" >Parked: {parked}");

			//END PRINTER PART 1

			
			// SKIPFRAME AND PERFORMANCE HANDLER
			ST.Run(); //RUNS VARIOUS PROCESSES SEPARATED BY A TIMER 
			if (argument.Equals("") && !cruise && !dampchanged)
			{ //handler to skip frames, it passes out if the player doesn't parse any command or do something relevant.
				bool handlers = false;
				if (!shutdown)
				{
					if (performanceHandler()) handlers = true;
					if (parkHandler()) handlers = true;
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
			else if (tagArg && !MainTag(argument)) { //TODO, SEE IF IS NEEDED TO PASS THE ENTIRE RUN FRAME IF THERE'S AN ARGUMENT
				Runtime.UpdateFrequency = UpdateFrequency.None;
				return;
				//HANDLES TAG ARGUMENTS, IF IT FAILS, IT STOPS
			}
			// END SKIPFRAME 

			/*echosb.Append("" + tankblocks.Length);
			tagArg = false;
			if ((CompareVars(programCounter, 100, 200, 300)[1] || tagArg || justCompiled) && !MainTag(argument))
			{Runtime.UpdateFrequency = UpdateFrequency.None;return;}
			Echo($"Last Runtime {Runtime.LastRunTimeMs.Round(2)}ms");
			Echo("" + ProgressBar(20, 1, 20));
			Echo(programCounter + " " + screenCount + " " + surfaces.Count);
			if (!initialized) {write(ProgressBar(60, (int)programCounter, 200));
				if (programCounter >= 200) initialized = true;}
			print("%scr%", sv);
			Echo("gl:" + gravLength);
			if (justCompiled) StabilizeRotors(false);
			double atotal = Accelerations.Sum(item => Math.Abs(item));
			write(ProgressBar(40, accelExponent, atotal));
			Echo(temp);
			 only accept arguments on certain update types
			UpdateType valid_argument_updates = UpdateType.None;
			valid_argument_updates |= UpdateType.Terminal;
			valid_argument_updates |= UpdateType.Trigger;
			valid_argument_updates |= UpdateType.Mod;
			valid_argument_updates |= UpdateType.Script;
			valid_argument_updates |= UpdateType.Update1;
			valid_argument_updates |= UpdateType.Update10;
			valid_argument_updates |= UpdateType.Update100;
			if ((runType & valid_argument_updates) == UpdateType.None)
			{ runtype is not one that is allowed to give arguments
				argument = "";}
			Echo("Starting Main");
			bool togglePower = argument.Contains(standbytogArg.ToLower());
			// set standby mode on
			if (argument.Contains(standbyonArg.ToLower()) || goToStandby)
			{enterStandby();
				temp += "a"; 
				return;
				// set standby mode off}
			else if (argument.Contains(standbyoffArg.ToLower()) || comeFromStandby)
			{temp += "b";
				exitStandby();
				return;
				// going into standby mode toggle}
			else if ((togglePower && !standby) || goToStandby)
			{temp += "c";
				enterStandby();
				return;
				// coming back from standby mode toggle}
			else if ((anyArg || runType == UpdateType.Terminal) && standby || comeFromStandby)
			{temp += "d";
				exitStandby();}
			else {Echo("Normal Running");}
			Echo("Temp:" + temp);
			if (justCompiled || controllers.Count == 0 || argument.Contains(resetArg.ToLower())){if (!init())
				{return;}}
			// tags
			Echo(programCounter + "");
			if (justCompiled)
			{	Runtime.UpdateFrequency = UpdateFrequency.Once;
				if (Storage == "" || !startInStandby)
				{	//Storage = "Don't Start Automatically";
					// run normally
					comeFromStandby = true;
					return;} else
				{	// go into standby mode
					goToStandby = true;
					return;}}
			if (standby)
			{	Echo("Standing By");
				write("Standind By");
				return; }
			get gravity in world space
			if (alreadyparked)
			{alreadyparked = false;
			StabilizeRotors(t_rotors, false);}
			get velocity
			MyShipVelocities shipVelocities = controlledControllers[0].theBlock.GetShipVelocities();
			shipVelocity = shipVelocities.LinearVelocity;
			sv = shipVelocity.Length();
			Vector3D shipAngularVelocity = shipVelocities.AngularVelocity;
			setup mass*/

			// ========== PHYSICS ==========
			//TODO: SEE IF I CAN SPLIT AT LEAST SOME OF THE STEPS BY SEQUENCES

			MyShipMass myShipMass = controlledControllers[0].theBlock.CalculateShipMass();
			float shipMass = myShipMass.PhysicalMass;

			if (myShipMass.BaseMass < 0.001f)
			{
				Echo("Can't fly a Station");
				shipMass = 0.001f;
			}

			Vector3D worldGrav = controlledControllers[0].theBlock.GetNaturalGravity();
			gravLength = (float)worldGrav.Length();

			bool gravChanged = Math.Abs(lastGrav - gravLength) > 0.05f;
			foreach (VectorThrust n in vectorthrusters)
				if (!n.validateThrusters() || gravChanged) n.detectThrustDirection();

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

			//Vector3D desiredVec = getMovementInput(argument);
			//mvin = desiredVec.Length();
			// f=ma
			Vector3D shipWeight = shipMass * worldGrav;

			if (dampeners)
			{
				Vector3D dampVec = Vector3D.Zero;
				if (desiredVec != Vector3D.Zero)
				{
					// cancel movement opposite to desired movement direction
					if (desiredVec.dot(shipVelocity) < 0)
					{
						//if you want to go oppisite to velocity
						dampVec += shipVelocity.project(desiredVec.normalized());
					}
					// cancel sideways movement
					dampVec += shipVelocity.reject(desiredVec.normalized());
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
						if (onlyMain() && cont != mainController) continue;
						if (!cont.theBlock.IsUnderControl) continue;

						if (dampVec.dot(cont.theBlock.WorldMatrix.Forward) > 0 || cruisePlane)
						{ // only front, or front+back if cruisePlane is activated
							dampVec -= dampVec.project(cont.theBlock.WorldMatrix.Forward);
						}

						if (cruisePlane)
						{
							shipWeight -= shipWeight.project(cont.theBlock.WorldMatrix.Forward);
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
			accel = getAcceleration(gravLength);
			gearaccel = getAcceleration(gravLength, Accelerations[gear]);
			accelval = accel / gravLength;
			desiredVec *= shipMass * (float)accel;

			// point thrust in opposite direction, add weight. this is force, not acceleration
			Vector3D requiredVec = -desiredVec + shipWeight;

			// remove thrust done by normal thrusters
			foreach (IMyThrust t in normalThrusters)
			{
				requiredVec -= -1 * t.WorldMatrix.Backward * t.CurrentThrust;
				// Echo($"{t.CustomName}: {Vector3D.TransformNormal(t.CurrentThrust * t.WorldMatrix.Backward, MatrixD.Invert(t.WorldMatrix))}");
				// write($"{t.CustomName}: \n{Vector3D.TransformNormal(t.CurrentThrust * t.WorldMatrix.Backward, MatrixD.Invert(t.WorldMatrix))}");
			}

			double len = requiredVec.Length();
			echosb.AppendLine($"Required Force: {len.Round(0)}N");
			// ========== END OF PHYSICS ==========


			// ========== DISTRIBUTE THE FORCE EVENLY BETWEEN NACELLES ==========

			// hysteresis
			double force = gravCutoff * shipMass;

			//double cutoff = lowThrustCutOff * force;
			//double cuton = lowThrustCutOn * force; not necesary anymore
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
			
			//Echo($"thrustOn: {thrustOn} \n{Math.Round(requiredVec.Length()/(gravCutoff*shipMass), 2)}\n{Math.Round(requiredVec.Length()/(gravCutoff*shipMass*0.01), 2)}");

			// "maybe lerp this in the future" - (SOVLED) It's fine
			if (!thrustOn)
			{// Zero G
				Vector3D zero_G_accel = Vector3D.Zero;
				if (mainController != null)
				{
					zero_G_accel = (mainController.theBlock.WorldMatrix.Down + mainController.theBlock.WorldMatrix.Backward) * zeroGAcceleration / 1.414f;
				}
				else
				{
					zero_G_accel = (controlledControllers[0].theBlock.WorldMatrix.Down + controlledControllers[0].theBlock.WorldMatrix.Backward) * zeroGAcceleration / 1.414f;
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

			// update thrusters on/off and re-check nacelles direction
			/*bool gravChanged = Math.Abs(lastGrav - gravLength) > 0.05f;
			lastGrav = gravLength;
			foreach (Nacelle n in nacelles)
			{// we want to update if the thrusters are not valid, or atmosphere has changed
				if (!n.validateThrusters(jetpack) || gravChanged)
				{n.detectThrustDirection();}
				// Echo($"thrusters: {n.thrusters.Count}");
				// Echo($"avaliable: {n.availableThrusters.Count}");
				// Echo($"active: {n.activeThrusters.Count}");}*/
			/* TOOD: redo this : SOLVED TODO */
			// group similar nacelles (rotor axis is same direction)
			/*for (int i = 0; i < nacelles.Count; i++)
			{	bool foundGroup = false;
				foreach (List<Nacelle> g in nacelleGroups)
				{// check each group to see if its lined up
					if (Math.Abs(Vector3D.Dot(nacelles[i].rotor.theBlock.WorldMatrix.Up, g[0].rotor.theBlock.WorldMatrix.Up)) > 0.9f)
					{g.Add(nacelles[i]);
						foundGroup = true;
						break;}}
				if (!foundGroup)
				{// if it never found a group, add a group
					nacelleGroups.Add(new List<Nacelle>());
					nacelleGroups[nacelleGroups.Count - 1].Add(nacelles[i]);}}*/


			// correct for misaligned nacelles
			Vector3D asdf = Vector3D.Zero;
			// 1
			foreach (List<VectorThrust> g in VTThrGroups)
			{
				g[0].requiredVec = requiredVec.reject(g[0].rotor.theBlock.WorldMatrix.Up);
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
			// apply first nacelle settings to rest in each group
			double total = 0;
			int j = 0;
			totalVTThrprecision = 0;
			string edge = separator();

			StringBuilder info = new StringBuilder($"{separator("[Metrics]")}\n");
			if (ShowMetrics) {
				info.Append("| Axis |=> | VTLength | MaxRPM | Far% |\n")
					.Append(edge);
			}

			foreach (List<VectorThrust> g in VTThrGroups)
			{
				double precision = 0;
				Vector3D req = g[0].requiredVec / g.Count;
				for (int i = 0; i < g.Count; i++)
				{
					g[i].requiredVec = req;
					g[i].thrustModifierAbove = thrustModifierAbove;
					g[i].thrustModifierBelow = thrustModifierBelow;

					g[i].go(/*jetpack, */dampeners, shipMass);
					total += req.Length();

					totalVTThrprecision += g[i].old_angleCos;
					precision += g[i].old_angleCos;
					j++;

					// Echo(g[i].errStr);
					// write($"nacelle {i} avail: {g[i].availableThrusters.Count} updates: {g[i].detectThrustCounter}");
					// write(g[i].errStr);
					// foreach(Thruster t in g[i].activeThrusters) {
					// 	// Echo($"Thruster: {t.theBlock.CustomName}\n{t.errStr}");
					// }

					if (i == 0 && ShowMetrics) info.Append($"\n| {g[i].Role} |=>")
							.Append($" | {(req.Length() / RotorStMultiplier).Round(1)}")
							.Append($" |  {g[i].rotor.maxRPM.Round(0)} ");
				}
				if(ShowMetrics) info.Append($" |  {(precision / g.Count).Round(1)}%  |\n");
			}
			if (ShowMetrics) info.Append(edge);
			totalVTThrprecision /= j;

			echosb.AppendLine($"Total Force: {total.Round(0)}N\n");
			echosb = RT.Append(echosb);
			echosb.AppendLine("--- Log ---");
			echosb.Append(log);

			if (ShowMetrics) { 
				echosb.Append(info);
				screensb.Append(info);
			}

			//write("Thrusters: " + jetpack);
			//TODO: make activeNacelles account for the number of nacelles that are actually active (activeThrusters.Count > 0) (I GUESS SOLVED??)
			// write("Got Nacelles: " + gotNacellesCount);
			// write("Update Nacelles: " + updateNacellesCount);
			// ========== END OF MAIN ==========
			// echo the errors with surface provider

			log.Append(surfaceProviderErrorStr);
			justCompiled = false;
			RT.AddInstructions();
		}

		// ------- Default configs --------
		string myName = "VT";
		double TimeForRefresh = 10;
		bool ShowMetrics = false;
		//public bool SmartThrusters = true;
		int SkipFrames = 0;
		double TimeBetweenAction = 1;

		double RotorStMultiplier = 1000;
		bool SlowThrustOff = false;
		double SlowThrustOffRPM = 5;
		double lowThrustCutOn = 0.5;
		double lowThrustCutOff = 0.01;
		double lowThrustCutCruiseOn = 1;
		double lowThrustCutCruiseOff = 0.15;

		double[] Accelerations = new double[] { 0, 1.875, 3.75 };
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
		const string ShowMetricsStr = "Show Metrics";
		const string TimeForRefreshStr = "Time For Each Refresh";
		//const string SmartThrustersStr = "Smart Thrusters Mode";
		const string SkipFramesStr = "Skip Frames";
		const string TimeBetweenActionStr = "Time Checking Intervals";
		

		const string RotorStMultiplierStr = "Rotor Stabilization Multiplier";
		const string lowThrustCutStr = "Calculated Velocity To Turn On/Off VectorThrusters";
		const string lowThrustCutCruiseStr = "Calculated Velocity To Turn On/Off VectorThrusters In Cruise";
		const string SlowThrustOffStr = "Slow Reposition Of Rotors On Turn Off";
		const string SlowThrustOffRPMStr = "Slow Rotor Reposition Value (RPM)";

		const string AccelerationsStr = "Acelerations";
		const string gearStr = "Starting Acceleration Position";

		const string TurnOffThrustersOnParkStr = "Turn Off Thrusters On Park";
		const string PerformanceWhileParkStr = "Run Script Each 100 Frames When Parked";
		const string ConnectorNeedsSuffixToParkStr = "Connector Needs Suffix To Toggle Park Mode";
		const string UsePIDParkStr = "Use PID Controller to Handle Parking";

		const string thrustModifierSpaceStr = "Thruster Modifier Turn On/Off Space";
		const string thrustModifierGravStr = "Thruster Modifier Turn On/Off Gravity";

		const string RotationAverageSamplesStr = "Rotor Velocity Average Samples";
		const string TagSurroundStr = "Tag Surround Char(s)";
		const string UsePIDStr = "Use PID Controller";
		const string cruisePlaneStr = "Cruise Mode Act Like Plane";

		// END STRINGS AND VARS
		void setConfig(bool force)
		{

			double[] defaultltc = new double[] { 0.5, 0.01 };
			double[] defaultltcc = new double[] { 1, 0.15 };
			double[] defaultacc = new double[] { 0, 1.875, 3.75 };
			double[] defaulttms = new double[] { 0.1, 0.1 };
			double[] defaulttmg = new double[] { 0.1, 0.1 };

			config.Set(inistr, myNameStr, myName);
			config.Set(inistr, TimeForRefreshStr, TimeForRefresh);
			config.Set(inistr, ShowMetricsStr, ShowMetrics);
			//config.Set(inistr, SmartThrustersStr, SmartThrusters);
			config.Set(inistr, SkipFramesStr, SkipFrames);
			config.Set(inistr, TimeBetweenActionStr, TimeBetweenAction);

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
			string sstr = tagSurround[0].Equals(tagSurround[1]) ? tagSurround[0] : tagSurround[0] + tagSurround[1];
			config.Set(miscstr, TagSurroundStr, sstr);
			config.Set(miscstr, UsePIDStr, UsePID);
			config.Set(miscstr, cruisePlaneStr, cruisePlane);
		}

		void Config()
		{
			config.Clear();
			double[] defaultltc = new double[] { 0.5, 0.01 };
			double[] defaultltcc = new double[] { 1, 0.15 };
			double[] defaultacc = new double[] { 0, 1.875, 3.75 };
			double[] defaulttms = new double[] { 0.1, 0.1 };
			double[] defaulttmg = new double[] { 0.1, 0.1 };

			bool force = false;
			keepConfig();

			if (config.TryParse(Me.CustomData))
			{
				myName = config.Get(inistr, myNameStr).ToString(myName);
				textSurfaceKeyword = $"{myName}:";
				LCDName = $"{myName}LCD";

				TimeForRefresh = config.Get(inistr, TimeForRefreshStr).ToDouble(TimeForRefresh);
				ShowMetrics = config.Get(inistr, ShowMetricsStr).ToBoolean(ShowMetrics);
				//SmartThrusters = config.Get(inistr, SmartThrustersStr).ToBoolean(SmartThrusters);
				SkipFrames = config.Get(inistr, SkipFramesStr).ToInt32(SkipFrames);
				TimeBetweenAction = config.Get(inistr, TimeBetweenActionStr).ToDouble(TimeBetweenAction);

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


				string sstr = config.Get(miscstr, TagSurroundStr).ToString(myName);
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

				UsePID = config.Get(miscstr, UsePIDStr).ToBoolean(UsePID);
				cruisePlane = config.Get(miscstr, cruisePlaneStr).ToBoolean(cruisePlane);
			}

			setConfig(force);
			RConfig(config.ToString(), force);
		}

		void RConfig(string output, bool force = false)
		{
			if (force || output != Me.CustomData) Me.CustomData = output;
			try { if (!force && !Me.CustomData.Contains($"\n---\n{textSurfaceKeyword}0")) Me.CustomData = Me.CustomData.Replace(Me.CustomData.Between("\n---\n", "0")[0], textSurfaceKeyword); }
			catch { if (!justCompiled) log.AppendLine("No tag found textSufaceKeyword\n"); }
			if (!force && !Me.CustomData.Contains($"\n---\n{textSurfaceKeyword}0")) Me.CustomData += $"\n---\n{textSurfaceKeyword}0";
		}

		void keepConfig()
		{
			if (justCompiled && configCD.TryParse(Me.CustomData))
			{
				setConfig(false);
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

			public RuntimeTracker(Program program, int capacity = 100, double sensitivity = 0.005)
			{
				_program = program;
				Capacity = capacity;
				Sensitivity = sensitivity;
				_instructionLimit = _program.Runtime.MaxInstructionCount;
			}

			public void AddRuntime()
			{
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
		List<ShipController> controlledControllers = new List<ShipController>();
		List<ShipController> usableControllers = new List<ShipController>();
		List<VectorThrust> vectorthrusters = new List<VectorThrust>();
		List<IMyThrust> normalThrusters = new List<IMyThrust>();
		List<IMyTextPanel> screens = new List<IMyTextPanel>();
		List<IMyTextPanel> usableScreens = new List<IMyTextPanel>();
		HashSet<IMyTextSurface> surfaces = new HashSet<IMyTextSurface>();
		List<IMyProgrammableBlock> programBlocks = new List<IMyProgrammableBlock>();

		float oldMass = 0;
		int rotorCount = 0;
		int rotorTopCount = 0;
		int thrusterCount = 0;
		int screenCount = 0;
		//int programBlockCount = 0;
		int frame = 0;
		double sumlastrun = 0;

		string separator(string title = "", int len=58) {
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

		//public bool jetpackIsPressed = false;
		//public string offtag = ".VT.";
		//public bool standby = startInStandby;
		//public bool goToStandby = false;
		//public bool comeFromStandby = false;

		bool applyTags = false;
		bool removeTags = false;
		bool greedy = true;
		float lastGrav = 0;
		bool thrustOn = true;
		Dictionary<string, object> CMinputs = null;


		void ManageTag(bool force = false)
		{
			tag = tagSurround[0] + myName + tagSurround[1];
			bool cond1 = oldTag.Length > 0;
			bool cond2 = !tag.Equals(oldTag) && Me.CustomName.Contains(oldTag);
			bool cond3 = greedy && Me.CustomName.Contains(oldTag);

			if (cond1 && (cond2 || cond3 || force))
			{
				log.AppendLine(" -Cleaning Tags To Prevent Future Errors, just in case\n^Ignore this if you read it again");
				List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
				GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);
				foreach (IMyTerminalBlock block in blocks)
					block.CustomName = block.CustomName.Replace(oldTag, "").Trim();
			}
			this.greedy = !hasTag(Me);
			oldTag = tag;
		}

		bool parkHandler()
		{
			if (parkblocks.Count > 0)
			{
				int parks = 0;
				int cnparks = 0;

				foreach (IMyTerminalBlock cn in parkblocks)
				{
					bool cnpark = (cn is IMyShipConnector) && ((IMyShipConnector)cn).Status == MyShipConnectorStatus.Connected;
					bool lgpark = (cn is IMyLandingGear) && ((IMyLandingGear)cn).IsLocked;

					if (cnpark || lgpark) parks++;
					if (cnpark) cnparks++;
				}
				if (parks > 0) parked = true;
				else parked = false;

				if (parked && !alreadyparked)
				{
					alreadyparked = true;
					if (PerformanceWhilePark && gravLength == 0 && Runtime.UpdateFrequency != UpdateFrequency.Update100) Runtime.UpdateFrequency = UpdateFrequency.Update100;
					else if ((!PerformanceWhilePark || gravLength > 0) && Runtime.UpdateFrequency != UpdateFrequency.Update10) Runtime.UpdateFrequency = UpdateFrequency.Update10;
				}
				else if (!parked && alreadyparked)
				{
					alreadyparked = false;
					thrustOn = true;
					Runtime.UpdateFrequency = update_frequency;
					tankthrusterbatteryManager(parked);
				}
				else if (alreadyparked && totalVTThrprecision.Round(1) == 100)
				{
					tankthrusterbatteryManager(parked, cnparks);
					screensb.AppendLine("PARKED");
					return alreadyparked;
				}
				else if (alreadyparked && totalVTThrprecision.Round(1) != 100)
				{
					screensb.AppendLine("PARKING");
				}
			}

			return false;
		}

		void tankthrusterbatteryManager(bool park = true, int cnparks = 1)
		{
			if (TurnOffThrustersOnPark && normalThrusters.Count > 0)
				foreach (IMyFunctionalBlock tr in normalThrusters) if ((!park && !tr.Enabled) || (park && tr.Enabled)) tr.Enabled = !park;

			int gbc = gridbats.Count;
			if ((tankblocks.Count > 0 || gbc > 1) && cnparks > 0)
			{
				List<IMyTerminalBlock> bats = gridbats;
				bats.AddRange(batteriesblocks);
				foreach (IMyFunctionalBlock t in tankblocks)
				{
					if (t is IMyGasTank) (t as IMyGasTank).Stockpile = park;

				}
				int bl = bats.Count;
				if (bl > 0)
				{
					if (park)
					{
						foreach (IMyBatteryBlock b in bats)
						{
							b.ChargeMode = ChargeMode.Recharge;
						}
						if (gbc == 0) (bats[0] as IMyBatteryBlock).ChargeMode = ChargeMode.Auto;
						else (gridbats[0] as IMyBatteryBlock).ChargeMode = ChargeMode.Auto;
					}
					else
					{ //I prefer to loop all instead
						foreach (IMyBatteryBlock b in bats)
						{
							b.ChargeMode = ChargeMode.Auto;
						}
					}

				}
			}
		}

		bool performanceHandler()
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

		/*public static T[] ClearA<T>(ref T[] arr)
		{
			return new T[] { };
		}

		public static void AddA<T>(ref T[] elm, T val)
		{
			elm = elm.Concat(new[] { val }).ToArray();
		}
		public static void AddA<T>(ref T[] elm, T[] val)
		{
			elm = elm.Concat(val).ToArray();
		}*/

		bool MainTag(string argument)
		{

			//tags and getting blocks
			TagAll = argument.Contains(applyTagsAllArg);
			this.applyTags = argument.Contains(applyTagsArg) || TagAll;
			this.removeTags = !this.applyTags && argument.Contains(Program.removeTagsArg);
			// switch on: removeTags
			// switch off: applyTags
			this.greedy = (!this.applyTags && this.greedy) || this.removeTags;
			// this automatically calls getVectorThrusters() as needed, and passes in previous GTS data
			if (this.applyTags)
			{
				addTag(Me);
			}
			else if (this.removeTags)
			{
				removeTag(Me);
			}

			bool cnc = checkVectorThrusters(true);
			TagAll = false;
			this.applyTags = false;
			this.removeTags = false;
			return cnc;
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
						//print("%sep% ", "Found group", na.rotor.Name, g[0].rotor.Name);
						break;
					}
				}
				if (!foundGroup)
				{// if it never found a group, add a group
				 //print("%sep% ", "New group", na.rotor.Name);
					VTThrGroups.Add(new List<VectorThrust>());
					VTThrGroups[VTThrGroups.Count - 1].Add(na);
				}
			}
		}


		/*public void enterStandby()
		{
			standby = true;
			goToStandby = false;

			//set status of blocks
			foreach (Nacelle n in nacelles)
			{
				n.rotor.theBlock.Enabled = false;
				standbyTag(n.rotor.theBlock);
				foreach (Thruster t in n.thrusters)
				{
					t.theBlock.Enabled = false;
					standbyTag(t.theBlock);
				}
			}
			foreach (IMyTextPanel screen in usableScreens)
			{
				standbyTag(screen);
			}
			foreach (ShipController cont in controlledControllers)
			{
				standbyTag(cont.theBlock);
			}
			standbyTag(Me);

			Runtime.UpdateFrequency = UpdateFrequency.None;

			Echo("Standing By");
			write("Standing By");
		}*/

		/*public void InitNacelles()
		{
			//standby = false;
			//comeFromStandby = false;

			//set status of blocks
			foreach (Nacelle n in nacelles)
			{
				n.rotor.theBlock.Enabled = true;
				activeTag(n.rotor.theBlock);
				foreach (Thruster t in n.thrusters)
				{
					if (t.IsOn)
					{
						t.theBlock.Enabled = true;
					}
					activeTag(t.theBlock);
				}
			}
			foreach (IMyTextPanel screen in usableScreens)
			{
				activeTag(screen);
			}
			foreach (ShipController cont in controlledControllers)
			{
				activeTag(cont.theBlock);
			}
			activeTag(Me);

			//Runtime.UpdateFrequency = update_frequency;
		}*/

		bool hasTag(IMyTerminalBlock block)
		{
			return block.CustomName.Contains(tag)/* || block.CustomName.Contains(offtag)*/;
		}

		void addTag(IMyTerminalBlock block)
		{
			string name = block.CustomName;

			/*if (name.Contains(tag))
			{
				// there is already a tag, just set it to current status
				if (standby)
				{
					block.CustomName = name.Replace(tag, offtag);
				}

			}
			else if (name.Contains(offtag))
			{
				// there is already a tag, just set it to current status
				if (!standby)
				{
					block.CustomName = name.Replace(offtag, tag);
				}

			}
			else*/
			/*{
				// no tag found, add tag to start of string

				if (standby)
				{
					block.CustomName = offtag + " " + name;
				}
				else
				{
					block.CustomName = tag + " " + name;
				}
			}*/
			if (!name.Contains(tag)) block.CustomName = tag + " " + name;
		}

		void removeTag(IMyTerminalBlock block)
		{
			block.CustomName = block.CustomName.Replace(tag, "").Trim();
			//block.CustomName = block.CustomName.Replace(offtag, "").Trim();
		}

		/*public void standbyTag(IMyTerminalBlock block)
		{
			block.CustomName = block.CustomName.Replace(tag, offtag);
		}

		public void activeTag(IMyTerminalBlock block)
		{
			block.CustomName = block.CustomName.Replace(offtag, tag);
		}*/


		// true: only main cockpit can be used even if there is no one in the main cockpit
		// false: any cockpits can be used, but if there is someone in the main cockpit, it will only obey the main cockpit
		// no main cockpit: any cockpits can be used
		bool onlyMain()
		{
			return mainController != null && (mainController.theBlock.IsUnderControl || onlyMainCockpit);
		}

		void getScreens()
		{
			getScreens(this.screens);
		}

		void getScreens(List<IMyTextPanel> screens)
		{
			bool greedy = this.greedy || this.applyTags || this.removeTags;
			this.screens = screens;
			usableScreens.Clear();
			foreach (IMyTextPanel screen in screens)
			{
				bool continue_ = false;

				if (this.removeTags)
				{
					removeTag(screen);
				}

				if (!greedy && !hasTag(screen)) { continue_ = true; }
				if (!screen.IsWorking) continue_ = true;
				if (!hasTag(screen) && !screen.CustomName.ToLower().Contains(LCDName.ToLower())) continue_ = true;

				if (continue_)
				{
					surfaces.Remove(screen);
					continue;
				}
				if (this.applyTags)
				{
					addTag(screen);
				}
				usableScreens.Add(screen);
				surfaces.Add(screen);
			}
			screenCount = screens.Count;
			log.AppendLine("  --Updating Vector Thrusters\n");
		}

		void write(params object[] obj)
		{
			string sep = ", ";
			string init = obj[0].ToString();
			if (init.Contains("%sep%")) sep = init.Replace("%sep%", "");
			string result = "\n" + string.Join(sep, obj);
			if (this.surfaces.Count > 0)
			{
				foreach (IMyTextSurface surface in this.surfaces)
				{
					surface.WriteText(result, globalAppend);
					surface.ContentType = ContentType.TEXT_AND_IMAGE;
				}
			}
			else if (!globalAppend)
			{
				if (!justCompiled) log.AppendLine("No text surfaces available");
			}
			globalAppend = true;
		}

		double getAcceleration(double gravity, double exp = 0)
		{
			// look through boosts, applies acceleration of first one found
			if (Program.useBoosts && this.controlModule)
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

			//none found or boosts not enabled, go for normal accel
			return Math.Pow(accelBase, accelExponent + accelexpaval) * gravity * defaultAccel;
		}

		Vector3D getMovementInput(string arg, bool perf = false)
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

				/*if (this.CMinputs.ContainsKey(jetpackButton) && !jetpackIsPressed)
				{//jetpack key
					jetpack = !jetpack;//toggle
					jetpackIsPressed = true;
				}
				if (!this.CMinputs.ContainsKey(jetpackButton))
				{
					jetpackIsPressed = false;
				}*/

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
			if (arg.Contains(cruiseArg))
			{
				cruise = !cruise;
			}
			/*if (arg.Contains(jetpackArg))
			{
				jetpack = !jetpack;
			}*/
			if (arg.Contains(raiseAccelArg))
			{
				accelExponent++;
			}
			if (arg.Contains(lowerAccelArg))
			{
				accelExponent--;
			}
			if (arg.Contains(resetAccelArg))
			{
				accelExponent = 0;
			}
			if (arg.Contains("gear"))
			{
				if (gear == Accelerations.Length - 1) gear = 0;
				else gear++;

			}

			// dampeners (if there are any normal thrusters, the dampeners control works)
			if (normalThrusters.Count != 0)
			{

				if (onlyMain())
				{

					if (changeDampeners)
					{
						mainController.theBlock.DampenersOverride = dampeners;
					}
					else
					{
						dampeners = mainController.theBlock.DampenersOverride;
					}
				}
				else
				{

					if (changeDampeners)
					{
						// make all conform
						foreach (ShipController cont in controlledControllers)
						{
							cont.setDampener(dampeners);
						}
					}
					else
					{

						// check if any are different to us
						bool any_different = false;
						foreach (ShipController cont in controlledControllers)
						{
							if (cont.theBlock.DampenersOverride != dampeners)
							{
								any_different = true;
								dampeners = cont.theBlock.DampenersOverride;
								break;
							}
						}

						if (any_different)
						{
							// update all others to new value too
							foreach (ShipController cont in controlledControllers)
							{
								cont.setDampener(dampeners);
							}
						}
					}
				}
			}


			// movement controls
			if (perf) return new Vector3D();

			if (onlyMain())
			{
				moveVec = mainController.theBlock.getWorldMoveIndicator();
			}
			else
			{
				foreach (ShipController cont in controlledControllers)
				{
					if (cont.theBlock.IsUnderControl)
					{
						moveVec += cont.theBlock.getWorldMoveIndicator();
					}
				}
			}

			return moveVec;
		}

		void removeSurface(IMyTextSurface surface)
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

		bool removeSurfaceProvider(IMyTerminalBlock block)
		{
			if (!(block is IMyTextSurfaceProvider)) return false;
			IMyTextSurfaceProvider provider = (IMyTextSurfaceProvider)block;

			for (int i = 0; i < provider.SurfaceCount; i++)
			{
				if (surfaces.Contains(provider.GetSurface(i)))
				{
					removeSurface(provider.GetSurface(i));
				}
			}
			return true;
		}
		bool addSurfaceProvider(IMyTerminalBlock block)
		{
			if (!(block is IMyTextSurfaceProvider)) return false;
			IMyTextSurfaceProvider provider = (IMyTextSurfaceProvider)block;
			bool retval = true;
			//temp += "surface";
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

			//temp += "surfacebs";

			while (begin_search >= 0)
			{
				//temp += ",search";
				string data = block.CustomData;
				int start = data.IndexOf(textSurfaceKeyword, begin_search);

				if (start < 0)
				{
					// true if it found at least 1
					retval = begin_search != 0;
					break;
				}
				int end = data.IndexOf("\n", start);
				begin_search = end;

				string display = "";
				if (end < 0)
				{
					display = data.Substring(start + textSurfaceKeyword.Length);
				}
				else
				{
					display = data.Substring(start + textSurfaceKeyword.Length, end - (start + textSurfaceKeyword.Length));
				}

				int display_num = 0;
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
						string err_str = "";
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
					string err_str = "";
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
				if (to_add[i])
				{
					this.surfaces.Add(provider.GetSurface(i));
				}
				else
				{
					removeSurface(provider.GetSurface(i));
				}
			}
			return retval;
		}

		bool getControllers()
		{
			return getControllers(this.controllers);
		}

		bool getControllers(List<IMyShipController> blocks)
		{
			List<ShipController> conts = new List<ShipController>();
			foreach (IMyShipController imy in blocks)
			{
				conts.Add(new ShipController(imy));
			}
			return getControllers(conts);
		}

		bool getControllers(List<ShipController> blocks)
		{
			//Echo("cont");
			//temp = "getcont3";
			//this.controllers = blocks;
			bool greedy = this.greedy || this.applyTags || this.removeTags;
			mainController = null;

			usableControllers.Clear();
			controlledControllers.Clear();

			StringBuilder reason = new StringBuilder();
			foreach (ShipController s in blocks)
			{
				bool canAdd = true;
				StringBuilder currreason = new StringBuilder(s.theBlock.CustomName + "\n");
				if (!s.theBlock.ShowInTerminal && ignoreHiddenBlocks)
				{
					currreason.AppendLine("  ShowInTerminal not set\n");
					canAdd = false;
				}
				if (!s.theBlock.CanControlShip)
				{
					currreason.AppendLine("  CanControlShip not set\n");
					canAdd = false;
				}
				if (!s.theBlock.ControlThrusters)
				{
					currreason.AppendLine("  Can't ControlThrusters\n");
					canAdd = false;
				}
				if (s.theBlock.IsMainCockpit)
				{
					mainController = s;
				}
				if (!(greedy || hasTag(s.theBlock)))
				{
					currreason.AppendLine("  Doesn't match my tag\n");
					canAdd = false;
				}
				if (this.removeTags)
				{
					removeTag(s.theBlock);
				}

				if (canAdd)
				{
					addSurfaceProvider(s.theBlock);
					s.Dampener = s.theBlock.DampenersOverride;
					usableControllers.Add(s);
					if (s.theBlock.IsUnderControl)
					{
						controlledControllers.Add(s);
					}

					if (this.applyTags)
					{
						addTag(s.theBlock);
					}
				}
				else
				{
					removeSurfaceProvider(s.theBlock);
					reason.Append(currreason);
				}
			}
			if (blocks.Count == 0)
			{
				reason.AppendLine("No Controller Found.\nEither for missing tag, not working or removed.");
			}

			if (controlledControllers.Count == 0 && usableControllers.Count == 0)
			{
				log.AppendLine("ERROR: no usable ship controller found. Reason: \n");
				log.Append(reason);
				ManageTag(true);
				return false;
			}
			else if (controlledControllers.Count == 1)
			{
				mainController = controlledControllers[0];
			}
			else if (usableControllers.Count >= 1)
			{
				foreach (ShipController c in usableControllers) controlledControllers.Add(c);
				mainController = usableControllers.Count == 1 ? usableControllers[0] : mainController == null ? usableControllers[0] : mainController;
			}
			controllers = blocks;
			//temp += $"{mainController.theBlock.CustomName}, {usableControllers.Count}, {controlledControllers.Count}, {blocks.Count}, {reason}";
			return true;
		}

		ShipController findACockpit()
		{
			foreach (ShipController cont in controlledControllers)
			{
				if (cont.theBlock.IsWorking)
				{
					return cont;
				}
			}

			return null;
		}

		// checks to see if the nacelles have changed
		bool checkVectorThrusters(bool vanilla = false)
		{
			bool greedy = this.applyTags || this.removeTags;

			ShipController cont = findACockpit();
			if (cont == null)
			{
				log.AppendLine("  -No cockpit registered, checking everything\n");
			}
			else if (!greedy)
			{
				MyShipMass shipmass = cont.theBlock.CalculateShipMass();
				if (this.oldMass == shipmass.BaseMass)
				{
					log.AppendLine("  -Mass is the same, everything is good\n");

					// they may have changed the screen name to be a VT one
					// DO THIS IN A SEPARATED INSTANCE

					if (vanilla)
					{
						getControllers();
						getScreens();
					}
					docheck = true;

					return true;
				}
				log.AppendLine("  -Mass is different, checking everything\n");
				this.oldMass = shipmass.BaseMass;
				// surface may be exploded if mass changes, in this case, ghost surfaces my be left behind
				this.surfaces.Clear();
			}
			docheck = false;

			parkblocks.Clear();
			tankblocks.Clear();
			batteriesblocks.Clear();
			gridbats.Clear();
			cruiseThr.Clear();
			normalThrusters.Clear();

			List<ShipController> conts = new List<ShipController>();
			List<IMyMotorStator> rots = new List<IMyMotorStator>();
			List<IMyThrust> thrs = new List<IMyThrust>();
			List<IMyTextPanel> txts = new List<IMyTextPanel>();
			List<IMyProgrammableBlock> programBlocks = new List<IMyProgrammableBlock>();

			if (true)
			{//artificial scope :D

				List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
				GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);
				foreach (IMyTerminalBlock b in blocks)
				{
					if (b is IMyShipController)
					{
						conts.Add(new ShipController((IMyShipController)b));
					}
					if (b is IMyMotorStator)
					{
						IMyMotorStator rt = (IMyMotorStator)b;
						rt.TargetVelocityRPM = 0;
						rt.RotorLock = false;
						rt.Enabled = true;
						rots.Add(rt);

					}
					if (b is IMyThrust)
					{
						IMyThrust tr = (IMyThrust)b;
						tr.ThrustOverridePercentage = 0;
						tr.Enabled = true;
						thrs.Add(tr);

						if (filterThis(b))
						{
							normalThrusters.Add((IMyThrust)b);

							if (b.Orientation.Forward == usableControllers[0].theBlock.Orientation.Forward)
								cruiseThr.Add((IMyThrust)b);
						}
					}
					if (b is IMyTextPanel)
					{
						txts.Add((IMyTextPanel)b);
					}
					if (b is IMyProgrammableBlock)
					{
						programBlocks.Add((IMyProgrammableBlock)b);
					}
					if ((b is IMyShipConnector || b is IMyLandingGear) && ((ConnectorNeedsSuffixToPark && hasTag(b)) || (!ConnectorNeedsSuffixToPark && !hasTag(b) && filterThis(b)) || TagAll))
					{
						if (TagAll) addTag(b);
						parkblocks.Add(b);
					}
					if (b is IMyGasTank && (hasTag(b) || TagAll || filterThis(b)))
					{
						if (TagAll) addTag(b);
						tankblocks.Add(b);
					}
					if (b is IMyBatteryBlock)
					{
						if (TagAll) addTag(b);
						(b as IMyBatteryBlock).ChargeMode = ChargeMode.Auto;
						if (hasTag(b)) batteriesblocks.Add(b);
						else if (filterThis(b)) gridbats.Add(b);
					}
				}
			}

			if (Me.SurfaceCount > 0)
			{
				surfaceProviderErrorStr = "";
				//Me.CustomData = textSurfaceKeyword + 0;
				addSurfaceProvider(Me);
				Me.GetSurface(0).FontSize = 2.2f;// this isn't really the right place to put this, but doing it right would be a lot more code
			}

			bool updateVTThrs = false;

			// if you use the following if statement, it won't lock the non-main cockpit if someone sets the main cockpit, until a recompile or world load :/
			if (/*(mainController != null ? !mainController.theBlock.IsMainCockpit : false) || */controllers.Count != conts.Count || cont == null || greedy)
			{
				log.AppendLine($"  --Controller count ({controllers.Count}) is out of whack (current: {conts.Count})\n");
				if (!getControllers(conts))
				{
					return false;
				}
			}

			if (screenCount != txts.Count || greedy)
				log.AppendLine($"  --Screen count ({screenCount}) is out of whack (current: {txts.Count})\n");
			else
			{
				foreach (IMyTextPanel screen in txts)
				{
					if (!screen.IsWorking) continue;
					if (!screen.CustomName.ToLower().Contains(LCDName.ToLower())) continue;
					//getScreens(txts);
				}
			}
			//probably may-aswell just getScreens either way. seems like there wouldn't be much performance hit, I put it down instead
			getScreens(txts);

			if (rotorCount != rots.Count)
			{

				log.AppendLine($"  --Rotor count ({rotorCount}) is out of whack (current: {rots.Count})\n");
				updateVTThrs = true;
			}

			var rotorHeads = new List<IMyAttachableTopBlock>();
			foreach (IMyMotorStator rotor in rots)
			{
				if (rotor.Top != null)
				{
					rotorHeads.Add(rotor.Top);
				}
			}
			if (rotorTopCount != rotorHeads.Count)
			{
				log.AppendLine($"  --Rotor Head count ({rotorTopCount}) is out of whack (current: {rotorHeads.Count})\n");
				log.AppendLine($"   .-Rotors: {rots.Count}\n");
				updateVTThrs = true;
			}

			if (thrusterCount != thrs.Count)
			{
				log.AppendLine($"  --Thruster count ({thrusterCount}) is out of whack (current: {thrs.Count})\n");
				updateVTThrs = true;
			}


			if (updateVTThrs || greedy)
			{
				log.AppendLine("  --Updating Vector Thrusters\n");
				getVectorThrusters(rots, thrs);
			}
			else
			{
				log.AppendLine("  --Everything in order\n");
			}

			return true;
		}

		bool init()
		{
			log.AppendLine("Initialising..");
			List<IMyShipController> conts = new List<IMyShipController>();
			GridTerminalSystem.GetBlocksOfType<IMyShipController>(conts);
			if (!getControllers(conts))
			{
				log.AppendLine("Init failed.");
				return false;
			}
			getVectorThrusters();
			log.AppendLine("Init success.");
			return true;
		}

		//addTag(IMyTerminalBlock block)
		//removeTag(IMyTerminalBlock block)
		//standbyTag(IMyTerminalBlock block)
		//activeTag(IMyTerminalBlock block)

		// gets all the rotors and thrusters
		void getVectorThrusters()
		{
			var blocks = new List<IMyTerminalBlock>();

			// 1 call to GTS
			GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, block => (block is IMyThrust) || (block is IMyMotorStator));

			log.AppendLine("getVectorThrusters Init");
			// get the blocks we care about
			var rotors = new List<IMyMotorStator>();
			var thrs = new List<IMyThrust>();
			//normalThrusters.Clear();

			// var thrusters = new List<IMyThrust>();
			for (int i = blocks.Count - 1; i >= 0; i--)
			{
				if (blocks[i] is IMyThrust)
				{
					thrs.Add((IMyThrust)blocks[i]);
					if (filterThis(blocks[i]))
					{
						normalThrusters.Add((IMyThrust)blocks[i]);
						if (blocks[i].Orientation.Forward == usableControllers[0].theBlock.Orientation.Forward)
							cruiseThr.Add((IMyThrust)blocks[i]);
					}
				}
				else /*if(blocks[i] is IMyMotorStator) */
				{
					rotors.Add((IMyMotorStator)blocks[i]);
				}
				blocks.RemoveAt(i);
			}
			rotorCount = rotors.Count;
			thrusterCount = thrs.Count;

			getVectorThrusters(rotors, thrs);
		}
		void getVectorThrusters(List<IMyMotorStator> rotors, List<IMyThrust> thrusters)
		{
			//Echo("nacelles");
			bool greedy = this.applyTags || this.removeTags || this.greedy;
			//gotNacellesCount++;
			this.vectorthrusters.Clear();




			log.AppendLine("  >Getting Rotors\n");
			//Echo("Getting Rotors");
			// make this.nacelles out of all valid rotors
			rotorTopCount = 0;
			foreach (IMyMotorStator current in rotors)
			{
				if (this.removeTags)
				{
					removeTag(current);
				}
				else if (this.applyTags)
				{
					addTag(current);
				}


				if (!(greedy || hasTag(current))) { continue; }

				if (current.Top == null)
				{
					continue;
				}
				else
				{
					rotorTopCount++;
				}

				//if topgrid is not programmable blocks grid
				if (current.TopGrid == Me.CubeGrid)
				{
					continue;
				}

				// it's not set to not be a nacelle rotor
				// it's topgrid is not the programmable blocks grid
				Rotor rotor = new Rotor(current, this);
				this.vectorthrusters.Add(new VectorThrust(rotor, this));
			}

			log.AppendLine("  >Getting Thrusters\n");
			// add all thrusters to their corrisponding nacelle and remove this.nacelles that have none
			for (int i = this.vectorthrusters.Count - 1; i >= 0; i--)
			{
				for (int j = thrusters.Count - 1; j >= 0; j--)
				{
					if (!(greedy || hasTag(thrusters[j]))) { continue; }
					if (this.removeTags)
					{
						removeTag(thrusters[j]);
					}

					if (thrusters[j].CubeGrid != this.vectorthrusters[i].rotor.theBlock.TopGrid) continue;// thruster is not for the current nacelle
																								   // if(!thrusters[j].IsFunctional) continue;// broken, don't add it

					if (this.applyTags)
					{
						addTag(thrusters[j]);
					}

					this.vectorthrusters[i].thrusters.Add(new Thruster(thrusters[j]));
					thrusters.RemoveAt(j);// shorten the list we have to check
				}
				// remove this.nacelles (rotors) without thrusters
				if (this.vectorthrusters[i].thrusters.Count == 0)
				{
					removeTag(this.vectorthrusters[i].rotor.theBlock);
					this.vectorthrusters.RemoveAt(i);// there is no more reference to the rotor, should be garbage collected
					continue;
				}
				// if its still there, setup the nacelle
				this.vectorthrusters[i].validateThrusters(/*jetpack*/);
				this.vectorthrusters[i].detectThrustDirection();
			}
			log.AppendLine("  >Grouping VTThrs\n");
			GroupVectorThrusters();
		}

		/*public float lerp(float a, float b, float cutoff)
		{
			float percent = a / b;
			percent -= cutoff;
			percent *= 1 / (1 - cutoff);
			if (percent > 1)
			{
				percent = 1;
			}
			if (percent < 0)
			{
				percent = 0;
			}
			return percent;
		}*/

		/*void displayNacelles(List<Nacelle> nacelles)
		{
			foreach (Nacelle n in nacelles)
			{
				Echo($"\nRotor Name: {n.rotor.theBlock.CustomName}");
				// n.rotor.theBlock.SafetyLock = false;//for testing
				// n.rotor.theBlock.SafetyLockSpeed = 100;//for testing

				// Echo($@"deltaX: {Vector3D.Round(oldTranslation - km.Translation.Translation, 0)}");

				Echo("Thrusters:");

				int i = 0;
				foreach (Thruster t in n.thrusters)
				{
					i++;
					Echo($@"{i}: {t.theBlock.CustomName}");
				}

			}
		}*/


		/*public List<bool> CompareVars<T>(params T[] values)
		{
			T[] arr = values.Skip(1).ToArray();
			return new List<bool> { values.All(v => v.Equals(values[0])), arr.Contains(values[0]) };
		}

		public void print(params object[] args)
		{
			string separator = args[0].ToString();
			bool writes = false;
			bool ech = true;
			if (separator.Contains("%sep%")) separator = separator.Replace("%sep%", "");
			else separator = " - ";

			string result = "";

			foreach (object arg in args)
			{
				string arg_s = arg.ToString();
				if (arg_s.Equals("%scr%")) writes = true;
				else if (arg_s.Equals("%ech%")) ech = false;
				else if (!arg_s.Contains("%sep%")) result += arg_s + separator;
			}

			if (ech) Echo(result);
			if (writes) write(result);
		}*/

		class VectorThrust
		{
			//public String errStr;
			//public String DTerrStr;
			public Program program;
			readonly PID pid = new PID(1, 0, 0, (1.0 / 60.0));
			Lag avg;

			// physical parts
			public Rotor rotor;
			public HashSet<Thruster> thrusters;// all the thrusters
			public HashSet<Thruster> availableThrusters;// <= thrusters: the ones the user chooses to be used (ShowInTerminal)
			public HashSet<Thruster> activeThrusters;// <= activeThrusters: the ones that are facing the direction that produces the most thrust (only recalculated if available thrusters changes)

			public double thrustModifierAbove = 0.1;// how close the rotor has to be to target position before the thruster gets to full power
			public double thrustModifierBelow = 0.1;// how close the rotor has to be to opposite of target position before the thruster gets to 0 power

			//public bool oldJetpack = true;
			//double rVecLength = 0;
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
				this.thrusters = new HashSet<Thruster>();
				this.availableThrusters = new HashSet<Thruster>();
				this.activeThrusters = new HashSet<Thruster>();
				Role = GetVTThrRole(program);
				this.avgsamples = program.RotationAverageSamples;
				this.avg = new Lag(this.avgsamples);

				//errStr = "";
				//DTerrStr = "";
			}

			// final calculations and setting physical components
			public void go(/*bool jetpack, */bool dampeners, float shipMass)
			{
				//errStr = "=======Nacelle=======";
				/*errStr += $"\nactive thrusters: {activeThrusters.Count}";
				errStr += $"\nall thrusters: {thrusters.Count}";
				errStr += $"\nrequired force: {(int)requiredVec.Length()}N\n";*/
				/*errStr += $"\n=======rotor=======";
				errStr += $"\nname: '{rotor.theBlock.CustomName}'";
				errStr += $"\n{rotor.errStr}";
				errStr += $"\n-------rotor-------";*/

				if (avgsamples != program.RotationAverageSamples) {
					avgsamples = program.RotationAverageSamples;
					avg = new Lag(avgsamples);
				}

				totalEffectiveThrust = (float)calcTotalEffectiveThrust(activeThrusters);

				bool usepid = (program.parked && program.UsePIDPark) || program.UsePID;

				double angleCos = rotor.setFromVec(requiredVec, !usepid);
				double angleCosPercent = angleCos * 100;

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
				float iarpm = (float)AI(angleCosPercent, rVecLength / multiplier);

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


				double rtangle = rotor.theBlock.Angle;
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

				if (!usepid)
				{
					double truerpm = PreciseRpm + iarpm;
					avg.Update(truerpm);
					float finalrpm = (float)avg.Value;
					rotor.maxRPM = (TO) ? finalrpm : ((!TO && (slowThrustOff || cruise)) ? STval : finalrpm);
				}
				else {
					rotor.theBlock.TargetVelocityRad = Math.Abs(error) < 0.01 ? 0 : (float)result;
				}
				//program.print("%scr%", rotor.maxRPM);
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
				// errStr += $"\n=======thrusters=======";
				foreach (Thruster thruster in activeThrusters)
				{
					// errStr += thrustOffset.progressBar();
					Vector3D thrust = thrustOffset * requiredVec * thruster.theBlock.MaxEffectiveThrust / totalEffectiveThrust;
					bool noThrust = thrust.LengthSquared() < 0.001f;
					if (/*!jetpack || */!program.thrustOn || noThrust)
					{
						thruster.setThrust(0);
						thruster.theBlock.Enabled = false;
						thruster.IsOffBecauseDampeners = !program.thrustOn || noThrust;
						//thruster.IsOffBecauseJetpack = !jetpack;
					}
					else
					{
						thruster.setThrust(thrust);
						thruster.theBlock.Enabled = true;
						thruster.IsOffBecauseDampeners = false;
						//thruster.IsOffBecauseJetpack = false;
					}

					// errStr += $"\nthruster '{thruster.theBlock.CustomName}': {thruster.errStr}\n";
				}
				// errStr += $"\n-------thrusters-------";
				// errStr += $"\n-------Nacelle-------";
				//oldJetpack = jetpack;
			}

			public float calcTotalEffectiveThrust(IEnumerable<Thruster> thrusters)
			{
				float total = 0;
				foreach (Thruster t in thrusters)
				{
					total += t.theBlock.MaxEffectiveThrust;
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
					//print(i.ToString(), cdirs[i].ToString(), rdirs[1].ToString());
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
			public bool validateThrusters(/*bool jetpack*/)
			{
				bool needsUpdate = false;
				//errStr += "validating thrusters: (jetpack {jetpack})\n";
				foreach (Thruster curr in thrusters)
				{

					bool shownAndFunctional = (curr.theBlock.ShowInTerminal || !program.ignoreHiddenBlocks) && curr.theBlock.IsFunctional;
					if (availableThrusters.Contains(curr))
					{//is available
					 //errStr += "in available thrusters\n";

						bool wasOnAndIsNowOff = curr.IsOn && !curr.theBlock.Enabled /*&& !curr.IsOffBecauseJetpack*/ && !curr.IsOffBecauseDampeners;

						if ((!shownAndFunctional || wasOnAndIsNowOff) /*&& (jetpack && oldJetpack)*/)
						{
							// if jetpack is on, the thruster has been turned off
							// if jetpack is off, the thruster should still be in the group

							curr.IsOn = false;
							//remove the thruster
							availableThrusters.Remove(curr);
							needsUpdate = true;
						}

					}
					else
					{//not available
						/*errStr += "not in available thrusters\n";
						if (program.ignoreHiddenBlocks)
						{errStr += $"ShowInTerminal {curr.theBlock.ShowInTerminal}\n";}
						errStr += $"IsWorking {curr.theBlock.IsWorking}\n";
						errStr += $"IsFunctional {curr.theBlock.IsFunctional}\n";*/

						bool wasOffAndIsNowOn = !curr.IsOn && curr.theBlock.Enabled;
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
				//print("%scr%", result1, result2, mres1, mres2, sum);
				return Math.Max(0, sum);
			}

			public void detectThrustDirection()
			{
				// DTerrStr = "";
				//detectThrustCounter++;
				Vector3D engineDirection = Vector3D.Zero;
				Vector3D engineDirectionNeg = Vector3D.Zero;
				Vector3I thrustDir = Vector3I.Zero;
				Base6Directions.Direction rotTopUp = rotor.theBlock.Top.Orientation.Up;

				// add all the thrusters effective power
				foreach (Thruster t in availableThrusters)
				{
					// Base6Directions.Direction thrustForward = t.theBlock.Orientation.TransformDirection(Base6Directions.Direction.Forward); // Exhaust goes this way
					Base6Directions.Direction thrustForward = t.theBlock.Orientation.Forward; // Exhaust goes this way

					//if its not facing rotor up or rotor down
					if (!(thrustForward == rotTopUp || thrustForward == Base6Directions.GetFlippedDirection(rotTopUp)))
					{
						// add it in
						var thrustForwardVec = Base6Directions.GetVector(thrustForward);
						if (thrustForwardVec.X < 0 || thrustForwardVec.Y < 0 || thrustForwardVec.Z < 0)
						{
							engineDirectionNeg += Base6Directions.GetVector(thrustForward) * t.theBlock.MaxEffectiveThrust;
						}
						else
						{
							engineDirection += Base6Directions.GetVector(thrustForward) * t.theBlock.MaxEffectiveThrust;
						}
					}
				}

				// get single most powerful direction
				double max = Math.Max(engineDirection.Z, Math.Max(engineDirection.X, engineDirection.Y));
				double min = Math.Min(engineDirectionNeg.Z, Math.Min(engineDirectionNeg.X, engineDirectionNeg.Y));
				// DTerrStr += $"\nmax:\n{Math.Round(max, 2)}";
				// DTerrStr += $"\nmin:\n{Math.Round(min, 2)}";
				double maxAbs = 0;
				if (max > -1 * min)
				{
					maxAbs = max;
				}
				else
				{
					maxAbs = min;
				}
				// DTerrStr += $"\nmaxAbs:\n{Math.Round(maxAbs, 2)}";

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
				rotor.setPointDir((Vector3D)thrustDir);
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
					t.theBlock.Enabled = false;
					t.IsOn = false;
				}
				activeThrusters.Clear();

				// put thrusters into the active list
				Base6Directions.Direction thrDir = Base6Directions.GetDirection(thrustDir);
				foreach (Thruster t in availableThrusters)
				{
					Base6Directions.Direction thrustForward = t.theBlock.Orientation.Forward; // Exhaust goes this way

					if (thrDir == thrustForward)
					{
						t.theBlock.Enabled = true;
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

			// these 2 indicate the thruster was turned off from the script, and should be kept in the active list
			public bool IsOffBecauseDampeners = true;
			//public bool IsOffBecauseJetpack = true;

			//public string errStr = "";

			public Thruster(IMyThrust thruster) : base(thruster)
			{
				// this.IsOn = theBlock.Enabled;
				this.IsOn = false;
				this.theBlock.Enabled = true;
			}

			// sets the thrust in newtons (N)
			// thrustVec is in worldspace, who'se length is the desired thrust
			public void setThrust(Vector3D thrustVec)
			{
				setThrust(thrustVec.Length());
			}

			// sets the thrust in newtons (N)
			public void setThrust(double thrust)
			{
				//errStr = "";
				/*errStr += $"\ntheBlock.Enabled: {theBlock.Enabled.toString()}";
				errStr += $"\nIsOffBecauseDampeners: {IsOffBecauseDampeners.toString()}";
				errStr += $"\nIsOffBecauseJetpack: {IsOffBecauseJetpack.toString()}";*/

				if (thrust > theBlock.MaxThrust)
				{
					thrust = theBlock.MaxThrust;
					// errStr += $"\nExceeding max thrust";
				}
				else if (thrust < 0)
				{
					// errStr += $"\nNegative Thrust";
					thrust = 0;
				}

				theBlock.ThrustOverride = (float)(thrust * theBlock.MaxThrust / theBlock.MaxEffectiveThrust);
				/*errStr += $"\nEffective {(100*theBlock.MaxEffectiveThrust / theBlock.MaxThrust).Round(1)}%";
				errStr += $"\nOverride {theBlock.ThrustOverride}N";*/
			}
		}


		/*public StringBuilder ProgressBar(double percent, int amount)
		{
			StringBuilder sb = new StringBuilder();
			int a = (int)(percent * amount);
			int rz = amount - a;
			sb.Append('[').Append('║', a).Append(' ', rz).Append(']');
			return sb;
		}*/

		/*public string ProgressBar(int chars, double progress, double total)
		{
			//calculate percent
			double p = progress * 100 / total;
			double avgbars = p * chars / 100;
			int bars = Convert.ToInt32(avgbars);

			StringBuilder result = new StringBuilder("[", chars+2);
			int j = 1;
			for (int i = 0; i < chars; i++) {
				if (j <= bars) result.Append("+");
				else result.Append(" ");
				j++;
			}
			result.Append("]");
			return result.ToString();
		}*/

		public bool filterThis(IMyTerminalBlock block)
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
						IMyMotorStator rt = na.rotor.theBlock;
						HashSet<Thruster> tr = na.thrusters;
						foreach (Thruster t in tr) { t.theBlock.Enabled = !rotorlock; t.theBlock.ThrustOverride = 0; }
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

			public void setPointDir(Vector3D dir)
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
				double err2 = Vector3D.Dot(angle.normalized(), rotor.WorldMatrix.Up);
				double diff = (rotor.WorldMatrix.Up - angle.normalized()).Length();

				/*this.errStr += $"\nrotor.WorldMatrix.Up: {rotor.WorldMatrix.Up}";
				this.errStr += $"\nangle: {Math.Acos(angleBetweenCos(angle, rotor.WorldMatrix.Up)) * 180.0 / Math.PI}";
				this.errStr += $"\nerr: {err}";
				this.errStr += $"\ndirection difference: {diff}";

				this.errStr += $"\ncurrDir vs Up: {currentDirection.Dot(rotor.WorldMatrix.Up)}";
				this.errStr += $"\ntargetDir vs Up: {targetDirection.Dot(rotor.WorldMatrix.Up)}";

				this.errStr += $"\nmaxRPM: {maxRPM}";
				this.errStr += $"\nerrorScale: {errorScale}";
				this.errStr += $"\nmultiplier: {multiplier}";*/

				float result = 0;

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

				rotor.TargetVelocityRPM = result;
				// this.errStr += $"\nRPM: {(rotor.TargetVelocityRPM).Round(5)}";
			}

			// this sets the rotor to face the desired direction in worldspace
			// desiredVec doesn't have to be in-line with the rotors plane of rotation
			public double setFromVec(Vector3D desiredVec, float multiplier, bool point = true)
			{
				desiredVec.Normalize();
				//errStr = "";
				//desiredVec = desiredVec.reject(theBlock.WorldMatrix.Up);
				//this.errStr += $"\ncurrent dir: {currentDir}\ntarget dir: {desiredVec}\ndiff: {currentDir - desiredVec}";
				//Vector3D currentDir = Vector3D.TransformNormal(this.direction, theBlock.Top.WorldMatrix);
				//                                    only correct if it was built from the head ^ 
				//                                    it needs to be based on the grid
				Vector3D currentDir = Vector3D.TransformNormal(this.direction, theBlock.Top.CubeGrid.WorldMatrix);
				if (point) PointRotorAtVector(theBlock, desiredVec, currentDir/*theBlock.Top.WorldMatrix.Forward*/, multiplier);

				return angleBetweenCos(currentDir, desiredVec, desiredVec.Length());
			}

			public double setFromVec(Vector3D desiredVec, bool point = true)
			{
				return setFromVec(desiredVec, 1, point);
			}

			// this sets the rotor to face the desired direction in worldspace
			// desiredVec doesn't have to be in-line with the rotors plane of rotation
			/*public double setFromVecOld(Vector3D desiredVec)
			{
				desiredVec = desiredVec.reject(theBlock.WorldMatrix.Up);
				if (Vector3D.IsZero(desiredVec) || !desiredVec.IsValid())
				{
					//errStr = $"\nERROR (setFromVec()):\n\tdesiredVec is invalid\n\t{desiredVec}";
					return -1;
				}

				double des_vec_len = desiredVec.Length();
				double angleCos = angleBetweenCos(theBlock.WorldMatrix.Forward, desiredVec, des_vec_len);

				// angle between vectors
				float angle = -(float)Math.Acos(angleCos);

				//disambiguate
				if (Math.Acos(angleBetweenCos(theBlock.WorldMatrix.Left, desiredVec, des_vec_len)) > Math.PI / 2)
				{
					angle = (float)(2 * Math.PI - angle);
				}

				setPos(angle + (float)(offset/* * Math.PI / 180));
				return angleCos;
			}*/

			// gets cos(angle between 2 vectors)
			// cos returns a number between 0 and 1
			// use Acos to get the angle
			//THIS COULD BE NECESSARY IN SOME FUTURE.....
			public double angleBetweenCos(Vector3D a, Vector3D b)
			{
				double dot = Vector3D.Dot(a, b);
				double Length = a.Length() * b.Length();
				return dot / Length;
			}

			// gets cos(angle between 2 vectors)
			// cos returns a number between 0 and 1
			// use Acos to get the angle
			// doesn't calculate length because thats expensive
			public double angleBetweenCos(Vector3D a, Vector3D b, double len_a_times_len_b)
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

			// move rotor to the angle (radians), make it go the shortest way possible
			/*public void setPos(float x)
			{
				theBlock.Enabled = true;
				x = cutAngle(x);
				float velocity = maxRPM;
				float x2 = cutAngle(theBlock.Angle);
				if (Math.Abs(x - x2) < Math.PI)
				{
					//dont cross origin
					if (x2 < x)
					{
						theBlock.SetValue<float>("Velocity", velocity * Math.Abs(x - x2));
					}
					else
					{
						theBlock.SetValue<float>("Velocity", -velocity * Math.Abs(x - x2));
					}
				}
				else
				{
					//cross origin
					if (x2 < x)
					{
						theBlock.SetValue<float>("Velocity", -velocity * Math.Abs(x - x2));
					}
					else
					{
						theBlock.SetValue<float>("Velocity", velocity * Math.Abs(x - x2));
					}
				}
			}*/

		}
		class ShipController : BlockWrapper<IMyShipController>
		{
			public bool Dampener;


			public ShipController(IMyShipController theBlock) : base(theBlock)
			{
				Dampener = theBlock.DampenersOverride;
			}

			public void setDampener(bool val)
			{
				Dampener = val;
				theBlock.DampenersOverride = val;
			}
		}

		interface IBlockWrapper
		{
			IMyTerminalBlock theBlock { get; set; }
			List<Base6Directions.Axis> Directions { get; }
		}

		abstract class BlockWrapper<T> : IBlockWrapper where T : class, IMyTerminalBlock
		{
			public T theBlock { get; set; }

			public List<Base6Directions.Axis> Directions { get; }

			public BlockWrapper(T block)
			{
				theBlock = block;
				Directions = GetDirections(block);
			}

			// not allowed for some reason
			//public static implicit operator IMyTerminalBlock(BlockWrapper<T> wrap) => wrap.theBlock;

			IMyTerminalBlock IBlockWrapper.theBlock
			{
				get { return theBlock; }
				set { theBlock = (T)value; }
			}

			public List<Base6Directions.Axis> GetDirections(IMyTerminalBlock block)
			{
				MyBlockOrientation o = block.Orientation;
				return new List<Base6Directions.Axis> { Base6Directions.GetAxis(o.Forward), Base6Directions.GetAxis(o.Up), Base6Directions.GetAxis(o.Left) };
			}
		}
	}
}
